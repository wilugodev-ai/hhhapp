using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hhh.Domain;
using Hhh.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hhh.Api;

public sealed class AdministrationException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
public sealed record CreateUserRequest(string Email, string DisplayName, string Role);
public sealed record UpdateUserRequest(string DisplayName, string Role, bool IsActive, int ExpectedVersion);
public sealed record AccessVersionRequest(int ExpectedVersion);
public sealed record CreateOrganizationRequest(string Name, string AdminEmail, string AdminName);
public sealed record UpdateOrganizationRequest(string Name, bool IsActive, int ExpectedVersion);
public sealed record AcceptInvitationRequest(string UserId, string Token, string Password);
public sealed record FirstAdministratorRequest(string? SetupKey, string Email, string DisplayName, string OrganizationName, string? Password);
public sealed record AdminUserRow(string Id, string Email, string DisplayName, string Role, bool IsActive,
    bool InvitationPending, DateTimeOffset? InvitationExpiresAt, DateTimeOffset? LockoutEnd, int Version);
public sealed record InvitationResult(AdminUserRow User, string SetupPath, DateTimeOffset ExpiresAt);

public sealed class AdministrationService(HhhDbContext db, UserManager<AppUser> users, IConfiguration configuration)
{
    public static bool IsSystem(ClaimsPrincipal actor) => actor.IsInRole(Roles.SystemAdministrator);
    private static string ActorId(ClaimsPrincipal actor) => actor.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static Guid OwnOrganization(ClaimsPrincipal actor) => Guid.Parse(actor.FindFirstValue("organization")!);
    private static readonly string[] OrgRoles = Roles.All.Where(x => x != Roles.SystemAdministrator).ToArray();

    public async Task<object> Organizations(ClaimsPrincipal actor, CancellationToken ct) => await db.Organizations.AsNoTracking()
        .Where(o => IsSystem(actor) || o.Id == OwnOrganization(actor)).OrderBy(o => o.Name)
        .Select(o => new { o.Id, o.Name, o.IsActive, version = o.AccessVersion,
            userCount = db.Users.Count(u => u.OrganizationId == o.Id),
            activeUsers = db.Users.Count(u => u.OrganizationId == o.Id && u.IsActive && !u.InvitationPending) }).ToListAsync(ct);

    private async Task<Organization> Scope(ClaimsPrincipal actor, Guid orgId, CancellationToken ct)
    {
        if (!IsSystem(actor) && orgId != OwnOrganization(actor)) throw new AdministrationException(404, "Organization not found.");
        return await db.Organizations.SingleOrDefaultAsync(o => o.Id == orgId, ct)
            ?? throw new AdministrationException(404, "Organization not found.");
    }

