using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Hhh.Api;
using Hhh.Domain;
using Hhh.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 32 * 1024);
builder.Services.AddProblemDetails();
string Connection() => builder.Configuration.GetConnectionString("Hhh") ?? throw new InvalidOperationException("Configure ConnectionStrings__Hhh.");
var postgres = string.Equals(builder.Configuration["Database:Provider"], "PostgreSql", StringComparison.OrdinalIgnoreCase);
if (postgres)
{
    builder.Services.AddDbContext<PostgresHhhDbContext>(o => o.UseNpgsql(Connection()));
    builder.Services.AddScoped<HhhDbContext>(services => services.GetRequiredService<PostgresHhhDbContext>());
    if (!builder.Environment.IsDevelopment())
    {
        var certificatePath = builder.Configuration["DataProtection:CertificatePath"]
            ?? throw new InvalidOperationException("Configure DataProtection__CertificatePath for persistent, encrypted cloud sessions.");
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, null, X509KeyStorageFlags.EphemeralKeySet);
        builder.Services.AddDataProtection().SetApplicationName("HHH")
            .PersistKeysToDbContext<PostgresHhhDbContext>().ProtectKeysWithCertificate(certificate);
    }
}
else builder.Services.AddDbContext<HhhDbContext>(o => o.UseSqlServer(Connection()));
builder.Services.AddIdentityCore<AppUser>(o =>
{
    o.Password.RequiredLength = 12;
    o.User.RequireUniqueEmail = true;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddRoles<IdentityRole>().AddEntityFrameworkStores<HhhDbContext>().AddSignInManager();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme, o =>
{
    o.Cookie.Name = "hhh.session"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = false;
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    o.Events.OnValidatePrincipal = async ctx =>
    {
        var users = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
        var principal = ctx.Principal;
        if (principal is null) { ctx.RejectPrincipal(); return; }
        var user = await users.GetUserAsync(principal);
        if (user is null || !user.IsActive || user.InvitationPending || !Guid.TryParse(principal.FindFirstValue("organization"), out var org) || user.OrganizationId != org
            || principal.FindFirstValue("AspNet.Identity.SecurityStamp") != user.SecurityStamp)
        {
            ctx.RejectPrincipal();
            return;
        }
        var db = ctx.HttpContext.RequestServices.GetRequiredService<HhhDbContext>();
        if (!await db.Organizations.AnyAsync(o => o.Id == org && o.IsActive)) { ctx.RejectPrincipal(); return; }
        var roles = await users.GetRolesAsync(user);
        if (!roles.ToHashSet(StringComparer.Ordinal).SetEquals(principal.FindAll(ClaimTypes.Role).Select(x => x.Value)))
            ctx.RejectPrincipal();
    };
});
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("ManageSettings", p => p.RequireRole(Roles.Cfo, Roles.OrganizationAdministrator, Roles.SystemAdministrator));
    o.AddPolicy("Audit", p => p.RequireRole(Roles.OrganizationAdministrator, Roles.SystemAdministrator));
    o.AddPolicy("SystemAdmin", p => p.RequireRole(Roles.SystemAdministrator));
});
builder.Services.AddAntiforgery(o => o.HeaderName = "X-XSRF-TOKEN");
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<MlrConfigurationService>();
builder.Services.AddScoped<MlrService>();
builder.Services.AddScoped<ProfitLossService>();
builder.Services.AddScoped<AdministrationService>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue<int?>("Authentication:LoginRequestsPerMinute") ?? 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("setup", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue<int?>("Authentication:SetupRequestsPerMinute") ?? 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-cache, no-store";
    try { await next(); }
    catch (AdministrationException ex) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = ex.Status;
        await context.Response.WriteAsJsonAsync(new { title = ex.Message, status = ex.Status });
    }
    catch (SqlException ex) when (!context.Response.HasStarted && ex.Number == 51000)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { title = "Access administration is busy. Try again.", status = 409 });
    }
    catch (PostgresException ex) when (!context.Response.HasStarted && ex.SqlState == PostgresErrorCodes.LockNotAvailable)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { title = "Access administration is busy. Try again.", status = 409 });
    }
    catch (ArgumentException ex) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { title = ex.Message, status = 400 });
    }
    catch (Exception ex) when (!context.Response.HasStarted && (ex is MlrConfigurationConflict or DbUpdateConcurrencyException
        || ex is DbUpdateException { InnerException: SqlException sql } && sql.Number is 2601 or 2627
        || ex is DbUpdateException { InnerException: PostgresException pg } && pg.SqlState == PostgresErrorCodes.UniqueViolation))
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { title = "The record changed or conflicts with an existing record. Reload before saving again.", status = 409 });
    }
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { title = "Your session token expired. Refresh and try again.", status = 400 });
            return;
        }
    }
    await next();
});

