import { CommonModule } from "@angular/common";
import { Component, inject, OnInit, PendingTasks, signal } from "@angular/core";
import { ApiService, errorMessage } from "./api.service";
import {
  Admission,
  AdmissionPage,
  AdmissionRecords,
} from "./admissions.models";

@Component({
  selector: "hhh-admissions",
  standalone: true,
  imports: [CommonModule],
  templateUrl: "./admissions.component.html",
  styles: [
    `
      :host,
      section {
        display: block;
        min-width: 0;
      }
      .panel-heading,
      .record-toolbar {
        display: flex;
        flex-wrap: wrap;
        gap: 14px;
        align-items: center;
        justify-content: space-between;
      }
      .record-toolbar {
        padding: 0 22px 16px;
      }
      .record-toolbar select {
        max-width: 100%;
        margin-inline-start: 8px;
      }
      .source-records {
        width: 100%;
        max-width: 100%;
      }
      .source-records td {
        max-width: 25rem;
        overflow-wrap: anywhere;
        white-space: normal;
      }
      .text-link {
        text-align: start;
      }
      .state {
        padding: 18px 22px;
      }
    `,
  ],
})
export class AdmissionsComponent implements OnInit {
  private api = inject(ApiService);
  private pending = inject(PendingTasks);
  private request = 0;
  private lastQuery: { page: number; table?: string } = { page: 1 };
  readonly list = signal<AdmissionPage | null>(null);
  readonly selected = signal<Admission | null>(null);
  readonly records = signal<AdmissionRecords | null>(null);
  readonly busy = signal(false);
  readonly error = signal("");
  readonly pageCount = (total: number, size: number) => Math.ceil(total / size);

  ngOnInit() {
    this.pending.run(() => this.load(1));
  }

  async load(page: number, table?: string) {
    const request = ++this.request;
    const admission = this.selected();
    this.lastQuery = { page, table };
    this.busy.set(true);
    this.error.set("");
    try {
      if (admission) {
        if (table && table !== this.records()?.table) this.records.set(null);
        const result = await this.api.admissionRecords(
          admission.encounterId,
          page,
          table,
        );
        if (request === this.request) this.records.set(result);
      } else {
        const result = await this.api.admissions(page);
        if (request === this.request) this.list.set(result);
      }
    } catch (error) {
      if (request === this.request) this.error.set(errorMessage(error));
    } finally {
      if (request === this.request) this.busy.set(false);
    }
  }

  retry() {
    void this.load(this.lastQuery.page, this.lastQuery.table);
  }

  open(admission: Admission) {
    this.selected.set(admission);
    this.records.set(null);
    void this.load(1);
  }

  back() {
    ++this.request;
    this.selected.set(null);
    this.records.set(null);
    this.error.set("");
    this.busy.set(false);
  }
}
