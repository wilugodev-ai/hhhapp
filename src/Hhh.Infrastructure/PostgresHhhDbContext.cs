using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hhh.Infrastructure;

// Separate context keeps the SQL Server migration history and model snapshot intact.
public sealed class PostgresHhhDbContext(DbContextOptions<PostgresHhhDbContext> options)
    : HhhDbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}

public sealed class PostgresHhhDbContextFactory : IDesignTimeDbContextFactory<PostgresHhhDbContext>
{
    public PostgresHhhDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PostgresHhhDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("HHH_POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Database=hhh_design;Username=postgres;Password=design-time-unused").Options);
}