app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/health/ready", async (HhhDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));

app.MapGet("/api/auth/csrf", async (HttpContext ctx, IAntiforgery antiforgery, AdministrationService administration, CancellationToken ct) =>
{
    var tokens = antiforgery.GetAndStoreTokens(ctx);
    ctx.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
    {
        HttpOnly = false,
        SameSite = SameSiteMode.Strict,
        Secure = !app.Environment.IsDevelopment(),
        Path = "/"
    });
    var setupState = await administration.SetupState(ct);
    return Results.Ok(new { demoEnabled = app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Demo:Enabled"), setupAvailable = setupState == "available", setupState, sampleDataEnabled = app.Configuration.GetValue<bool>("SampleData:Enabled") });
});

app.MapPost("/api/auth/setup", async (FirstAdministratorRequest request, AdministrationService administration, CancellationToken ct) =>
{
    await administration.CreateFirstAdministrator(request, ct);
    return Results.Ok(new { message = "Your administrator account is ready. Sign in to continue." });
}).RequireRateLimiting("setup");

app.MapPost("/api/auth/login", async (LoginRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn, HhhDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254 || request.Password is null || request.Password.Length > 256)
        return Results.BadRequest(new { title = "Enter a valid email and password." });
    var user = await users.FindByEmailAsync(request.Email);
    if (user is null || !user.IsActive || user.InvitationPending || !await db.Organizations.AnyAsync(o => o.Id == user.OrganizationId && o.IsActive))
        return Results.Json(new { title = "Email or password is incorrect, or the account is unavailable." }, statusCode: 401);
    var result = await signIn.CheckPasswordSignInAsync(user, request.Password, true);
    if (!result.Succeeded) return Results.Json(new { title = "Email or password is incorrect, or this account is temporarily locked." }, statusCode: 401);
    await signIn.SignInWithClaimsAsync(user, false, [new Claim("organization", user.OrganizationId.ToString())]);
    db.AuditEvents.Add(new() { OrganizationId = user.OrganizationId, UserId = user.Id, Action = "auth.login", OccurredAt = DateTimeOffset.UtcNow });
    await db.SaveChangesAsync();
    return Results.Ok(new { success = true });
}).RequireRateLimiting("login");

app.MapPost("/api/auth/demo", async (UserManager<AppUser> users, SignInManager<AppUser> signIn, HhhDbContext db) =>
{
    if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("Demo:Enabled")) return Results.NotFound();
    var user = await users.FindByEmailAsync("demo@hhh.local");
    if (user is null || !user.IsActive || user.InvitationPending || !await db.Organizations.AnyAsync(o => o.Id == user.OrganizationId && o.IsActive)) return Results.NotFound();
    await signIn.SignInWithClaimsAsync(user, false, [new Claim("organization", user.OrganizationId.ToString())]);
    db.AuditEvents.Add(new() { OrganizationId = user.OrganizationId, UserId = user.Id, Action = "auth.demo", OccurredAt = DateTimeOffset.UtcNow });
    await db.SaveChangesAsync();
    return Results.Ok(new { success = true });
}).RequireRateLimiting("login");

