using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CoachingClassManager.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoachingClassManager.Api.Auth;

public sealed record RequestOtp(string Phone);
public sealed record VerifyOtp(Guid ChallengeId, string Code);
public sealed record RefreshRequest(string RefreshToken);
public sealed record InstituteRequest(string Name, string Language);
public sealed record Profile(Guid TeacherId, Guid InstituteId, string Phone, string InstituteName, string Language);
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresAt,
    DateTimeOffset RefreshExpiresAt, Profile Profile);

public sealed class AuthGate
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
}

public sealed partial class AuthService(
    AppDbContext db, IOptions<AuthOptions> options, TimeProvider clock,
    IHostEnvironment environment, AuthGate gate)
{
    private readonly AuthOptions settings = options.Value;

    [GeneratedRegex(@"^\+91[6-9][0-9]{9}$")]
    private static partial Regex IndianPhone();

    public Task<IResult> RequestAsync(RequestOtp request, CancellationToken cancellation) => LockedAsync(async () =>
    {
        var phone = request.Phone?.Trim() ?? "";
        if (!IndianPhone().IsMatch(phone))
            return Error(400, "invalid_phone", "Enter an Indian mobile number in +91 format.");
        // No production adapter is configured yet. Never fall back to a fake OTP provider.
        if (!environment.IsDevelopment() || !settings.EnableDevelopmentOtp)
            return Error(503, "otp_provider_unavailable", "SMS sign-in is not configured on this server.");

        var now = clock.GetUtcNow();
        var recent = await db.OtpChallenges.Where(x => x.Phone == phone)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellation);
        if (recent is not null && recent.CreatedAt.AddSeconds(settings.OtpResendSeconds) > now)
            return Error(429, "otp_resend_wait", "Please wait before requesting another code.");

        var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        if (await db.OtpChallenges.CountAsync(x => x.Phone == phone && x.CreatedAt >= today, cancellation)
                >= settings.OtpDailyLimitPerPhone
            || await db.OtpChallenges.CountAsync(x => x.CreatedAt >= today, cancellation)
                >= settings.OtpDailyGlobalLimit)
            return Error(429, "otp_daily_limit", "The daily sign-in code limit has been reached.");

        var previous = await db.OtpChallenges.Where(x => x.Phone == phone && !x.Consumed)
            .ToListAsync(cancellation);
        previous.ForEach(x => x.Consumed = true);
        var id = Guid.NewGuid();
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var challenge = new OtpChallenge
        {
            Id = id, Phone = phone, CodeHash = HashOtp(id, code), CreatedAt = now,
            ExpiresAt = now.AddMinutes(settings.OtpExpiryMinutes)
        };
        db.OtpChallenges.Add(challenge);
        await db.SaveChangesAsync(cancellation);
        return Results.Ok(new
        {
            challengeId = id, expiresAt = challenge.ExpiresAt,
            resendAfterSeconds = settings.OtpResendSeconds,
            // Returned only from the explicitly enabled Development environment. Not logged.
            developmentCode = code
        });
    }, cancellation);

    public Task<IResult> VerifyAsync(VerifyOtp request, CancellationToken cancellation) => LockedAsync(async () =>
    {
        if (!environment.IsDevelopment() || !settings.EnableDevelopmentOtp)
            return Error(503, "otp_provider_unavailable", "SMS sign-in is not configured on this server.");
        var now = clock.GetUtcNow();
        var challenge = await db.OtpChallenges.SingleOrDefaultAsync(x => x.Id == request.ChallengeId, cancellation);
        if (challenge is null || challenge.Consumed || challenge.ExpiresAt <= now
            || challenge.Attempts >= settings.OtpMaxAttempts)
            return Error(400, "invalid_otp", "This code is invalid or expired. Request a new one.");

        challenge.Attempts++;
        var code = request.Code ?? "";
        if (code.Length != 6 || !code.All(char.IsAsciiDigit)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(challenge.CodeHash), Encoding.UTF8.GetBytes(HashOtp(challenge.Id, code))))
        {
            await db.SaveChangesAsync(cancellation);
            return Error(400, "invalid_otp", "This code is invalid or expired. Request a new one.");
        }

        challenge.Consumed = true;
        var teacher = await db.Teachers.Include(x => x.Institute)
            .SingleOrDefaultAsync(x => x.Phone == challenge.Phone, cancellation);
        if (teacher is null)
        {
            var institute = new Institute { Id = Guid.NewGuid(), CreatedAt = now, UpdatedAt = now };
            teacher = new Teacher
            {
                Id = Guid.NewGuid(), InstituteId = institute.Id, Institute = institute,
                Phone = challenge.Phone, CreatedAt = now
            };
            db.Teachers.Add(teacher);
        }
        var tokens = CreateSession(teacher, Guid.NewGuid(), now.AddDays(settings.RefreshTokenDays));
        await db.SaveChangesAsync(cancellation);
        return Results.Ok(tokens);
    }, cancellation);

    public Task<IResult> RefreshAsync(RefreshRequest request, CancellationToken cancellation) => LockedAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.RefreshToken.Length > 256)
            return Error(401, "invalid_session", "Sign in again to continue.");
        var hash = HashToken(request.RefreshToken);
        var session = await db.AuthSessions.Include(x => x.Teacher).ThenInclude(x => x.Institute)
            .SingleOrDefaultAsync(x => x.RefreshTokenHash == hash, cancellation);
        var now = clock.GetUtcNow();
        if (session is null || session.ExpiresAt <= now)
            return Error(401, "invalid_session", "Sign in again to continue.");
        if (session.RevokedAt is not null)
        {
            // Reuse of a rotated token invalidates its entire device-session family.
            var family = await db.AuthSessions.Where(x => x.FamilyId == session.FamilyId && x.RevokedAt == null)
                .ToListAsync(cancellation);
            family.ForEach(x => x.RevokedAt = now);
            await db.SaveChangesAsync(cancellation);
            return Error(401, "invalid_session", "Sign in again to continue.");
        }
        session.RevokedAt = now;
        var tokens = CreateSession(session.Teacher, session.FamilyId, session.ExpiresAt);
        await db.SaveChangesAsync(cancellation);
        return Results.Ok(tokens);
    }, cancellation);

    public Task<IResult> LogoutAsync(TenantContext tenant, CancellationToken cancellation) => LockedAsync(async () =>
    {
        var session = await db.AuthSessions.SingleAsync(x => x.Id == tenant.SessionId, cancellation);
        var family = await db.AuthSessions.Where(x => x.FamilyId == session.FamilyId && x.RevokedAt == null)
            .ToListAsync(cancellation);
        family.ForEach(x => x.RevokedAt = clock.GetUtcNow());
        await db.SaveChangesAsync(cancellation);
        return Results.NoContent();
    }, cancellation);

    public async Task<Profile> ProfileAsync(TenantContext tenant, CancellationToken cancellation)
    {
        var teacher = await db.Teachers.Include(x => x.Institute)
            .SingleAsync(x => x.Id == tenant.TeacherId && x.InstituteId == tenant.InstituteId, cancellation);
        return ToProfile(teacher);
    }

    public async Task<IResult> UpdateInstituteAsync(InstituteRequest request, TenantContext tenant,
        CancellationToken cancellation)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 2 or > 120 || request.Language is not ("en" or "hi" or "mr"))
            return Error(400, "invalid_institute", "Enter an institute name (2–120 characters) and a supported language.");
        var institute = await db.Institutes.SingleAsync(x => x.Id == tenant.InstituteId, cancellation);
        institute.Name = name;
        institute.Language = request.Language;
        institute.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellation);
        return Results.Ok(await ProfileAsync(tenant, cancellation));
    }

    private TokenResponse CreateSession(Teacher teacher, Guid familyId, DateTimeOffset refreshExpires)
    {
        var now = clock.GetUtcNow();
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var session = new AuthSession
        {
            Id = Guid.NewGuid(), FamilyId = familyId, Teacher = teacher, TeacherId = teacher.Id,
            RefreshTokenHash = HashToken(refresh), CreatedAt = now, ExpiresAt = refreshExpires
        };
        db.AuthSessions.Add(session);
        var expires = now.AddMinutes(settings.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, teacher.Id.ToString()),
            new Claim("tenant_id", teacher.InstituteId.ToString()),
            new Claim("session_id", session.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var jwt = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            now.UtcDateTime, expires.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(jwt), refresh, expires,
            refreshExpires, ToProfile(teacher));
    }

    private string HashOtp(Guid id, string code) => Convert.ToHexString(
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.OtpPepper), Encoding.UTF8.GetBytes($"{id}:{code}")));
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static Profile ToProfile(Teacher teacher) => new(teacher.Id, teacher.InstituteId, teacher.Phone,
        teacher.Institute.Name, teacher.Institute.Language);
    private static IResult Error(int status, string code, string message) => Results.Problem(
        statusCode: status, title: message, extensions: new Dictionary<string, object?> { ["code"] = code });

    private async Task<IResult> LockedAsync(Func<Task<IResult>> operation, CancellationToken cancellation)
    {
        await gate.Semaphore.WaitAsync(cancellation);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellation);
            if (db.Database.IsNpgsql())
                // Serializes OTP budgets, challenge use, and refresh rotation across API instances.
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73012026)", cancellation);
            var result = await operation();
            await transaction.CommitAsync(cancellation);
            return result;
        }
        finally { gate.Semaphore.Release(); }
    }
}
