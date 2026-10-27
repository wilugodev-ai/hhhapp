export interface Metrics {
  revenue: number;
  medicalExpenses: number;
  administrativeExpenses: number;
  netProfit: number;
  capitationRevenue: number;
  memberMonths: number;
  grossMargin: number | null;
  netMargin: number | null;
  mlr: number | null;
  revenuePmpm: number | null;
  costPmpm: number | null;
  mlrRevenue: number;
  mlrExpenses: number;
}
export interface Group {
  id: string;
  name: string;
  metrics: Metrics;
  mlrTarget?: number;
}
export interface User {
  displayName: string;
  email: string;
  organization: string;
  roles: string[];
  mlrTarget: number;
}
export interface Metadata {
  dimensions: { id: string; name: string; kind: string }[];
  months: string[];
  reportMonths: string[];
  years: number[];
}
export interface Dashboard {
  mlrDefinitionName: string;
  mlrDefinitionVersion: number;
  organization: string;
  mlrTarget: number;
  period: string | null;
  periodMode: "month" | "year";
  isSynthetic: boolean;
  totals: Metrics;
  endingMembership: number;
  snapshotCount: number;
  latestAvailableMonth: string | null;
  comparisons: {
    currentMonth: Metrics;
    previous: Metrics;
    priorYear: Metrics;
    previousAvailable: boolean;
    priorYearAvailable: boolean;
  };
  trends: { month: string; metrics: Metrics }[];
  payers: Group[];
  centers: Group[];
  providers: Group[];
  pnl: { category: string; type: string; amount: number }[];
}
export interface Batch {
  id: string;
  name: string;
  loadedAt: string;
  isSynthetic: boolean;
  snapshotCount: number;
}
export type Filters = Record<string, string>;