app.MapPost("/api/auth/demo-admin", async (UserManager<AppUser> users, SignInManager<AppUser> signIn, HhhDbContext db) =>
{
    if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("Demo:Enabled")) return Results.NotFound();
    var user = await users.FindByEmailAsync("sysadmin@hhh.local");
    if (user is null || !user.IsActive || user.InvitationPending || await users.IsLockedOutAsync(user)
        || !await users.IsInRoleAsync(user, Roles.SystemAdministrator)
        || !await db.Organizations.AnyAsync(o => o.Id == user.OrganizationId && o.IsActive)) return Results.NotFound();
    await signIn.SignInWithClaimsAsync(user, false, [new Claim("organization", user.OrganizationId.ToString())]);
    db.AuditEvents.Add(new() { OrganizationId = user.OrganizationId, UserId = user.Id, Action = "auth.demo.admin", OccurredAt = DateTimeOffset.UtcNow });
    await db.SaveChangesAsync();
    return Results.Ok(new { success = true });
}).RequireRateLimiting("login");

app.MapPost("/api/auth/activate", async (AcceptInvitationRequest request, AdministrationService service, CancellationToken ct) =>
{
    await service.Accept(request, ct); return Results.Ok(new { message = "Your account is ready. Sign in with your password." });
}).RequireRateLimiting("login");

var api = app.MapGroup("/api").RequireAuthorization();
api.MapGet("/auth/me", async (ClaimsPrincipal principal, UserManager<AppUser> users, HhhDbContext db) =>
{
    var user = (await users.GetUserAsync(principal))!;
    var orgId = Org(principal);
    var organization = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == orgId);
    return Results.Ok(new { user.Id, organizationId = orgId, user.DisplayName, user.Email, organization = organization.Name, roles = await users.GetRolesAsync(user), organization.MlrTarget });
});
api.MapPost("/auth/logout", async (HttpContext ctx, HhhDbContext db) =>
{
    await Audit(db, ctx.User, "auth.logout");
    await ctx.SignOutAsync(IdentityConstants.ApplicationScheme);
    return Results.Ok(new { success = true });
});
api.MapGet("/analytics/filters", (ClaimsPrincipal principal, AnalyticsService analytics, CancellationToken ct) => analytics.Metadata(Org(principal), ct));
var administration = api.MapGroup("/admin").RequireAuthorization("Audit");
administration.MapGet("/organizations", (ClaimsPrincipal actor, AdministrationService service, CancellationToken ct) => service.Organizations(actor, ct));
administration.MapPost("/organizations", (CreateOrganizationRequest request, ClaimsPrincipal actor, AdministrationService service, CancellationToken ct) => service.CreateOrganization(actor, request, ct)).RequireAuthorization("SystemAdmin");
administration.MapPut("/organizations/{orgId:guid}", (Guid orgId, UpdateOrganizationRequest request, ClaimsPrincipal actor, AdministrationService service, CancellationToken ct) => service.UpdateOrganization(actor, orgId, request, ct)).RequireAuthorization("SystemAdmin");
administration.MapGet("/organizations/{orgId:guid}/users", (Guid orgId, string? search, string? status, int? page, ClaimsPrincipal actor, AdministrationService service, CancellationToken ct) => service.Directory(actor, orgId, search, status, page ?? 1, ct));
administration.MapGet("/organizations/{orgId:guid}/audit", (Guid orgId, int? page, ClaimsPrincipal actor, AdministrationService service, CancellationToken ct) => service.Activity(actor, orgId, page ?? 1, ct));
administration.MapPost("/organizations/{orgId:guid}/users", (Guid orgId, CreateUserRequest request, ClaimsPrincipal actor, AdministrationService service, CancellationToken ct) => service.CreateUser(actor, orgId, request, ct));
administration.MapPut("/organizations/{orgId:guid}/users/{id}", (Guid orgId, string id, UpdateUserRequest request, ClaimsPrincipal actor, AdministrationService service, CancellationToken ct) => service.UpdateUser(actor, orgId, id, request, ct));
foreach (var action in new[] { "invitation", "recovery", "revoke", "unlock" })
{
    var operation = action;
    administration.MapPost("/organizations/{orgId:guid}/users/{id}/" + operation,
        (Guid orgId, string id, AccessVersionRequest request, ClaimsPrincipal actor, AdministrationService service, CancellationToken ct) => service.AccountAction(actor, orgId, id, operation, request.ExpectedVersion, ct));
}
api.MapGet("/analytics/dashboard", (HttpRequest req, ClaimsPrincipal principal, AnalyticsService analytics, CancellationToken ct) =>
    analytics.Dashboard(Org(principal), AnalyticsFilter.Parse(req.Query), ct));
