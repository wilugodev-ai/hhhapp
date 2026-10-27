# HHH: Humana warehouse reference

The user identified `C:\Workspaces\Workspaces\DMCWarehouse\Trunk\Warehouse.Load.Humana` as a reference for developing Healthcare Horizon Hub (HHH). HHH is a broader, independent platform using the stack in the supplied product brief. The existing loader is not the architectural foundation, a required runtime dependency, or the limit of the product scope.

## Product direction and stack

- Build an extensible, multi-organization healthcare financial intelligence platform covering executive analytics, P&L, MLR, payer and medical center performance, membership, forecasting, scenario modeling, HHH Intelligence, reports, ingestion, and administration.
- Frontend: Angular, TypeScript, Tailwind CSS, and Apache ECharts or another professional charting library.
- Backend: ASP.NET Core Web API, C#, REST APIs, and Entity Framework Core where appropriate.
- Database and data engineering: Microsoft SQL Server, stored procedures, ETL pipelines, historical data, and auditable financial aggregation.
- Forecasting: separate Python services, starting with explainable statistical models, backtesting, assumptions, and confidence intervals.
- Financial assistant: LLM integration using authorized data and validated analytical queries.
- Deployment: Azure-compatible infrastructure, Docker containers, and environment-based configuration.
- Implement incrementally, beginning with architecture, schema, application structure, and a functional Phase 1 using persistent synthetic data. Maintain the wider product scope throughout development.

## Verified observations

- `Warehouse.Load.Humana.dtproj` targets SQL Server 2019 and contains SSIS packages (`.dtsx`).
- Source ingestion (`Source_File_*`) and warehouse processing (`Warehouse_Single_*`) are separate, with ordered load packages.
- Available packages cover claims, capitation, payments, accruals, IBNR, fundpool, member attribution, and accounting P&L.
- `Warehouse_AccountingPL.dtsx` references `stg_Humana_AccountingPL_Cube`, `stg_AP_AccountingPL_Cube`, `stg_Humana_MemberAttribution_Cube`, `Facts_Humana_MEMFUNDPOOL`, and `usp_rpt_AccountingPL_Insert`.
- P&L includes distinct `reportDate`, `generalLedgerDate`, and `effectiveDate` fields. Some inspected logic derives the Humana report month as one month after the general ledger month; this must be mapped explicitly rather than assumed universal.
- Existing dimensions include date, line of business, plan class, member, contract, group, center, and at-risk flag. Warehouse keys use patterns such as `Id_ReportDate_DW` and `Id_EffectiveDate_DW`.
- P&L tracks funding, capitation passed and received, bonuses, MRA accruals, FFS, care coordination, specialist capitation, medical cost categories, pharmacy, adjustments, stop-loss, plan share, and distinct accrual/IBNR amounts.
- The inspected IBNR package uses source, clean, and clean historical tables. The claims package also references error tables.

## HHH implementation implications

- Use the supplied HHH brief as the product scope: Angular/TypeScript frontend, ASP.NET Core/C# API, SQL Server storage, and a separate Python forecasting service.
- Design HHH's own application architecture and data model around the broader product brief. Use the Humana project to understand existing business rules, accounting semantics, source conventions, and possible future import mappings.
- Do not require the existing SSIS project or warehouse for HHH to run. Any future Humana integration should use explicit mappings behind a data ingestion boundary.
- Preserve source identifiers, report/effective/accounting periods, lineage, and historical snapshot semantics in mappings.
- Define financial signs and inclusion rules before calculating revenue, expenses, profit, operational MLR, or PMPM. Do not simply sum every existing financial column.
- When implementing a Humana import, reconcile HHH P&L against the existing accounting output for matching periods and filters.
- Validate attribution grain and snapshot selection before combining membership, fundpool, and claims. Aggregate each dataset to compatible grain to prevent double counting.
- Keep actuals, accruals, IBNR, forecasts, and scenarios distinguishable.
- Use synthetic data for development. Keep credentials and member data out of fixtures and documentation.

## Review scope and next work

This is a preliminary package-level review, not a verified database schema or complete business-rule inventory. No packages were executed and no database connection was made. Confirm table definitions, package dependencies, attribution uniqueness, restatement behavior, sign conventions, and approved metric definitions before implementing the integration.

The source project was read only and remains unchanged.
