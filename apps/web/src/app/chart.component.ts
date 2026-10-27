import {
  afterNextRender,
  Component,
  effect,
  ElementRef,
  inject,
  input,
  OnDestroy,
  viewChild,
} from "@angular/core";
import * as echarts from "echarts/core";
import { LineChart, BarChart, PieChart } from "echarts/charts";
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
} from "echarts/components";
import { CanvasRenderer } from "echarts/renderers";
import type { EChartsCoreOption } from "echarts/core";
echarts.use([
  LineChart,
  BarChart,
  PieChart,
  GridComponent,
  TooltipComponent,
  LegendComponent,
  CanvasRenderer,
]);

@Component({
  selector: "hhh-chart",
  standalone: true,
  template: `<div
    #canvas
    class="chart-canvas"
    role="img"
    [attr.aria-label]="label()"
  ></div>`,
})
export class ChartComponent implements OnDestroy {
  option = input.required<EChartsCoreOption>();
  label = input("Financial chart");
  canvas = viewChild.required<ElementRef<HTMLDivElement>>("canvas");
  private chart?: echarts.ECharts;
  private observer?: ResizeObserver;
  constructor() {
    afterNextRender(() => {
      this.chart = echarts.init(this.canvas().nativeElement);
      this.chart.setOption(this.option());
      this.observer = new ResizeObserver(() => this.chart?.resize());
      this.observer.observe(this.canvas().nativeElement);
    });
    effect(() => {
      const options = this.option();
      this.chart?.setOption(options, true);
    });
  }
  ngOnDestroy() {
    this.observer?.disconnect();
    this.chart?.dispose();
  }
}
