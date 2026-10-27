import { Component, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { Router, RouterLink } from "@angular/router";
import { ApiService, errorMessage } from "./api.service";
import { IconComponent } from "./icon.component";
import { PasswordToggleComponent } from "./password-toggle.component";
@Component({
  selector: "hhh-login",
  standalone: true,
  imports: [FormsModule, RouterLink, IconComponent, PasswordToggleComponent],
  template: ` <main class="login-layout">
    <section class="login-story">
      <a routerLink="/" class="brand"
        ><span class="brand-symbol">H<span>↗</span></span
        ><strong>HHH<span>Healthcare Horizon Hub</span></strong></a
      >
      <div>
        <span class="eyebrow">A CLEARER FINANCIAL HORIZON</span>
        <h1>
          Better visibility.<br />Smarter decisions.<br /><em
            >Healthier futures.</em
          >
        </h1>
        <p>Your financial story, brought into focus.</p>
        <div class="login-grid-art" aria-hidden="true">
          <span></span><span></span><span></span><span></span><span></span
          ><span></span>
        </div>
      </div>
      <small>Transforming Healthcare Data Into Financial Foresight.</small>
    </section>
    <section class="login-form-section">
      <div class="login-form">
        <span class="eyebrow">WELCOME TO HHH</span>
        <h2>Sign in to your workspace</h2>
        <p>Your organization’s financial intelligence starts here.</p>
        @if (error()) {
          <div class="error-banner" role="alert">{{ error() }}</div>
        }
        <form (ngSubmit)="login()">
          <label
            >Work email<input
              type="email"
              name="email"
              [(ngModel)]="email"
              autocomplete="username"
              required
              placeholder="you@organization.com"
              maxlength="254"
          /></label>
          <div class="secret-form-field">
            <label for="login-password">Password</label>
            <div class="secret-input">
              <input
                #loginPassword
                id="login-password"
                type="password"
                name="password"
                [(ngModel)]="password"
                autocomplete="current-password"
                required
                placeholder="Enter your password"
                maxlength="256"
              />
              <hhh-password-toggle [field]="loginPassword" />
            </div>
          </div>
          <button class="btn btn-primary" type="submit" [disabled]="busy()">
            {{ busy() ? "Signing in…" : "Sign in" }} <hhh-icon name="arrow" />
          </button>
        </form>
        @if (api.setupAvailable()) {
          <div class="setup-entry">
            <span>New installation?</span
            ><a routerLink="/setup">Set up your administrator account</a>
          </div>
        }
        @if (api.demoEnabled()) {
          <div class="login-divider"><span>Explore with sample data</span></div>
          <button
            class="btn btn-outline demo-btn"
            (click)="login(true)"
            [disabled]="busy()"
          >
            <hhh-icon name="spark" /> Open synthetic demo</button
          ><small class="demo-note"
            >Development workspace · Synthetic data only<br />No real member or
            financial information.</small
          >
        }
        <a routerLink="/" class="back-link">← Back to Healthcare Horizon Hub</a>
      </div>
    </section>
  </main>`,
})
export class LoginComponent {
  constructor() {
    document.documentElement.classList.remove("dark");
  }
  api = inject(ApiService);
  router = inject(Router);
  email = "";
  password = "";
  error = signal("");
  busy = signal(false);
  async login(demo = false) {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set("");
    try {
      await this.api.login(this.email, this.password, demo);
      const administrator = this.api
        .user()
        ?.roles.some((role) =>
          ["System Administrator", "Organization Administrator"].includes(role),
        );
      await this.router.navigateByUrl(
        administrator ? "/app/administration" : "/app/overview",
      );
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
