namespace CoachingClassManager.Api.Data;

public sealed class Institute
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Language { get; set; } = "en";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Teacher
{
    public Guid Id { get; set; }
    public Guid InstituteId { get; set; }
    public Institute Institute { get; set; } = null!;
    public string Phone { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OtpChallenge
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public bool Consumed { get; set; }
}

public sealed class AuthSession
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Guid TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public string RefreshTokenHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
