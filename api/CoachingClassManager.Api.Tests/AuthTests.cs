using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachingClassManager.Api.Auth;
using CoachingClassManager.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace CoachingClassManager.Api.Tests;

public sealed class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static int phoneSequence = 100;
    private static string NewPhone() => $"+91900000{Interlocked.Increment(ref phoneSequence):D4}";
    private sealed record Challenge(Guid ChallengeId, string DevelopmentCode);

    private async Task<Challenge> RequestAsync(HttpClient client, string? phone = null)
    {
        var response = await client.PostAsJsonAsync("/api/auth/otp/request", new { phone = phone ?? NewPhone() });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Challenge>())!;
    }

    private async Task<TokenResponse> LoginAsync(HttpClient client, string? phone = null)
    {
        var challenge = await RequestAsync(client, phone);
        var response = await client.PostAsJsonAsync("/api/auth/otp/verify", new
        { challengeId = challenge.ChallengeId, code = challenge.DevelopmentCode });
        response.EnsureSuccessStatusCode();
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return tokens;
    }

    [Fact]
    public async Task AccountCannotReadOrChangeAnotherInstitute()
    {
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        var a = await LoginAsync(first);
        var b = await LoginAsync(second);
        var update = await first.PutAsJsonAsync("/api/institute", new
        { name = "First Institute", language = "mr", instituteId = b.Profile.InstituteId });
        update.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/api/institutes/{a.Profile.InstituteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await first.GetAsync($"/api/institutes/{b.Profile.InstituteId}")).StatusCode);
        Assert.Equal("First Institute", (await first.GetFromJsonAsync<Profile>("/api/me"))!.InstituteName);
        Assert.Equal("", (await second.GetFromJsonAsync<Profile>("/api/me"))!.InstituteName);
    }

    [Fact]
    public async Task UnauthenticatedAccountEndpointsAreRejected()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/institute", new { name = "School", language = "en" })).StatusCode);
    }

    [Fact]
    public async Task CodeIsSingleUseAndRefreshReplayRevokesReplacement()
    {
        using var client = factory.CreateClient();
        var challenge = await RequestAsync(client);
        var body = new { challengeId = challenge.ChallengeId, code = challenge.DevelopmentCode };
        var response = await client.PostAsJsonAsync("/api/auth/otp/verify", body);
        var original = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/otp/verify", body)).StatusCode);
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = original.RefreshToken });
        refresh.EnsureSuccessStatusCode();
        var replacement = (await refresh.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", replacement.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = original.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task LogoutInvalidatesAccessAndRefreshTokens()
    {
        using var client = factory.CreateClient();
        var tokens = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task WrongAttemptsExhaustChallengeAndResendsAreThrottled()
    {
        using var client = factory.CreateClient();
        var phone = NewPhone();
        var challenge = await RequestAsync(client, phone);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.PostAsJsonAsync("/api/auth/otp/request", new { phone })).StatusCode);
        var wrong = challenge.DevelopmentCode == "000000" ? "999999" : "000000";
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/otp/verify", new
            { challengeId = challenge.ChallengeId, code = wrong })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/otp/verify", new
        { challengeId = challenge.ChallengeId, code = challenge.DevelopmentCode })).StatusCode);
    }

    [Fact]
    public async Task ExpiredChallengeIsRejectedAndSecretsAreStoredHashed()
    {
        using var client = factory.CreateClient();
        var challenge = await RequestAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.OtpChallenges.SingleAsync(x => x.Id == challenge.ChallengeId);
            Assert.NotEqual(challenge.DevelopmentCode, stored.CodeHash);
            stored.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/otp/verify", new
        { challengeId = challenge.ChallengeId, code = challenge.DevelopmentCode })).StatusCode);
        var tokens = await LoginAsync(client);
        using var sessionScope = factory.Services.CreateScope();
        var hashes = await sessionScope.ServiceProvider.GetRequiredService<AppDbContext>().AuthSessions
            .Where(x => x.TeacherId == tokens.Profile.TeacherId).Select(x => x.RefreshTokenHash).ToListAsync();
        Assert.DoesNotContain(tokens.RefreshToken, hashes);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void DevelopmentOtpCannotStartOutsideDevelopment(string environment)
    {
        using var restricted = factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        var error = Assert.Throws<InvalidOperationException>(() => restricted.CreateClient());
        Assert.Contains("Development OTP is forbidden", error.Message);
    }

    [Fact]
    public async Task ExpiredSessionCannotRefreshOrAccessAccount()
    {
        using var client = factory.CreateClient();
        var tokens = await LoginAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.AuthSessions.SingleAsync(x => x.TeacherId == tokens.Profile.TeacherId);
            session.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task ConcurrentVerificationConsumesCodeOnlyOnce()
    {
        using var client = factory.CreateClient();
        var challenge = await RequestAsync(client);
        var body = new { challengeId = challenge.ChallengeId, code = challenge.DevelopmentCode };
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/auth/otp/verify", body),
            client.PostAsJsonAsync("/api/auth/otp/verify", body));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("+911234567890")]
    [InlineData("+12025550100")]
    public async Task InvalidPhonesAreRejected(string phone)
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/auth/otp/request", new { phone })).StatusCode);
    }
}
