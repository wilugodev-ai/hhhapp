# API reference

All `/api` responses are non-cacheable. Authentication uses an HttpOnly session cookie. Mutating requests require an antiforgery token from `GET /api/auth/csrf`; send the `XSRF-TOKEN` cookie value in the `X-XSRF-TOKEN` header. Refresh the token after signing in or out. Angular handles this header automatically.

| Method | Path | Behavior |
| --- | --- | --- |
| GET | `/health/live` | Process liveness |
| GET | `/health/ready` | SQL connectivity readiness |
| GET | `/api/auth/csrf` | Set antiforgery cookies; return whether the synthetic demo is enabled |
| POST | `/api/auth/login` | Sign in using `{ "email": "...", "password": "..." }` |
| POST | `/api/auth/demo` | Development-only synthetic CFO access |
| GET | `/api/auth/me` | Current user, organization, role, MLR target |
| POST | `/api/auth/logout` | End the browser session |
| GET | `/api/analytics/filters` | Authorized dimensions and available periods |
| GET | `/api/analytics/dashboard` | Metrics, comparisons, trends, P&L components, and payer/center/provider groups |
| GET | `/api/analytics/mlr` | Weighted MLR windows, group targets, alerts, trends, and reconciled contributions |
| GET | `/api/mlr/configuration` | Active definition, authorized category catalogs, organization target, and overrides |
| PUT | `/api/mlr/definition` | Save versioned category definition; CFO/administrator only |
| PUT | `/api/mlr/targets/{dimensionId}` | Save an organization-scoped dimension target; CFO/administrator only |
| DELETE | `/api/mlr/targets/{dimensionId}?version=N` | Reset an override to the organization default; CFO/administrator only |
| GET | `/api/reports/mlr.csv` | Selected group/window MLR and target/coverage export |
| GET | `/api/analytics/profit-loss` | Period P&L, approved budget, variance, matched comparisons, coverage, and trends |
| GET | `/api/analytics/profit-loss/details` | Financial component groups and paginated cohort/source detail |
| GET | `/api/reports/profit-loss.csv` | Selected P&L statement, budget variance, comparison periods, and plan version |
| GET | `/api/reports/financial.csv` | Financial details for the current period and dimensions |
| GET | `/api/data/batches` | Organization source batches and synthetic classification |
| PUT | `/api/settings` | Update `{ "mlrTarget": 85 }`; CFO or administrator only |
| GET | `/api/audit` | Most recent 100 organization audit events; administrators only |

## Analytics and export parameters

- `month`: effective month in `YYYY-MM` format. Defaults to the latest available month.
- `year`: aggregate all available effective months within a year; used when `month` is absent.
- `reportMonth`: separate report snapshot month in `YYYY-MM` format.
- `payer`, `center`, `provider`, `contract`, `plan`, `lineOfBusiness`, `region`: authorized dimension GUIDs returned by filter metadata.

There is no organization selector parameter. The signed-in user's organization claim determines data access. An `organizationId` parameter is rejected. Foreign organization dimension IDs produce no matching records.

`totals.memberMonths` measures exposure over the selected period. `endingMembership` measures members in its final effective month. MoM/YoY comparisons always refer to that final month, including when `year` selects a multi-month period; the UI labels that behavior. Trends show up to 12 available months ending at the selected month. The report-month filter also applies to trends and comparisons, so a single report version may have no comparable prior period.

Money is represented as decimal USD amounts. Percentages are in percentage units (85 means 85%, not 0.85). Undefined ratios are null. No forecast values are included in historical results.

## Errors and controls

`400` indicates invalid input or antiforgery failure; `401` requires sign-in; `403` indicates insufficient role privileges; `429` indicates sign-in rate limiting. Login also enforces account lockout. The development demo route returns `404` outside the enabled development environment.

Organization scoping is implemented in application queries and reinforced by composite organization foreign keys in the financial schema. This is application-level isolation, not SQL Server row-level security. New endpoints must explicitly scope every query by the authenticated organization. See the integration tests for isolation and role restrictions.

## P&L parameters

P&L endpoints accept the shared dimension and report-month filters, plus:

- `period`: `month` (default), `quarter`, or `year`.
- `month`: effective month for monthly mode; defaults to the latest authorized actual month.
- `year`: calendar year for quarterly/yearly mode; defaults to the latest authorized actual year.
- `quarter`: 1–4 for quarterly mode; defaults to the quarter of the latest actual month.
- `budgetPlan`: authorized approved plan GUID for the selected year; defaults to the latest approved version.

Detail endpoints additionally require `category` and `type` (`revenue`, `medical`, or `administrative`). Optional `groupBy` accepts `payer`, `center`, `provider`, `contract`, `plan`, `lineOfBusiness`, `region`, or `month`. `page` starts at 1; `pageSize` defaults to 25 and is capped at 100.

`coverage` reports requested/actual/missing months, cohort counts, and comparison availability. Monetary comparison cells and their variances are nullable. Statement budget values align to covered actual months; future monthly budget values appear only in separately labeled trend data. See [P&L rules](profit-loss.md) for coverage and variance semantics.

## MLR parameters and updates

MLR reads share the dimension, effective-month, and report-month filters. `window` accepts 1, 3, 6, or 12 and defaults to 1. `groupBy` accepts `payer`, `center`, `provider`, `plan`, `contract`, `lineOfBusiness`, or `region` and defaults to `payer`. A month is the window end; it does not filter out earlier rolling-window months.

Definition updates use `{ "name": "Operational MLR", "expectedVersion": 1, "revenueCategories": ["Capitation"], "expenseCategories": ["Inpatient"] }`. Both category arrays must be nonempty, distinct, and authorized. Administrative categories are rejected.

Target updates use `{ "target": 85, "expectedVersion": 0 }`; version 0 creates an override, and later writes require its current version. Targets range from 1–100%. Reads expose each version. Conflicting or concurrent stale writes return `409`, including creation races; reload configuration before retrying. Updates and deletes require the normal antiforgery header.

Dashboard financial metrics now include `mlrRevenue` and `mlrExpenses`, plus root definition name/version and per-group targets. Other financial totals preserve their existing meaning. See [MLR rules](mlr.md) for complete coverage and contribution semantics.
