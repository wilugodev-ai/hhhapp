import {
  Component,
  computed,
  inject,
  input,
  output,
  OnChanges,
  PendingTasks,
  signal,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { RouterLink } from "@angular/router";
import { ApiService, errorMessage } from "./api.service";
import { Dashboard, Filters } from "./models";
import { ProfitLossLine } from "./profit-loss.models";

@Component({
  selector: "hhh-financial-health",
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <section class="panel health-panel" aria-label="Executive financial health">
      <div class="panel-heading">
        <div>
          <span class="eyebrow">PERFORMANCE & EXPOSURE</span>
          <h2>Financial health</h2>
          <p>
            Profitability after medical and administrative expenses. Uses the
            selected period and filters.
          </p>
        </div>
        <a class="text-link" routerLink="/app/profit-loss">View P&L</a>
      </div>
      <div class="health-grid">
        <article>
          <span>Net profit margin</span
          ><strong [class.health-negative]="(netMargin() ?? 0) < 0">{{
            percentage(netMargin())
          }}</strong
          ><small>Net profit ÷ revenue</small>
        </article>
        <article>
          <span>Profit PMPM</span
          ><strong [class.health-negative]="(profitPmpm() ?? 0) < 0">{{
            money(profitPmpm())
          }}</strong
          ><small>Net profit ÷ member months</small>
        </article>
        <article>
          <span>Administrative expense ratio</span
          ><strong>{{ percentage(adminRatio()) }}</strong
          ><small>Administrative expenses ÷ revenue</small>
        </article>
        <article [attr.aria-busy]="busy()">
          <span>Profit vs. approved budget</span>
          <strong
            [class.health-negative]="
              budget()?.budgetVariance?.status === 'unfavorable'
            "
            >{{
              busy()
                ? "Loading..."
                : money(budget()?.budgetVariance?.amount ?? null)
            }}</strong
          >
          <small>{{ budgetLabel() }}</small>
        </article>
      </div>
      @if (error()) {
        <div class="error-banner" role="alert">
          {{ error() }}
          <button
            class="btn btn-outline"
            type="button"
            (click)="actualsChanged() ? refresh.emit() : loadBudget()"
            [disabled]="busy()"
          >
            {{
              actualsChanged() ? "Reload overview" : "Retry budget comparison"
            }}
          </button>
        </div>
      }
      @if (budgetName()) {
        <p class="health-note">{{ budgetName() }} · {{ coverageNote() }}</p>
      }
      <div class="health-risk-grid">
        <a routerLink="/app/payers"
          ><span>Loss-making payers</span
          ><strong
            >{{ lossPayers().length }}
            <small>of {{ data().payers.length }}</small></strong
          >
          <p>{{ lossNames(lossPayers()) }}</p></a
        >
        <a routerLink="/app/centers"
          ><span>Loss-making medical centers</span
          ><strong
            >{{ lossCenters().length }}
            <small>of {{ data().centers.length }}</small></strong
          >
          <p>{{ lossNames(lossCenters()) }}</p></a
        >
        <a routerLink="/app/data"
          ><span>Latest available financial month</span
          ><strong>{{
            data().latestAvailableMonth
              ? (data().latestAvailableMonth + "-01T12:00:00"
                | date: "MMM yyyy")
              : "Unavailable"
          }}</strong>
          <p>
            For the current cohort filters. View source history and month
            coverage.
          </p></a
        >
      </div>
    </section>
  `,
})
export class FinancialHealthComponent implements OnChanges {
  private api = inject(ApiService);
  private pending = inject(PendingTasks);
  data = input.required<Dashboard>();
  filters = input.required<Filters>();
  refresh = output<void>();
  actualsChanged = signal(false);
  busy = signal(false);
  error = signal("");
  budget = signal<ProfitLossLine | null>(null);
  budgetName = signal("");
  coverageNote = signal("");
  private request = 0;
  netMargin = computed(() =>
    this.ratio(this.data().totals.netProfit, this.data().totals.revenue, 100),
  );
  profitPmpm = computed(() =>
    this.ratio(this.data().totals.netProfit, this.data().totals.memberMonths),
  );
  adminRatio = computed(() =>
    this.ratio(
      this.data().totals.administrativeExpenses,
      this.data().totals.revenue,
      100,
    ),
  );
  lossPayers = computed(() =>
    this.data().payers.filter((row) => row.metrics.netProfit < 0),
  );
  lossCenters = computed(() =>
    this.data().centers.filter((row) => row.metrics.netProfit < 0),
  );
  budgetLabel = computed(() =>
    this.busy()
      ? "Matching approved budget to actuals"
      : this.budget()
        ? `${this.budget()!.budgetVariance.status}${this.budget()!.budgetVariance.percent === null ? "" : " · " + this.percentage(this.budget()!.budgetVariance.percent)} · actual profit minus budget`
        : this.actualsChanged()
          ? "Actuals changed; reload the overview"
          : this.error()
            ? "Budget comparison unavailable"
            : "Unavailable: no matching approved budget",
  );
  ngOnChanges() {
    this.pending.run(() => this.loadBudget());
  }
  private ratio(numerator: number, denominator: number, scale = 1) {
    return denominator > 0 ? (numerator / denominator) * scale : null;
  }
  async loadBudget() {
    const request = ++this.request;
    const data = this.data();
    this.busy.set(true);
    this.error.set("");
    this.actualsChanged.set(false);
    this.budget.set(null);
    this.budgetName.set("");
    this.coverageNote.set("");
    if (!data.period || !data.snapshotCount) {
      this.busy.set(false);
      return;
    }
    try {
      const report = await this.api.profitLoss({
        ...this.filters(),
        period: data.periodMode,
        month: data.periodMode === "month" ? data.period : "",
        year: data.periodMode === "year" ? data.period.slice(0, 4) : "",
        budgetPlan: "",
      });
      if (request !== this.request) return;
      const profit = report.totals.find((line) => line.type === "profit");
      if (
        report.coverage.budgetAvailable &&
        profit?.budgetVariance.amount != null &&
        profit.actual != null
      ) {
        if (
          report.coverage.actualSnapshots !== data.snapshotCount ||
          Math.abs(profit.actual - data.totals.netProfit) >= 0.005
        ) {
          this.error.set(
            "Actuals changed while loading the budget comparison. Reload the overview to compare the same records.",
          );
          this.actualsChanged.set(true);
          return;
        }
        this.budget.set(profit);
        this.budgetName.set(
          report.budgetPlan
            ? `${report.budgetPlan.name} · ${report.budgetPlan.version}${report.budgetPlan.isSynthetic ? " · Sample budget" : ""}`
            : "Approved budget",
        );
        this.coverageNote.set(
          `${report.coverage.actualMonths.length} actual month(s) compared${report.comparedThroughMonth ? " through " + report.comparedThroughMonth : ""}`,
        );
      }
    } catch (error) {
      if (request === this.request) this.error.set(errorMessage(error));
    } finally {
      if (request === this.request) this.busy.set(false);
    }
  }
  lossNames(rows: { name: string }[]) {
    return rows.length
      ? rows
          .slice(0, 3)
          .map((row) => row.name)
          .join(", ") + (rows.length > 3 ? ` and ${rows.length - 3} more` : "")
      : "No reported losses in this selection";
  }
  money(value: number | null) {
    return value === null
      ? "Unavailable"
      : new Intl.NumberFormat("en-US", {
          style: "currency",
          currency: "USD",
          maximumFractionDigits: 2,
        }).format(value);
  }
  percentage(value: number | null) {
    return value === null ? "Unavailable" : value.toFixed(1) + "%";
  }
}
