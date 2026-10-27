import {
  afterNextRender,
  Component,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  viewChild,
} from "@angular/core";
import { RouterLink, RouterLinkActive } from "@angular/router";
import { ApiService } from "./api.service";
import { IconComponent } from "./icon.component";
import { NavigationService, workspaceNavigation } from "./navigation.service";

@Component({
  selector: "hhh-main-navigation",
  standalone: true,
  imports: [RouterLink, RouterLinkActive, IconComponent],
  styles: [":host { display: contents; }"],
  host: { "(document:keydown.escape)": "closeFromKeyboard($event)" },
  template: `
    @if (navigation.mobileOpen()) {
      <button
        class="sidebar-scrim"
        aria-label="Close navigation"
        (click)="close(true)"
      ></button>
    }
    <aside
      id="main-sidebar"
      class="sidebar"
      aria-label="Workspace menu"
      (keydown)="trapFocus($event)"
    >
      <button
        #closeButton
        class="icon-button sidebar-close"
        type="button"
        aria-label="Close main menu"
        (click)="close(true)"
      >
        <hhh-icon name="close" />
      </button>
      <a
        class="brand"
        routerLink="/"
        aria-label="Healthcare Horizon Hub home"
        (click)="close()"
      >
        <span class="brand-symbol">H<span>↗</span></span>
        <strong>HHH<span>Healthcare Horizon Hub</span></strong>
      </a>
      <div class="workspace-picker">
        <span class="workspace-avatar">{{
          api.user()?.organization?.slice(0, 1)
        }}</span>
        <div>
          <strong>Organization workspace</strong
          ><small>{{ api.user()?.organization }}</small>
        </div>
      </div>
      <nav aria-label="Main navigation">
        <a
          routerLink="/"
          [attr.aria-current]="activeSection() === 'home' ? 'page' : null"
          [class.active]="activeSection() === 'home'"
          title="Home"
          (click)="close()"
        >
          <hhh-icon name="home" /><span>Home</span>
        </a>
        @for (item of items; track item.id) {
          @if (item.group) {
            <div class="nav-label">{{ item.group }}</div>
          }
          <a
            [routerLink]="'/app/' + item.id"
            routerLinkActive="active"
            [title]="item.label"
            [attr.aria-current]="activeSection() === item.id ? 'page' : null"
            (click)="close()"
          >
            <hhh-icon [name]="item.icon" /><span>{{ item.label }}</span>
            @if (item.planned) {
              <i class="planned-dot" title="Planned module"></i>
            }
          </a>
        }
      </nav>
      <div class="sidebar-bottom">
        @if (sampleStatus()) {
          <div class="demo-card">
            <hhh-icon name="shield" />
            <div>
              <strong>Sample financial data</strong
              ><small>{{
                sampleStatus() === "mixed"
                  ? "Includes sample source batches"
                  : "Starter data for your workspace"
              }}</small>
            </div>
          </div>
        }
        <button
          class="collapse-button"
          type="button"
          (click)="navigation.collapsed.update(toggle)"
          [attr.aria-label]="
            navigation.collapsed() ? 'Expand sidebar' : 'Collapse sidebar'
          "
        >
          <hhh-icon name="menu" /><span>Collapse sidebar</span>
        </button>
      </div>
    </aside>
  `,
})
export class MainNavigationComponent {
  readonly api = inject(ApiService);
  readonly navigation = inject(NavigationService);
  readonly activeSection = input("");
  readonly sampleStatus = input<"sample" | "mixed" | null>(null);
  readonly items = workspaceNavigation;
  readonly toggle = (value: boolean) => !value;
  private element: ElementRef<HTMLElement> = inject(ElementRef);
  private closeButton = viewChild<ElementRef<HTMLButtonElement>>("closeButton");
  constructor() {
    const injector = inject(Injector);
    effect(() => {
      if (this.navigation.mobileOpen())
        afterNextRender(() => this.closeButton()?.nativeElement.focus(), {
          injector,
        });
    });
  }
  close(restoreFocus = false) {
    if (!this.navigation.mobileOpen()) return;
    this.navigation.mobileOpen.set(false);
    if (restoreFocus)
      this.element.nativeElement.parentElement
        ?.querySelector<HTMLElement>('[aria-controls="main-sidebar"]')
        ?.focus();
  }
  closeFromKeyboard(event: Event) {
    if (!this.navigation.mobileOpen()) return;
    event.preventDefault();
    this.close(true);
  }
  trapFocus(event: KeyboardEvent) {
    if (!this.navigation.mobileOpen() || event.key !== "Tab") return;
    if (!this.closeButton()?.nativeElement.getClientRects().length) return;
    const controls = [
      ...this.element.nativeElement.querySelectorAll<HTMLElement>(
        "aside a, aside button",
      ),
    ].filter(
      (element) =>
        element.getClientRects().length > 0 &&
        !element.hasAttribute("disabled"),
    );
    const first = controls[0],
      last = controls.at(-1);
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first?.focus();
    }
  }
}
