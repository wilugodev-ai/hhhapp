import { Component, inject, OnInit, PendingTasks, signal } from "@angular/core";
import { HttpErrorResponse } from "@angular/common/http";
import { FormsModule } from "@angular/forms";
import { Router, RouterLink } from "@angular/router";
import { ApiService, errorMessage } from "./api.service";
import { PasswordToggleComponent } from "./password-toggle.component";

@Component({
  selector: "hhh-setup",
  standalone: true,
  imports: [FormsModule, RouterLink, PasswordToggleComponent],
  template: `<main class="login-layout">
    <section class="login-story">
      <a routerLink="/" class="brand"
        ><strong>HHH<span>Healthcare Horizon Hub</span></strong></a
      >
      <div>
        <span class="eyebrow">YOUR FIRST HORIZON</span>
        <h1>One workspace.<br />A clearer future.</h1>
        <p>
          Create your administrator account and bring your organization into
          HHH.
        </p>
      </div>
      <small>Transforming Healthcare Data Into Financial Foresight.</small>
    </section>
    <section class="login-form-section">
      <div class="login-form">
        @if (complete()) {
          <span class="eyebrow">READY TO BEGIN</span>
          <h2>Your administrator account is ready</h2>
          <p>
            Sign in with your email and the password you just created to manage
            your organization and invite your team.
          </p>
          <a routerLink="/login" class="btn btn-primary">Continue to sign in</a>
        } @else {
          <span class="eyebrow">{{
            api.setupState() === "configured"
              ? "ADMINISTRATOR ACCESS"
              : "FIRST ADMINISTRATOR"
          }}</span>
          <h2>
            {{
              api.setupState() === "configured"
                ? api.demoEnabled()
                  ? "This demo is already set up"
                  : "Your workspace is already set up"
                : "Set up your organization"
            }}
          </h2>
          @if (error()) {
            <div class="error-banner" role="alert">{{ error() }}</div>
          }
          @if (loading()) {
            <p role="status">Checking administrator setup...</p>
          } @else if (!api.setupAvailable()) {
            @if (api.setupState() === "configured") {
              @if (api.demoEnabled()) {
                <p>
                  This demo already has a system administrator. Open
                  Administration to create your own account using
                  <strong>Invite user</strong>, then choose the administrator
                  role you need.
                </p>
                <button
                  class="btn btn-primary"
                  type="button"
                  (click)="openDemoAdministration()"
                  [disabled]="busy()"
                >
                  {{
                    busy()
                      ? "Opening administration..."
                      : "Open demo administration"
                  }}
                </button>
                <p class="setup-demo-note">
                  Synthetic development workspace only.
                </p>
              } @else {
                <p>
                  An administrator account already exists. To add another
                  administrator, open Administration and choose
                  <strong>Invite user</strong>.
                </p>
                @if (isAdministrator()) {
                  <a routerLink="/app/administration" class="btn btn-primary"
                    >Open administration</a
                  >
                } @else {
                  <a routerLink="/login" class="btn btn-primary"
                    >Sign in as an administrator</a
                  >
                }
              }
            } @else {
              <p>
                {{
                  api.setupState() === "disabled"
                    ? "Administrator setup has not been enabled. Contact the person managing this installation to enable setup and provide your installation key."
                    : "Administrator setup is unavailable. Sign in with your existing account, or contact the person managing this installation."
                }}
              </p>
              <a routerLink="/login" class="btn btn-primary">Go to sign in</a>
              <button
                class="btn btn-outline setup-check"
                type="button"
                (click)="load()"
              >
                Check again
              </button>
            }
          } @else {
            <p>
              Use the setup key provided for this installation. This account
              will manage HHH and your first organization.
            </p>
            <form #setupForm="ngForm" (ngSubmit)="submit()">
              <label
                >Full name<input
                  name="displayName"
                  [(ngModel)]="displayName"
                  autocomplete="name"
                  required
                  minlength="2"
                  maxlength="180"
              /></label>
              <label
                >Work email<input
                  name="email"
                  type="email"
                  [(ngModel)]="email"
                  autocomplete="username"
                  required
                  maxlength="254"
              /></label>
              <label
                >Organization name<input
                  name="organizationName"
                  [(ngModel)]="organizationName"
                  autocomplete="organization"
                  required
                  minlength="2"
                  maxlength="180"
              /></label>
              <div class="secret-form-field">
                <label for="installation-key">Installation setup key</label>
                <div class="secret-input">
                  <input
                    #installationKey
                    id="installation-key"
                    name="setupKey"
                    type="password"
                    [(ngModel)]="setupKey"
                    autocomplete="off"
                    spellcheck="false"
                    autocapitalize="none"
                    required
                    minlength="32"
                    maxlength="256"
                  /><hhh-password-toggle
                    [field]="installationKey"
                    label="installation key"
                  />
                </div>
              </div>
              @if (api.sampleDataEnabled()) {
                <label class="setup-starter"
                  ><input
                    type="checkbox"
                    name="includeSampleData"
                    [(ngModel)]="includeSampleData"
                  /><span
                    >Start with sample financial data<small
                      >21 months of financials, budgets, and MLR settings in
                      your own workspace. Your administrator account uses the
                      details you enter here.</small
                    ></span
                  ></label
                >
              }
              <div class="secret-form-field">
                <label for="setup-password">Password</label>
                <div class="secret-input">
                  <input
                    #setupPassword
                    id="setup-password"
                    name="password"
                    type="password"
                    [(ngModel)]="password"
                    autocomplete="new-password"
                    required
                    minlength="12"
                    maxlength="256"
                  /><hhh-password-toggle [field]="setupPassword" />
                </div>
              </div>
              <small class="setup-password-help"
                >Use at least 12 characters, including uppercase and lowercase
                letters, a number, and a symbol.</small
              >
              <div class="secret-form-field">
                <label for="setup-confirmation">Confirm password</label>
                <div class="secret-input">
                  <input
                    #setupConfirmation
                    id="setup-confirmation"
                    name="confirmation"
                    type="password"
                    [(ngModel)]="confirmation"
                    autocomplete="new-password"
                    required
                    minlength="12"
                    maxlength="256"
                  /><hhh-password-toggle
                    [field]="setupConfirmation"
                    label="password confirmation"
                  />
                </div>
              </div>
              <button
                type="submit"
                class="btn btn-primary"
                [disabled]="busy() || setupForm.invalid"
              >
                {{
                  busy()
                    ? "Creating administrator..."
                    : "Create administrator account"
                }}
              </button>
            </form>
          }
          @if (api.setupAvailable()) {
            <a routerLink="/login" class="back-link">Back to sign in</a>
          }
        }
      </div>
    </section>
  </main>`,
})
export class SetupComponent implements OnInit {
  api = inject(ApiService);
  private pending = inject(PendingTasks);
  private router = inject(Router);
  loading = signal(true);
  busy = signal(false);
  error = signal("");
  complete = signal(false);
  displayName = "";
  email = "";
  organizationName = "";
  setupKey = "";
  password = "";
  confirmation = "";
  includeSampleData = true;
  constructor() {
    document.documentElement.classList.remove("dark");
  }
  ngOnInit() {
    this.pending.run(() => this.load());
  }
  async load() {
    this.loading.set(true);
    this.error.set("");
    try {
      await this.api.csrf();
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }
  async submit() {
    if (this.busy() || !this.api.setupAvailable()) return;
    this.error.set("");
    if (this.password !== this.confirmation) {
      this.error.set("Passwords must match.");
      return;
    }
    this.busy.set(true);
    try {
      await this.api.completeAdministratorSetup({
        setupKey: this.setupKey,
        email: this.email,
        displayName: this.displayName,
        organizationName: this.organizationName,
        password: this.password,
        includeSampleData:
          this.api.sampleDataEnabled() && this.includeSampleData,
      });
      this.clearSecrets();
      this.complete.set(true);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.api.setupAvailable.set(false);
        this.api.setupState.set("unknown");
        this.clearSecrets();
      }
      this.error.set(
        error instanceof HttpErrorResponse && error.status === 403
          ? "The setup key is not valid. Use the key provided for this installation."
          : errorMessage(error),
      );
    } finally {
      this.busy.set(false);
    }
  }
  private clearSecrets() {
    this.setupKey = "";
    this.password = "";
    this.confirmation = "";
  }
  isAdministrator() {
    return (
      this.api
        .user()
        ?.roles.some((role) =>
          ["System Administrator", "Organization Administrator"].includes(role),
        ) ?? false
    );
  }
  async openDemoAdministration() {
    if (this.busy() || !this.api.demoEnabled()) return;
    this.busy.set(true);
    this.error.set("");
    try {
      await this.api.demoAdministrator();
      await this.router.navigateByUrl("/app/administration");
    } catch (error) {
      this.error.set(
        error instanceof HttpErrorResponse && error.status === 404
          ? "Demo administrator access is unavailable. Sign in with an existing administrator account."
          : errorMessage(error),
      );
    } finally {
      this.busy.set(false);
    }
  }
}
