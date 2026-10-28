import { readFileSync, writeFileSync } from 'node:fs';
import assert from 'node:assert/strict';
const deployment=JSON.parse(readFileSync('.artifacts/cloud/deployment-result.json','utf8'));
const verification=JSON.parse(readFileSync('.artifacts/cloud/private/hosted-verifier.json','utf8'));
const origin=new URL(deployment.url);
assert.equal(origin.protocol,'https:'); assert.match(origin.hostname,/\.run\.app$/);
class Client {
  cookies=new Map();
  async request(path, options={}) {
    const response=await fetch(new URL(path,origin),{...options,redirect:'manual',headers:{...options.headers,Cookie:[...this.cookies].map(([key,value])=>`${key}=${value}`).join('; ')}});
    for(const header of response.headers.getSetCookie()) { const pair=header.split(';')[0]; const index=pair.indexOf('='); this.cookies.set(pair.slice(0,index),pair.slice(index+1)); }
    return response;
  }
  async json(path) { const response=await this.request(path); assert.equal(response.status,200,`Hosted GET failed: ${path}`); return response.json(); }
  async csrf() { return this.json('/api/auth/csrf'); }
  async post(path,body={}) {
    return this.write(path,body,'POST');
  }
  async write(path,body,method) {
    return this.request(path,{method,headers:{'Content-Type':'application/json','X-XSRF-TOKEN':decodeURIComponent(this.cookies.get('XSRF-TOKEN')||'')},body:JSON.stringify(body)});
  }
  async login(account) {
    await this.csrf(); const response=await this.post('/api/auth/login',{email:account.Email,password:account.Password});
    assert.equal(response.status,200,'Hosted sign-in failed');
    const cookie=response.headers.getSetCookie().find(value=>value.startsWith('hhh.session='));
    assert.ok(cookie&&/httponly/i.test(cookie)&&/secure/i.test(cookie)&&/samesite=strict/i.test(cookie),'Session cookie protections missing');
    await this.csrf();
  }
}
if(process.argv.includes('--check-administration')) {
  const admin=new Client(); await admin.login(verification.Accounts[0]);
  const organizations=await admin.json('/api/admin/organizations');
  assert.ok(organizations.some(org=>org.id===verification.Accounts[0].Organization));
  const temporary=organizations.find(org=>org.id===verification.TemporaryOrganization);
  assert.ok(temporary?.name.startsWith('HHH deployment verification '));
  const directory=await admin.json(`/api/admin/organizations/${verification.Accounts[0].Organization}/users`);
  assert.ok(directory.total>=2&&directory.users.length>=2,'Administrator directory missing');
  await admin.json(`/api/admin/organizations/${verification.Accounts[0].Organization}/audit`);
  const path=`/api/admin/organizations/${temporary.id}`;
  const body={name:temporary.name,isActive:true,expectedVersion:temporary.version};
  assert.equal((await admin.write(path,body,'PUT')).status,200,'Hosted administrator transaction failed');
  assert.equal((await admin.write(path,body,'PUT')).status,409,'Stale administrator update was accepted');
  const activity=await admin.json(`${path}/audit`);
  assert.ok(activity.items.some(item=>item.action==='admin.organization.updated'));
  await admin.csrf(); assert.equal((await admin.post('/api/auth/logout')).status,200);
  console.log(JSON.stringify({administratorDirectory:true,organizationManagement:true,auditTrail:true,concurrencyProtection:true}));
} else if(process.argv.includes('--check-restart')) {
  const saved=JSON.parse(readFileSync('.artifacts/cloud/private/session-check.json','utf8'));
  const client=new Client(); client.cookies=new Map(saved.cookies);
  const me=await client.json('/api/auth/me'); assert.equal(me.id,verification.Accounts[0].Id,'Session did not survive restart');
  await client.csrf(); assert.equal((await client.post('/api/auth/logout')).status,200);
  assert.equal((await client.request('/api/auth/me')).status,401,'Sign out did not end the session');
  console.log(JSON.stringify({sessionSurvivedNewRevision:true,signOutVerified:true}));
} else {
  const anonymous=new Client();
  const home=await anonymous.request('/'); assert.equal(home.status,200);
  const html=await home.text(); assert.ok(html.includes('<hhh-root'));
  assert.ok(!/<script(?![^>]*\bsrc=)[^>]*>\s*[^<\s]/i.test(html),'Inline bootstrap would be blocked by CSP');
  assert.ok(home.headers.get('Content-Security-Policy')?.includes("script-src 'self'"));
  const scripts=[...html.matchAll(/(?:src|href)="([^"\s]+\.(?:js|css))"/g)].map(match=>match[1]);
  for(const path of scripts) assert.equal((await anonymous.request('/'+path)).status,200,'Published asset missing');
  assert.equal((await anonymous.request('/app/overview')).status,200,'SPA deep link failed');
  assert.equal((await anonymous.request('/api/missing-endpoint')).status,404,'API fallback served HTML');
  assert.equal((await anonymous.request('/api/analytics/dashboard')).status,401);
  const config=await anonymous.csrf(); assert.equal(config.demoEnabled,false); assert.equal(config.setupAvailable,false);
  const primary=new Client(); await primary.login(verification.Accounts[0]);
  const me=await primary.json('/api/auth/me'); assert.equal(me.organizationId,verification.Accounts[0].Organization);
  const metadata=await primary.json('/api/analytics/filters'); assert.ok(metadata.months.length>0,'Transferred financial months missing');
  const month=metadata.months[0];
  const dashboard=await primary.json('/api/analytics/dashboard?month='+month); assert.ok(dashboard.snapshotCount>0); assert.ok(dashboard.totals.memberMonths>0);
  const report=await primary.json('/api/analytics/profit-loss?period=month&month='+month);
  assert.equal(report.coverage.actualSnapshots,dashboard.snapshotCount); assert.equal(report.totals.find(row=>row.type==='profit').actual,dashboard.totals.netProfit);
  assert.equal(report.coverage.budgetAvailable,true,'Matched approved budget missing');
  await primary.json('/api/analytics/profit-loss?period=year&year='+month.slice(0,4));
  await primary.json('/api/analytics/mlr?month='+month);
  assert.ok((await primary.json('/api/data/batches')).length>0);
  assert.equal((await primary.request('/api/admin/organizations')).status,403,'Viewer acquired administrator access');
  assert.equal((await primary.post('/api/auth/logout',{})).status,200); await primary.login(verification.Accounts[0]);
  const other=new Client(); await other.login(verification.Accounts[1]);
  assert.equal((await other.json('/api/analytics/filters')).months.length,0,'Cross-organization financial data leaked');
  assert.equal((await other.json('/api/data/batches')).length,0,'Cross-organization source data leaked');
  assert.equal((await other.request('/api/analytics/dashboard?organizationId='+verification.Accounts[0].Organization)).status,400);
  await other.csrf(); assert.equal((await other.post('/api/auth/logout')).status,200);
  writeFileSync('.artifacts/cloud/private/session-check.json',JSON.stringify({cookies:[...primary.cookies]}));
  console.log(JSON.stringify({https:true,assets:scripts.length,spaRouting:true,authentication:true,secureCookies:true,financialReports:true,matchedBudget:true,mlr:true,dataSources:true,tenantIsolation:true,administratorGuard:true,demoSignIn:false}));
}
