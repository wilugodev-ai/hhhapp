import {
  Component,
  computed,
  ElementRef,
  inject,
  OnInit,
  signal,
  viewChild,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import type { EChartsCoreOption } from "echarts/core";
import { ApiService, errorMessage } from "./api.service";
import { ChartComponent } from "./chart.component";
import { IconComponent } from "./icon.component";
import { ProfitLossComponent } from "./profit-loss.component";
import { MlrComponent } from "./mlr.component";
import { AdminComponent } from "./admin.component";
import { AppearanceService } from "./appearance.service";
import { AppearanceSettingsComponent } from "./appearance-settings.component";
import { MainNavigationComponent } from "./main-navigation.component";
import { NavigationService, workspaceNavigation } from "./navigation.service";
import { Batch, Dashboard, Filters, Group, Metadata, Metrics } from "./models";

@Component({
  selector: "hhh-workspace",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    IconComponent,
    ChartComponent,
    ProfitLossComponent,
    MlrComponent,
    AdminComponent,
    AppearanceSettingsComponent,
    MainNavigationComponent,
  ],
  templateUrl: "./workspace.component.html",
  host: {
    "(document:click)": "onAccountMenuOutsideClick($event)",
    "(document:keydown.escape)": "onAccountMenuEscape($event)",
  },
})
export class WorkspaceComponent implements OnInit {
  api = inject(ApiService);
  appearance = inject(AppearanceService);
  navigation = inject(NavigationService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  data = signal<Dashboard | null>(null);
  metadata = signal<Metadata | null>(null);
  loading = signal(true);
  error = signal("");
  notice = signal("");
  exporting = signal(false);
  signingOut = signal(false);
  accountMenuOpen = signal(false);
  isAdministrator = computed(
    () =>
      this.api
        .user()
        ?.roles.some(
          (role) =>
            role === "System Administrator" ||
            role === "Organization Administrator",
        ) ?? false,
  );
  private accountMenuRoot =
    viewChild<ElementRef<HTMLElement>>("accountMenuRoot");
  private accountMenuTrigger =
    viewChild<ElementRef<HTMLButtonElement>>("accountMenuTrigger");
  section = signal("overview");
  collapsed = this.navigation.collapsed;
  mobileMenu = this.navigation.mobileOpen;
  advanced = signal(false);
  dark = this.appearance.dark.asReadonly();
  search = signal("");
  batches = signal<Batch[]>([]);
  filters: Filters = {
    month: "",
    year: "",
    payer: "",
    center: "",
    reportMonth: "",
    provider: "",
    contract: "",
    plan: "",
    lineOfBusiness: "",
    region: "",
  };
  profitLossFilters = signal<Filters>({});
  mlrFilters = signal<Filters>({});
  target = 85;
  private requestId = 0;
  nav = workspaceNavigation;
  activeNav = computed(
    () => this.nav.find((x) => x.id === this.section()) || this.nav[0],
  );
  isPlanned = computed(() => !!this.activeNav().planned);
  canManage = computed(
    () =>
      this.api
        .user()
        ?.roles.some((r) =>
          [
            "Chief Financial Officer",
            "Organization Administrator",
            "System Administrator",
          ].includes(r),
        ) ?? false,
  );
  groupRows = computed(() => {
    const rows =
      this.section() === "centers" ? this.data()?.centers : this.data()?.payers;
    return (rows || [])
      .filter((x) => x.name.toLowerCase().includes(this.search().toLowerCase()))
      .sort((a, b) => b.metrics.revenue - a.metrics.revenue);
  });
  alerts = computed(() =>
    (this.data()?.payers || []).filter(
      (x) =>
        x.metrics.mlr !== null &&
        x.metrics.mlr > (x.mlrTarget ?? this.data()?.mlrTarget ?? 85),
    ),
  );
  hasUnavailableMlr = computed(() =>
    (this.data()?.payers || []).some((row) => row.metrics.mlr === null),
  );
  revenueChart = computed(() => this.lineChart("revenue"));
  membershipChart = computed(() => this.lineChart("membership"));
  pmpmChart = computed(() => this.lineChart("pmpm"));
  expenseChart = computed<EChartsCoreOption>(() => ({
    tooltip: { trigger: "item", formatter: "{b}: ${c} ({d}%)" },
    color: [
      "#00b8a9",
      "#0b3654",
      "#3b82f6",
      "#86d8d1",
      "#94a3b8",
      "#c2d2e0",
      "#bdcad6",
    ],
    series: [
      {
        type: "pie",
        radius: ["61%", "84%"],
        center: ["50%", "50%"],
        label: { show: false },
        emphasis: { scaleSize: 5 },
        data: (this.data()?.pnl || [])
          .filter((x) => x.type === "medical" && x.amount > 0)
          .map((x) => ({ name: x.category, value: Math.round(x.amount) })),
      },
    ],
  }));
  cards = computed(() => {
    const d = this.data();
    if (!d) return [];
    const entries: {
      key: keyof Metrics;
      label: string;
      icon: string;
      kind: string;
      lower?: boolean;
    }[] = [
      {
        key: "revenue",
        label: "Total revenue",
        icon: "wallet",
        kind: "currency",
      },
      {
        key: "medicalExpenses",
        label: "Medical expenses",
        icon: "heart",
        kind: "currency",
        lower: true,
      },
      {
        key: "netProfit",
        label: "Net profit",
        icon: "trend",
        kind: "currency",
      },
      {
        key: "mlr",
        label: "Medical loss ratio",
        icon: "chart",
        kind: "percent",
        lower: true,
      },
    ];
    return entries.map((e) => {
      const current = d.comparisons.currentMonth[e.key],
        previous = d.comparisons.previous[e.key],
        priorYear = d.comparisons.priorYear[e.key];
      const delta =
        current === null ||
        previous === null ||
        previous === 0 ||
        !d.comparisons.previousAvailable
          ? null
          : ((current - previous) / Math.abs(previous)) * 100;
      const yoy =
        current === null ||
        priorYear === null ||
        priorYear === 0 ||
        !d.comparisons.priorYearAvailable
          ? null
          : ((current - priorYear) / Math.abs(priorYear)) * 100;
      return {
        ...e,
        value: d.totals[e.key],
        previous: d.comparisons.previousAvailable ? previous : null,
        delta,
        yoy,
        positive: delta === null || (e.lower ? delta <= 0 : delta >= 0),
        spark: this.spark(e.key),
      };
    });
  });

  constructor() {
    this.appearance.applyTheme();
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const section = params.get("section") || "overview";
      if (!this.nav.some((x) => x.id === section)) {
        void this.router.navigateByUrl("/app/overview");
        return;
      }
      this.section.set(section);
      this.closeAccountMenu();
      this.search.set("");
      this.mobileMenu.set(false);
      this.notice.set("");
      if (this.metadata()) void this.load();
    });
  }
  async ngOnInit() {
    try {
      const metadata = await this.api.metadata();
      this.metadata.set(metadata);
      this.filters.month = metadata.months[0] || "";
      this.target = this.api.user()?.mlrTarget ?? 85;
      await this.load();
      this.batches.set(await this.api.batches());
    } catch (e) {
      this.error.set(errorMessage(e));
      this.loading.set(false);
    }
  }
  async load() {
    const request = ++this.requestId;
    if (this.section() === "administration") {
      this.loading.set(false);
      this.error.set("");
      return;
    }
    if (this.section() === "mlr") {
      this.mlrFilters.set({ ...this.filters });
      this.loading.set(false);
      this.error.set("");
      return;
    }
    if (this.section() === "profit-loss") {
      this.profitLossFilters.set({ ...this.filters });
      this.loading.set(false);
      this.error.set("");
      return;
    }
    this.loading.set(true);
    this.error.set("");
    try {
      const data = await this.api.dashboard({ ...this.filters });
      if (request === this.requestId) this.data.set(data);
    } catch (e) {
      if (request === this.requestId) {
        this.error.set(errorMessage(e));
        this.data.set(null);
      }
    } finally {
      if (request === this.requestId) this.loading.set(false);
    }
  }
  onMonth() {
    this.filters.year = "";
    void this.load();
  }
  onYear() {
    this.filters.month = "";
    void this.load();
  }
  options(kind: string) {
    return this.metadata()?.dimensions.filter((x) => x.kind === kind) || [];
  }
  async reset() {
    for (const key of Object.keys(this.filters)) this.filters[key] = "";
    this.filters.month = this.metadata()?.months[0] || "";
    await this.load();
  }
  drillPayer(group: Group) {
    this.filters.payer = group.id;
    void this.load();
    void this.router.navigateByUrl("/app/profit-loss");
  }
  async exportCsv() {
    if (this.exporting()) return;
    this.exporting.set(true);
    try {
      await this.api.export(this.filters);
      this.notice.set("Financial report downloaded.");
    } catch (e) {
      this.notice.set(errorMessage(e));
    } finally {
      this.exporting.set(false);
    }
  }
  async saveSettings() {
    try {
      const result = await this.api.saveSettings(this.target);
      this.target = result.mlrTarget;
      this.notice.set("Organization MLR target saved.");
      await this.load();
    } catch (e) {
      this.notice.set(errorMessage(e));
    }
  }
  toggleAccountMenu() {
    this.accountMenuOpen.update((open) => !open);
  }
  closeAccountMenu(restoreFocus = false) {
    if (!this.accountMenuOpen()) return;
    this.accountMenuOpen.set(false);
    if (restoreFocus) this.accountMenuTrigger()?.nativeElement.focus();
  }
  onAccountMenuOutsideClick(event: MouseEvent) {
    if (
      this.accountMenuOpen() &&
      !this.accountMenuRoot()?.nativeElement.contains(event.target as Node)
    )
      this.closeAccountMenu();
  }
  onAccountMenuFocusOut(event: FocusEvent) {
    const next = event.relatedTarget;
    if (
      !(next instanceof Node) ||
      !this.accountMenuRoot()?.nativeElement.contains(next)
    )
      this.closeAccountMenu();
  }
  onAccountMenuEscape(event: Event) {
    if (!this.accountMenuOpen()) return;
    event.preventDefault();
    this.closeAccountMenu(true);
  }
  async logout() {
    if (this.signingOut()) return;
    this.closeAccountMenu();
    this.signingOut.set(true);
    this.notice.set("");
    try {
      await this.api.logout();
      await this.router.navigateByUrl("/login");
    } catch (e) {
      this.notice.set(errorMessage(e));
    } finally {
      this.signingOut.set(false);
    }
  }
  toggleTheme() {
    this.appearance.setTheme(!this.dark());
  }
  money(value: number | null, compact = false) {
    return value === null
      ? "—"
      : new Intl.NumberFormat("en-US", {
          style: "currency",
          currency: "USD",
          notation: compact ? "compact" : "standard",
          maximumFractionDigits: compact ? 2 : 0,
        }).format(value);
  }
  number(value: number | null) {
    return value === null
      ? "—"
      : new Intl.NumberFormat("en-US", { maximumFractionDigits: 0 }).format(
          value,
        );
  }
  percent(value: number | null) {
    return value === null ? "—" : value.toFixed(1) + "%";
  }
  monthLabel(value: string | null, short = false) {
    return value
      ? new Date(value + "-01T12:00:00").toLocaleDateString("en-US", {
          month: short ? "short" : "long",
          year: "numeric",
        })
      : "No period";
  }
  signed(value: number | null) {
    return value === null
      ? "—"
      : (value >= 0 ? "+" : "") + value.toFixed(1) + "%";
  }
  formatMetric(value: number | null, kind: string) {
    return kind === "percent" ? this.percent(value) : this.money(value, true);
  }
  spark(key: keyof Metrics) {
    const points = (this.data()?.trends || []).map((t) => t.metrics[key]);
    if (points.some((value) => value === null)) return "";
    const values = points as number[];
    const min = Math.min(...values),
      max = Math.max(...values);
    return values
      .map(
        (v, i) =>
          `${(i * 90) / Math.max(1, values.length - 1)},${28 - ((v - min) / Math.max(1, max - min)) * 23}`,
      )
      .join(" ");
  }
  revenueRows() {
    return this.data()?.pnl.filter((x) => x.type === "revenue") || [];
  }
  expenseRows() {
    return this.data()?.pnl.filter((x) => x.type !== "revenue") || [];
  }
  expenseShare(amount: number) {
    const gross = this.grossMedicalCosts();
    return gross ? (amount / gross) * 100 : 0;
  }
  grossMedicalCosts() {
    return (this.data()?.pnl || [])
      .filter((row) => row.type === "medical" && row.amount > 0)
      .reduce((total, row) => total + row.amount, 0);
  }
  periodLabel() {
    return this.data()?.periodMode === "year"
      ? `${this.filters.year} year to date`
      : this.monthLabel(this.data()?.period ?? null);
  }
  initials() {
    return (this.api.user()?.displayName || "HHH")
      .split(" ")
      .map((x) => x[0])
      .slice(0, 2)
      .join("");
  }
  private lineChart(kind: string): EChartsCoreOption {
    const d = this.data(),
      dark = this.dark();
    const text = dark ? "#8fa6bd" : "#7d8ba0",
      grid = dark ? "#233951" : "#eef1f6";
    const configs: Record<
      string,
      { keys: (keyof Metrics)[]; names: string[]; colors: string[] }
    > = {
      revenue: {
        keys: ["revenue", "medicalExpenses"],
        names: ["Revenue", "Medical expenses"],
        colors: ["#00b8a9", "#7196c5"],
      },
      mlr: { keys: ["mlr"], names: ["Operational MLR"], colors: ["#00b8a9"] },
      membership: {
        keys: ["memberMonths"],
        names: ["Members"],
        colors: ["#3b82f6"],
      },
      pmpm: {
        keys: ["revenuePmpm", "costPmpm"],
        names: ["Revenue PMPM", "Medical cost PMPM"],
        colors: ["#00b8a9", "#7196c5"],
      },
    };
    const cfg = configs[kind];
    return {
      animationDuration: 400,
      color: cfg.colors,
      tooltip: {
        trigger: "axis",
        backgroundColor: dark ? "#142c45" : "#fff",
        textStyle: { color: dark ? "#edf4fa" : "#0b1f3a" },
        valueFormatter: (v: unknown) =>
          typeof v === "number"
            ? kind === "mlr"
              ? this.percent(v)
              : kind === "membership"
                ? this.number(v)
                : this.money(v)
            : String(v),
      },
      grid: {
        left: kind === "membership" ? 57 : 54,
        right: 18,
        top: 24,
        bottom: 29,
      },
      xAxis: {
        type: "category",
        boundaryGap: false,
        data: (d?.trends || []).map((x) =>
          new Date(x.month + "-01T12:00:00").toLocaleDateString("en-US", {
            month: "short",
          }),
        ),
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: text, fontSize: 11 },
      },
      yAxis: {
        type: "value",
        splitNumber: 4,
        axisLabel: {
          color: text,
          fontSize: 11,
          formatter: (v: number) =>
            kind === "mlr"
              ? `${v}%`
              : kind === "membership"
                ? `${(v / 1000).toFixed(0)}k`
                : kind === "pmpm"
                  ? `$${v}`
                  : `$${(v / 1e6).toFixed(0)}M`,
        },
        splitLine: { lineStyle: { color: grid, type: "dashed" } },
        min: kind === "mlr" ? 70 : undefined,
        max: kind === "mlr" ? 100 : undefined,
      },
      series: cfg.keys.map((key, i) => ({
        name: cfg.names[i],
        type: "line",
        smooth: 0.35,
        showSymbol: false,
        lineStyle: { width: 2.5, type: i === 1 ? "dashed" : "solid" },
        areaStyle:
          i === 0 ? { color: cfg.colors[i], opacity: 0.055 } : undefined,
        data: (d?.trends || []).map((x) => x.metrics[key]),
        markLine:
          kind === "mlr"
            ? {
                symbol: "none",
                label: { formatter: "Target", color: text },
                lineStyle: { color: "#e9ac61" },
                data: [{ yAxis: d?.mlrTarget || 85 }],
              }
            : undefined,
      })),
    };
  }
}
