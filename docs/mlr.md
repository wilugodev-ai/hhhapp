# Advanced operational MLR

**MLR Analytics** supports monthly and rolling 3-, 6-, and 12-month analysis. The effective-month filter selects the window end; payer, center, provider, contract, plan, line-of-business, region, and report-month filters apply before calculation. A year filter inherited from another report selects the latest available effective month in that year when no explicit month is selected.

## One definition across the application

Operational MLR = sum of included medical expense / sum of included revenue × 100. It is a ratio of summed amounts, never an average of monthly or cohort percentages. Administrative expenses are always excluded. Signed stop-loss recoveries reduce medical expense when their category is included.

The synthetic seed persists a standard definition selecting all available revenue and medical categories. **Definition & targets** lets CFOs and administrators choose a name and nonempty category selections. Other roles can read them. Each saved definition increments a concurrency-checked version and writes an audit event containing the name, version, and category sets. Stale updates return `409` and the interface retains the draft until the user reloads configuration.

The executive dashboard, payer/center metrics, and financial CSV use this same saved definition. Financial metrics expose `mlrRevenue` and `mlrExpenses` for the exact included amounts. Full revenue, medical expenses, administrative expenses, PMPM, and P&L profit retain their full financial definitions; changing MLR inclusion does not rewrite financial facts or profit. The financial CSV includes the MLR name/version and its included numerator/denominator.

Organizations without a saved definition fall back to all available revenue and medical facts, labeled version 0. A first saved definition requires expected version 0. The development seed creates version 1 without replacing existing definitions or target overrides.

## Coverage and comparison windows

Every requested calendar month and every selected component must be present. A zero amount must be recorded as an explicit fact; an absent selected component is not substituted with zero. Incomplete windows return a null ratio and missing-month/component flags. Nonpositive included revenue also makes MLR unavailable. Observed partial sums remain available for diagnosing coverage.

The previous comparison is the adjacent non-overlapping window of the same duration: September versus August, July–September versus April–June, and so on. A complete current 12-month window can therefore have an unavailable previous comparison if fewer than 24 months exist. Trend charts preserve gaps.

MLR queries read at most the 24-month reporting/comparison range. Multiple report versions for the same effective cohort are rejected; select a report-month filter to disambiguate. Restatement ingestion remains future work.

## Targets and automatic in-app alerts

The organization target is the default. Overrides can be saved for individual payer, center, provider, plan, contract, line-of-business, or region dimensions. The selected grouping determines which overrides apply. For example, a payer override does not become a center target when grouping by center. The overall trend always uses the organization target.

Group targets have concurrency-checked versions and audit history. Resetting an override restores the organization default. Target changes are current settings applied to the selected history; historical target schedules are not implemented.

An alert is produced when the complete window's MLR (rounded to two decimal places) exceeds the applicable target. Equality is within target. Missing/invalid ratios are unavailable, and do not trigger a performance alert. Alerts are returned and displayed in the application; no email, messaging, or scheduled notification service is used.

## Reconciled change attribution

Let `E0`/`R0` be previous included expense/revenue and `E1`/`R1` be current included expense/revenue. Both windows must be complete and both revenue totals positive.

- Expense category contribution in percentage points: `(current category − previous category) / R0 × 100`.
- Revenue category contribution in percentage points: `−E1 × (current category − previous category) / (R1 × R0) × 100`.
- Their sum equals `E1/R1 × 100 − E0/R0 × 100`.

This is an explicit ordered arithmetic attribution: expense effects use previous revenue, then revenue effects use current expense. Contributions are rounded to four decimal places, with a reported rounding reconciliation amount. The total uses unrounded ratios before final rounding; grouped ratio changes/gaps use the displayed two-decimal ratios.

Positive contributions increase MLR; negative contributions reduce it. This breakdown includes membership/cohort-mix changes and does not isolate utilization, clinical causes, or causal relationships. Excluded definition categories are excluded from both the ratio and its contribution breakdown.

The MLR CSV exports the selected group/window, definition version, included amounts, ratio, target, gap, previous ratio, change, status, and coverage. CSV exports are audited. Regulatory MLR calculations and regulatory reporting are not implemented.
