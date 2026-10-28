// Operator-only: credentials remain in memory or the private, ignored artifact directory.
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { parseEnv } from 'node:util';
import { Pool } from 'pg';
import { randomBytes } from 'node:crypto';
import { spawnSync } from 'node:child_process';
const project = process.env.HHH_CLOUD_PROJECT || 'ablest-20261023';
if (!/^(hhh-[a-z0-9-]+|ablest-20261023)$/.test(project)) throw new Error('Expected the HHH hosting project.');
const gcloud = process.env.HHH_GCLOUD || 'gcloud.cmd';
const runtime = `hhh-runtime@${project}.iam.gserviceaccount.com`;
const database = 'hhh', role = 'hhh_app_20261027';
const privateFile = '.artifacts/cloud/private/connections.json';
const environment = parseEnv(readFileSync(process.env.NEON_ADMIN_ENV_FILE || 'C:/ablest/apps/api/.env.hosted', 'utf8'));
const original = new URL(environment.DATABASE_URL);
if (!original.hostname.endsWith('.neon.tech')) throw new Error('Expected the existing Neon infrastructure.');
const administratorUrl = new URL(original); administratorUrl.hostname = administratorUrl.hostname.replace('-pooler.', '.');
function pg(url) {
  const clean = new URL(url); for (const key of ['sslmode','sslcert','sslkey','sslrootcert','channel_binding']) clean.searchParams.delete(key);
  return new Pool({ connectionString: clean.toString(), ssl: { rejectUnauthorized: true }, max: 1, connectionTimeoutMillis: 30000 });
}
function cloud(args, input) {
  const result = spawnSync('cmd.exe', ['/d','/c',gcloud,...args,`--project=${project}`,'--quiet'], { encoding: 'utf8', input, windowsHide: true });
  if (result.status !== 0) throw new Error(`Google ${args[0]} failed. Secret values were not printed.`);
  return result.stdout.trim();
}
function npgsql(url, size) {
  const quote = value => '"' + value.replaceAll('"', '""') + '"';
  return `Host=${quote(url.hostname)};Database=${quote(database)};Username=${quote(decodeURIComponent(url.username))};Password=${quote(decodeURIComponent(url.password))};SSL Mode=VerifyFull;Channel Binding=Require;Maximum Pool Size=${size};Timeout=30;Command Timeout=120;Include Error Detail=false`;
}
const administrator = pg(administratorUrl);
let phase = 'metadata check';
try {
  const existing = await administrator.query('SELECT datname FROM pg_database WHERE datname=$1',[database]);
  if (process.argv[2] === 'inspect') {
    const principal = await administrator.query('SELECT current_user,current_database()');
    console.log(JSON.stringify({ metadataAccess: true, targetExists: existing.rowCount > 0, administrator: principal.rows[0].current_user }));
  } else {
    const saved = existsSync(privateFile) ? JSON.parse(readFileSync(privateFile,'utf8')) : null;
    if (existing.rowCount && !saved) throw new Error('A target database already exists without HHH deployment metadata; no existing records were changed.');
    if (saved && (saved.database !== database || saved.role !== role)) throw new Error('Private deployment metadata does not match the HHH database and role.');
    const password = saved?.password || randomBytes(32).toString('hex');
    if (!/^[a-f0-9]{64}$/.test(password)) throw new Error('Invalid stored application credential.');
    const applicationUrl = new URL(original); applicationUrl.username=role; applicationUrl.password=password; applicationUrl.pathname=`/${database}`;
    const migrationUrl = new URL(administratorUrl); migrationUrl.pathname=`/${database}`;
    const connections = { database, role, password, runtime: npgsql(applicationUrl, 10), operator: npgsql(migrationUrl, 2) };
    writeFileSync(privateFile, JSON.stringify(connections));
    phase='isolated role creation';
    const roles = await administrator.query('SELECT rolname FROM pg_roles WHERE rolname=$1',[role]);
    if (!roles.rowCount) await administrator.query(`CREATE ROLE "${role}" LOGIN PASSWORD '${password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION`);
    phase='isolated database creation';
    if (!existing.rowCount) await administrator.query(`CREATE DATABASE "${database}"`);
    await administrator.query(`REVOKE ALL ON DATABASE "${database}" FROM PUBLIC`);
    await administrator.query(`GRANT CONNECT ON DATABASE "${database}" TO "${role}"`);
    const target = pg(migrationUrl);
    try {
      await target.query('REVOKE ALL ON SCHEMA public FROM PUBLIC');
      await target.query(`GRANT USAGE ON SCHEMA public TO "${role}"`);
      await target.query(`GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA public TO "${role}"`);
      await target.query(`GRANT USAGE,SELECT ON ALL SEQUENCES IN SCHEMA public TO "${role}"`);
      await target.query(`ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT,INSERT,UPDATE,DELETE ON TABLES TO "${role}"`);
      await target.query(`ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE,SELECT ON SEQUENCES TO "${role}"`);
    } finally { await target.end(); }
    phase='application credential verification';
    const application = pg(applicationUrl);
    try {
      const result = await application.query('SELECT current_database() AS database,current_user AS role,has_database_privilege(current_user,current_database(),\'CREATE\') AS can_create,rolsuper,rolcreatedb,rolcreaterole,rolreplication FROM pg_roles WHERE rolname=current_user');
      if (result.rows[0].can_create || result.rows[0].rolsuper || result.rows[0].rolcreatedb || result.rows[0].rolcreaterole || result.rows[0].rolreplication) throw new Error('Runtime has unexpected administrative privileges.');
      console.log(JSON.stringify({ database, role, ddlPermission: false, planChanged: false }));
    } finally { await application.end(); }
    phase='dedicated secret creation';
    const secretName='hhh-database-connection';
    const secrets=cloud(['secrets','list',`--filter=name:${secretName}`,'--format=value(name)']).split(/\r?\n/);
    if (!secrets.some(name=>name===secretName || name.endsWith('/'+secretName))) cloud(['secrets','create',secretName,'--replication-policy=automatic']);
    const versions=cloud(['secrets','versions','list',secretName,'--filter=state:ENABLED','--limit=1','--format=value(name)']);
    if (!versions) cloud(['secrets','versions','add',secretName,'--data-file=-'],connections.runtime);
    cloud(['secrets','add-iam-policy-binding',secretName,`--member=serviceAccount:${runtime}`,'--role=roles/secretmanager.secretAccessor','--condition=None','--format=none']);
    const certificateName='hhh-data-protection-cert';
    const certificateSecrets=cloud(['secrets','list',`--filter=name:${certificateName}`,'--format=value(name)']).split(/\r?\n/);
    if (!certificateSecrets.some(name=>name===certificateName || name.endsWith('/'+certificateName))) cloud(['secrets','create',certificateName,'--replication-policy=automatic']);
    const certificateVersions=cloud(['secrets','versions','list',certificateName,'--filter=state:ENABLED','--limit=1','--format=value(name)']);
    if (!certificateVersions) cloud(['secrets','versions','add',certificateName,'--data-file=-'],readFileSync('.artifacts/cloud/private/session-encryption.pfx'));
    cloud(['secrets','add-iam-policy-binding',certificateName,`--member=serviceAccount:${runtime}`,'--role=roles/secretmanager.secretAccessor','--condition=None','--format=none']);
    console.log('HHH credentials and session-encryption certificate are stored in dedicated Google secrets.');
  }
} catch (error) {
  console.error('Neon preparation stopped during',phase,':', error.code || error.message);
  process.exitCode=1;
} finally { await administrator.end(); }
