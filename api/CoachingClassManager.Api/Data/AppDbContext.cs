using Microsoft.EntityFrameworkCore;

namespace CoachingClassManager.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Institute> Institutes => Set<Institute>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Institute>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Language).HasMaxLength(5);
        });
        model.Entity<Teacher>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Phone).IsUnique();
            entity.HasIndex(x => x.InstituteId).IsUnique();
            entity.Property(x => x.Phone).HasMaxLength(16);
            entity.HasOne(x => x.Institute).WithMany().HasForeignKey(x => x.InstituteId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<OtpChallenge>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Phone, x.CreatedAt });
            entity.Property(x => x.Phone).HasMaxLength(16);
            entity.Property(x => x.CodeHash).HasMaxLength(64);
        });
        model.Entity<AuthSession>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.RefreshTokenHash).IsUnique();
            entity.HasIndex(x => x.FamilyId);
            entity.Property(x => x.RefreshTokenHash).HasMaxLength(64);
            entity.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
