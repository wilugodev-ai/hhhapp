# Solution architecture

HHH is an independent multi-organization financial analytics application. Phase 1 implements the complete synthetic-data dashboard workflow. Later phases extend the same API, dimensions, and financial definitions.

The first Phase 2 increment implements period-based P&L drilldowns and persistent approved budget comparisons. `ProfitLossService` applies organization scope, bounds actual reads to the reporting/comparison window, validates matching cohort/component coverage, and shares the same report calculation across the API, detail view, and CSV export. See [P&L rules](profit-loss.md).

## Boundaries

- `src/Hhh.Api`: ASP.NET Core API, cookie authentication, organization authorization, analytics, exports, health checks, and audit access.
- `src/Hhh.Domain`: financial entities, snapshot grain, metric definitions, and aggregation rules.
- `src/Hhh.Infrastructure`: SQL Server EF Core persistence, Identity, migrations, and deterministic synthetic seed data.
- `apps/web`: Angular standalone components, TypeScript, Tailwind, ECharts, routing, and same-origin API calls.
- `services/forecasting`: reserved Python service boundary for Phase 3. Forecast values are not fabricated in Phase 1.
- `tests/Hhh.Tests`: financial and authorization tests.
- `infra`: container configuration and deployment notes.

The production web container proxies `/api` to the API. Development uses Angular's proxy. Cookies remain HttpOnly, and a separate antiforgery request token protects mutations. User identity determines organization; clients cannot choose an arbitrary organization ID.

## Persistence and grain

Each financial snapshot belongs to one organization, one effective month, one report month, one general ledger month, one payer, one medical center, one provider, one contract, one plan, one line of business, and one region. Each dimension combination contains one non-overlapping synthetic membership cohort. A unique database index enforces snapshot identity.

Revenue, medical expenses, and membership are stored in separate facts sharing the snapshot key. Financial facts carry explicit categories and decimal amounts. Membership is stored once per snapshot, and is not joined to transaction rows when aggregating. Capitation is a revenue category, not additional revenue to sum a second time. Adjustments and IBNR remain explicit components. The synthetic seed currently has one report version per effective month; later restatements must select a single authorized version before aggregating.

Dimension tables belong to an organization. References are validated during seeding and through composite organization foreign keys. A source batch records lineage and synthetic status. An audit log records login, logout, exports, and settings changes. Identity users have one organization and one application role in Phase 1.

## Auditable metrics

- Revenue = sum of revenue facts (capitation, premium, risk adjustment, shared savings, quality bonus, incentives, other revenue).
- Medical expenses = sum of medical expense facts, including IBNR; stop-loss recoveries are negative medical costs. Administrative expenses are separate.
- Net profit = revenue - medical expenses - administrative expenses.
- Gross margin = (revenue - medical expenses) / revenue. Net margin = net profit / revenue.
- Operational MLR = medical expenses / revenue. This is not a regulatory MLR calculation.
- Monthly membership = sum of the non-overlapping cohort counts. Multi-month membership exposure = sum of member months; ending membership = membership in the final effective month.
- Revenue PMPM = total revenue / member months; cost PMPM = total medical expenses / member months.
- Period and payer ratios are calculated from aggregate numerators and denominators, never by averaging row percentages.
- Undefined ratios (zero denominators) return null and render as unavailable. MoM and YoY comparisons retain dimension filters and use the corresponding effective month.

## Phase 1 API

Authentication: CSRF token, login, development-only synthetic demo access, current user, logout.

Analytics: filter metadata, dashboard totals and comparisons, monthly trends, P&L breakdown, payer/center/provider groupings, raw snapshot lineage, and CSV export.

Organization settings: operational MLR target, authorized for organization administrators and CFOs. Audit access is restricted to administrators. All data endpoints require authentication and apply organization scoping.

## Sequence

1. Architecture, entities, SQL Server migrations, Identity, and seed.
2. Typed API and financial aggregation with meaningful tests.
3. Branded landing page, login, dashboard, filters, charts, tables, and reports.
4. SQL-backed integration verification, browser verification, container builds/configuration, documentation, and GitHub push.
5. Phase 2 expands budget comparisons, drilldown, restatement processing, and configurable metric definitions.
6. Phase 3 adds Python forecasting, backtests, confidence intervals, and saved scenarios.
7. Phase 4 adds permission-aware HHH Intelligence, ingestion connectors, advanced exports, and enterprise operations.

## Deployment boundary

Local development may use the running Windows SQL Express instance with a dedicated HHH database. Containers use an isolated SQL Server service and environment-provided credentials. Production requires HTTPS, persistent Data Protection keys, managed secrets, backups, enterprise identity, monitoring, and an operational security review before any real healthcare data is loaded. This MVP uses synthetic data exclusively.
