export interface MlrWindow {
  months: number;
  startMonth: string;
  endMonth: string;
  availableMonths: string[];
  missingMonths: string[];
  componentsComplete: boolean;
  snapshotCount: number;
  revenue: number;
  expenses: number;
  memberMonths: number;
  mlr: number | null;
  costPmpm: number | null;
  complete: boolean;
}
export interface MlrTarget {
  dimensionId: string;
  kind: string;
  name: string;
  target: number;
  version: number;
  updatedAt: string;
}
export interface MlrConfiguration {
  name: string;
  version: number;
  updatedAt: string | null;
  revenueCategories: string[];
  expenseCategories: string[];
  revenueCatalog: string[];
  expenseCatalog: string[];
  organizationTarget: number;
  targets: MlrTarget[];
}
export interface MlrGroup {
  id: string;
  name: string;
  target: number;
  targetSource: string;
  values: MlrWindow;
  previous: MlrWindow;
  changePercentagePoints: number | null;
  gapPercentagePoints: number | null;
  status: "unavailable" | "above-target" | "within-target";
}
export interface MlrDriver {
  category: string;
  type: string;
  previousAmount: number;
  currentAmount: number;
  amountChange: number;
  contributionPercentagePoints: number;
}
export interface MlrReport {
  organization: string;
  endMonth: string;
  window: number;
  groupBy: string;
  definitionName: string;
  definitionVersion: number;
  revenueCategories: string[];
  expenseCategories: string[];
  organizationTarget: number;
  windows: MlrWindow[];
  current: MlrWindow;
  previous: MlrWindow;
  attribution: {
    available: boolean;
    changePercentagePoints: number | null;
    roundingPercentagePoints: number | null;
    drivers: MlrDriver[];
  };
  groups: MlrGroup[];
  alerts: MlrGroup[];
  trends: {
    month: string;
    monthly: number | null;
    rolling3: number | null;
    rolling6: number | null;
    rolling12: number | null;
  }[];
}
