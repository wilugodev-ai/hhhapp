import { signal } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { HttpErrorResponse } from "@angular/common/http";
import { provideRouter } from "@angular/router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiService } from "./api.service";
import {
  DataQualityComponent,
  monthlyCoverage,
} from "./data-quality.component";
import { Batch, Metadata } from "./models";

const meta: Metadata = {
  dimensions: [],
  months: ["2026-02", "2025-12"],
  reportMonths: [],
  years: [2025, 2026],
};
const sources: Batch[] = [
  {
    id: "sample",
    name: "Starter financials",
    loadedAt: "2026-01-01T10:00:00Z",
    isSynthetic: true,
    snapshotCount: 10,
  },
  {
    id: "loaded",
    name: "Recorded source",
    loadedAt: "2026-03-01T10:00:00Z",
    isSynthetic: false,
    snapshotCount: 20,
  },
];
async function render(metadata = meta, batches = sources, fail = false) {
  const api = {
    user: signal({ organization: "Horizon" }),
    metadata: vi.fn(async () => metadata),
    batches: vi.fn(async () => batches),
  };
  if (fail)
    api.metadata.mockRejectedValueOnce(new HttpErrorResponse({ status: 503 }));
  await TestBed.configureTestingModule({
    imports: [DataQualityComponent],
    providers: [provideRouter([]), { provide: ApiService, useValue: api }],
  }).compileComponents();
  const fixture = TestBed.createComponent(DataQualityComponent);
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
describe("Data quality and source history", () => {
  it("finds internal monthly gaps across years without counting duplicate or invalid months", () => {
    expect(
      monthlyCoverage(["2026-02", "2025-12", "2026-02", "2026-13", "invalid"]),
    ).toEqual([
      { month: "2025-12", present: true },
      { month: "2026-01", present: false },
      { month: "2026-02", present: true },
    ]);
    expect(monthlyCoverage([])).toEqual([]);
  });
  it("shows source-derived freshness, missing periods, and both source classifications", async () => {
    const { root, component } = await render();
    expect(component.gaps()).toEqual(["2026-01"]);
    expect(component.latestMonth()).toBe("2026-02");
    expect(component.latestLoad()?.toISOString()).toBe(
      "2026-03-01T10:00:00.000Z",
    );
    expect(
      root.querySelector('[aria-label="2026-01: missing"]'),
    ).not.toBeNull();
    expect(root.textContent).toContain("Sample financial data");
    expect(root.textContent).toContain("Loaded financial data");
    expect(root.textContent).toContain("Not collected yet.");
    expect(root.textContent).not.toContain("January 2025–September 2026");
  });
  it("refreshes checks and updates gaps when a new month is recorded", async () => {
    const { api, root, fixture, component } = await render();
    api.metadata.mockResolvedValueOnce({
      ...meta,
      months: ["2026-02", "2026-01", "2025-12"],
    });
    root.querySelector<HTMLButtonElement>(".quality-header button")!.click();
    await vi.waitFor(() => expect(api.metadata).toHaveBeenCalledTimes(2));
    await fixture.whenStable();
    expect(component.gaps()).toEqual([]);
    expect(
      root.querySelector('[aria-label="2026-01: recorded"]'),
    ).not.toBeNull();
  });
  it("distinguishes empty datasets from unavailable checks", async () => {
    const { root, component } = await render({ ...meta, months: [] }, []);
    expect(component.latestLoad()).toBeNull();
    expect(component.latestMonth()).toBeNull();
    expect(root.textContent).toContain(
      "No financial months have been recorded yet.",
    );
    expect(root.textContent).toContain("No source batches have been recorded.");
    expect(component.checkedAt()).not.toBeNull();
  });
  it("does not report zero source batches when the initial source request fails", async () => {
    const { root, component } = await render(meta, sources, true);
    expect(component.checkedAt()).toBeNull();
    expect(root.querySelector('[role="alert"]')?.textContent).toContain(
      "HHH API",
    );
    expect(root.querySelectorAll(".quality-grid strong")[3].textContent).toBe(
      "Unavailable",
    );
  });
  it("preserves the last successful results when a refresh fails", async () => {
    const { api, component, fixture, root } = await render();
    const checkedAt = component.checkedAt();
    api.batches.mockRejectedValueOnce(new HttpErrorResponse({ status: 503 }));
    await component.load();
    fixture.detectChanges();
    expect(component.checkedAt()).toBe(checkedAt);
    expect(component.batches().length).toBe(2);
    expect(root.querySelector('[role="alert"]')).not.toBeNull();
    expect(
      root.querySelector<HTMLButtonElement>(".quality-header button")?.disabled,
    ).toBe(false);
  });
});
