using Hhh.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hhh.Tests;

public sealed class PostgresModelTests
{
    [Fact]
    public void Postgres_uses_its_own_schema_and_persistent_session_keys()
    {
        using var db = new PostgresHhhDbContext(new DbContextOptionsBuilder<PostgresHhhDbContext>()
            .UseNpgsql("Host=localhost;Database=hhh_model_tests;Username=unused;Password=unused").Options);
        var script = db.Database.GenerateCreateScript();
        Assert.Contains("DataProtectionKeys", script);
        Assert.Contains("CURRENT_TIMESTAMP", script);
        Assert.Contains("\"Id\" = 1", script);
        Assert.DoesNotContain("SYSUTCDATETIME", script);
        Assert.DoesNotContain("nvarchar", script);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Reference_seeding_refuses_the_normal_local_database_before_any_writes()
    {
        using var db = new HhhDbContext(new DbContextOptionsBuilder<HhhDbContext>()
            .UseSqlServer("Server=localhost;Database=HhhLocal;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SyntheticSeed.RunAsync(db, null!, null!));
        Assert.Contains("Reference identities cannot be seeded", error.Message);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
