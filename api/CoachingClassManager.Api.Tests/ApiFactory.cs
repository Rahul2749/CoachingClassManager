using CoachingClassManager.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace CoachingClassManager.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string databaseName = $"coaching_test_{Guid.NewGuid():N}";
    private string adminConnection = "";
    public string TestConnection { get; private set; } = "";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Auth:SigningKey", "test-signing-key-only-not-for-production-123456789");
        builder.UseSetting("Auth:OtpPepper", "test-otp-pepper-only-not-for-production-987654321");
        builder.UseSetting("Auth:EnableDevelopmentOtp", "true");
        builder.UseSetting("Auth:RequestsPerMinute", "1000");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(TestConnection));
        });
    }

    public async Task InitializeAsync()
    {
        adminConnection = Environment.GetEnvironmentVariable("COACHING_TEST_DATABASE")
            ?? throw new InvalidOperationException("Set COACHING_TEST_DATABASE to a local PostgreSQL connection. Tests create a separate temporary database.");
        var connection = new NpgsqlConnectionStringBuilder(adminConnection);
        connection.Database = "postgres";
        adminConnection = connection.ConnectionString;
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
        await create.ExecuteNonQueryAsync();
        connection.Database = databaseName;
        TestConnection = connection.ConnectionString;
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        if (string.IsNullOrEmpty(adminConnection)) return;
        // Only the generated test database is dropped; the app database is never reset.
        if (!databaseName.StartsWith("coaching_test_") || databaseName.Length != 46)
            throw new InvalidOperationException("Unexpected test database name.");
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}
