using Hhh.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hhh.Api;

// Select the SQL Server model explicitly when both provider contexts are available to EF tooling.
public sealed class HhhDbContextFactory : IDesignTimeDbContextFactory<HhhDbContext>
{
    public HhhDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<HhhDbContext>()
        .UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__Hhh")
            ?? @"Server=.\SQLEXPRESS01;Database=HhhLocal;Trusted_Connection=True;TrustServerCertificate=True").Options);
}