api.MapGet("/analytics/mlr", (HttpRequest req, ClaimsPrincipal principal, MlrService service, CancellationToken ct) =>
    service.Report(Org(principal), AnalyticsFilter.Parse(req.Query), MlrOptions.Parse(req.Query), ct));
api.MapGet("/mlr/configuration", (ClaimsPrincipal principal, MlrConfigurationService service, CancellationToken ct) => service.Read(Org(principal), ct));
api.MapPut("/mlr/definition", (MlrDefinitionRequest request, ClaimsPrincipal principal, MlrConfigurationService service, CancellationToken ct) =>
    service.Save(Org(principal), principal.FindFirstValue(ClaimTypes.NameIdentifier)!, request, ct)).RequireAuthorization("ManageSettings");
api.MapPut("/mlr/targets/{dimensionId:guid}", (Guid dimensionId, MlrTargetRequest request, ClaimsPrincipal principal, MlrConfigurationService service, CancellationToken ct) =>
    service.SaveTarget(Org(principal), principal.FindFirstValue(ClaimTypes.NameIdentifier)!, dimensionId, request, ct)).RequireAuthorization("ManageSettings");
api.MapDelete("/mlr/targets/{dimensionId:guid}", async (Guid dimensionId, HttpRequest request, ClaimsPrincipal principal, MlrConfigurationService service, CancellationToken ct) =>
{
    if (!int.TryParse(request.Query["version"], out var version) || version < 1) throw new ArgumentException("Provide the current target version.");
    await service.RemoveTarget(Org(principal), principal.FindFirstValue(ClaimTypes.NameIdentifier)!, dimensionId, version, ct);
    return Results.NoContent();
}).RequireAuthorization("ManageSettings");
api.MapGet("/reports/mlr.csv", async (HttpRequest req, ClaimsPrincipal principal, MlrService service, HhhDbContext db, CancellationToken ct) =>
{
    var report = await service.Report(Org(principal), AnalyticsFilter.Parse(req.Query), MlrOptions.Parse(req.Query), ct);
    var csv = new StringBuilder("Definition,Definition version,End month,Window months,Group by,Group,Included revenue,Included medical expenses,MLR percent,Target percent,Gap percentage points,Previous MLR percent,Change percentage points,Status,Missing months,Components complete\r\n");
    foreach (var row in report.Groups)
        csv.AppendLine(string.Join(",", new object?[] { report.DefinitionName, report.DefinitionVersion, report.EndMonth, report.Window, report.GroupBy, row.Name,
            row.Values.Revenue, row.Values.Expenses, row.Values.Mlr, row.Target, row.GapPercentagePoints, row.Previous.Mlr, row.ChangePercentagePoints,
            row.Status, string.Join(";", row.Values.MissingMonths), row.Values.ComponentsComplete }.Select(Csv)));
    await Audit(db, principal, "report.mlr.csv.exported");
    return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8", "hhh-mlr.csv");
});
api.MapGet("/analytics/profit-loss", (HttpRequest req, ClaimsPrincipal principal, ProfitLossService service, CancellationToken ct) =>
    service.Report(Org(principal), AnalyticsFilter.Parse(req.Query), ProfitLossOptions.Parse(req.Query), ct));
