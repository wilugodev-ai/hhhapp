import { TestBed } from "@angular/core/testing";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AdmissionsComponent } from "./admissions.component";
import { ApiService } from "./api.service";

afterEach(() => TestBed.resetTestingModule());

describe("Admissions", () => {
  it("opens linked files for a stay and returns to the same admissions page", async () => {
    const encounterId = "1c236d86-a9a2-4afe-a69b-524a627a34bf";
    const api = {
      admissions: vi.fn(async (page: number) => ({
        page,
        pageSize: 20,
        total: 21,
        patients: 12,
        facilities: 2,
        items: [
          {
            encounterId,
            patientName: "Sample Patient",
            facilityName: "Sample Hospital",
            description: "Inpatient stay",
            reason: "Condition",
            admittedAt: "2025-11-01T14:00:00Z",
            dischargedAt: null,
          },
        ],
      })),
      admissionRecords: vi.fn(
        async (_id: string, page: number, table = "conditions") => ({
          page,
          pageSize: 20,
          total: 1,
          table,
          columns: ["DESCRIPTION"],
          tables: [
            { name: "conditions", count: 1 },
            { name: "observations", count: 1 },
          ],
          rows: [
            [
              table === "conditions"
                ? "Sample diagnosis"
                : "Systolic blood pressure",
            ],
          ],
        }),
      ),
    };
    await TestBed.configureTestingModule({
      imports: [AdmissionsComponent],
      providers: [{ provide: ApiService, useValue: api }],
    }).compileComponents();
    const fixture = TestBed.createComponent(AdmissionsComponent);
    const root = fixture.nativeElement as HTMLElement;
    const button = (label: string) =>
      [...root.querySelectorAll("button")].find(
        (b) => b.textContent.trim() === label,
      )!;
    fixture.detectChanges();
    await fixture.whenStable();
    button("Next").click();
    await fixture.whenStable();
    expect(root.textContent).toContain("Page 2 of 2");
    button("Sample Patient").click();
    await fixture.whenStable();
    expect(root.textContent).toContain("Sample diagnosis");
    expect(root.querySelector("a")?.getAttribute("href")).toBe(
      `/api/admissions/${encounterId}/fhir`,
    );
    const files = root.querySelector("select")!;
    files.value = "observations";
    files.dispatchEvent(new Event("change"));
    await fixture.whenStable();
    expect(api.admissionRecords).toHaveBeenCalledWith(
      encounterId,
      1,
      "observations",
    );
    expect(root.textContent).toContain("Systolic blood pressure");
    button("Back to admissions").click();
    await fixture.whenStable();
    expect(root.textContent).toContain("Page 2 of 2");
  });
});
