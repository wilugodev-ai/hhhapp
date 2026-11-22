import { CommonModule } from "@angular/common";
import {
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  PendingTasks,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, ParamMap, Router, RouterLink } from "@angular/router";
import { ApiService, errorMessage } from "./api.service";
import { HealthcareFilePage, SourceFile } from "./healthcare-files.models";

const primaryFiles: Record<
  string,
  { label: string; columns: string[]; search: string }
> = {
  claims: {
    label: "Claims",
    columns: [
      "SERVICEDATE",
      "PATIENTID",
      "STATUS1",
      "DIAGNOSIS1",
      "OUTSTANDING1",
      "Id",
    ],
    search: "Id",
  },
  claims_transactions: {
    label: "Claim transactions",
    columns: [
      "FROMDATE",
      "PATIENTID",
      "TYPE",
      "PAYMENTS",
      "ADJUSTMENTS",
      "TRANSFERS",
      "CLAIMID",
    ],
    search: "CLAIMID",
  },
  medications: {
    label: "Pharmacy prescriptions",
    columns: [
      "DESCRIPTION",
      "PATIENT",
      "START",
      "STOP",
      "DISPENSES",
      "TOTALCOST",
      "PAYER",
    ],
    search: "DESCRIPTION",
  },
  payer_transitions: {
    label: "Membership coverage",
    columns: [
      "PATIENT",
      "PAYER",
      "START_DATE",
      "END_DATE",
      "MEMBERID",
      "SECONDARY_PAYER",
    ],
    search: "MEMBERID",
  },
};
const columnLabels: Record<string, string> = {
  SERVICEDATE: "Service date",
  PATIENTID: "Patient",
  PATIENT: "Patient",
  STATUS1: "Primary claim status",
  DIAGNOSIS1: "Diagnosis code",
  OUTSTANDING1: "Primary balance ($)",
  FROMDATE: "From",
  TYPE: "Type",
  PAYMENTS: "Payments ($)",
  ADJUSTMENTS: "Adjustments ($)",
  TRANSFERS: "Transfers ($)",
  CLAIMID: "Claim ID",
  DESCRIPTION: "Description",
  START: "Start",
  STOP: "Stop",
  DISPENSES: "Dispenses",
  TOTALCOST: "Total cost ($)",
  PAYER: "Payer",
  START_DATE: "Coverage start",
  END_DATE: "Coverage end",
  MEMBERID: "Member ID",
  SECONDARY_PAYER: "Secondary payer",
  Id: "ID",
};

