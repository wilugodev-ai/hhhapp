import { Component, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { ApiService, errorMessage } from "./api.service";
import { PasswordToggleComponent } from "./password-toggle.component";

@Component({
  selector: "hhh-activate",
  standalone: true,
  imports: [FormsModule, RouterLink, PasswordToggleComponent],
  template: `<main class="login-layout">
    <section class="login-story">
      <a routerLink="/" class="brand"
        ><strong>HHH<span>Healthcare Horizon Hub</span></strong></a
      >
      <div>
        <span class="eyebrow">YOUR WORKSPACE AWAITS</span>
        <h1>A clearer financial horizon starts here.</h1>
        <p>Set your password to access your organization's workspace.</p>
      </div>
    </section>
    <section class="login-form-section">
      <div class="login-form">
        @if (complete()) {
          <span class="eyebrow">ACCOUNT READY</span>
          <h2>Your password is set</h2>
          <p>Sign in with your work email and your new password.</p>
          <a routerLink="/login" class="btn btn-primary">Continue to sign in</a>
        } @else {
          <span class="eyebrow">SECURE ACCOUNT SETUP</span>
          <h2>Choose your password</h2>
          <p>
            Use at least 12 characters, including uppercase and lowercase
            letters, a number, and a symbol.
          </p>
          @if (error()) {
            <div class="error-banner" role="alert">{{ error() }}</div>
          }
          @if (!userId || !token) {
            <p>
              This page needs a setup link. Open the full link provided by your
              administrator.
            </p>
          } @else {
            <form (ngSubmit)="submit()">
              <div class="secret-form-field">
                <label for="activation-password">New password</label>
                <div class="secret-input">
                  <input
                    #activationPassword
                    id="activation-password"
                    name="password"
                    type="password"
                    autocomplete="new-password"
                    [(ngModel)]="password"
                    required
                    minlength="12"
                    maxlength="256"
                  /><hhh-password-toggle
                    [field]="activationPassword"
                    label="new password"
                  />
                </div>
              </div>
              <div class="secret-form-field">
                <label for="activation-confirmation">Confirm password</label>
                <div class="secret-input">
                  <input
                    #activationConfirmation
                    id="activation-confirmation"
                    name="confirm"
                    type="password"
                    autocomplete="new-password"
                    [(ngModel)]="confirmation"
                    required
                    minlength="12"
                    maxlength="256"
                  /><hhh-password-toggle
                    [field]="activationConfirmation"
                    label="password confirmation"
                  />
                </div>
              </div>
              <button class="btn btn-primary" [disabled]="busy()">
                {{ busy() ? "Setting up account..." : "Set password" }}
              </button>
            </form>
          }
          <a routerLink="/login" class="back-link">Back to sign in</a>
        }
      </div>
    </section>
  </main>`,
})
export class ActivateComponent {
  private api = inject(ApiService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private fragment = new URLSearchParams(this.route.snapshot.fragment ?? "");
  userId = this.fragment.get("user") ?? "";
  token = this.fragment.get("token") ?? "";
  password = "";
  confirmation = "";
  busy = signal(false);
  error = signal("");
  complete = signal(false);
  constructor() {
    // Keep the bearer secret out of server logs, referrers, and subsequent browser history.
    if (this.route.snapshot.fragment)
      void this.router.navigate([], {
        relativeTo: this.route,
        fragment: undefined,
        replaceUrl: true,
      });
  }
  async submit() {
    if (this.busy()) return;
    this.error.set("");
    if (this.password !== this.confirmation) {
      this.error.set("Passwords must match.");
      return;
    }
    this.busy.set(true);
    try {
      await this.api.activateAccount({
        userId: this.userId,
        token: this.token,
        password: this.password,
      });
      this.password = "";
      this.confirmation = "";
      this.token = "";
      this.complete.set(true);
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
