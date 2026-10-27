# P&L and budget comparisons

Open **Profit & Loss** in the workspace. Choose monthly, quarterly, or yearly reporting, then apply organization-scoped payer, center, provider, contract, plan, line of business, region, and report-month filters. Period controls belong to the P&L module and remain selected when dimension filters change.

## Independent persisted budget plans

`BudgetPlans`, `BudgetSnapshots`, and `Facts_Budget` store approved plans separately from actual snapshots. The synthetic development seed creates annual 2025 and 2026 plans for each organization. Each plan has a version, approval date, and 144 non-overlapping monthly cohort snapshots.

Planning drivers are defined in `BudgetSeed.cs`: planned membership growth of 0.75% per elapsed month, planned revenue PMPM starting at $1,035 plus payer/center differences and a monthly $3.20 increment, planned medical cost ratios by payer and center, and administrative expense at 6.2% of planned revenue. Financial categories are allocated explicitly, and final category amounts absorb cent-rounding differences. Budgets are not derived by applying a discount to observed actuals.

The UI defaults to the most recently approved plan for the selected year. A plan ID from a different organization or year is rejected. Plan authoring, import, and an approval workflow are future work; the current comparison feature reads persisted synthetic approved plans.

## Matched comparisons

Monthly reports contain one effective month; quarterly reports contain three calendar months; yearly reports request January–December. If actuals cover only part of a period, statement comparisons include only those covered months. Missing months are listed in coverage metadata and flagged in the interface.

For example, 2026 actuals end in September. The yearly statement compares January–September actuals to January–September budget and prior-year January–September results. It does not compare nine actual months against a twelve-month budget. The chart separately labels future October–December plan amounts as budget, with no actual values.

Budget comparisons require matching cohort keys (effective month and seven dimensions) and matching financial component coverage. If a plan is missing a cohort or a component, the statement does not substitute a zero budget: the budget and variance fields become unavailable. Previous-period and prior-year comparisons also require complete matching cohorts and components. An explicit report-month filter may remove historical comparison data.

For a period without any actuals, available budget is shown separately and actuals/variances remain unavailable. Such a plan amount is neither historical performance nor a forecast.

## Variance definitions

- Amount variance = actual − budget.
- Percentage variance = (actual − budget) / abs(budget) × 100; zero budget produces an unavailable percentage.
- Higher revenue or net profit is favorable; lower medical or administrative expense is favorable.
- Negative stop-loss recoveries reduce medical expense, so a larger recovery is favorable under the expense rule.
- Prior-year and previous-period percentages use the same absolute-denominator convention.
- Net profit = revenue − medical expenses − administrative expenses; component totals reconcile to the statement.

Previous period means previous month, previous quarter, or previous year, matching the reporting mode. Prior-year comparisons always shift the covered effective months back twelve months.

## Drilldown and export

Select a financial component to see its totals grouped by payer, medical center, provider, contract, plan, line of business, region, or effective month. A paginated detail table displays the underlying cohorts, actual/budget values, report and general ledger months, snapshot identifier, and source batch. Group totals include every matching cohort and reconcile independently of pagination.

**Export P&L** downloads the selected statement and its subtotal rows, with period boundaries, comparison cutoff, budget plan/version, actual, budget, variance, status, prior year, and previous period. Unavailable values are blank, not zero. Exports are audited.

P&L queries bound actual snapshot reads to the requested period and its comparison window. If multiple reporting versions exist for the same effective cohort, the endpoint rejects the ambiguous selection and requires a report-month filter. This prevents double counting; it is not a restatement import or reconciliation workflow.
