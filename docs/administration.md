# Users and administration

Open `/app/administration`. Organization administrators manage only their own workspace. System administrators can select an organization, create a new organization with its first administrator, rename it, and deactivate/reactivate it. Platform administration does not bypass organization scope for financial analytics.

## Roles

| Role | Analytics and CSV exports | Financial settings and MLR definitions | Users and audit | Organization provisioning |
| --- | --- | --- | --- | --- |
| Executive Viewer | Read/export own organization | Read | No | No |
| Financial Analyst | Read/export own organization | Read | No | No |
| Chief Financial Officer | Read/export own organization | Manage | No | No |
| Organization Administrator | Read/export own organization | Manage | Own organization | No |
| System Administrator | Read/export own organization | Manage | All organizations | Yes |

Roles are fixed application roles, not custom permission sets. Each managed account receives one role. Organization administrators cannot grant system access or modify a system administrator. A user's email and organization cannot be changed through the account editor.

## User lifecycle

1. An administrator creates an account with its name, email, and role. It has no password and cannot sign in until setup is complete.
2. The API returns a single-use setup path. The UI shows a link to the current frontend origin. Share it privately with the intended person; no email is sent automatically.
3. The recipient opens `/activate#user=...&token=...`, chooses a password, and signs in. The fragment keeps the secret out of page request URLs and referrers. The page removes the fragment from the current history entry and keeps the token only in memory; reopen the original link after refreshing.
4. The token contains 256 random bits, expires after 48 hours, and is stored only as a SHA-256 hash. Consumption is transactionally serialized. Replaced, expired, used, and revoked links fail. Links and passwords never appear in audit details.
5. Deactivation blocks new sign-in and invalidates existing sessions. Reactivation does not revive old sessions or revoked setup links. For a pending account, issue a new link after reactivation.

Administrators can edit names/roles/status, replace pending setup links, revoke all sessions, clear a temporary password sign-in lockout, and create a recovery link. Recovery immediately invalidates sessions and blocks sign-in until a new password is set. The previous password no longer works after successful recovery. Temporary password lockout is distinct from administrative deactivation.

Users cannot change their own account through administration. Another administrator must make access changes. A completed, active administrator must remain in an organization; pending invitations do not count. Recovery that would leave no active administrator is also rejected. System administrators cannot deactivate their own organization.

## Transactions and sessions

All access writes and setup link consumption use SQL Server's transaction-owned `sp_getapplock` on `HHH:AccessAdministration`. This serializes low-frequency administration across API instances and protects last-administrator decisions from concurrent changes. User/organization changes and audit events commit together. Version numbers reject stale edits with HTTP 409. Writes recheck the acting administrator after acquiring the lock so waiting requests cannot retain revoked privileges.

Every account administration change rotates the Identity security stamp. Cookie validation checks user status, setup completion, organization status, stamp, and current roles on every authenticated request. Organization deactivation rotates all its users' stamps, increments their access versions, and revokes pending links. Changes take effect on the next request; they cannot erase information already downloaded by an authorized user.

The directory supports name/email search, status filters, and pages of 25. Audit history has separate pages of 25, actor names, time, and before/after details. Accounts and organizations are deactivated rather than deleted through the UI.

## First system administrator

Normal prototype startup creates no users. The first administrator supplies their own name, email, organization, and password through `/setup`. `scripts/dev.ps1 -ApiOnly` prepares the private installation key and the separate `HhhLocal` database. Follow the [first administrator handoff](first-admin-handoff.md).

When `SampleData:Enabled=true`, setup and new-organization forms offer an optional starter financial dataset. The owner can accept it or start empty. The dataset is independent of identities, and never creates a default administrator or bypasses authentication. The local prototype enables this choice; non-development configuration defaults it off.

Only explicit reference-demo startup (`scripts/dev.ps1 -ApiOnly -Demo`, separate `HhhDevelopment` database) adds `sysadmin@hhh.local` and the other synthetic identities. Existing users and access changes are preserved there.

The configured demo's `/setup` page offers **Open demo administration**. This signs in only to the fixed synthetic `sysadmin@hhh.local` account and opens Administration. Choose **Invite user** to create another administrator, or **New organization** to provision a separate workspace and its administrator. The shortcut requires both Development environment and demo enablement; it returns 404 in other environments and cannot reactivate, unlock, or promote the sample account. It is audited as `auth.demo.admin`.

Configured non-demo installations direct users to administrator sign-in or their existing Administration page. Installations missing an enabled setup key explain that setup must be enabled. The public status includes no administrator identities or setup-key values.

For a fresh non-demo deployment, the operator sets `ASPNETCORE_ENVIRONMENT=Production` and `ConnectionStrings__Hhh`, then applies migrations using a provisioning identity. Start the deployed application with an API-only `Bootstrap__SetupKey` environment variable. Generate a random key of at least 32 characters; this PowerShell example creates a 256-bit key without printing it:

```powershell
$setupRandom = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$setupBytes = New-Object byte[] 32
try {
    $setupRandom.GetBytes($setupBytes)
    $env:Bootstrap__SetupKey = [Convert]::ToBase64String($setupBytes)
} finally {
    $setupRandom.Dispose()
}
```

The key must be 32–256 characters with no whitespace. Configure it on the API process through the deployment secret mechanism, and provide it privately to the first administrator. It is never returned by the API, embedded in the Angular build, or included in a URL. Use HTTPS for deployed setup. There is no default setup key.

The sign-in page shows **Set up your administrator account** while setup is enabled. Open `/setup`, enter the installation key, name, work email, organization name, and password. The API creates the organization and system administrator, activates the account, records audit events, and permanently closes setup in one transaction. The administrator then signs in. The supplied email is not marked verified; email delivery/verification remains a later feature.

`InstallationStates` contains the permanent completion marker. Once completed, changing/deleting administrator roles, changing the key, or restarting the server cannot reopen setup. Existing installations and synthetic demo accounts close setup during migration/seeding. Remove the setup-key environment variable after installation. Incorrect keys return 403; closed/unconfigured setup returns 404; requests are CSRF-protected and limited to five per IP per minute by default. The key is compared using fixed-length hashes and a timing-resistant comparison.

### CLI provisioning

The operator can also set `Bootstrap__Email`, `Bootstrap__Name`, and `Bootstrap__Organization`, then run:

```powershell
dotnet run --project src/Hhh.Api --no-launch-profile -- --bootstrap-admin
```

The command applies migrations, initializes fixed roles, creates the first system administrator and organization, and exits. It prints one private setup path to open on the deployed frontend. Treat the output as a credential and avoid shared logs. There is no default production password. Once setup is complete, the command cannot replace the administrator. Before initial activation, rerunning with the same email replaces a lost/expired first link. If browser setup is enabled, it can finish a pending CLI account using the original email; otherwise the private activation link completes it. Both paths close the same permanent installation marker. Normal `--migrate` initializes roles without enabling demo users.

## Remaining account features

MFA, enterprise SSO, self-service email password recovery, automatic invitation delivery, custom permission sets, and membership in multiple organizations are later increments. The current module provides administrator-assisted password recovery and role-based access.
