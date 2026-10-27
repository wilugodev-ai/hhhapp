import { Component, input, signal } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { HttpErrorResponse } from "@angular/common/http";
import { afterEach, describe, expect, it, vi } from "vitest";
import { MlrComponent } from "./mlr.component";
import { ChartComponent } from "./chart.component";
import { ApiService } from "./api.service";
import { Filters, Metadata, User } from "./models";
import { MlrConfiguration, MlrReport, MlrWindow } from "./mlr.models";

@Component({ selector: "hhh-chart", standalone: true, template: "" })
class ChartStub {
  option = input<unknown>();
  label = input("");
}
const metadata: Metadata = {
  months: ["2026-09"],
  reportMonths: ["2026-10"],
  years: [2026],
  dimensions: [{ id: "payer-a", kind: "Payer", name: "Sample payer" }],
};
function configuration(): MlrConfiguration {
  return {
    name: "Standard operational MLR",
    version: 1,
    updatedAt: null,
    revenueCategories: ["Capitation"],
    expenseCategories: ["Inpatient", "Pharmacy"],
    revenueCatalog: ["Capitation"],
    expenseCatalog: ["Inpatient", "Pharmacy"],
    organizationTarget: 85,
    targets: [],
  };
}
function measure(n: number, ratio: number | null): MlrWindow {
  const months = Array.from({ length: n }, (_, i) => {
    const date = new Date("2026-09-01T12:00:00Z");
    date.setUTCMonth(date.getUTCMonth() + i + 1 - n);
    return date.toISOString().slice(0, 7);
  });
  return {
    months: n,
    startMonth: months[0],
    endMonth: "2026-09",
    availableMonths: ratio === null ? months.slice(-1) : months,
    missingMonths: ratio === null ? months.slice(0, -1) : [],
    componentsComplete: true,
    snapshotCount: n,
    revenue: 100,
    expenses: ratio ?? 80,
    memberMonths: 10 * n,
    mlr: ratio,
    costPmpm: 8,
    complete: ratio !== null,
  };
}
function report(filters: Filters = {}): MlrReport {
  const windows = [
    measure(1, 83),
    measure(3, 84),
    measure(6, null),
    measure(12, null),
  ];
  const current = windows.find(
    (x) => x.months === Number(filters["window"] || 1),
  )!;
  return {
    organization: "Sample organization",
    endMonth: "2026-09",
    window: current.months,
    groupBy: filters["groupBy"] || "payer",
    definitionName: "Standard operational MLR",
    definitionVersion: 1,
    revenueCategories: ["Capitation"],
    expenseCategories: ["Inpatient", "Pharmacy"],
    organizationTarget: 85,
    windows,
    current,
    previous: measure(current.months, 80),
    attribution: {
      available: false,
      changePercentagePoints: null,
      roundingPercentagePoints: null,
      drivers: [],
    },
    groups: [
      {
        id: "payer-a",
        name: "Sample group",
        target: 85,
        targetSource: "Organization default",
        values: current,
        previous: measure(current.months, 80),
        changePercentagePoints: 3,
        gapPercentagePoints: -2,
        status: current.mlr === null ? "unavailable" : "within-target",
      },
    ],
    alerts: [],
    trends: [
      {
        month: "2026-09",
        monthly: 83,
        rolling3: 84,
        rolling6: null,
        rolling12: null,
      },
    ],
  };
}
async function setup(role = "Chief Financial Officer") {
  const api = {
    user: signal<User | null>({
      displayName: "Sample user",
      email: "sample@hhh.local",
      organization: "Sample organization",
      roles: [role],
      mlrTarget: 85,
    }),
    mlr: vi.fn(async (filters: Filters) => report(filters)),
    mlrConfiguration: vi.fn(async () => configuration()),
    saveMlrDefinition: vi.fn(
      async (body: {
        name: string;
        expectedVersion: number;
        revenueCategories: string[];
        expenseCategories: string[];
      }) => ({
        ...configuration(),
        name: body.name,
        version: body.expectedVersion + 1,
        revenueCategories: body.revenueCategories,
        expenseCategories: body.expenseCategories,
      }),
    ),
    saveMlrTarget: vi.fn(),
    resetMlrTarget: vi.fn(),
    exportMlr: vi.fn(),
  };
  await TestBed.configureTestingModule({
    imports: [MlrComponent],
    providers: [{ provide: ApiService, useValue: api }],
  })
    .overrideComponent(MlrComponent, {
      remove: { imports: [ChartComponent] },
      add: { imports: [ChartStub] },
    })
    .compileComponents();
  const fixture = TestBed.createComponent(MlrComponent);
  fixture.componentRef.setInput("filters", { month: "2026-09" });
  fixture.componentRef.setInput("metadata", metadata);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  await fixture.whenStable();
  return {
    api,
    fixture,
    component: fixture.componentInstance,
    root: fixture.nativeElement as HTMLElement,
  };
}
afterEach(() => TestBed.resetTestingModule());
describe("MLR analytics interactions", () => {
  it("renders incomplete rolling windows as unavailable instead of zero", async () => {
    const { root } = await setup();
    const cards = root.querySelectorAll<HTMLButtonElement>(".mlr-window-card");
    expect(cards).toHaveLength(4);
    expect(cards[2].querySelector("strong")!.textContent?.trim()).toBe("—");
    expect(cards[2].textContent).toContain("Incomplete");
    expect(cards[2].textContent).not.toContain("0.00%");
  });
  it("preserves the rolling window and grouping when payer filters change", async () => {
    const { api, fixture, component } = await setup();
    component.window = 6;
    component.groupBy = "provider";
    await component.load();
    fixture.componentRef.setInput("filters", {
      month: "2026-09",
      payer: "payer-a",
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(api.mlr).toHaveBeenLastCalledWith(
      expect.objectContaining({
        window: "6",
        groupBy: "provider",
        payer: "payer-a",
      }),
    );
    expect(component.report()?.window).toBe(6);
    expect(component.groupBy).toBe("provider");
  });
  it("saves changed category choices with the loaded definition version and reloads analytics", async () => {
    const { api, fixture, component, root } = await setup();
    component.editor.set(true);
    fixture.detectChanges();
    await fixture.whenStable();
    const pharmacy = Array.from(
      root.querySelectorAll<HTMLLabelElement>(".mlr-checkbox"),
    ).find((x) => x.textContent?.trim() === "Pharmacy")!;
    pharmacy.querySelector("input")!.click();
    fixture.detectChanges();
    component.definitionName = "Without pharmacy";
    await component.saveDefinition();
    fixture.detectChanges();
    expect(api.saveMlrDefinition).toHaveBeenCalledWith({
      name: "Without pharmacy",
      expectedVersion: 1,
      revenueCategories: ["Capitation"],
      expenseCategories: ["Inpatient"],
    });
    expect(api.mlr).toHaveBeenCalledTimes(2);
    expect(component.configuration()?.version).toBe(2);
    expect(root.textContent).toContain("MLR definition saved");
  });
  it("keeps configuration read-only for viewers", async () => {
    const { api, fixture, component, root } = await setup("Executive Viewer");
    component.editor.set(true);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(
      Array.from(root.querySelectorAll("fieldset")).every((x) => x.disabled),
    ).toBe(true);
    const save = Array.from(
      root.querySelectorAll<HTMLButtonElement>("button"),
    ).find((x) => x.textContent?.trim() === "Save MLR definition")!;
    expect(save.disabled).toBe(true);
    await component.saveDefinition();
    expect(api.saveMlrDefinition).not.toHaveBeenCalled();
  });
  it("shows version conflicts without discarding the draft definition", async () => {
    const { api, fixture, component, root } = await setup();
    component.editor.set(true);
    component.definitionName = "My unsaved definition";
    api.saveMlrDefinition.mockRejectedValueOnce(
      new HttpErrorResponse({
        status: 409,
        error: { title: "Settings changed. Reload the configuration." },
      }),
    );
    await component.saveDefinition();
    fixture.detectChanges();
    expect(root.querySelector(".error-banner")?.textContent).toContain(
      "Settings changed",
    );
    expect(component.definitionName).toBe("My unsaved definition");
    expect(component.configuration()?.version).toBe(1);
    expect(api.mlrConfiguration).toHaveBeenCalledTimes(1);
  });
});
