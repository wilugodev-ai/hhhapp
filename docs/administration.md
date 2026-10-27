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

Development adds `sysadmin@hhh.local` (System Administrator) and `summit-admin@hhh.local` (Organization Administrator for Summit). Both use the documented synthetic demo password. Existing users, role changes, and deactivations are preserved by subsequent seeding.

For a fresh non-demo deployment, use an operator-controlled connection with migration/provisioning privileges. Set `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__Hhh`, `Bootstrap__Email`, `Bootstrap__Name`, and `Bootstrap__Organization`, then run:

```powershell
dotnet run --project src/Hhh.Api --no-launch-profile -- --bootstrap-admin
```

The command applies migrations, initializes fixed roles, creates the first system administrator and organization, and exits. It prints one private setup path to open on the deployed frontend. Treat the output as a credential and avoid shared logs. There is no bootstrap HTTP endpoint or default production password. Once a system administrator completes setup, bootstrap cannot replace them. Before initial setup is completed, rerunning with the same email replaces a lost/expired first link. Normal `--migrate` also initializes roles without enabling demo users.

## Remaining account features

MFA, enterprise SSO, self-service email password recovery, automatic invitation delivery, custom permission sets, and membership in multiple organizations are later increments. The current module provides administrator-assisted password recovery and role-based access.
