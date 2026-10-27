import {
  Component,
  computed,
  inject,
  OnInit,
  PendingTasks,
  signal,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { RouterLink } from "@angular/router";
import { ApiService, errorMessage } from "./api.service";
import { Batch, Metadata } from "./models";

export function monthlyCoverage(months: string[]) {
  const valid = [...new Set(months)]
    .filter((month) => /^(20\d{2}|2100)-(0[1-9]|1[0-2])$/.test(month))
    .sort();
  if (!valid.length) return [];
  const index = (month: string) =>
    Number(month.slice(0, 4)) * 12 + Number(month.slice(5)) - 1;
  const present = new Set(valid);
  return Array.from(
    { length: index(valid.at(-1)!) - index(valid[0]) + 1 },
    (_, offset) => {
      const value = index(valid[0]) + offset;
      const month = `${Math.floor(value / 12)}-${String((value % 12) + 1).padStart(2, "0")}`;
      return { month, present: present.has(month) };
    },
  );
}

@Component({
  selector: "hhh-data-quality",
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="quality-workspace" [attr.aria-busy]="busy()">
      <section class="panel quality-header">
        <div>
          <span class="eyebrow">DATA YOU CAN TRACE</span>
          <h2>Data quality & sources</h2>
          <p>
            Organization-wide history for {{ api.user()?.organization }}.
            Financial report filters do not restrict these checks.
          </p>
        </div>
        <button
          class="btn btn-outline"
          type="button"
          (click)="load()"
          [disabled]="busy()"
        >
          {{ busy() ? "Checking sources..." : "Refresh checks" }}
        </button>
      </section>
      @if (error()) {
        <div class="error-banner" role="alert">{{ error() }}</div>
      }
      @if (checkedAt()) {
        <p class="health-note" role="status">
          Last successful check {{ checkedAt() | date: "medium" }}
        </p>
      }
      <section class="quality-grid" aria-label="Data quality indicators">
        <article class="panel">
          <span>Latest financial month</span
          ><strong>{{
            latestMonth()
              ? (latestMonth() + "-01T12:00:00" | date: "MMM yyyy")
              : "Unavailable"
          }}</strong
          ><small>Most recent effective month recorded</small>
        </article>
        <article class="panel">
          <span>Last saved source batch</span
          ><strong>{{
            latestLoad() ? (latestLoad() | date: "mediumDate") : "Unavailable"
          }}</strong
          ><small>{{
            latestLoad()
              ? (latestLoad() | date: "shortTime")
              : "No source load timestamp recorded"
          }}</small>
        </article>
        <article class="panel">
          <span>Gaps in monthly history</span
          ><strong [class.health-negative]="gaps().length > 0">{{
            coverage().length ? gaps().length : "Unavailable"
          }}</strong
          ><small>Between the first and last recorded months</small>
        </article>
        <article class="panel">
          <span>Persisted source batches</span
          ><strong>{{ checkedAt() ? batches().length : "Unavailable" }}</strong
          ><small>{{
            checkedAt()
              ? sampleCount() +
                " sample · " +
                (batches().length - sampleCount()) +
                " loaded"
              : "Source checks have not completed"
          }}</small>
        </article>
      </section>
      <section class="panel quality-coverage">
        <div class="panel-heading">
          <div>
            <h2>Monthly history coverage</h2>
            <p>
              A recorded month contains at least one financial snapshot. P&L and
              MLR provide detailed cohort and component coverage checks.
            </p>
          </div>
        </div>
        @if (coverage().length) {
          <div class="coverage-legend">
            <span>● Recorded</span><span>○ Missing</span
            ><span
              >{{ coverage().length - gaps().length }} of
              {{ coverage().length }} months recorded</span
            >
          </div>
          <div class="coverage-months">
            @for (row of shownMonths(); track row.month) {
              <span
                [class.coverage-missing]="!row.present"
                [attr.aria-label]="
                  row.month + (row.present ? ': recorded' : ': missing')
                "
                >{{ row.present ? "●" : "○" }} {{ row.month }}</span
              >
            }
          </div>
          @if (coverage().length > 24) {
            <button
              class="btn btn-outline"
              type="button"
              (click)="showAll.update(toggle)"
            >
              {{ showAll() ? "Show latest 24 months" : "Show full history" }}
            </button>
          }
        } @else {
          <p>
            {{
              checkedAt()
                ? "No financial months have been recorded yet."
                : "Monthly coverage is unavailable until source checks complete."
            }}
          </p>
        }
        @if (gaps().length) {
          <div class="inline-info">
            Missing effective months: {{ gaps().join(", ") }}. Review the source
            history before comparing periods.
          </div>
        }
      </section>
      <section class="panel">
        <div class="panel-heading">
          <div>
            <h2>Source history & classification</h2>
            <p>
              Source batches persisted in SQL Server. Classification follows
              each batch's sample-data flag.
            </p>
          </div>
        </div>
        <div class="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Source batch</th>
                <th>Saved</th>
                <th class="numeric">Snapshots</th>
                <th>Classification</th>
              </tr>
            </thead>
            <tbody>
              @for (batch of batches(); track batch.id) {
                <tr>
                  <td>
                    <strong>{{ batch.name }}</strong
                    ><small class="cell-sub">{{ batch.id }}</small>
                  </td>
                  <td>{{ batch.loadedAt | date: "medium" }}</td>
                  <td class="numeric">{{ batch.snapshotCount | number }}</td>
                  <td>
                    <span class="status-chip">{{
                      batch.isSynthetic
                        ? "Sample financial data"
                        : "Loaded financial data"
                    }}</span>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="4">
                    {{
                      checkedAt()
                        ? "No source batches have been recorded."
                        : "Source history is unavailable until checks complete."
                    }}
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
      <section class="panel quality-validation">
        <h2>Import validation</h2>
        <p>
          <strong>Not collected yet.</strong> Rejected records, duplicate-file
          checks, and import failures will appear when the file-import pipeline
          records validation results.
        </p>
        <a class="text-link" routerLink="/app/profit-loss"
          >Review actual and budget coverage in P&L</a
        >
      </section>
    </div>
  `,
})
export class DataQualityComponent implements OnInit {
  readonly api = inject(ApiService);
  private pending = inject(PendingTasks);
  metadata = signal<Metadata | null>(null);
  batches = signal<Batch[]>([]);
  busy = signal(false);
  error = signal("");
  checkedAt = signal<Date | null>(null);
  showAll = signal(false);
  readonly toggle = (value: boolean) => !value;
  coverage = computed(() => monthlyCoverage(this.metadata()?.months ?? []));
  gaps = computed(() =>
    this.coverage()
      .filter((row) => !row.present)
      .map((row) => row.month),
  );
  shownMonths = computed(() =>
    this.showAll() ? this.coverage() : this.coverage().slice(-24),
  );
  latestMonth = computed(() => this.coverage().at(-1)?.month ?? null);
  latestLoad = computed(() => {
    const times = this.batches()
      .map((batch) => Date.parse(batch.loadedAt))
      .filter(Number.isFinite);
    return times.length ? new Date(Math.max(...times)) : null;
  });
  sampleCount = computed(
    () => this.batches().filter((batch) => batch.isSynthetic).length,
  );
  ngOnInit() {
    this.pending.run(() => this.load());
  }
  async load() {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set("");
    try {
      const [metadata, batches] = await Promise.all([
        this.api.metadata(),
        this.api.batches(),
      ]);
      this.metadata.set(metadata);
      this.batches.set(batches);
      this.checkedAt.set(new Date());
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
