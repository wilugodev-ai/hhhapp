// Read-only metadata checks. Never print connection strings, keys, or application rows.
import { readFileSync } from 'node:fs';
import { parseEnv } from 'node:util';
import assert from 'node:assert/strict';
import { Pool } from 'pg';
const environment=parseEnv(readFileSync(process.env.NEON_ADMIN_ENV_FILE || 'C:/ablest/apps/api/.env.hosted','utf8'));
const credentials=JSON.parse(readFileSync('.artifacts/cloud/private/connections.json','utf8'));
const administratorUrl=new URL(environment.DATABASE_URL);
assert.ok(administratorUrl.hostname.endsWith('.neon.tech'));
administratorUrl.hostname=administratorUrl.hostname.replace('-pooler.','.');
administratorUrl.pathname='/hhh';
const applicationUrl=new URL(administratorUrl); applicationUrl.username=credentials.role; applicationUrl.password=credentials.password;
function connect(url) {
  const clean=new URL(url); for(const key of ['sslmode','sslcert','sslkey','sslrootcert','channel_binding']) clean.searchParams.delete(key);
  return new Pool({connectionString:clean.toString(),ssl:{rejectUnauthorized:true},max:1,connectionTimeoutMillis:30000});
}
const administrator=connect(administratorUrl),application=connect(applicationUrl);
try {
  const {rows:[role]}=await application.query(`SELECT current_database() AS database,
    rolsuper,rolcreatedb,rolcreaterole,rolreplication,
    has_database_privilege(current_user,current_database(),'CREATE') AS create_database_objects,
    has_schema_privilege(current_user,'public','CREATE') AS create_schema_objects
    FROM pg_roles WHERE rolname=current_user`);
  assert.equal(role.database,'hhh');
  for(const key of ['rolsuper','rolcreatedb','rolcreaterole','rolreplication','create_database_objects','create_schema_objects']) assert.equal(role[key],false);
  const {rows:[membership]}=await administrator.query('SELECT count(*)::int AS count FROM pg_auth_members WHERE member=(SELECT oid FROM pg_roles WHERE rolname=$1)',[credentials.role]);
  assert.equal(membership.count,0,'Runtime inherited another role');
  const {rows:keys}=await application.query('SELECT "Xml" FROM "DataProtectionKeys"');
  assert.ok(keys.length>0,'Persistent session keys missing');
  assert.ok(keys.every(key=>/encryptedSecret/.test(key.Xml)&&!/<masterKey(?:\s|>)/.test(key.Xml)),'Session keys were not encrypted');
  const {rows:databases}=await administrator.query('SELECT datname FROM pg_database WHERE NOT datistemplate AND datname<>\'hhh\'');
  let checked=0;
  for(const database of databases) {
    const url=new URL(administratorUrl); url.pathname='/'+database.datname;
    const other=connect(url);
    try {
      const {rows:[access]}=await other.query(`SELECT count(*)::int AS tables,
        count(*) FILTER (WHERE has_table_privilege($1,c.oid,'SELECT') OR has_table_privilege($1,c.oid,'INSERT')
          OR has_table_privilege($1,c.oid,'UPDATE') OR has_table_privilege($1,c.oid,'DELETE'))::int AS accessible
        FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        WHERE n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname NOT LIKE 'pg_%' AND c.relkind IN ('r','p')`,[credentials.role]);
      assert.equal(access.accessible,0,'HHH runtime could access another application database');
      checked+=access.tables;
    } finally { await other.end(); }
  }
  console.log(JSON.stringify({runtimeAdministrativePrivileges:false,roleMemberships:0,encryptedPersistentKeys:true,otherApplicationTablesChecked:checked,otherApplicationTableAccess:false}));
} finally { await administrator.end(); await application.end(); }
