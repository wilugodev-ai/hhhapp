# Executive financial health and data quality

The executive overview adds financial health indicators calculated from the same organization, period, and cohort filters as the existing dashboard.

| Indicator | Definition |
| --- | --- |
| Net profit margin | Total net profit / total revenue × 100 |
| Profit PMPM | Total net profit / total member months |
| Administrative expense ratio | Total administrative expenses / total revenue × 100 |
| Profit versus approved budget | Actual profit minus matched approved budget profit |
| Loss-making payers and centers | Groups with aggregate net profit below zero |
| Latest available financial month | Latest effective month available under the current cohort and report-month filters |

Ratios use aggregate numerators and denominators. Nonpositive denominators render as unavailable. Negative profit remains negative.

Budget comparisons reuse the P&L API's approved-plan selection, cohort and component matching, and covered-actual-month rules. A yearly comparison uses the same year's actual months, rather than comparing partial-year actuals with a full-year budget. The card verifies snapshot count and actual profit against the overview before displaying a variance. Missing coverage and inconsistent actuals do not produce a variance. Sample budgets are labeled separately from actual-data classification.

Data Management now contains Data Quality & Sources. Its scope is the signed-in organization, independent of financial-report filters. It shows:

- Latest recorded effective month and latest persisted source-batch timestamp.
- Calendar gaps between the first and last recorded effective months.
- Source-batch history, recorded snapshot counts, and sample/loaded classification.
- Refresh status and errors, retaining the previous successful results after a failed refresh.

A recorded month means at least one snapshot exists. Monthly continuity is not a completeness check for every cohort or financial component; P&L and MLR retain their detailed coverage checks. File-import validation results, rejected records, and failed-import history remain unavailable until an ingestion pipeline records them.
