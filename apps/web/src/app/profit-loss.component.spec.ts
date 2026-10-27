import { Component, input } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ProfitLossComponent } from "./profit-loss.component";
import { ChartComponent } from "./chart.component";
import { ApiService } from "./api.service";
import { Filters, Metadata } from "./models";
import { ProfitLossLine, ProfitLossReport } from "./profit-loss.models";

@Component({ selector: "hhh-chart", standalone: true, template: "" })
class ChartStub {
  option = input<unknown>();
  label = input("");
}
function report(query: Filters): ProfitLossReport {
  const line: ProfitLossLine = {
    category: "Capitation",
    type: "revenue",
    actual: 100,
    budget: null,
    priorYear: null,
    previousPeriod: null,
    budgetVariance: { amount: null, percent: null, status: "unavailable" },
    priorYearChangePercent: null,
    previousPeriodChangePercent: null,
  };
  return {
    organization: "Sample organization",
    period: query["period"] as "month" | "quarter" | "year",
    startMonth: query["period"] === "quarter" ? "2026-07" : "2026-09",
    endMonth: "2026-09",
    comparedThroughMonth: "2026-09",
    budgetPlan: null,
    availablePlans: [],
    coverage: {
      requestedMonths: ["2026-09"],
      actualMonths: ["2026-09"],
      missingActualMonths: [],
      missingBudgetMonths: ["2026-09"],
      budgetMatchesCohorts: false,
      budgetAvailable: false,
      priorYearAvailable: false,
      previousPeriodAvailable: false,
      actualSnapshots: 1,
      budgetSnapshots: 0,
      isPartial: false,
    },
    lines: [line],
    totals: [{ ...line, category: "Total revenue" }],
    trends: [],
  };
}
async function setup() {
  const api = {
    profitLoss: vi.fn(async (filters: Filters) => report(filters)),
    profitLossDetails: vi.fn(),
    exportProfitLoss: vi.fn(),
  };
  await TestBed.configureTestingModule({
    imports: [ProfitLossComponent],
    providers: [{ provide: ApiService, useValue: api }],
  })
    .overrideComponent(ProfitLossComponent, {
      remove: { imports: [ChartComponent] },
      add: { imports: [ChartStub] },
    })
    .compileComponents();
  const fixture = TestBed.createComponent(ProfitLossComponent);
  const metadata: Metadata = {
    months: ["2026-09"],
    reportMonths: ["2026-10"],
    years: [2026],
    dimensions: [],
  };
  fixture.componentRef.setInput("filters", { month: "2026-09" });
  fixture.componentRef.setInput("metadata", metadata);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return {
    fixture,
    api,
    component: fixture.componentInstance,
    root: fixture.nativeElement as HTMLElement,
  };
}
afterEach(() => TestBed.resetTestingModule());
describe("P&L period and coverage behavior", () => {
  it("keeps the quarter selected when the payer changes", async () => {
    const { fixture, api, component } = await setup();
    component.period = "quarter";
    component.year = "2026";
    component.quarter = "3";
    await component.load();
    fixture.componentRef.setInput("filters", {
      month: "2026-09",
      payer: "payer-a",
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(api.profitLoss).toHaveBeenLastCalledWith(
      expect.objectContaining({
        period: "quarter",
        year: "2026",
        quarter: "3",
        payer: "payer-a",
        month: "",
      }),
    );
    expect(component.report()?.period).toBe("quarter");
  });
  it("labels missing budget coverage as unavailable", async () => {
    const { root } = await setup();
    expect(root.textContent).toContain("Budget comparison unavailable");
    const row =
      root.querySelector(".pnl-variance-table tbody tr.selected-component") ||
      Array.from(root.querySelectorAll(".pnl-variance-table tbody tr")).find(
        (x) => x.textContent?.includes("Capitation"),
      )!;
    expect(row.textContent).toContain("Unavailable");
    expect(row.textContent).not.toContain("Favorable");
    expect(row.textContent).toContain("—");
  });
});
