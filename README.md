# HHH - Healthcare Horizon Hub

**Transforming Healthcare Data Into Financial Foresight.**

HHH is an independent healthcare financial intelligence platform for organizations operating in value-based care, capitation, Medicare Advantage, and other risk-based arrangements.

The product will bring together executive dashboards, profit and loss analytics, medical loss ratio analysis, payer and medical center performance, membership analytics, explainable forecasts, scenario modeling, reporting, data ingestion, and the HHH Intelligence assistant.

## Technology stack

| Area | Technology |
| --- | --- |
| Frontend | Angular, TypeScript, Tailwind CSS, Apache ECharts |
| API | ASP.NET Core Web API, C#, Entity Framework Core where appropriate |
| Database | SQL Server locally; Neon PostgreSQL for the hosted application |
| Data engineering | SQL Server stored procedures, ETL pipelines, historical snapshots |
| Forecasting | Separate Python services with statistical models and backtesting |
| Financial assistant | LLM integration with authorized data and validated analytical queries |
| Deployment | Google Cloud Run, Cloud Build, Artifact Registry, Secret Manager, Docker |

## Working application

The application now includes a public landing page, sign-in, an interactive executive dashboard, P&L, operational MLR, payer and medical center comparisons, membership trends, filtered CSV reports, source lineage, organization settings, and administrator audit access. Financial metrics come from the persistent application database through the authenticated ASP.NET API.

The hosted application is available at **https://hhh-j2uk5d5lkq-uc.a.run.app** on Google Cloud Run with a dedicated Neon database. Existing administrator credentials work there. The original administrator, organization configuration, and 10,046 records were transferred and verified. Local data was retained unchanged; reference fixtures introduced during testing were excluded from the hosted export. See the [deployment and operations guide](deploy/cloud-run/README.md).

The Angular interface supports light and dark mode, mobile navigation, payer drilldown, and filters for effective month, report month, year, payer, center, provider, contract, plan, line of business, and region. Settings includes personal appearance controls with preset backgrounds, a custom tint, previews, and reset. An optional starter dataset adds 21 months of fictional financials and independent budgets to the administrator's own organization. Starter financials do not create user accounts or include real member records.

The first Phase 2 increment adds monthly, quarterly, and yearly P&L statements, persistent approved budget plans, favorable/unfavorable variance, matched prior-year and previous-period comparisons, component drilldowns, paginated cohort lineage, and a P&L CSV export. Payer and center filters preserve the P&L reporting period.

Budget comparisons use the same effective months, cohorts, and financial components as actuals. The 2026 yearly statement compares January–September actuals with January–September budget, while the chart separately shows October–December planned amounts. Missing comparisons remain unavailable. Synthetic budgets use independent planning assumptions and are stored separately from historical results.

Advanced MLR now includes weighted monthly and rolling 3/6/12-month ratios, versioned revenue/medical-category definitions, group target overrides, coverage-aware alerts, and a cost/revenue contribution breakdown that reconciles to the change in MLR. The dashboard and financial exports use the same saved definition. Definition and target updates require CFO/administrator access, detect stale versions, and record audit details.

Forecasting, scenario modeling, HHH Intelligence, recurring imports, regulatory MLR, restatement ingestion, budget authoring, and Excel/PDF exports remain planned. The Python service boundary is documented; no forecasting or LLM service is running yet.

Administration now supports a searchable user directory, role and activation changes, private expiring setup/recovery links, session revocation, lockout reset, and paginated audit details. System administrators can provision and manage organizations. Version checks, transactional last-administrator protection, and per-request account validation enforce access changes across the API. See [administration and first-admin setup](docs/administration.md).

Fresh installations have a `/setup` screen where the first administrator supplies their own identity and organization, and chooses whether to load starter financial data. Setup closes permanently after creation. See the [first administrator handoff](docs/first-admin-handoff.md).

Reference-only demo accounts remain available through an explicit optional mode; they are separate from the normal prototype installation and its users.

## Run locally

Prerequisites: .NET 10 SDK, Node.js 24.15 or later in the Node 24 release line, and SQL Server 2019 or later. The prototype uses Windows authentication to .\SQLEXPRESS01 and the dedicated HhhLocal database. Override ConnectionStrings__Hhh for another instance. Use an account that can create this application database and apply migrations.

From the repository root:

```powershell
dotnet restore Hhh.slnx
.\scripts\dev.ps1 -ApiOnly
```

In a second terminal:

```powershell
cd apps/web
npm ci
npm start
```

Open **http://127.0.0.1:4300/setup**. The API script generates an installation key in the private, Git-ignored `.artifacts/first-admin-setup.key` file. The first person enters this key and their own administrator/organization details. Leave starter financial data selected to use the financial modules immediately. The API runs at http://localhost:5080. The script uses the workspace-local .NET SDK when available; `scripts/dev.ps1` starts the frontend.