api.MapGet("/analytics/profit-loss/details", (HttpRequest req, ClaimsPrincipal principal, ProfitLossService service, CancellationToken ct) =>
    service.Details(Org(principal), AnalyticsFilter.Parse(req.Query), ProfitLossOptions.Parse(req.Query), ct));
api.MapGet("/reports/profit-loss.csv", async (HttpRequest req, ClaimsPrincipal principal, ProfitLossService service, HhhDbContext db, CancellationToken ct) =>
{
    var report = await service.Report(Org(principal), AnalyticsFilter.Parse(req.Query), ProfitLossOptions.Parse(req.Query), ct);
    var csv = new StringBuilder("Period,Start month,End month,Compared through,Budget plan,Budget version,Component,Type,Actual,Budget,Actual minus budget,Variance percent,Variance status,Prior year,Prior year change percent,Previous period,Previous period change percent\r\n");
    foreach (var line in report.Lines.Concat(report.Totals))
        csv.AppendLine(string.Join(",", new object?[] { report.Period, report.StartMonth, report.EndMonth, report.ComparedThroughMonth,
            report.BudgetPlan?.Name, report.BudgetPlan?.Version, line.Category, line.Type, line.Actual, line.Budget, line.BudgetVariance.Amount,
            line.BudgetVariance.Percent, line.BudgetVariance.Status, line.PriorYear, line.PriorYearChangePercent, line.PreviousPeriod, line.PreviousPeriodChangePercent }.Select(Csv)));
    await Audit(db, principal, "report.profit-loss.csv.exported");
    return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8", "hhh-profit-loss.csv");
});
api.MapGet("/data/batches", async (ClaimsPrincipal principal, HhhDbContext db, CancellationToken ct) =>
{
    var orgId = Org(principal);
    return await db.SourceBatches.AsNoTracking().Where(x => x.OrganizationId == orgId).OrderByDescending(x => x.LoadedAt)
        .Select(x => new { x.Id, x.Name, x.LoadedAt, x.IsSynthetic, x.SnapshotCount }).ToListAsync(ct);
});
api.MapGet("/audit", async (ClaimsPrincipal principal, HhhDbContext db, CancellationToken ct) =>
{
    var orgId = Org(principal);
    return await db.AuditEvents.AsNoTracking().Where(x => x.OrganizationId == orgId).OrderByDescending(x => x.OccurredAt).Take(100)
        .Select(x => new { x.Id, x.Action, x.OccurredAt, x.DetailsJson }).ToListAsync(ct);
}).RequireAuthorization("Audit");
api.MapPut("/settings", async (SettingsRequest request, ClaimsPrincipal principal, HhhDbContext db) =>
{
    if (request.MlrTarget is < 1 or > 100) return Results.BadRequest(new { title = "MLR target must be between 1 and 100%." });
    var orgId = Org(principal);
    var org = await db.Organizations.SingleAsync(x => x.Id == orgId);
    var previousTarget = org.MlrTarget;
    org.MlrTarget = decimal.Round(request.MlrTarget, 2);
    await Audit(db, principal, "settings.mlr-target.updated", new { previousTarget, target = org.MlrTarget });
    return Results.Ok(new { org.MlrTarget });
}).RequireAuthorization("ManageSettings");
api.MapGet("/reports/financial.csv", async (HttpRequest request, ClaimsPrincipal principal, AnalyticsService analytics, MlrConfigurationService mlrConfiguration, HhhDbContext db, CancellationToken ct) =>
{
    var filter = AnalyticsFilter.Parse(request.Query);
    var rows = await analytics.Rows(Org(principal), filter, ct);
    if (filter.Month is { } month) rows = rows.Where(x => x.EffectiveMonth.ToString("yyyy-MM") == month).ToList();
    else if (filter.Year is { } year) rows = rows.Where(x => x.EffectiveMonth.Year == year).ToList();
    else if (rows.Count > 0) { var latest = rows.Max(x => x.EffectiveMonth); rows = rows.Where(x => x.EffectiveMonth == latest).ToList(); }
    var orgId = Org(principal);
    var mlrSelection = await mlrConfiguration.Selection(orgId, ct);
    var names = await db.Dimensions.AsNoTracking().Where(x => x.OrganizationId == orgId).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
    var sourceIds = rows.Select(s => s.SourceBatchId).Distinct().ToArray();
    var sourceFlags = await db.SourceBatches.Where(b => b.OrganizationId == orgId && sourceIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.IsSynthetic, ct);
    var csv = new StringBuilder("Effective month,Report month,General ledger month,Payer,Medical center,Revenue,Medical expenses,Administrative expenses,Net profit,Member months,Operational MLR,Synthetic,MLR definition,MLR definition version,Included MLR revenue,Included MLR medical expenses\r\n");
    foreach (var row in rows.OrderBy(x => x.EffectiveMonth).ThenBy(x => x.PayerId))
    {
        var m = FinancialMetrics.Calculate([row], mlrSelection);
        csv.AppendLine(string.Join(",", new object?[] { row.EffectiveMonth, row.ReportMonth, row.GeneralLedgerMonth, names[row.PayerId], names[row.MedicalCenterId],
            m.Revenue, m.MedicalExpenses, m.AdministrativeExpenses, m.NetProfit, m.MemberMonths, m.Mlr, sourceFlags.GetValueOrDefault(row.SourceBatchId), mlrSelection.Name, mlrSelection.Version, m.MlrRevenue, m.MlrExpenses }.Select(Csv)));
    }
    await Audit(db, principal, "report.financial.csv.exported");
    return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8", "hhh-financial-report.csv");
});

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup") || args.Contains("--migrate") || args.Contains("--bootstrap-admin"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HhhDbContext>();
    await db.Database.MigrateAsync();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in Roles.All)
    {
        if (await roles.RoleExistsAsync(role)) continue;
        var result = await roles.CreateAsync(new IdentityRole(role));
        if (!result.Succeeded) throw new InvalidOperationException("Could not initialize application roles.");
    }
    if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Demo:Enabled"))
        await SyntheticSeed.RunAsync(db, scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(), scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    if (args.Contains("--bootstrap-admin"))
    {
        var invitation = await scope.ServiceProvider.GetRequiredService<AdministrationService>().Bootstrap(
            app.Configuration["Bootstrap:Email"] ?? "", app.Configuration["Bootstrap:Name"] ?? "", app.Configuration["Bootstrap:Organization"] ?? "", CancellationToken.None);
        Console.WriteLine("System administrator created. Privately open this path on your HHH frontend to set the password (expires in 48 hours):");
        Console.WriteLine(invitation.SetupPath);
    }
}
if (args.Contains("--migrate") || args.Contains("--bootstrap-admin")) return;
app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
await app.RunAsync();

static Guid Org(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue("organization") ?? throw new UnauthorizedAccessException());
static async Task Audit(HhhDbContext db, ClaimsPrincipal principal, string action, object? details = null)
{
    db.AuditEvents.Add(new()
    {
        OrganizationId = Org(principal),
        UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!,
        Action = action,
        OccurredAt = DateTimeOffset.UtcNow,
        DetailsJson = details is null ? null : System.Text.Json.JsonSerializer.Serialize(details)
    });
    await db.SaveChangesAsync();
}
static string Csv(object? value)
{
    var text = value switch { DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), IFormattable number => number.ToString(null, CultureInfo.InvariantCulture), _ => value?.ToString() ?? "" };
    if (text.Length > 0 && "=+-@\t\r".Contains(text[0]) && value is string) text = "'" + text;
    return "\"" + text.Replace("\"", "\"\"") + "\"";
}
public sealed record LoginRequest(string Email, string? Password);
public sealed record SettingsRequest(decimal MlrTarget);
public partial class Program;
