import { Injectable, signal } from "@angular/core";

export interface NavigationItem {
  id: string;
  label: string;
  icon: string;
  group?: string;
  planned?: boolean;
}

export const workspaceNavigation: NavigationItem[] = [
  { id: "overview", label: "Overview", icon: "grid", group: "WORKSPACE" },
  { id: "profit-loss", label: "Profit & Loss", icon: "wallet" },
  { id: "mlr", label: "MLR Analytics", icon: "heart" },
  { id: "payers", label: "Payer Performance", icon: "building" },
  { id: "centers", label: "Medical Centers", icon: "building" },
  { id: "membership", label: "Membership", icon: "users" },
  {
    id: "forecast",
    label: "Financial Forecast",
    icon: "trend",
    group: "INTELLIGENCE",
    planned: true,
  },
  {
    id: "scenarios",
    label: "Scenario Modeling",
    icon: "sliders",
    planned: true,
  },
  {
    id: "intelligence",
    label: "HHH Intelligence",
    icon: "spark",
    planned: true,
  },
  { id: "reports", label: "Reports", icon: "file", group: "MANAGE" },
  { id: "data", label: "Data Management", icon: "database" },
  { id: "administration", label: "Administration", icon: "shield" },
  { id: "settings", label: "Settings", icon: "settings" },
];

@Injectable({ providedIn: "root" })
export class NavigationService {
  readonly collapsed = signal(false);
  readonly mobileOpen = signal(false);
}
