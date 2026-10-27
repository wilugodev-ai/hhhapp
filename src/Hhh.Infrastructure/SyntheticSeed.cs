using Hhh.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hhh.Infrastructure;

public static class SyntheticSeed
{
    public const string DemoPassword = "Hhh-Demo-2026!";

    public static async Task RunAsync(HhhDbContext db, UserManager<AppUser> users, RoleManager<IdentityRole> roles)
    {
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role)) await Ensure(roles.CreateAsync(new IdentityRole(role)));

        for (var orgIndex = 0; orgIndex < 2; orgIndex++)
        {
            var orgId = Guid.Parse(orgIndex == 0 ? "11111111-1111-1111-1111-111111111111" : "22222222-2222-2222-2222-222222222222");
            if (!await db.Organizations.AnyAsync(x => x.Id == orgId))
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                db.Organizations.Add(new() { Id = orgId, Name = orgIndex == 0 ? "Horizon Care Partners" : "Summit Health Group" });
                await db.SaveChangesAsync();
                await StarterFinancialData.RunAsync(db, orgId, orgIndex);
                await transaction.CommitAsync();
            }
            else await StarterFinancialData.RunAsync(db, orgId, orgIndex);

            var accounts = orgIndex == 0
                ? new[] { ("demo@hhh.local", "Alex Morgan", Roles.Cfo), ("analyst@hhh.local", "Jordan Lee", Roles.Analyst),
                    ("viewer@hhh.local", "Taylor Reed", Roles.Viewer), ("admin@hhh.local", "Casey Rivera", Roles.OrganizationAdministrator),
                    ("sysadmin@hhh.local", "Morgan Blake", Roles.SystemAdministrator) }
                : new[] { ("summit@hhh.local", "Sam Patel", Roles.Cfo), ("summit-admin@hhh.local", "Jamie Park", Roles.OrganizationAdministrator) };
            foreach (var (email, name, role) in accounts)
            {
                if (await users.FindByEmailAsync(email) is not null) continue;
                var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = name, OrganizationId = orgId };
                await Ensure(users.CreateAsync(user, DemoPassword));
                await Ensure(users.AddToRoleAsync(user, role));
            }

        }
        var installation = await db.InstallationStates.SingleAsync(s => s.Id == 1);
        if (installation.AdministratorSetupCompletedAt is null && await (from ur in db.UserRoles
            join role in db.Roles on ur.RoleId equals role.Id
            join user in db.Users on ur.UserId equals user.Id
            where role.Name == Roles.SystemAdministrator && (user.PasswordHash != null || !user.InvitationPending)
            select user.Id).AnyAsync())
        {
            installation.AdministratorSetupCompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    private static async Task Ensure(Task<IdentityResult> task)
    {
        var result = await task;
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
