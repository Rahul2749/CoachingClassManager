namespace CoachingClassManager.Api.Auth;

public sealed class AuthOptions
{
    public string Issuer { get; set; } = "CoachingClassManager";
    public string Audience { get; set; } = "CoachingClassManager.App";
    public string SigningKey { get; set; } = "";
    public string OtpPepper { get; set; } = "";
    public bool EnableDevelopmentOtp { get; set; }
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
    public int OtpExpiryMinutes { get; set; } = 5;
    public int OtpResendSeconds { get; set; } = 60;
    public int OtpMaxAttempts { get; set; } = 5;
    public int OtpDailyLimitPerPhone { get; set; } = 10;
    public int OtpDailyGlobalLimit { get; set; } = 100;
    public int RequestsPerMinute { get; set; } = 30;
}
