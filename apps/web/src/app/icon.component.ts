import { Component, input } from "@angular/core";

const paths: Record<string, string> = {
  home: "m3 10 9-7 9 7M5 9v12h14V9M9 21v-7h6v7",
  grid: "M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z",
  trend: "M3 17l6-6 4 4 8-10M15 5h6v6",
  chart: "M4 20V10m8 10V4m8 16v-7M2 21h20",
  wallet: "M3 6h17v15H3zM3 6V3h14v3M16 12h5v4h-5z",
  heart:
    "M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8L12 21l8.8-8.6a5.5 5.5 0 0 0 0-7.8z",
  building: "M4 21V3h12v18M16 9h4v12M8 7h4M8 11h4M8 15h4M2 21h20",
  users:
    "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M22 21v-2a4 4 0 0 0-3-3.9M15 3.1a4 4 0 0 1 0 7.8M13 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0",
  spark:
    "M12 3l2.4 6.6L21 12l-6.6 2.4L12 21l-2.4-6.6L3 12l6.6-2.4zM20 2v4m-2-2h4",
  sliders: "M4 21V14m0-5V3m8 18v-9m0-5V3m8 18v-5m0-5V3M1 14h6m2-7h6m2 9h6",
  file: "M14 2H4v20h16V8zM14 2v6h6M8 12h8m-8 4h8",
  database:
    "M21 5c0 2.2-4 4-9 4S3 7.2 3 5s4-4 9-4 9 1.8 9 4zM3 5v14c0 2.2 4 4 9 4s9-1.8 9-4V5M3 12c0 2.2 4 4 9 4s9-1.8 9-4",
  settings:
    "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8M9 2h6l1 4 4 1 2 5-3 3v4l-5 3-3-3H7l-4-4 1-4L2 7l5-1z",
  shield: "M12 2l9 4v6c0 5-9 10-9 10S3 17 3 12V6zM8 12l3 3 5-6",
  search: "M21 21l-6-6M17 10a7 7 0 1 1-14 0 7 7 0 0 1 14 0",
  bell: "M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4",
  down: "M6 9l6 6 6-6",
  right: "M5 12h14m-6-6l6 6-6 6",
  download: "M12 3v12m-5-5l5 5 5-5M4 17v4h16v-4",
  calendar: "M4 5h16v16H4zM16 3v4M8 3v4M4 11h16",
  filter: "M3 4h18l-7 8v7l-4 2v-9z",
  arrow: "M5 12h14M12 5l7 7-7 7",
  moon: "M21 12.8A9 9 0 0 1 11.2 3 9 9 0 1 0 21 12.8z",
  sun: "M12 3V1m0 22v-2M3 12H1m22 0h-2M5 5L3 3m18 18-2-2M5 19l-2 2M21 3l-2 2M17 12a5 5 0 1 1-10 0 5 5 0 0 1 10 0",
  logout: "M9 3H3v18h6m6-14l5 5-5 5m-6-5h11",
  menu: "M3 6h18M3 12h18M3 18h18",
  info: "M12 11v6m0-10v1M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0",
  check: "M5 12l4 4L19 6",
  close: "M6 6l12 12M6 18L18 6",
  mail: "M3 4h18v16H3zM3 4l9 9 9-9",
  lock: "M5 10h14v11H5zM8 10V6a4 4 0 0 1 8 0v4",
};
@Component({
  selector: "hhh-icon",
  standalone: true,
  template: `<svg
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.6"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <path [attr.d]="path()" />
  </svg>`,
})
export class IconComponent {
  name = input("grid");
  path() {
    return paths[this.name()] || paths["grid"];
  }
}
