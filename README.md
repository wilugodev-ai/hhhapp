# HHH - Healthcare Horizon Hub

**Transforming Healthcare Data Into Financial Foresight.**

HHH is an independent healthcare financial intelligence platform for organizations operating in value-based care, capitation, Medicare Advantage, and other risk-based arrangements.

The product will bring together executive dashboards, profit and loss analytics, medical loss ratio analysis, payer and medical center performance, membership analytics, explainable forecasts, scenario modeling, reporting, data ingestion, and the HHH Intelligence assistant.

## Technology stack

| Area | Technology |
| --- | --- |
| Frontend | Angular, TypeScript, Tailwind CSS, Apache ECharts |
| API | ASP.NET Core Web API, C#, Entity Framework Core where appropriate |
| Database | Microsoft SQL Server |
| Data engineering | SQL Server stored procedures, ETL pipelines, historical snapshots |
| Forecasting | Separate Python services with statistical models and backtesting |
| Financial assistant | LLM integration with authorized data and validated analytical queries |
| Deployment | Docker, Azure-compatible infrastructure, environment-based configuration |

## Working Phase 1

The application now includes a public landing page, sign-in, an interactive executive dashboard, P&L, operational MLR, payer and medical center comparisons, membership trends, filtered CSV reports, source lineage, organization settings, and administrator audit access. Financial metrics come from persistent SQL Server data through the authenticated ASP.NET API.

The Angular interface supports light and dark mode, mobile navigation, payer drilldown, and filters for effective month, report month, year, payer, center, provider, contract, plan, line of business, and region. The seed contains 21 months of synthetic financials for two isolated organizations. No real member records are included.

Forecasting, scenario modeling, HHH Intelligence, recurring imports, budget comparisons, and Excel/PDF exports are planned for later phases and are labeled accordingly. The Python service boundary is documented; no forecasting or LLM service is running yet.

## Run locally

Prerequisites: .NET 10 SDK, Node.js 24.15 or later in the Node 24 release line, and SQL Server 2019 or later. The default development connection uses Windows authentication to `.\SQLEXPRESS01` and a dedicated `HhhDevelopment` database. Override it using `ConnectionStrings__Hhh` for another instance. Use an account that can create this development database and apply migrations.

From the repository root:

```powershell
dotnet restore Hhh.slnx
dotnet run --project src/Hhh.Api
```

In a second terminal:

```powershell
cd apps/web
npm ci
npm start
```

Open **http://127.0.0.1:4300** and choose **Explore the platform → Open synthetic demo**. The API runs at http://localhost:5080. In this workspace, `scripts/dev.ps1 -ApiOnly` uses the workspace-local .NET SDK when available; `scripts/dev.ps1` starts the frontend.

Development startup applies committed EF Core migrations and seeds synthetic data idempotently. Existing databases are not deleted or reset. Demo access and seeding require both the Development environment and `Demo:Enabled=true`; they are disabled by default outside development.

| Development account | Role | Organization |
| --- | --- | --- |
| `demo@hhh.local` | Chief Financial Officer | Horizon Care Partners |
| `analyst@hhh.local` | Financial Analyst | Horizon Care Partners |
| `viewer@hhh.local` | Executive Viewer | Horizon Care Partners |
| `admin@hhh.local` | Organization Administrator | Horizon Care Partners |
| `summit@hhh.local` | Chief Financial Officer | Summit Health Group |

All synthetic development accounts use `Hhh-Demo-2026!`. These are demo-only identities and must never be used for real data. Real-user provisioning and enterprise identity integration remain deployment work.

## Verify

```powershell
dotnet test Hhh.slnx
cd apps/web
npm run build
```

API integration tests use a separate `HhhIntegrationTests` SQL Server database by default. Set `HHH_TEST_CONNECTION_STRING` to use another dedicated test database. Tests apply migrations and seed synthetic data. Financial unit tests verify weighted ratios, zero denominators, IBNR, recoveries, and member-month aggregation. API tests verify isolation, authorization, antiforgery, filters, year selection, CSV output, and logout.

GitHub Actions runs the backend tests against SQL Server and builds the Angular frontend.

## Docker demo

Copy `.env.example` to `.env`, set a unique strong `HHH_SQL_PASSWORD`, then run:

```powershell
docker compose up --build
```

Open http://127.0.0.1:4300. This Compose configuration is a localhost-only synthetic development demo, uses SQL Server Developer edition, and persists SQL data and authentication keys in volumes. Docker execution has not been verified on this workstation because Docker is not installed.

For production, use the API and web Dockerfiles with managed SQL Server, HTTPS ingress, environment-provided secrets, persistent Data Protection keys, deployment-controlled migrations, and enterprise user provisioning. Use a least-privileged runtime database identity, separate from the migration identity. Do not deploy the development Compose configuration as a production system.

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
- [Python forecasting service plan](services/forecasting/README.md)
