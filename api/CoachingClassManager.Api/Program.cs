using System.Security.Claims;
using System.Text;
using System.Net;
using System.Threading.RateLimiting;
using CoachingClassManager.Api.Auth;
using CoachingClassManager.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();
var auth = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new();
if (Encoding.UTF8.GetByteCount(auth.SigningKey) < 32 || Encoding.UTF8.GetByteCount(auth.OtpPepper) < 32
    || auth.SigningKey == auth.OtpPepper)
    throw new InvalidOperationException("Configure different Auth:SigningKey and Auth:OtpPepper secrets of at least 32 bytes.");
if (!builder.Environment.IsDevelopment() && auth.EnableDevelopmentOtp)
    throw new InvalidOperationException("Development OTP is forbidden outside Development.");
if (auth.AccessTokenMinutes is < 1 or > 60 || auth.RefreshTokenDays is < 1 or > 90
    || auth.OtpExpiryMinutes is < 1 or > 10 || auth.OtpMaxAttempts is < 1 or > 10
    || auth.OtpResendSeconds < 30 || auth.OtpDailyLimitPerPhone < 1 || auth.OtpDailyGlobalLimit < 1
    || auth.RequestsPerMinute is < 1 or > 1000)
    throw new InvalidOperationException("Authentication policy values are outside their permitted bounds.");

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AuthGate>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("Configure ConnectionStrings:Database.")));
builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    foreach (var address in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(IPAddress.Parse(address));
});
builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = auth.Issuer,
        ValidateAudience = true, ValidAudience = auth.Audience,
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(5),
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (!Guid.TryParse(context.Principal?.FindFirstValue("session_id"), out var sessionId)
                || !Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var teacherId)
                || !Guid.TryParse(context.Principal?.FindFirstValue("tenant_id"), out var instituteId))
            { context.Fail("Invalid session."); return; }
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var now = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            if (!await db.AuthSessions.AnyAsync(x => x.Id == sessionId && x.TeacherId == teacherId
                && x.Teacher.InstituteId == instituteId && x.RevokedAt == null && x.ExpiresAt > now,
                context.HttpContext.RequestAborted))
                context.Fail("Session revoked or expired.");
        }
    };
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = auth.RequestsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var app = builder.Build();
if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    return;
}
app.UseExceptionHandler();
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next(context);
});

app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (AppDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(503));
var login = app.MapGroup("/api/auth").RequireRateLimiting("auth");
login.MapPost("/otp/request", (RequestOtp request, AuthService service, CancellationToken ct) => service.RequestAsync(request, ct));
login.MapPost("/otp/verify", (VerifyOtp request, AuthService service, CancellationToken ct) => service.VerifyAsync(request, ct));
login.MapPost("/refresh", (RefreshRequest request, AuthService service, CancellationToken ct) => service.RefreshAsync(request, ct));
login.MapPost("/logout", (AuthService service, TenantContext tenant, CancellationToken ct) => service.LogoutAsync(tenant, ct))
    .RequireAuthorization();
var account = app.MapGroup("/api").RequireAuthorization();
account.MapGet("/me", (AuthService service, TenantContext tenant, CancellationToken ct) => service.ProfileAsync(tenant, ct));
account.MapPut("/institute", (InstituteRequest request, AuthService service, TenantContext tenant, CancellationToken ct) =>
    service.UpdateInstituteAsync(request, tenant, ct));
account.MapGet("/institutes/{id:guid}", async (Guid id, AuthService service, TenantContext tenant, CancellationToken ct) =>
    id != tenant.InstituteId ? Results.NotFound() : Results.Ok(await service.ProfileAsync(tenant, ct)));

app.Run();
public partial class Program { }
