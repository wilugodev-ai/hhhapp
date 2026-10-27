import { Component, input, signal } from "@angular/core";

@Component({
  selector: "hhh-password-toggle",
  standalone: true,
  template: `<button
    type="button"
    [attr.aria-label]="(visible() ? 'Hide ' : 'Show ') + label()"
    [attr.aria-controls]="field().id"
    [attr.aria-pressed]="visible()"
    (click)="toggle()"
  >
    {{ visible() ? "Hide" : "Show" }}
  </button>`,
  styles: `
    :host {
      position: absolute;
      right: 6px;
      top: 50%;
      transform: translateY(-50%);
    }
    button {
      min-width: 44px;
      min-height: 36px;
      padding: 6px 8px;
      border-radius: 4px;
      background: transparent;
      color: #00897e;
      font-size: 12px;
      font-weight: 600;
    }
    button:hover {
      background: #00a9990d;
    }
  `,
})
export class PasswordToggleComponent {
  field = input.required<HTMLInputElement>();
  label = input("password");
  visible = signal(false);
  toggle() {
    const reveal = this.field().type === "password";
    this.field().type = reveal ? "text" : "password";
    this.visible.set(reveal);
  }
}