@Component({
  selector: "hhh-healthcare-files",
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: "./healthcare-files.component.html",
  styles: [
    `
      :host,
      .panel {
        display: block;
        min-width: 0;
      }
      .file-controls,
      .file-search,
      .file-summary,
      .file-state {
        padding: 12px 22px;
      }
      .file-controls,
      .file-search,
      .file-paging {
        display: flex;
        flex-wrap: wrap;
        align-items: end;
        gap: 12px;
      }
      .file-controls label,
      .file-search label {
        display: grid;
        gap: 5px;
        max-width: 100%;
      }
      select,
      input {
        max-width: 100%;
        padding: 6px 10px;
        border: 1px solid var(--line);
        border-radius: 5px;
        color: var(--ink);
        background: var(--surface);
      }
      .file-summary {
        color: var(--muted);
        margin: 0;
      }
      .file-paging {
        align-items: center;
        justify-content: flex-end;
        padding: 16px 22px;
      }
      .file-paging input {
        width: 6rem;
      }
      .file-table {
        max-width: 100%;
      }
      .file-table td {
        max-width: 24rem;
        overflow-wrap: anywhere;
        white-space: normal;
      }
    `,
  ],
})
export class HealthcareFilesComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private destroy = inject(DestroyRef);
  private pending = inject(PendingTasks);
  private sequence = 0;
  private catalog?: Promise<SourceFile[]>;
  readonly files = signal<SourceFile[]>([]);
  readonly data = signal<HealthcareFilePage | null>(null);
  readonly selected = signal("");
  readonly busy = signal(true);
  readonly error = signal("");
  readonly allFields = signal(false);
  readonly routeSearch = signal("");
  column = "";
  search = "";
  jump = 1;
  readonly label = (name: string) =>
    primaryFiles[name]?.label ??
    name.replaceAll("_", " ").replace(/^./, (c) => c.toUpperCase());
  readonly caption = (key: string) => columnLabels[key] ?? key;
  readonly pages = computed(() => {
    const data = this.data();
    return data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  });
  readonly shown = computed(() => {
    const data = this.data();
    if (!data) return [];
    const preferred = primaryFiles[data.table]?.columns ?? [];
    const keys = this.allFields()
      ? []
      : preferred.filter((key) => data.columns.includes(key));
    return (keys.length ? keys : data.columns).map((key) => ({
      key,
      index: data.columns.indexOf(key),
    }));
  });
  display(file: HealthcareFilePage, key: string, value: string) {
    if (
      !this.allFields() &&
      [
        "PATIENT",
        "PATIENTID",
        "PAYER",
        "SECONDARY_PAYER",
        "PRIMARYPATIENTINSURANCEID",
        "SECONDARYPATIENTINSURANCEID",
      ].includes(key)
    )
      return file.names[value] || value || "—";
    return value || "—";
  }

  ngOnInit() {
    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe((params) => {
        this.pending.run(() => this.load(params));
      });
  }

  private async load(params: ParamMap) {
    const request = ++this.sequence;
    this.busy.set(true);
    this.error.set("");
    this.data.set(null);
    try {
      this.catalog ??= this.api.healthcareFiles().catch((error) => {
        this.catalog = undefined;
        throw error;
      });
      const files = await this.catalog;
      if (request !== this.sequence) return;
      const first = Object.keys(primaryFiles);
      const rank = (name: string) =>
        first.includes(name) ? first.indexOf(name) : first.length;
      const ordered = [...files].sort(
        (a, b) => rank(a.name) - rank(b.name) || a.name.localeCompare(b.name),
      );
      this.files.set(ordered);
      const file = params.get("file") ?? ordered[0]?.name ?? "";
      if (file !== this.selected()) this.allFields.set(false);
      this.selected.set(file);
      if (!ordered.length) return;
      if (!ordered.some((item) => item.name === file)) {
        this.error.set("This source file is unavailable in your organization.");
        return;
      }
      const requested = Number(params.get("page") ?? 1);
      const page =
        Number.isSafeInteger(requested) &&
        requested > 0 &&
        requested <= 2147483647
          ? requested
          : 1;
      const term = params.get("q") ?? "";
      this.routeSearch.set(term);
      const column = params.get("column") ?? primaryFiles[file]?.search ?? "";
      this.column = column;
      this.search = term;
      const result = await this.api.healthcareFile(file, page, column, term);
      if (request !== this.sequence) return;
      this.data.set(result);
      this.column =
        result.columns.find(
          (key) => key.toLowerCase() === column.toLowerCase(),
        ) ??
        result.columns[0] ??
        "";
      this.jump = result.page;
    } catch (error) {
      if (request === this.sequence) this.error.set(errorMessage(error));
    } finally {
      if (request === this.sequence) this.busy.set(false);
    }
  }

  selectFile(file: string) {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { file },
    });
  }
  find() {
    const term = this.search.trim();
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        file: this.selected(),
        column: term ? this.column : null,
        q: term || null,
      },
    });
  }
  go(page: number) {
    if (!Number.isInteger(page) || page < 1 || page > this.pages()) return;
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParamsHandling: "merge",
      queryParams: { page: page > 1 ? page : null },
    });
  }
  retry() {
    this.pending.run(() => this.load(this.route.snapshot.queryParamMap));
  }
}
