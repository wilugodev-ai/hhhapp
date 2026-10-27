import { Component, inject } from "@angular/core";
import { AppearanceService } from "./appearance.service";

@Component({
  selector: "hhh-appearance-settings",
  standalone: true,
  template: `<section
    class="panel appearance-panel"
    aria-labelledby="appearance-title"
  >
    <div class="appearance-heading">
      <div>
        <span class="eyebrow">MAKE IT YOURS</span>
        <h2 id="appearance-title">Appearance</h2>
        <p>
          Personalize your workspace. Changes apply immediately and are saved
          for this browser.
        </p>
      </div>
      <button
        type="button"
        class="btn btn-outline"
        (click)="appearance.reset()"
      >
        Reset to defaults
      </button>
    </div>
    <fieldset class="appearance-group">
      <legend>Color mode</legend>
      <div class="appearance-modes">
        <label class="appearance-mode" [class.is-selected]="!appearance.dark()"
          ><input
            type="radio"
            name="color-mode"
            value="light"
            [checked]="!appearance.dark()"
            (change)="appearance.setTheme(false)"
          /><span class="mode-dot mode-light"></span><strong>Light</strong
          ><small>Bright and clear</small></label
        >
        <label class="appearance-mode" [class.is-selected]="appearance.dark()"
          ><input
            type="radio"
            name="color-mode"
            value="dark"
            [checked]="appearance.dark()"
            (change)="appearance.setTheme(true)"
          /><span class="mode-dot mode-dark"></span><strong>Dark</strong
          ><small>A softer evening view</small></label
        >
      </div>
    </fieldset>
    <fieldset class="appearance-group">
      <legend>Workspace background</legend>
      <div class="appearance-backgrounds">
        @for (background of appearance.backgrounds; track background.id) {
          <label
            class="appearance-choice"
            [class.is-selected]="appearance.background() === background.id"
          >
            <input
              type="radio"
              name="workspace-background"
              [value]="background.id"
              [checked]="appearance.background() === background.id"
              (change)="appearance.setBackground(background.id)"
            />
            <span
              class="appearance-preview"
              [style.background]="appearance.preview(background.id)"
              aria-hidden="true"
              ><i class="appearance-preview-nav"></i
              ><span class="appearance-preview-content"
                ><i></i><span><i></i><i></i></span><i></i></span
            ></span>
            <span class="appearance-choice-label"
              ><strong>{{ background.name }}</strong
              ><span class="appearance-check" aria-hidden="true">{{
                appearance.background() === background.id ? "✓" : ""
              }}</span></span
            ><small>{{ background.description }}</small>
          </label>
        }
      </div>
    </fieldset>
    @if (appearance.background() === "custom") {
      <div class="appearance-color">
        <label for="background-tint">Background tint</label
        ><input
          id="background-tint"
          type="color"
          [value]="appearance.tint()"
          (input)="tintChanged($event)"
        /><code>{{ appearance.tint() }}</code
        ><span>A subtle tint keeps workspace text easy to read.</span>
      </div>
    }
    <p class="appearance-saved" role="status">
      {{
        appearance.storageAvailable()
          ? "Your appearance preference is saved on this browser."
          : "Appearance applies for this visit. Your browser is not allowing saved preferences."
      }}
    </p>
  </section>`,
})
export class AppearanceSettingsComponent {
  appearance = inject(AppearanceService);
  tintChanged(event: Event) {
    this.appearance.setTint((event.target as HTMLInputElement).value);
  }
}