Normal startup applies migrations and creates no accounts. Demo:Enabled defaults to false, including development. SampleData:Enabled=true in the local prototype allows optional starter financials; outside development it defaults to false. Initial account creation, selected starter data, and setup completion commit together. Existing databases are preserved.

For the optional reference environment, run .\scripts\dev.ps1 -ApiOnly -Demo. It uses the separate HhhDevelopment database and enables synthetic reference accounts only in Development. The following accounts belong to that optional environment:

| Development account | Role | Organization |
| --- | --- | --- |
| `demo@hhh.local` | Chief Financial Officer | Horizon Care Partners |
| `analyst@hhh.local` | Financial Analyst | Horizon Care Partners |
| `viewer@hhh.local` | Executive Viewer | Horizon Care Partners |
| `admin@hhh.local` | Organization Administrator | Horizon Care Partners |
| `sysadmin@hhh.local` | System Administrator | Horizon Care Partners |
| `summit@hhh.local` | Chief Financial Officer | Summit Health Group |
| `summit-admin@hhh.local` | Organization Administrator | Summit Health Group |

All synthetic development accounts use `Hhh-Demo-2026!`. These are demo-only identities and must never be used for real data. Non-demo deployments use the one-off first-admin bootstrap and private setup links. Enterprise SSO, MFA, and email delivery remain deployment work.

## Verify

```powershell
dotnet test Hhh.slnx
cd apps/web
npm run build
npm test
```

API integration tests use a separate `HhhIntegrationTests` SQL Server database by default. Set `HHH_TEST_CONNECTION_STRING` to use another dedicated test database. Tests apply migrations and seed synthetic data. Financial tests verify weighted ratios, zero denominators, IBNR, recoveries, member-month aggregation, expense/revenue variance direction, and period boundaries. API tests verify isolation, authorization, antiforgery, filters, year selection, quarterly reconciliation, incomplete-period budget coverage, missing budget components, drilldown pagination, source lineage, CSV output, and logout. The test host raises the sign-in request limit to accommodate independent test accounts; the application default remains 10 requests per minute.

GitHub Actions runs the backend tests against SQL Server and builds the Angular frontend.
It also runs Angular component tests in a Node/jsdom environment. These cover missing-window rendering, MLR definition editing, viewer restrictions, version-conflict handling, and preservation of MLR/P&L period controls when payer filters change. Component tests do not replace visual browser verification.

The frontend pins Vite 8.3.5 through an override while the Angular builder still pins an affected older patch. This keeps the development tooling on the patched release line; see the [upstream advisory](https://github.com/advisories/GHSA-587h-7wmj-h5pm).

## Docker demo

Copy `.env.example` to `.env`, set a unique strong `HHH_SQL_PASSWORD`, then run:

```powershell
docker compose up --build
```

Open http://127.0.0.1:4300. This Compose configuration is a localhost-only synthetic development demo, uses SQL Server Developer edition, and persists SQL data and authentication keys in volumes. Docker execution has not been verified on this workstation because Docker is not installed.

The [Cloud Run deployment](deploy/cloud-run/README.md) builds Angular and ASP.NET into one container, uses same-origin HTTPS APIs, and connects to Neon with a restricted runtime role. Session keys persist in PostgreSQL, encrypted with a certificate from Secret Manager. Migrations and data transfers run separately with an operator identity. The development Compose configuration remains a local reference environment.

## Development sequence

1. Define solution boundaries, database grain, financial formulas, organization isolation, and API contracts.
2. Build Phase 1: persistent synthetic data, authentication, navigation, executive dashboard, and working financial calculations.
3. Add P&L, MLR, payer and medical center analytics, and interactive filters.
4. Add explainable forecasts, confidence intervals, historical backtesting, and scenario modeling.
5. Add HHH Intelligence, reporting, ingestion, and enterprise administration.

Development and demonstrations will use synthetic, non-identifiable data. Financial calculations must be auditable, preserve accounting and snapshot semantics, and prevent double counting across datasets.

## Documentation

- [Full product brief](docs/product-brief.md)
- [Architecture, data grain, formulas, and development sequence](docs/architecture.md)
- [API reference](docs/api.md)
- [Database schema and migrations](docs/database.md)
- [P&L periods, budgets, variance rules, and drilldowns](docs/profit-loss.md)
- [Operational MLR definitions, rolling windows, targets, and contribution formulas](docs/mlr.md)
- [Users, roles, organizations, setup links, and administration](docs/administration.md)
- [Executive financial health KPIs and data quality](docs/financial-health.md)
- [First administrator handoff](docs/first-admin-handoff.md)
- [Google Cloud Run and Neon deployment](deploy/cloud-run/README.md)
- [Python forecasting service plan](services/forecasting/README.md)
