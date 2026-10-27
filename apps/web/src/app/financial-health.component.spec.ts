import { TestBed } from "@angular/core/testing";
import { HttpErrorResponse } from "@angular/common/http";
import { provideRouter } from "@angular/router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiService } from "./api.service";
import { FinancialHealthComponent } from "./financial-health.component";
import { Dashboard, Metrics } from "./models";
import { ProfitLossReport } from "./profit-loss.models";

function dashboard(): Dashboard {
  const metrics: Metrics = {
    revenue: 1000,
    medicalExpenses: 700,
    administrativeExpenses: 100,
    netProfit: 200,
    capitationRevenue: 1000,
    memberMonths: 100,
    grossMargin: 30,
    netMargin: 20,
    mlr: 70,
    revenuePmpm: 10,
    costPmpm: 7,
    mlrRevenue: 1000,
    mlrExpenses: 700,
  };
  return {
    organization: "Horizon",
    mlrTarget: 85,
    mlrDefinitionName: "Operational",
    mlrDefinitionVersion: 1,
    period: "2026-09",
    periodMode: "month",
    isSynthetic: true,
    dataStatus: "sample",
    totals: metrics,
    comparisons: {
      currentMonth: metrics,
      previous: metrics,
      priorYear: metrics,
      previousAvailable: true,
      priorYearAvailable: true,
    },
    endingMembership: 100,
    snapshotCount: 2,
    latestAvailableMonth: "2026-09",
    trends: [],
    pnl: [],
    providers: [],
    payers: [
      {
        id: "a",
        name: "Payer A",
        metrics: {
          ...metrics,
          revenue: 100,
          netProfit: -100,
          memberMonths: 10,
        },
      },
      {
        id: "b",
        name: "Payer B",
        metrics: { ...metrics, revenue: 900, netProfit: 300, memberMonths: 90 },
      },
    ],
    centers: [
      { id: "c", name: "Center C", metrics: { ...metrics, netProfit: -50 } },
      { id: "d", name: "Center D", metrics: { ...metrics, netProfit: 250 } },
    ],
  };
}
function budget(data: Dashboard): ProfitLossReport {
  return {
    organization: "Horizon",
    period: data.periodMode,
    startMonth: data.period!,
    endMonth: data.period!,
    comparedThroughMonth: data.period,
    budgetPlan: {
      id: "plan-1",
      name: "Approved plan",
      year: 2026,
      version: "v1",
      approvedAt: "2026-01-01T00:00:00Z",
      isSynthetic: true,
    },
    availablePlans: [],
    coverage: {
      requestedMonths: [data.period!],
      actualMonths: [data.period!],
      missingActualMonths: [],
      missingBudgetMonths: [],
      budgetMatchesCohorts: true,
      budgetAvailable: true,
      priorYearAvailable: false,
      previousPeriodAvailable: false,
      actualSnapshots: data.snapshotCount,
      budgetSnapshots: data.snapshotCount,
      isPartial: false,
    },
    lines: [],
    trends: [],
    totals: [
      {
        category: "Net profit / loss",
        type: "profit",
        actual: data.totals.netProfit,
        budget: 150,
        priorYear: null,
        previousPeriod: null,
        budgetVariance: {
          amount: data.totals.netProfit - 150,
          percent: 33.33,
          status: "favorable",
        },
        priorYearChangePercent: null,
        previousPeriodChangePercent: null,
      },
    ],
  };
}
async function render(data = dashboard(), response = budget(data)) {
  const api = { profitLoss: vi.fn(async () => response) };
  await TestBed.configureTestingModule({
    imports: [FinancialHealthComponent],
    providers: [provideRouter([]), { provide: ApiService, useValue: api }],
  }).compileComponents();
  const fixture = TestBed.createComponent(FinancialHealthComponent);
  fixture.componentRef.setInput("data", data);
  fixture.componentRef.setInput("filters", {
    month: "2026-09",
    payer: "payer-a",
    reportMonth: "2026-10",
  });
  fixture.detectChanges();
  await fixture.whenStable();
  const root = fixture.nativeElement as HTMLElement;
  return { api, fixture, root, component: fixture.componentInstance };
}
afterEach(() => TestBed.resetTestingModule());
describe("Executive financial health", () => {
  it("uses aggregate revenue and exposure for margins and PMPM and identifies loss-making groups", async () => {
    const { component, root, api } = await render();
    expect(component.netMargin()).toBe(20);
    expect(component.profitPmpm()).toBe(2);
    expect(component.adminRatio()).toBe(10);
    const values = [...root.querySelectorAll(".health-grid strong")].map(
      (item) => item.textContent?.trim(),
    );
    expect(values).toEqual(["20.0%", "$2.00", "10.0%", "$50.00"]);
    expect(root.textContent).toContain("Payer A");
    expect(root.textContent).toContain("Center C");
    expect(component.lossPayers().length).toBe(1);
    expect(component.lossCenters().length).toBe(1);
    expect(api.profitLoss).toHaveBeenCalledWith(
      expect.objectContaining({
        period: "month",
        month: "2026-09",
        year: "",
        payer: "payer-a",
        reportMonth: "2026-10",
      }),
    );
    expect(root.querySelector('a[href="/app/data"]')).not.toBeNull();
    expect(root.textContent).toContain("Sample budget");
    expect(root.textContent).toContain("33.3%");
  });
  it("renders unavailable ratios for nonpositive denominators and unavailable budget coverage", async () => {
    const data = dashboard();
    data.totals = { ...data.totals, revenue: 0, memberMonths: 0 };
    const report = budget(data);
    report.coverage.budgetAvailable = false;
    const { component, root } = await render(data, report);
    expect(component.netMargin()).toBeNull();
    expect(component.profitPmpm()).toBeNull();
    expect(component.adminRatio()).toBeNull();
    expect(
      [...root.querySelectorAll(".health-grid strong")].every(
        (node) => node.textContent?.trim() === "Unavailable",
      ),
    ).toBe(true);
    expect(root.textContent).toContain("no matching approved budget");
  });
  it("requests a year comparison with the same cohort and report-month filters", async () => {
    const data = dashboard();
    data.periodMode = "year";
    const { api } = await render(data);
    expect(api.profitLoss).toHaveBeenCalledWith(
      expect.objectContaining({
        period: "year",
        month: "",
        year: "2026",
        payer: "payer-a",
        reportMonth: "2026-10",
      }),
    );
  });
  it("rejects a budget result that contains different actual records", async () => {
    const data = dashboard();
    const report = budget(data);
    report.coverage.actualSnapshots = 3;
    const { root, component } = await render(data, report);
    expect(component.budget()).toBeNull();
    expect(root.querySelector('[role="alert"]')?.textContent).toContain(
      "Actuals changed",
    );
    const refresh = vi.fn();
    component.refresh.subscribe(refresh);
    root.querySelector<HTMLButtonElement>('[role="alert"] button')!.click();
    expect(refresh).toHaveBeenCalledTimes(1);
  });
  it("keeps negative profit metrics negative and shows an unfavorable budget variance", async () => {
    const data = dashboard();
    data.totals = { ...data.totals, netProfit: -200 };
    const report = budget(data);
    report.totals[0].budgetVariance.status = "unfavorable";
    const { root, component } = await render(data, report);
    expect(component.netMargin()).toBe(-20);
    expect(component.profitPmpm()).toBe(-2);
    expect(
      root.querySelectorAll(".health-grid strong.health-negative").length,
    ).toBe(3);
    expect(root.textContent).toContain("unfavorable");
  });
  it("shows budget errors, then retries without hiding the other KPIs", async () => {
    const { api, component, fixture, root } = await render();
    api.profitLoss.mockRejectedValueOnce(
      new HttpErrorResponse({ status: 503 }),
    );
    await component.loadBudget();
    fixture.detectChanges();
    expect(root.querySelector('[role="alert"]')?.textContent).toContain(
      "HHH API",
    );
    expect(root.textContent).toContain("20.0%");
    root.querySelector<HTMLButtonElement>('[role="alert"] button')!.click();
    await vi.waitFor(() => expect(component.budget()).not.toBeNull());
    await fixture.whenStable();
    expect(root.querySelector('[role="alert"]')).toBeNull();
  });
});
