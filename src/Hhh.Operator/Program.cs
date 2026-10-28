using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Hhh.Infrastructure;
using Hhh.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using NpgsqlTypes;

var command = args.FirstOrDefault() ?? "";
try
{
    if (command == "certificate")
    {
        var path = PrivatePath(args.ElementAtOrDefault(1));
        if (File.Exists(path)) throw new InvalidOperationException("Certificate already exists; rotation must be explicit.");
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=HHH Data Protection", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment, true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(3));
        await File.WriteAllBytesAsync(path, certificate.Export(X509ContentType.Pfx));
        Console.WriteLine("Private session-key encryption certificate created.");
        return;
    }
    if (command is "export" or "export-hosted")
    {
        var sourceConnection = Environment.GetEnvironmentVariable("HHH_SOURCE_CONNECTION_STRING")
            ?? "Server=.\\SQLEXPRESS01;Database=HhhLocal;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";
        if (new SqlConnectionStringBuilder(sourceConnection).InitialCatalog != "HhhLocal")
            throw new InvalidOperationException("Export is restricted to the existing HhhLocal database.");
        using var source = new HhhDbContext(new DbContextOptionsBuilder<HhhDbContext>().UseSqlServer(sourceConnection).Options);
        await using var connection = new SqlConnection(sourceConnection); await connection.OpenAsync();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        var tables = new List<TableData>();
        foreach (var entity in Entities(source)) tables.Add(await ReadTable(connection, transaction, entity, false));
        if (command == "export-hosted")
        {
            var demoIds = new HashSet<string> { "11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222" };
            var organizations = tables.Single(table => table.Name == "Organizations");
            var orgIdColumn = Array.IndexOf(organizations.Columns, "Id"); var orgNameColumn = Array.IndexOf(organizations.Columns, "Name");
            foreach (var row in organizations.Rows.Where(row => demoIds.Contains(row[orgIdColumn]!)))
                if (row[orgNameColumn] is not ("Horizon Care Partners" or "Summit Health Group")) throw new InvalidOperationException("A reference organization has an unexpected name; hosted export was not produced.");
            var model = Entities(source);
            var removed = model.ToDictionary(entity => entity, _ => new HashSet<string>());
            foreach (var entity in model)
            {
                var table = tables.Single(table => table.Name == entity.GetTableName());
                var index = Array.IndexOf(table.Columns, table.Name == "Organizations" ? "Id" : "OrganizationId");
                if (index >= 0) foreach (var row in table.Rows.Where(row => demoIds.Contains(row[index]!))) removed[entity].Add(JsonSerializer.Serialize(row));
            }
            bool changed;
            do
            {
                changed = false;
                foreach (var entity in model)
                {
                    var table = tables.Single(table => table.Name == entity.GetTableName());
                    foreach (var foreignKey in entity.GetForeignKeys())
                    {
                        var principal = foreignKey.PrincipalEntityType;
                        var parent = tables.Single(value => value.Name == principal.GetTableName());
                        var parentColumns = foreignKey.PrincipalKey.Properties.Select(property => Array.IndexOf(parent.Columns, Column(principal, property))).ToArray();
                        var keys = parent.Rows.Where(row => removed[principal].Contains(JsonSerializer.Serialize(row)))
                            .Select(row => JsonSerializer.Serialize(parentColumns.Select(index => row[index]))).ToHashSet();
                        var childColumns = foreignKey.Properties.Select(property => Array.IndexOf(table.Columns, Column(entity, property))).ToArray();
                        foreach (var row in table.Rows)
                            if (keys.Contains(JsonSerializer.Serialize(childColumns.Select(index => row[index]))))
                                changed |= removed[entity].Add(JsonSerializer.Serialize(row));
                    }
                }
            } while (changed);
            var users = tables.Single(table => table.Name == "AspNetUsers");
            var userEntity = model.Single(entity => entity.GetTableName() == "AspNetUsers");
            if (!users.Rows.Any(row => !removed[userEntity].Contains(JsonSerializer.Serialize(row)))) throw new InvalidOperationException("Hosted export must preserve the existing administrator accounts.");
            var preservedTables = tables.Select(table => new TableData(table.Name, table.Columns, table.Rows.Where(row => !removed[model.Single(entity => entity.GetTableName() == table.Name)].Contains(JsonSerializer.Serialize(row))).ToArray())).ToArray();
            tables = preservedTables.ToList();
            Console.WriteLine(JsonSerializer.Serialize(new { hostedExportOnly=true, excludedReferenceFixtureRows=removed.Values.Sum(value=>value.Count), originalAccounts=tables.Single(table=>table.Name=="AspNetUsers").Rows.Length }));
        }
        var snapshot = new Snapshot(1, DateTimeOffset.UtcNow, tables.ToArray());
        if (tables.Single(table => table.Name == "AspNetUsers").Rows.Length == 0)
            throw new InvalidOperationException("No existing accounts were found; stopped instead of exporting a different installation.");
        await File.WriteAllTextAsync(PrivatePath(args.ElementAtOrDefault(1)), JsonSerializer.Serialize(snapshot));
        await transaction.CommitAsync();
        Console.WriteLine(JsonSerializer.Serialize(new { exportedTables = tables.Count, rows = tables.Sum(table => table.Rows.Length), fingerprint = Fingerprint(tables) }));
        return;
    }
    var targetConnection = Environment.GetEnvironmentVariable("HHH_POSTGRES_CONNECTION_STRING")
        ?? throw new InvalidOperationException("HHH_POSTGRES_CONNECTION_STRING is required.");
    if (new NpgsqlConnectionStringBuilder(targetConnection).Database != "hhh")
        throw new InvalidOperationException("Cloud operations are restricted to the dedicated hhh database.");
    await using var target = new PostgresHhhDbContext(new DbContextOptionsBuilder<PostgresHhhDbContext>().UseNpgsql(targetConnection).Options);
    if (command is "create-verifier" or "promote-verifier" or "remove-verifier")
    {
        var path = PrivatePath(args.ElementAtOrDefault(1));
        if (command == "create-verifier")
        {
            if (File.Exists(path)) throw new InvalidOperationException("Verifier metadata already exists; remove the previous verifier before creating another.");
            var original = await target.Organizations.AsNoTracking().Where(org => !org.Name.StartsWith("HHH deployment verification")).OrderBy(org => org.Id).FirstAsync();
            var other = new Organization { Id = Guid.NewGuid(), Name = "HHH deployment verification " + Guid.NewGuid().ToString("N") };
            var viewer = await target.Roles.SingleAsync(role => role.Name == Roles.Viewer);
            var accounts = new List<Verifier>();
            await using var transaction = await target.Database.BeginTransactionAsync();
            target.Organizations.Add(other);
            foreach (var organization in new[] { original.Id, other.Id })
            {
                var id = "hhh-deploy-check-" + Guid.NewGuid().ToString("N");
                var email = id + "@example.invalid";
                var password = "Hhh-Verify-!A1" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
                var user = new AppUser { Id = id, UserName = email, NormalizedUserName = email.ToUpperInvariant(), Email = email, NormalizedEmail = email.ToUpperInvariant(),
                    EmailConfirmed = true, DisplayName = "Deployment verification", OrganizationId = organization, SecurityStamp = Guid.NewGuid().ToString("N"), ConcurrencyStamp = Guid.NewGuid().ToString("N") };
                user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, password);
                target.Users.Add(user); target.UserRoles.Add(new IdentityUserRole<string> { UserId = id, RoleId = viewer.Id });
                accounts.Add(new(id, email, password, organization));
            }
            await target.SaveChangesAsync();
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new VerificationData(other.Id, accounts.ToArray())));
            await transaction.CommitAsync();
            Console.WriteLine("Two private synthetic verification accounts created in the hosted database only.");
        }
        else
        {
            var verifier = JsonSerializer.Deserialize<VerificationData>(await File.ReadAllTextAsync(path))!;
            var ids = verifier.Accounts.Select(account => account.Id).ToArray();
            if (ids.Length != 2 || ids.Any(id => !id.StartsWith("hhh-deploy-check-", StringComparison.Ordinal))) throw new InvalidOperationException("Unexpected verifier identities.");
            await using var transaction = await target.Database.BeginTransactionAsync();
            if (command == "promote-verifier")
            {
                var account = verifier.Accounts[0];
                var user = await target.Users.SingleAsync(user => user.Id == account.Id && user.Email == account.Email && user.OrganizationId == account.Organization);
                var role = await target.Roles.SingleAsync(role => role.Name == Roles.SystemAdministrator);
                target.UserRoles.RemoveRange(await target.UserRoles.Where(row => row.UserId == account.Id).ToListAsync());
                target.UserRoles.Add(new IdentityUserRole<string> { UserId = account.Id, RoleId = role.Id });
                user.SecurityStamp = Guid.NewGuid().ToString("N");
                await target.SaveChangesAsync(); await transaction.CommitAsync();
                Console.WriteLine("Only the generated hosted verifier was enabled for temporary administrator checks.");
                return;
            }
            target.AuditEvents.RemoveRange(await target.AuditEvents.Where(row => ids.Contains(row.UserId)).ToListAsync());
            target.UserRoles.RemoveRange(await target.UserRoles.Where(row => ids.Contains(row.UserId)).ToListAsync());
            target.Users.RemoveRange(await target.Users.Where(user => ids.Contains(user.Id)).ToListAsync());
            var other = await target.Organizations.SingleOrDefaultAsync(org => org.Id == verifier.TemporaryOrganization);
            if (other is not null)
            {
                if (!other.Name.StartsWith("HHH deployment verification ", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected verification organization.");
                target.Organizations.Remove(other);
            }
            await target.SaveChangesAsync(); await transaction.CommitAsync();
            Console.WriteLine("Only the generated hosted verification accounts, organization and their audit events were removed.");
        }
        return;
    }
    if (command == "migrate")
    {
        await target.Database.MigrateAsync();
        Console.WriteLine("Dedicated HHH PostgreSQL schema is current.");
        return;
    }
    if (command is not ("import" or "verify")) throw new InvalidOperationException("Use certificate, export, export-hosted, migrate, import, verify, create-verifier, promote-verifier, or remove-verifier.");
    var snapshotData = JsonSerializer.Deserialize<Snapshot>(await File.ReadAllTextAsync(PrivatePath(args.ElementAtOrDefault(1))))
        ?? throw new InvalidOperationException("Invalid snapshot.");
    if (snapshotData.Format != 1) throw new InvalidOperationException("Unsupported snapshot format.");
    var models = Entities(target).Where(entity => entity.GetTableName() != "DataProtectionKeys").ToArray();
    if (!models.Select(entity => entity.GetTableName()).Order().SequenceEqual(snapshotData.Tables.Select(table => table.Name).Order()))
        throw new InvalidOperationException("Snapshot tables do not match this application model.");
    await using var destination = new NpgsqlConnection(targetConnection); await destination.OpenAsync();
    await using var targetTransaction = await destination.BeginTransactionAsync();
    if (command == "import")
    {
        await Execute(destination, targetTransaction, "SELECT pg_advisory_xact_lock(520850010427)");
        foreach (var entity in models)
        {
            var table = entity.GetTableName()!;
            var count = Convert.ToInt64(await Scalar(destination, targetTransaction, $"SELECT COUNT(*) FROM {Quote(table, true)}"), CultureInfo.InvariantCulture);
            if (table == "InstallationStates" && count == 1)
            {
                var initial = Convert.ToInt64(await Scalar(destination, targetTransaction, "SELECT COUNT(*) FROM \"InstallationStates\" WHERE \"Id\"=1 AND \"AdministratorSetupCompletedAt\" IS NULL"), CultureInfo.InvariantCulture);
                if (initial == 1) continue;
            }
            if (count != 0) throw new InvalidOperationException("The target already contains application data. Existing records were not overwritten.");
        }
        await Execute(destination, targetTransaction, "DELETE FROM \"InstallationStates\" WHERE \"Id\"=1 AND \"AdministratorSetupCompletedAt\" IS NULL");
        foreach (var entity in DependencyOrder(models))
        {
            var table = snapshotData.Tables.Single(value => value.Name == entity.GetTableName());
            var columns = Properties(entity);
            if (!columns.Select(property => Column(entity, property)).SequenceEqual(table.Columns)) throw new InvalidOperationException("Snapshot columns do not match the target.");
            if (table.Rows.Length == 0) continue;
            await using var writer = await destination.BeginBinaryImportAsync($"COPY {Quote(table.Name, true)} ({string.Join(',', table.Columns.Select(column => Quote(column, true)))}) FROM STDIN (FORMAT BINARY)");
            foreach (var row in table.Rows)
            {
                if (row.Length != columns.Length) throw new InvalidOperationException("Invalid snapshot row.");
                await writer.StartRowAsync();
                for (var index = 0; index < columns.Length; index++)
                {
                    if (row[index] is null) await writer.WriteNullAsync();
                    else await writer.WriteAsync(Parse(row[index]!, columns[index].ClrType), PgType(columns[index]));
                }
            }
            await writer.CompleteAsync();
        }
        foreach (var entity in models)
        {
            foreach (var property in entity.GetProperties().Where(property => property.ValueGenerated == ValueGenerated.OnAdd && (property.ClrType == typeof(int) || property.ClrType == typeof(long))))
            {
                var table = Quote(entity.GetTableName()!, true); var column = Quote(Column(entity, property), true);
                using var sequence = new NpgsqlCommand($"SELECT setval(pg_get_serial_sequence(@table,@column), GREATEST(COALESCE(MAX({column}),0),1), COUNT(*)>0) FROM {table}", destination, targetTransaction);
                sequence.Parameters.AddWithValue("table", table); sequence.Parameters.AddWithValue("column", Column(entity, property));
                await sequence.ExecuteNonQueryAsync();
            }
        }
    }
    var copied = new List<TableData>();
    foreach (var entity in models) copied.Add(await ReadTable(destination, targetTransaction, entity, true));
    var expected = Fingerprint(snapshotData.Tables); var actual = Fingerprint(copied);
    if (expected != actual) throw new InvalidOperationException("Transfer verification failed; the transaction was not committed.");
    await targetTransaction.CommitAsync();
    Console.WriteLine(JsonSerializer.Serialize(new { verifiedTables = copied.Count, rows = copied.Sum(table => table.Rows.Length), fingerprint = actual, accounts = copied.Single(table => table.Name == "AspNetUsers").Rows.Length, command }));
}
catch (Exception error)
{
    Console.Error.WriteLine(error is PostgresException pg ? $"HHH operator stopped: PostgreSQL {pg.SqlState}." : $"HHH operator stopped: {error.GetType().Name}: {error.Message}");
    Environment.ExitCode = 1;
}

