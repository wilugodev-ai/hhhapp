import {
  Component,
  computed,
  inject,
  input,
  OnChanges,
  OnInit,
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
import { MlrConfiguration, MlrGroup, MlrReport, MlrWindow } from "./mlr.models";

@Component({
  selector: "hhh-mlr",
  standalone: true,
  imports: [CommonModule, FormsModule, ChartComponent, IconComponent],
  templateUrl: "./mlr.component.html",
})
export class MlrComponent implements OnInit, OnChanges {
  private api = inject(ApiService);
  filters = input.required<Filters>();
  metadata = input.required<Metadata>();
  dark = input(false);
  report = signal<MlrReport | null>(null);
  configuration = signal<MlrConfiguration | null>(null);
  busy = signal(true);
  error = signal("");
  notice = signal("");
  editorError = signal("");
  editor = signal(false);
  saving = signal(false);
  exporting = signal(false);
  window = 1;
  groupBy = "payer";
  definitionName = "";
  revenue: string[] = [];
  expenses: string[] = [];
  targetDimension = "";
  target = 85;
  private request = 0;
  private loadedQuery: Filters = {};
  private configRequest = 0;
  groups = [
    { id: "payer", kind: "Payer", name: "Payer" },
    { id: "center", kind: "MedicalCenter", name: "Medical center" },
    { id: "provider", kind: "Provider", name: "Provider / PCP" },
    { id: "plan", kind: "Plan", name: "Plan" },
    { id: "contract", kind: "Contract", name: "Contract" },
    { id: "lineOfBusiness", kind: "LineOfBusiness", name: "Line of business" },
    { id: "region", kind: "Region", name: "Region" },
  ];
  canManage = computed(
    () =>
      this.api
        .user()
        ?.roles.some((x) =>
          [
            "Chief Financial Officer",
            "Organization Administrator",
            "System Administrator",
          ].includes(x),
        ) ?? false,
  );
  chart = computed<EChartsCoreOption>(() => {
    const r = this.report(),
      muted = this.dark() ? "#8fa6bd" : "#7d8ba0",
      grid = this.dark() ? "#233951" : "#eef1f6";
    const key =
      this.window === 1
        ? "monthly"
        : this.window === 3
          ? "rolling3"
          : this.window === 6
            ? "rolling6"
            : "rolling12";
    return {
      color: ["#00b8a9"],
      tooltip: {
        trigger: "axis",
        valueFormatter: (x: unknown) =>
          typeof x === "number" ? this.percent(x) : "Unavailable",
      },
      grid: { left: 58, right: 25, top: 25, bottom: 35 },
      xAxis: {
        type: "category",
        boundaryGap: false,
        data: (r?.trends || []).map((x) => this.month(x.month, true)),
        axisLabel: { color: muted, fontSize: 10 },
        axisLine: { show: false },
        axisTick: { show: false },
      },
      yAxis: {
        type: "value",
        axisLabel: { color: muted, fontSize: 10, formatter: "{value}%" },
        splitLine: { lineStyle: { color: grid, type: "dashed" } },
        scale: true,
      },
      series: [
        {
          name:
            this.window === 1
              ? "Monthly MLR"
              : `Rolling ${this.window}-month MLR`,
          type: "line",
          smooth: 0.2,
          showSymbol: false,
          connectNulls: false,
          lineStyle: { width: 2.5 },
          areaStyle: { opacity: 0.05 },
          data: (r?.trends || []).map((x) => x[key]),
          markLine: {
            symbol: "none",
            label: {
              formatter: "Organization target",
              fontSize: 9,
              color: muted,
            },
            lineStyle: { color: "#d5a262" },
            data: [{ yAxis: r?.organizationTarget ?? 85 }],
          },
        },
      ],
    };
  });
  ngOnChanges(changes: SimpleChanges) {
    if (changes["filters"] && !changes["filters"].firstChange) void this.load();
  }
  async ngOnInit() {
    await Promise.all([this.load(), this.loadConfiguration()]);
  }
  query(): Filters {
    return {
      ...this.filters(),
      window: String(this.window),
      groupBy: this.groupBy,
    };
  }
  async load() {
    const id = ++this.request;
    this.busy.set(true);
    this.error.set("");
    const query = this.query();
    try {
      const data = await this.api.mlr(query);
      if (id === this.request) {
        this.report.set(data);
        this.loadedQuery = query;
      }
    } catch (e) {
      if (id === this.request) {
        this.error.set(errorMessage(e));
        this.report.set(null);
      }
    } finally {
      if (id === this.request) this.busy.set(false);
    }
  }
  async loadConfiguration() {
    const id = ++this.configRequest;
    try {
      const config = await this.api.mlrConfiguration();
      if (id === this.configRequest) this.applyConfiguration(config);
    } catch (e) {
      if (id === this.configRequest) this.editorError.set(errorMessage(e));
    }
  }
  private applyConfiguration(config: MlrConfiguration) {
    this.configuration.set(config);
    this.definitionName = config.name;
    this.revenue = [...config.revenueCategories];
    this.expenses = [...config.expenseCategories];
    this.targetChanged();
  }
  async saveDefinition() {
    if (!this.canManage() || !this.configuration() || this.saving()) return;
    this.saving.set(true);
    this.editorError.set("");
    try {
      const config = await this.api.saveMlrDefinition({
        name: this.definitionName,
        expectedVersion: this.configuration()!.version,
        revenueCategories: this.revenue,
        expenseCategories: this.expenses,
      });
      this.applyConfiguration(config);
      this.notice.set(
        "MLR definition saved. The same definition now applies to the dashboard and financial exports.",
      );
      await this.load();
    } catch (e) {
      this.editorError.set(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }
  toggle(type: "revenue" | "medical", category: string, included: boolean) {
    const values = type === "revenue" ? this.revenue : this.expenses;
    const next = included
      ? [...values, category]
      : values.filter((x) => x !== category);
    if (type === "revenue") this.revenue = next;
    else this.expenses = next;
  }
  targetChanged() {
    this.target =
      this.configuration()?.targets.find(
        (x) => x.dimensionId === this.targetDimension,
      )?.target ??
      this.configuration()?.organizationTarget ??
      85;
  }
  async saveTarget() {
    if (!this.canManage() || !this.targetDimension || this.saving()) return;
    this.saving.set(true);
    this.editorError.set("");
    try {
      const version =
        this.configuration()?.targets.find(
          (x) => x.dimensionId === this.targetDimension,
        )?.version ?? 0;
      this.applyConfiguration(
        await this.api.saveMlrTarget(
          this.targetDimension,
          this.target,
          version,
        ),
      );
      this.notice.set("Group MLR target saved.");
      await this.load();
    } catch (e) {
      this.editorError.set(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }
  async resetTarget() {
    const rule = this.configuration()?.targets.find(
      (x) => x.dimensionId === this.targetDimension,
    );
    if (!this.canManage() || !rule || this.saving()) return;
    this.saving.set(true);
    this.editorError.set("");
    try {
      await this.api.resetMlrTarget(rule.dimensionId, rule.version);
      await this.loadConfiguration();
      await this.load();
      this.notice.set("Group target reset to the organization default.");
    } catch (e) {
      this.editorError.set(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }
  targetRule() {
    return this.configuration()?.targets.find(
      (x) => x.dimensionId === this.targetDimension,
    );
  }
  selectTarget(row: MlrGroup) {
    this.targetDimension = row.id;
    this.targetChanged();
    this.editor.set(true);
  }
  async export() {
    if (this.exporting()) return;
    this.exporting.set(true);
    try {
      await this.api.exportMlr(this.loadedQuery);
      this.notice.set(
        "MLR report downloaded with its definition version and coverage.",
      );
    } catch (e) {
      this.notice.set(errorMessage(e));
    } finally {
      this.exporting.set(false);
    }
  }
  percent(value: number | null) {
    return value === null ? "—" : value.toFixed(2) + "%";
  }
  money(value: number | null) {
    return value === null
      ? "—"
      : new Intl.NumberFormat("en-US", {
          style: "currency",
          currency: "USD",
          maximumFractionDigits: 0,
        }).format(value);
  }
  points(value: number | null, precision = 2) {
    return value === null
      ? "—"
      : (value > 0 ? "+" : "") + value.toFixed(precision) + " pp";
  }
  month(value: string, short = false) {
    return new Date(value + "-01T12:00:00").toLocaleDateString("en-US", {
      month: short ? "short" : "long",
      year: short ? undefined : "numeric",
    });
  }
  groupName() {
    return this.groups.find((x) => x.id === this.groupBy)?.name ?? "Group";
  }
  status(row: MlrGroup) {
    return row.status === "above-target"
      ? "Above target"
      : row.status === "within-target"
        ? "Within target"
        : "Unavailable";
  }
  coverage(window: MlrWindow) {
    return window.missingMonths.length
      ? `Incomplete · ${window.availableMonths.length}/${window.months} months`
      : !window.componentsComplete
        ? "Missing components"
        : window.revenue <= 0
          ? "Revenue must be positive"
          : "Complete window";
  }
}