    public async Task<object> Directory(ClaimsPrincipal actor, Guid orgId, string? search, string? status, int page, CancellationToken ct)
    {
        await Scope(actor, orgId, ct);
        if (page < 1 || page > 100000 || search?.Length > 180 || status is not (null or "" or "active" or "inactive" or "invited"))
            throw new ArgumentException("Invalid user directory filters.");
        var query = db.Users.AsNoTracking().Where(u => u.OrganizationId == orgId);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(u => u.DisplayName.Contains(term) || u.Email!.Contains(term)); }
        query = status switch { "active" => query.Where(u => u.IsActive && !u.InvitationPending), "inactive" => query.Where(u => !u.IsActive),
            "invited" => query.Where(u => u.IsActive && u.InvitationPending), _ => query };
        var total = await query.CountAsync(ct);
        var records = await query.OrderBy(u => u.DisplayName).ThenBy(u => u.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct);
        var ids = records.Select(u => u.Id).ToArray();
        var assignments = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
            where ids.Contains(ur.UserId) select new { ur.UserId, r.Name }).ToListAsync(ct);
        return new { total, page, pageSize = 25, roles = IsSystem(actor) ? Roles.All : OrgRoles,
            users = records.Select(u => Row(u, assignments.Where(a => a.UserId == u.Id).Select(a => a.Name!).ToArray())) };
    }

    public async Task<object> Activity(ClaimsPrincipal actor, Guid orgId, int page, CancellationToken ct)
    {
        await Scope(actor, orgId, ct);
        if (page is < 1 or > 100000) throw new ArgumentException("Invalid audit page.");
        var query = db.AuditEvents.AsNoTracking().Where(a => a.OrganizationId == orgId);
        var total = await query.CountAsync(ct);
        var items = await (from a in query join u in db.Users on a.UserId equals u.Id into actors
            from u in actors.DefaultIfEmpty() orderby a.OccurredAt descending, a.Id descending
            select new { a.Id, a.Action, a.OccurredAt, actor = u == null ? "Former user" : u.DisplayName,
                a.DetailsJson }).Skip((page - 1) * 25).Take(25).ToListAsync(ct);
        return new { total, page, pageSize = 25, items };
    }

    // Serialize access administration across API instances, including last-admin checks and link consumption.
    private async Task<IDbContextTransaction> Lock(CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource=N'HHH:AccessAdministration', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Access administration is busy. Try again.', 1;", ct);
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    private async Task AuthorizeWrite(ClaimsPrincipal actor, CancellationToken ct)
    {
        // Recheck after acquiring the lock: a waiting request may have lost its privileges.
        var id = ActorId(actor);
        var current = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
        var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == id select r.Name!).ToArrayAsync(ct);
        if (current is null || !current.IsActive || current.InvitationPending || current.OrganizationId != OwnOrganization(actor) || current.SecurityStamp != actor.FindFirstValue("AspNet.Identity.SecurityStamp")
            || !await db.Organizations.AnyAsync(o => o.Id == current.OrganizationId && o.IsActive, ct)
            || !roles.ToHashSet().SetEquals(actor.FindAll(ClaimTypes.Role).Select(c => c.Value)))
            throw new AdministrationException(401, "Your access changed. Sign in again.");
        if (!roles.Contains(Roles.SystemAdministrator) && !roles.Contains(Roles.OrganizationAdministrator))
            throw new AdministrationException(403, "Administrator access is required.");
    }

    private static string Name(string? value)
    {
        var name = value?.Trim() ?? "";
        if (name.Length is < 2 or > 180 || name.Any(char.IsControl)) throw new ArgumentException("Enter a name between 2 and 180 characters.");
        return name;
    }
    private static string Email(string? value)
    {
        var email = value?.Trim() ?? "";
        if (email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email || email.Any(char.IsWhiteSpace))
            throw new ArgumentException("Enter a valid work email address.");
        return email;
    }
    private static void Role(ClaimsPrincipal actor, string? role)
    {
        if (!Roles.All.Contains(role)) throw new ArgumentException("Select an available role.");
        if (role == Roles.SystemAdministrator && !IsSystem(actor)) throw new AdministrationException(403, "Only a system administrator can assign platform access.");
    }
    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new ArgumentException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
    private static AdminUserRow Row(AppUser user, IList<string> roles) => new(user.Id, user.Email!, user.DisplayName,
        roles.Contains(Roles.SystemAdministrator) ? Roles.SystemAdministrator : roles.FirstOrDefault() ?? "Unassigned",
        user.IsActive, user.InvitationPending, user.InvitationExpiresAt, user.LockoutEnd, user.AccessVersion);
    private void Audit(Guid orgId, string actorId, string action, object details) => db.AuditEvents.Add(new()
    {
        OrganizationId = orgId, UserId = actorId, Action = action, OccurredAt = DateTimeOffset.UtcNow,
        DetailsJson = JsonSerializer.Serialize(details)
    });
    private static void Version(AppUser user, int expected)
    {
        if (user.AccessVersion != expected) throw new AdministrationException(409, "This account changed. Reload the directory before saving.");
    }
    private async Task<(AppUser User, IList<string> Roles)> Subject(ClaimsPrincipal actor, Guid orgId, string id, int version, CancellationToken ct)
    {
        await Scope(actor, orgId, ct);
        var user = await db.Users.SingleOrDefaultAsync(u => u.OrganizationId == orgId && u.Id == id, ct)
            ?? throw new AdministrationException(404, "User not found.");
        if (user.Id == ActorId(actor)) throw new AdministrationException(409, "Another administrator must change your account. You cannot modify your own access here.");
        var roles = await users.GetRolesAsync(user);
        if (roles.Contains(Hhh.Domain.Roles.SystemAdministrator) && !IsSystem(actor)) throw new AdministrationException(403, "Only a system administrator can manage this account.");
        Version(user, version);
        return (user, roles);
    }
    private async Task GuardAdministrator(AppUser user, IList<string> roles, bool retainsOrgAdmin, bool retainsSystem, CancellationToken ct)
    {
        if (!user.IsActive || user.InvitationPending) return;
        var active = db.Users.Where(u => u.Id != user.Id && u.IsActive && !u.InvitationPending);
        var adminRoleIds = db.Roles.Where(r => r.Name == Roles.OrganizationAdministrator || r.Name == Roles.SystemAdministrator).Select(r => r.Id);
        if (!retainsOrgAdmin && roles.Any(r => r == Roles.OrganizationAdministrator || r == Roles.SystemAdministrator)
            && !await active.AnyAsync(u => u.OrganizationId == user.OrganizationId && db.UserRoles.Any(ur => ur.UserId == u.Id && adminRoleIds.Contains(ur.RoleId)), ct))
            throw new AdministrationException(409, "Keep at least one active administrator in this organization. Activate another administrator first.");
        if (!retainsSystem && roles.Contains(Roles.SystemAdministrator))
        {
            var systemRoleIds = db.Roles.Where(r => r.Name == Roles.SystemAdministrator).Select(r => r.Id);
            if (!await active.AnyAsync(u => db.Organizations.Any(o => o.Id == u.OrganizationId && o.IsActive)
                && db.UserRoles.Any(ur => ur.UserId == u.Id && systemRoleIds.Contains(ur.RoleId)), ct))
                throw new AdministrationException(409, "Keep at least one active system administrator.");
        }
    }
    private static InvitationResult Issue(AppUser user, IList<string> roles)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        user.InvitationHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        user.InvitationExpiresAt = DateTimeOffset.UtcNow.AddHours(48);
        user.InvitationPending = true;
        return new(Row(user, roles), $"/activate#user={Uri.EscapeDataString(user.Id)}&token={token}", user.InvitationExpiresAt.Value);
    }

    private async Task<InvitationResult> AddUser(Guid orgId, CreateUserRequest request, ClaimsPrincipal actor)
    {
        Role(actor, request.Role);
        var email = Email(request.Email);
        if (await users.FindByEmailAsync(email) is not null) throw new AdministrationException(409, "This email cannot be registered. Use a different email or manage the existing account.");
        var user = new AppUser { OrganizationId = orgId, Email = email, UserName = email, DisplayName = Name(request.DisplayName), InvitationPending = true };
        var invitation = Issue(user, [request.Role]);
        Ensure(await users.CreateAsync(user));
        Ensure(await users.AddToRoleAsync(user, request.Role));
        Audit(orgId, ActorId(actor), "admin.user.invited", new { subjectId = user.Id, email, role = request.Role, expiresAt = invitation.ExpiresAt });
        return invitation;
    }

    public async Task<InvitationResult> CreateUser(ClaimsPrincipal actor, Guid orgId, CreateUserRequest request, CancellationToken ct)
    {
        await using var transaction = await Lock(ct);
        await AuthorizeWrite(actor, ct);
        var org = await Scope(actor, orgId, ct);
        if (!org.IsActive) throw new AdministrationException(409, "Reactivate the organization before inviting users.");
        var result = await AddUser(orgId, request, actor);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result;
    }

    public async Task<AdminUserRow> UpdateUser(ClaimsPrincipal actor, Guid orgId, string id, UpdateUserRequest request, CancellationToken ct)
    {
        await using var transaction = await Lock(ct); await AuthorizeWrite(actor, ct);
        var (user, roles) = await Subject(actor, orgId, id, request.ExpectedVersion, ct);
        Role(actor, request.Role);
        var isAdmin = request.Role == Roles.OrganizationAdministrator || request.Role == Roles.SystemAdministrator;
        await GuardAdministrator(user, roles, request.IsActive && !user.InvitationPending && isAdmin,
            request.IsActive && !user.InvitationPending && request.Role == Roles.SystemAdministrator, ct);
        var before = new { user.DisplayName, roles, user.IsActive };
        user.DisplayName = Name(request.DisplayName); user.IsActive = request.IsActive; user.AccessVersion++;
        if (!request.IsActive) { user.InvitationHash = null; user.InvitationExpiresAt = null; }
        // Every administration change invalidates existing cookies, even if the role later changes back.
        Ensure(await users.UpdateSecurityStampAsync(user));
        if (roles.Count != 1 || roles[0] != request.Role)
        {
            if (roles.Count > 0) Ensure(await users.RemoveFromRolesAsync(user, roles));
            Ensure(await users.AddToRoleAsync(user, request.Role));
        }
        Audit(orgId, ActorId(actor), "admin.user.updated", new { subjectId = id, before, after = new { user.DisplayName, role = request.Role, user.IsActive } });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Row(user, [request.Role]);
    }

    public async Task<object> AccountAction(ClaimsPrincipal actor, Guid orgId, string id, string action, int version, CancellationToken ct)
    {
        await using var transaction = await Lock(ct); await AuthorizeWrite(actor, ct);
        var (user, roles) = await Subject(actor, orgId, id, version, ct);
        if (action is "invitation" or "recovery")
        {
            if (!user.IsActive || !await db.Organizations.AnyAsync(o => o.Id == orgId && o.IsActive, ct)) throw new AdministrationException(409, "Activate the organization and account before issuing a setup link.");
            if (action == "invitation" && !user.InvitationPending) throw new AdministrationException(409, "This account is already set up. Use password recovery instead.");
            await GuardAdministrator(user, roles, false, false, ct);
        }
        else if (action is not ("revoke" or "unlock")) throw new ArgumentException("Unknown account action.");
        user.AccessVersion++;
        InvitationResult? link = action is "invitation" or "recovery" ? Issue(user, roles) : null;
        if (action == "unlock") { user.LockoutEnd = null; user.AccessFailedCount = 0; }
        Ensure(await users.UpdateSecurityStampAsync(user));
        Audit(orgId, ActorId(actor), "admin.user." + action, new { subjectId = id, version = user.AccessVersion, expiresAt = link?.ExpiresAt });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return (object?)link ?? Row(user, roles);
    }

    public async Task<object> CreateOrganization(ClaimsPrincipal actor, CreateOrganizationRequest request, CancellationToken ct)
    {
        await using var transaction = await Lock(ct); await AuthorizeWrite(actor, ct);
        if (!IsSystem(actor)) throw new AdministrationException(403, "System administrator access is required.");
        var org = new Organization { Id = Guid.NewGuid(), Name = Name(request.Name) };
        db.Organizations.Add(org); await db.SaveChangesAsync(ct);
        var invitation = await AddUser(org.Id, new(request.AdminEmail, request.AdminName, Roles.OrganizationAdministrator), actor);
        Audit(org.Id, ActorId(actor), "admin.organization.created", new { org.Id, org.Name });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new { organizationId = org.Id, invitation };
    }

    public async Task<object> UpdateOrganization(ClaimsPrincipal actor, Guid id, UpdateOrganizationRequest request, CancellationToken ct)
    {
        await using var transaction = await Lock(ct); await AuthorizeWrite(actor, ct);
        if (!IsSystem(actor)) throw new AdministrationException(403, "System administrator access is required.");
        var org = await Scope(actor, id, ct);
        if (org.AccessVersion != request.ExpectedVersion) throw new AdministrationException(409, "This organization changed. Reload before saving.");
        if (!request.IsActive)
        {
            if (id == OwnOrganization(actor)) throw new AdministrationException(409, "You cannot deactivate your own organization.");
            // Rotate stamps so old cookies stay invalid after organization reactivation.
            foreach (var user in await db.Users.Where(u => u.OrganizationId == id).ToListAsync(ct))
            {
                user.SecurityStamp = Guid.NewGuid().ToString(); user.AccessVersion++;
                user.InvitationHash = null; user.InvitationExpiresAt = null;
            }
        }
        var before = new { org.Name, org.IsActive };
        org.Name = Name(request.Name); org.IsActive = request.IsActive; org.AccessVersion++;
        Audit(id, ActorId(actor), "admin.organization.updated", new { before, after = new { org.Name, org.IsActive } });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new { org.Id, org.Name, org.IsActive, version = org.AccessVersion };
    }

    public async Task Accept(AcceptInvitationRequest request, CancellationToken ct)
    {
        if (request.UserId is null || request.UserId.Length > 128 || request.Token is null || request.Token.Length is < 32 or > 100
            || request.Password is null || request.Password.Length is < 12 or > 256) throw new ArgumentException("Use a valid setup link and a password of 12 to 256 characters.");
        await using var transaction = await Lock(ct);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == request.UserId, ct);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Token));
        if (user is null || !user.IsActive || !user.InvitationPending || user.InvitationHash is null || user.InvitationExpiresAt is null || user.InvitationExpiresAt <= DateTimeOffset.UtcNow
            || !CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(user.InvitationHash))
            || !await db.Organizations.AnyAsync(o => o.Id == user.OrganizationId && o.IsActive, ct))
            throw new ArgumentException("This setup link is invalid or expired. Ask your administrator for a new link.");
        await CompleteAccount(user, request.Password, true, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }

    private async Task CompleteAccount(AppUser user, string password, bool emailConfirmed, CancellationToken ct)
    {
        user.PasswordHash = null;
        user.InvitationPending = false; user.InvitationHash = null; user.InvitationExpiresAt = null;
        user.EmailConfirmed = emailConfirmed;
        user.LockoutEnd = null; user.AccessFailedCount = 0; user.AccessVersion++;
        Ensure(await users.AddPasswordAsync(user, password));
        if (await users.IsInRoleAsync(user, Roles.SystemAdministrator))
        {
            var installation = await db.InstallationStates.SingleAsync(s => s.Id == 1, ct);
            installation.AdministratorSetupCompletedAt ??= DateTimeOffset.UtcNow;
        }
        Audit(user.OrganizationId, user.Id, "auth.account.setup", new { subjectId = user.Id });
    }

    private string? ConfiguredSetupKey()
    {
        var key = configuration["Bootstrap:SetupKey"];
        return key is { Length: >= 32 and <= 256 } && !key.Any(char.IsWhiteSpace) ? key : null;
    }

    private async Task<bool> SetupClosed(CancellationToken ct)
    {
        if (await db.InstallationStates.AnyAsync(s => s.Id == 1 && s.AdministratorSetupCompletedAt != null, ct)) return true;
        var systemRoles = db.Roles.Where(r => r.Name == Roles.SystemAdministrator).Select(r => r.Id);
        var existing = db.Users.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && systemRoles.Contains(ur.RoleId)));
        return await existing.CountAsync(ct) > 1 || await existing.AnyAsync(u => !u.IsActive || !u.InvitationPending || u.PasswordHash != null
            || !db.Organizations.Any(o => o.Id == u.OrganizationId && o.IsActive), ct);
    }

    public async Task<bool> SetupAvailable(CancellationToken ct) => ConfiguredSetupKey() is not null && !await SetupClosed(ct);

    public async Task<string> SetupState(CancellationToken ct) => await SetupClosed(ct) ? "configured"
        : ConfiguredSetupKey() is null ? "disabled" : "available";

    public async Task CreateFirstAdministrator(FirstAdministratorRequest request, CancellationToken ct)
    {
        if (!await SetupAvailable(ct)) throw new AdministrationException(404, "First-administrator setup is unavailable.");
        ValidateSetupKey(request.SetupKey);
        await using var transaction = await Lock(ct);
        // Recheck inside the same database lock used by CLI bootstrap and account activation.
        if (!await SetupAvailable(ct)) throw new AdministrationException(404, "First-administrator setup is unavailable.");
        ValidateSetupKey(request.SetupKey);
        if (request.Password is null || request.Password.Length is < 12 or > 256) throw new ArgumentException("Use a password of 12 to 256 characters.");
        var name = Name(request.DisplayName); var organization = Name(request.OrganizationName);
        var invitation = await BootstrapCore(request.Email, name, organization, ct);
        var user = await db.Users.SingleAsync(u => u.Id == invitation.User.Id, ct);
        user.DisplayName = name;
        var org = await db.Organizations.SingleAsync(o => o.Id == user.OrganizationId, ct); org.Name = organization;
        await CompleteAccount(user, request.Password, false, ct);
        Audit(org.Id, user.Id, "admin.platform.browser-setup.completed", new { subjectId = user.Id });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }

    private void ValidateSetupKey(string? supplied)
    {
        var expected = ConfiguredSetupKey();
        if (expected is null) throw new AdministrationException(404, "First-administrator setup is unavailable.");
        if (supplied is null || supplied.Length is < 32 or > 256 || !CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
            throw new AdministrationException(403, "The setup key is not valid. Use the key provided for this installation.");
    }

    // The operator's CLI command remains available; the browser must supply its installation key.
    public async Task<InvitationResult> Bootstrap(string email, string name, string organization, CancellationToken ct)
    {
        await using var transaction = await Lock(ct);
        var invitation = await BootstrapCore(email, name, organization, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return invitation;
    }

    private async Task<InvitationResult> BootstrapCore(string email, string name, string organization, CancellationToken ct)
    {
        if (await db.InstallationStates.AnyAsync(s => s.Id == 1 && s.AdministratorSetupCompletedAt != null, ct))
            throw new AdministrationException(409, "Administrator setup has already been completed.");
        var systemRoles = db.Roles.Where(r => r.Name == Roles.SystemAdministrator).Select(r => r.Id);
        var address = Email(email);
        var existing = await db.Users.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && systemRoles.Contains(ur.RoleId))).ToListAsync(ct);
        if (existing.Count != 0)
        {
            // Only the first, never-activated administrator can receive another bootstrap link.
            if (existing.Count != 1 || !existing[0].IsActive || !existing[0].InvitationPending || existing[0].PasswordHash is not null
                || !string.Equals(existing[0].NormalizedEmail, users.NormalizeEmail(address), StringComparison.Ordinal)
                || !await db.Organizations.AnyAsync(o => o.Id == existing[0].OrganizationId && o.IsActive, ct))
                throw new AdministrationException(409, "A system administrator already exists. Bootstrap cannot replace an established administrator.");
            var pending = existing[0]; pending.AccessVersion++;
            var replacement = Issue(pending, [Roles.SystemAdministrator]);
            Ensure(await users.UpdateSecurityStampAsync(pending));
            Audit(pending.OrganizationId, pending.Id, "admin.platform.bootstrap-link-reissued", new { subjectId = pending.Id });
            return replacement;
        }
        if (await users.FindByEmailAsync(address) is not null) throw new ArgumentException("The bootstrap email is already registered.");
        var org = new Organization { Id = Guid.NewGuid(), Name = Name(organization) };
        var user = new AppUser { Email = address, UserName = address, DisplayName = Name(name), OrganizationId = org.Id };
        var invitation = Issue(user, [Roles.SystemAdministrator]);
        db.Organizations.Add(org); await db.SaveChangesAsync(ct);
        Ensure(await users.CreateAsync(user)); Ensure(await users.AddToRoleAsync(user, Roles.SystemAdministrator));
        Audit(org.Id, user.Id, "admin.platform.bootstrapped", new { subjectId = user.Id, org.Name });
        return invitation;
    }
}
