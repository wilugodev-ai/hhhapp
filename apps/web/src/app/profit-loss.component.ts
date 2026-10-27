import {
  Component,
  afterNextRender,
  ElementRef,
  DestroyRef,
  Injector,
  computed,
  inject,
  input,
  OnInit,
  OnChanges,
  SimpleChanges,
  signal,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import type { EChartsCoreOption } from "echarts/core";
import { ApiService, errorMessage } from "./api.service";
import { ChartComponent } from "./chart.component";
import { IconComponent } from "./icon.component";
import { Filters, Metadata } from "./models";
import {
  ProfitLossDetails,
  ProfitLossLine,
  ProfitLossReport,
} from "./profit-loss.models";

@Component({
  selector: "hhh-profit-loss",
  standalone: true,
  imports: [CommonModule, FormsModule, ChartComponent, IconComponent],
  templateUrl: "./profit-loss.component.html",
})
export class ProfitLossComponent implements OnInit, OnChanges {
  private api = inject(ApiService);
  private host = inject<ElementRef<HTMLElement>>(ElementRef);
  private injector = inject(Injector);
  private destroy = inject(DestroyRef);
  filters = input.required<Filters>();
  metadata = input.required<Metadata>();
  dark = input(false);
  report = signal<ProfitLossReport | null>(null);
  details = signal<ProfitLossDetails | null>(null);
  busy = signal(true);
  detailBusy = signal(false);
  exporting = signal(false);
  error = signal("");
  detailError = signal("");
  notice = signal("");
  selected = signal<ProfitLossLine | null>(null);
  period: "month" | "quarter" | "year" = "month";
  month = "";
  year = "";
  quarter = "3";
  budgetPlan = "";
  groupBy = "payer";
  page = 1;
  private reportRequest = 0;
  private detailRequest = 0;
  private loadedQuery: Filters = {};
  groups = [
    { id: "payer", name: "Payer" },
    { id: "center", name: "Medical center" },
    { id: "provider", name: "Provider / PCP" },
    { id: "contract", name: "Contract" },
    { id: "plan", name: "Plan" },
    { id: "lineOfBusiness", name: "Line of business" },
    { id: "region", name: "Region" },
    { id: "month", name: "Effective month" },
  ];
  sections = [
    { type: "revenue", title: "REVENUE", total: "Total revenue" },
    {
      type: "medical",
      title: "MEDICAL EXPENSES",
      total: "Total medical expenses",
    },
    {
      type: "administrative",
      title: "ADMINISTRATIVE EXPENSES",
      total: "Administrative expenses",
    },
  ];
  plans() {
    return (this.report()?.availablePlans || []).filter(
      (x) => x.year === Number(this.year),
    );
  }
  chart = computed<EChartsCoreOption>(() => {
    const d = this.report(),
      muted = this.dark() ? "#8fa6bd" : "#7d8ba0",
      grid = this.dark() ? "#233951" : "#eef1f6";
    return {
      color: ["#00b8a9", "#a8bbd0"],
      tooltip: {
        trigger: "axis",
        valueFormatter: (x: unknown) =>
          typeof x === "number" ? this.money(x) : "Unavailable",
      },
      legend: { top: 2, textStyle: { color: muted, fontSize: 10 } },
      grid: { left: 65, right: 20, top: 40, bottom: 30 },
      xAxis: {
        type: "category",
        data: (d?.trends || []).map((x) => this.label(x.month, true)),
        axisLabel: { color: muted, fontSize: 10 },
        axisTick: { show: false },
        axisLine: { show: false },
      },
      yAxis: {
        type: "value",
        axisLabel: {
          color: muted,
          fontSize: 10,
          formatter: (n: number) => "$" + (n / 1e6).toFixed(1) + "M",
        },
        splitLine: { lineStyle: { color: grid, type: "dashed" } },
      },
      series: [
        {
          name: "Actual net profit",
          type: "bar",
          barMaxWidth: 38,
          data: (d?.trends || []).map((x) => x.actual),
          itemStyle: { borderRadius: [3, 3, 0, 0] },
        },
        {
          name: "Budget net profit",
          type: "bar",
          barMaxWidth: 38,
          data: (d?.trends || []).map((x) => x.budget),
          itemStyle: { borderRadius: [3, 3, 0, 0] },
        },
      ],
    };
  });
  ngOnChanges(changes: SimpleChanges) {
    if (changes["filters"] && !changes["filters"].firstChange) void this.load();
  }
  async ngOnInit() {
    const anchor =
      this.filters()["month"] ||
      this.metadata().months[0] ||
      new Date().toISOString().slice(0, 7);
    this.month = anchor;
    this.year = anchor.slice(0, 4);
    this.quarter = String(Math.ceil(Number(anchor.slice(5, 7)) / 3));
    if (this.filters()["year"]) {
      this.period = "year";
      this.year = this.filters()["year"];
    }
    await this.load();
  }
  query(): Filters {
    const base = { ...this.filters() };
    delete base["month"];
    delete base["year"];
    return {
      ...base,
      period: this.period,
      month: this.period === "month" ? this.month : "",
      year: this.period === "month" ? "" : this.year,
      quarter: this.period === "quarter" ? this.quarter : "",
      budgetPlan: this.budgetPlan,
    };
  }
  async load() {
    const id = ++this.reportRequest;
    ++this.detailRequest;
    this.busy.set(true);
    this.error.set("");
    this.details.set(null);
    this.selected.set(null);
    const query = this.query();
    try {
      const report = await this.api.profitLoss(query);
      if (id === this.reportRequest) {
        this.report.set(report);
        this.loadedQuery = query;
      }
    } catch (e) {
      if (id === this.reportRequest) {
        this.error.set(errorMessage(e));
        this.report.set(null);
      }
    } finally {
      if (id === this.reportRequest) this.busy.set(false);
    }
  }
  periodChanged() {
    this.budgetPlan = "";
    void this.load();
  }
  monthChanged() {
    this.year = this.month.slice(0, 4);
    this.quarter = String(Math.ceil(Number(this.month.slice(5, 7)) / 3));
    this.periodChanged();
  }
  async open(line: ProfitLossLine) {
    this.selected.set(line);
    this.page = 1;
    await this.loadDetails();
    if (this.destroy.destroyed || this.selected() !== line) return;
    afterNextRender(
      () =>
        this.host.nativeElement
          .querySelector<HTMLElement>(".pnl-drilldown")
          ?.scrollIntoView({
            behavior: window.matchMedia("(prefers-reduced-motion: reduce)")
              .matches
              ? "auto"
              : "smooth",
            block: "start",
          }),
      { injector: this.injector },
    );
  }
  async loadDetails() {
    const line = this.selected();
    if (!line) return;
    const id = ++this.detailRequest;
    this.detailBusy.set(true);
    this.detailError.set("");
    this.details.set(null);
    try {
      const d = await this.api.profitLossDetails({
        ...this.loadedQuery,
        category: line.category,
        type: line.type,
        groupBy: this.groupBy,
        page: String(this.page),
      });
      if (id === this.detailRequest) this.details.set(d);
    } catch (e) {
      if (id === this.detailRequest) this.detailError.set(errorMessage(e));
    } finally {
      if (id === this.detailRequest) this.detailBusy.set(false);
    }
  }
  close() {
    ++this.detailRequest;
    this.selected.set(null);
    this.details.set(null);
    this.detailBusy.set(false);
  }
  groupChanged() {
    this.page = 1;
    void this.loadDetails();
  }
  nextPage(offset: number) {
    this.page += offset;
    void this.loadDetails();
  }
  async export() {
    if (this.exporting()) return;
    this.exporting.set(true);
    this.notice.set("");
    try {
      await this.api.exportProfitLoss(this.loadedQuery);
      this.notice.set("P&L with budget and period comparisons downloaded.");
    } catch (e) {
      this.notice.set(errorMessage(e));
    } finally {
      this.exporting.set(false);
    }
  }
  lines(type: string) {
    return this.report()?.lines.filter((x) => x.type === type) || [];
  }
  total(type: string) {
    return this.report()?.totals.find((x) => x.type === type);
  }
  money(n: number | null, compact = false) {
    return n === null
      ? "—"
      : new Intl.NumberFormat("en-US", {
          style: "currency",
          currency: "USD",
          notation: compact ? "compact" : "standard",
          maximumFractionDigits: compact ? 2 : 0,
        }).format(n);
  }
  signed(n: number | null, percent = false) {
    return n === null
      ? "—"
      : (n > 0 ? "+" : "") + (percent ? n.toFixed(1) + "%" : this.money(n));
  }
  status(line: ProfitLossLine) {
    return {
      favorable: "Favorable",
      unfavorable: "Unfavorable",
      "on-budget": "On budget",
      unavailable: "Unavailable",
    }[line.budgetVariance.status];
  }
  label(month: string, short = false) {
    return new Date(month + "-01T12:00:00").toLocaleDateString("en-US", {
      month: short ? "short" : "long",
      year: short ? undefined : "numeric",
    });
  }
  title() {
    const r = this.report();
    return !r
      ? "Profit & loss"
      : r.period === "month"
        ? this.label(r.startMonth)
        : r.period === "quarter"
          ? `Q${Math.ceil(Number(r.startMonth.slice(5, 7)) / 3)} ${r.startMonth.slice(0, 4)}`
          : r.startMonth.slice(0, 4) +
            (r.coverage.isPartial ? " year to date" : " full year");
  }
  comparisonLabel() {
    return this.report()?.period === "quarter"
      ? "Previous quarter"
      : this.report()?.period === "year"
        ? "Previous year"
        : "Previous month";
  }
  pageCount() {
    const d = this.details();
    return d ? Math.max(1, Math.ceil(d.totalRows / d.pageSize)) : 1;
  }
  groupName() {
    return (
      this.groups.find((group) => group.id === this.groupBy)?.name || "Group"
    );
  }
}
