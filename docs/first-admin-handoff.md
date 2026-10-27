# HHH prototype: first administrator handoff

HHH is a working Angular, ASP.NET Core, and SQL Server application. The first administrator creates their own account and organization, then configures the workspace through Administration. Optional starter financials do not create sample user accounts.

## Access on this workstation

- Setup: `http://127.0.0.1:4300/setup`
- Sign in: `http://127.0.0.1:4300/login`
- Administration: `http://127.0.0.1:4300/app/administration`
- Application database: `HhhLocal` on `.\SQLEXPRESS01`
- Installation key: the operator provides it separately from `.artifacts/first-admin-setup.key`. This private file is excluded from Git and restricted to the Windows user who generated it. Keep its contents out of shared project documents.

These localhost URLs work on this computer. Remote handoff requires a deployed HTTPS URL and that deployment's installation key.

## First administrator

1. Open the setup URL and enter the installation key.
2. Enter your own name, work email, organization name, and password.
3. Leave **Start with sample financial data** selected to explore the financial modules immediately, or clear it to start empty.
4. Create the account and sign in. You receive **System Administrator** access and land in Administration.
5. Use **Organization details** for the organization name, **Financial settings** for the MLR target and appearance, and **MLR definitions & targets** for included categories and group thresholds.
6. Use **Invite your team** to add users and assign roles. Share their generated setup links privately so each recipient chooses a password. No automatic email is sent.
7. Review audit activity and exports. Additional organizations can have their own administrator and optional starter data.

Initial setup closes permanently after creation. There is no default administrator password. Access changes take effect on the next request, and the application protects the last active administrator.

## Starter data

The dataset includes January 2025 through September 2026 financials, three fictional payers, four centers, provider/contract/plan/region dimensions, categorized revenue and expenses, membership, two years of independent budgets, and operational MLR configuration. It is stored in your organization's SQL Server records and supports the dashboard, P&L, rolling MLR, drilldowns, filters, and CSV exports.

Sample source batches and budgets are marked as synthetic and contain no real member records. Loading this data does not create demo identities or bypass sign-in. Existing financials and settings are preserved, and the loader does not duplicate its dataset.

## Available configuration and scope

The administrator can manage organizations, users, roles, activation, setup/recovery links, sessions, financial/MLR settings, personal appearance, and audit review. Financial analytics and CSV reporting are operational.

Data ingestion/restatements, budget authoring, forecasting/scenarios, HHH Intelligence, automatic email, MFA/SSO, and Excel/PDF exports remain subsequent development work.

## Restart

Run `.\scripts\dev.ps1 -ApiOnly` for the API and `.\scripts\dev.ps1` in another terminal for the frontend. The API script reuses the private installation key and preserves the database. Once the administrator is created, the completion marker keeps setup closed.

The optional reference environment uses `.\scripts\dev.ps1 -ApiOnly -Demo` and the separate `HhhDevelopment` database. It is not the default administrator onboarding path.
