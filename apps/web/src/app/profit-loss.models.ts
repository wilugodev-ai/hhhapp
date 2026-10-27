export interface Variance {
  amount: number | null;
  percent: number | null;
  status: "favorable" | "unfavorable" | "on-budget" | "unavailable";
}
export interface ProfitLossLine {
  category: string;
  type: string;
  actual: number | null;
  budget: number | null;
  priorYear: number | null;
  previousPeriod: number | null;
  budgetVariance: Variance;
  priorYearChangePercent: number | null;
  previousPeriodChangePercent: number | null;
}
export interface BudgetPlanInfo {
  id: string;
  name: string;
  year: number;
  version: string;
  approvedAt: string;
  isSynthetic: boolean;
}
export interface ProfitLossReport {
  organization: string;
  period: "month" | "quarter" | "year";
  startMonth: string;
  endMonth: string;
  comparedThroughMonth: string | null;
  budgetPlan: BudgetPlanInfo | null;
  availablePlans: BudgetPlanInfo[];
  coverage: {
    requestedMonths: string[];
    actualMonths: string[];
    missingActualMonths: string[];
    missingBudgetMonths: string[];
    budgetMatchesCohorts: boolean;
    budgetAvailable: boolean;
    priorYearAvailable: boolean;
    previousPeriodAvailable: boolean;
    actualSnapshots: number;
    budgetSnapshots: number;
    isPartial: boolean;
  };
  lines: ProfitLossLine[];
  totals: ProfitLossLine[];
  trends: { month: string; actual: number | null; budget: number | null }[];
}
export interface ProfitLossDetails {
  category: string;
  type: string;
  groupBy: string;
  total: ProfitLossLine;
  groups: { id: string; name: string; values: ProfitLossLine }[];
  rows: {
    month: string;
    snapshotId: number | null;
    reportMonth: string | null;
    generalLedgerMonth: string | null;
    sourceBatchId: string | null;
    sourceName: string | null;
    dimensions: Record<string, string>;
    values: ProfitLossLine;
  }[];
  page: number;
  pageSize: number;
  totalRows: number;
}
