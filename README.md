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

## Project status

This initial commit establishes the product brief and architectural direction. Application code, database schema, authentication, and deployment configuration have not been implemented yet.

The existing Humana SSIS loader is reference material for business rules and data conventions. HHH will have its own architecture and will not require that loader to run.

## Development sequence

1. Define solution boundaries, database grain, financial formulas, organization isolation, and API contracts.
2. Build Phase 1: persistent synthetic data, authentication, navigation, executive dashboard, and working financial calculations.
3. Add P&L, MLR, payer and medical center analytics, and interactive filters.
4. Add explainable forecasts, confidence intervals, historical backtesting, and scenario modeling.
5. Add HHH Intelligence, reporting, ingestion, and enterprise administration.

Development and demonstrations will use synthetic, non-identifiable data. Financial calculations must be auditable, preserve accounting and snapshot semantics, and prevent double counting across datasets.

## Documentation

- [Full product brief](docs/product-brief.md)
- [Humana reference and implementation boundaries](docs/humana-warehouse-reference.md)
