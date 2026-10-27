# Python forecasting service boundary

This service is planned for Phase 3. Phase 1 provides historical actuals and does not generate or display invented forecasts.

The API will supply an authorized, aggregated monthly series, never direct member-level data. The Python service will compare seasonal-naive, exponential-smoothing, and regression baselines using rolling historical backtests. Responses must include the model version, training cutoff, forecast horizon, backtest accuracy, assumptions, point forecasts, and calibrated prediction intervals.

Proposed internal endpoint: `POST /v1/forecasts`, with organization authorization enforced by the ASP.NET API before dispatch. Forecast outputs will be stored separately from actuals and scenarios. The service will have independent dependency locking, tests, and a Docker image when implemented.
