import { DOCUMENT } from "@angular/common";
import { computed, inject, Injectable, signal } from "@angular/core";

export type WorkspaceBackground =
  "classic" | "horizon" | "ocean" | "warm" | "custom";

@Injectable({ providedIn: "root" })
export class AppearanceService {
  private document = inject(DOCUMENT);
  readonly storageAvailable = signal(true);
  readonly dark = signal(this.read("hhh-theme") === "dark");
  readonly backgrounds: {
    id: WorkspaceBackground;
    name: string;
    description: string;
  }[] = [
    {
      id: "classic",
      name: "Classic",
      description: "A clean, solid background",
    },
    { id: "horizon", name: "Horizon", description: "Soft mint and sky blue" },
    { id: "ocean", name: "Ocean", description: "Calm blue and lavender" },
    { id: "warm", name: "Warm", description: "A touch of peach and gold" },
    {
      id: "custom",
      name: "Custom tint",
      description: "Choose your own accent color",
    },
  ];
  readonly background = signal<WorkspaceBackground>(this.savedBackground());
  readonly tint = signal(this.savedTint());
  readonly backgroundStyle = computed(() => this.preview(this.background()));

  applyTheme() {
    this.document.documentElement.classList.toggle("dark", this.dark());
  }
  setTheme(dark: boolean) {
    this.dark.set(dark);
    this.applyTheme();
    this.persist();
  }
  setBackground(background: WorkspaceBackground) {
    if (!this.backgrounds.some((option) => option.id === background)) return;
    this.background.set(background);
    this.persist();
  }
  setTint(color: string) {
    if (!/^#[0-9a-f]{6}$/i.test(color)) return;
    this.tint.set(color.toLowerCase());
    this.background.set("custom");
    this.persist();
  }
  reset() {
    this.dark.set(false);
    this.background.set("classic");
    this.tint.set("#00a999");
    this.applyTheme();
    this.persist();
  }
  preview(background: WorkspaceBackground): string {
    const base = this.dark() ? "#0b1c2f" : "#f5f7fa";
    const alpha = this.dark() ? "24" : "20";
    if (background === "custom")
      return `linear-gradient(${this.tint()}${alpha}, ${this.tint()}${alpha}), ${base}`;
    const colors: Partial<Record<WorkspaceBackground, string[]>> = {
      horizon: ["#00a999", "#579de0"],
      ocean: ["#458af0", "#a179d6"],
      warm: ["#e9a044", "#dd8395"],
    };
    const accents = colors[background];
    return accents
      ? `radial-gradient(ellipse at 5% 0%, ${accents[0]}${alpha}, transparent 60%), radial-gradient(ellipse at 95% 65%, ${accents[1]}${alpha}, transparent 65%), ${base}`
      : base;
  }
  private read(key: string) {
    try {
      return localStorage.getItem(key);
    } catch {
      this.storageAvailable.set(false);
      return null;
    }
  }
  private savedBackground(): WorkspaceBackground {
    const saved = this.read("hhh-background");
    return (
      this.backgrounds.find((option) => option.id === saved)?.id ?? "classic"
    );
  }
  private savedTint() {
    const saved = this.read("hhh-background-tint");
    return saved && /^#[0-9a-f]{6}$/i.test(saved)
      ? saved.toLowerCase()
      : "#00a999";
  }
  private persist() {
    try {
      localStorage.setItem("hhh-theme", this.dark() ? "dark" : "light");
      localStorage.setItem("hhh-background", this.background());
      localStorage.setItem("hhh-background-tint", this.tint());
      this.storageAvailable.set(true);
    } catch {
      this.storageAvailable.set(false);
    }
  }
}