static string PrivatePath(string? path)
{
    if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("A private artifact path is required.");
    var full = Path.GetFullPath(path); var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".artifacts", "cloud", "private")) + Path.DirectorySeparatorChar;
    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Private files must stay inside .artifacts/cloud/private.");
    return full;
}
static IEntityType[] Entities(DbContext context) => context.Model.GetEntityTypes().Where(entity => entity.GetTableName() is not null).OrderBy(entity => entity.GetTableName(), StringComparer.Ordinal).ToArray();
static IProperty[] Properties(IEntityType entity) => entity.GetProperties().OrderBy(property => Column(entity, property), StringComparer.Ordinal).ToArray();
static string Column(IEntityType entity, IProperty property) => property.GetColumnName(StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema()))!;
static string Quote(string name, bool pg) => pg ? "\"" + name.Replace("\"", "\"\"") + "\"" : "[" + name.Replace("]", "]]") + "]";
static async Task<TableData> ReadTable(DbConnection connection, DbTransaction transaction, IEntityType entity, bool pg)
{
    var properties = Properties(entity); var columns = properties.Select(property => Column(entity, property)).ToArray();
    await using var query = connection.CreateCommand(); query.Transaction = transaction;
    query.CommandText = $"SELECT {string.Join(',', columns.Select(column => Quote(column, pg)))} FROM {Quote(entity.GetTableName()!, pg)}";
    query.CommandTimeout = 120;
    var rows = new List<string?[]>(); await using var reader = await query.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var row = new string?[columns.Length];
        for (var index = 0; index < columns.Length; index++) row[index] = reader.IsDBNull(index) ? null : Canonical(reader.GetValue(index), properties[index].ClrType);
        rows.Add(row);
    }
    return new(entity.GetTableName()!, columns, rows.OrderBy(row => JsonSerializer.Serialize(row), StringComparer.Ordinal).ToArray());
}
static string Canonical(object value, Type type)
{
    type = Nullable.GetUnderlyingType(type) ?? type;
    if (type == typeof(DateOnly)) return (value is DateOnly date ? date : DateOnly.FromDateTime((DateTime)value)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    if (type == typeof(DateTimeOffset) || type == typeof(DateTime))
    {
        var utc = value is DateTimeOffset offset ? offset.UtcDateTime : DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc);
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero).ToString("O", CultureInfo.InvariantCulture);
    }
    if (type == typeof(decimal)) return ((decimal)value).ToString("G29", CultureInfo.InvariantCulture);
    if (type == typeof(bool)) return (bool)value ? "true" : "false";
    if (type == typeof(Guid)) return ((Guid)value).ToString("D");
    if (type == typeof(byte[])) return Convert.ToBase64String((byte[])value);
    return Convert.ToString(value, CultureInfo.InvariantCulture)!;
}
static object Parse(string value, Type type)
{
    type = Nullable.GetUnderlyingType(type) ?? type;
    if (type.IsEnum) return Convert.ChangeType(value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture);
    if (type == typeof(string)) return value;
    if (type == typeof(DateOnly)) return DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    if (type == typeof(DateTimeOffset)) return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture).ToUniversalTime();
    if (type == typeof(DateTime)) return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture).UtcDateTime;
    if (type == typeof(Guid)) return Guid.Parse(value);
    if (type == typeof(byte[])) return Convert.FromBase64String(value);
    return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
}
static NpgsqlDbType PgType(IProperty property)
{
    var store = property.GetColumnType()!;
    if (store.StartsWith("numeric", StringComparison.Ordinal)) return NpgsqlDbType.Numeric;
    if (store.StartsWith("character", StringComparison.Ordinal) || store == "text") return NpgsqlDbType.Text;
    return store switch { "integer" => NpgsqlDbType.Integer, "bigint" => NpgsqlDbType.Bigint, "smallint" => NpgsqlDbType.Smallint,
        "boolean" => NpgsqlDbType.Boolean, "uuid" => NpgsqlDbType.Uuid, "date" => NpgsqlDbType.Date,
        "timestamp with time zone" => NpgsqlDbType.TimestampTz, "timestamp without time zone" => NpgsqlDbType.Timestamp, "bytea" => NpgsqlDbType.Bytea,
        _ => throw new InvalidOperationException("Unsupported target column type: " + store) };
}
static IEntityType[] DependencyOrder(IEntityType[] entities)
{
    var result = new List<IEntityType>(); var remaining = entities.ToList();
    while (remaining.Count > 0)
    {
        var ready = remaining.Where(entity => entity.GetForeignKeys().All(key => key.PrincipalEntityType == entity || result.Contains(key.PrincipalEntityType))).ToArray();
        if (ready.Length == 0) throw new InvalidOperationException("Cannot safely order foreign-key dependencies.");
        result.AddRange(ready); remaining.RemoveAll(ready.Contains);
    }
    return result.ToArray();
}
static string Fingerprint(IEnumerable<TableData> tables) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tables.OrderBy(table => table.Name, StringComparer.Ordinal)))));
static async Task Execute(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql) { await using var query = new NpgsqlCommand(sql, connection, transaction); await query.ExecuteNonQueryAsync(); }
static async Task<object?> Scalar(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql) { await using var query = new NpgsqlCommand(sql, connection, transaction); return await query.ExecuteScalarAsync(); }
record Snapshot(int Format, DateTimeOffset ExportedAt, TableData[] Tables);
record TableData(string Name, string[] Columns, string?[][] Rows);
record Verifier(string Id, string Email, string Password, Guid Organization);
record VerificationData(Guid TemporaryOrganization, Verifier[] Accounts);
