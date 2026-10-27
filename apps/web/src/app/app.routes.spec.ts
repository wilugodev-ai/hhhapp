import { signal } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { provideRouter, Router } from "@angular/router";
import { RouterTestingHarness } from "@angular/router/testing";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiService } from "./api.service";
import { routes } from "./app.routes";
import { LandingComponent } from "./landing.component";
import { LoginComponent } from "./login.component";
import { User } from "./models";
import { WorkspaceComponent } from "./workspace.component";

async function render(signedIn = true) {
  const api = {
    user: signal<User | null>(
      signedIn
        ? {
            id: "current",
            organizationId: "org-1",
            displayName: "Current User",
            email: "user@example.com",
            organization: "Horizon",
            roles: ["Executive Viewer"],
            mlrTarget: 85,
          }
        : null,
    ),
    setupAvailable: signal(false),
    demoEnabled: signal(false),
    restore: vi.fn(async () => {}),
    logout: vi.fn(async () => {}),
    metadata: vi.fn(async () => ({
      dimensions: [],
      months: [],
      reportMonths: [],
      years: [],
    })),
    batches: vi.fn(async () => []),
    dashboard: vi.fn(async () => null),
  };
  await TestBed.configureTestingModule({
    providers: [provideRouter(routes), { provide: ApiService, useValue: api }],
  }).compileComponents();
  const harness = await RouterTestingHarness.create();
  return { api, harness, router: TestBed.inject(Router) };
}

afterEach(() => {
  TestBed.resetTestingModule();
  document.documentElement.classList.remove("dark");
});
describe("Session-aware home navigation", () => {
  it("opens data quality without requiring financial records or loading the financial dashboard", async () => {
    const { api, harness, router } = await render();
    await harness.navigateByUrl("/app/data", WorkspaceComponent);
    await harness.fixture.whenStable();
    const root = harness.routeNativeElement!;
    expect(router.url).toBe("/app/data");
    expect(root.textContent).toContain("Data quality & sources");
    expect(root.textContent).toContain(
      "No financial months have been recorded yet.",
    );
    expect(root.textContent).toContain("Not collected yet.");
    expect(api.dashboard).not.toHaveBeenCalled();
    expect(
      root.querySelector('nav[aria-label="Main navigation"]'),
    ).not.toBeNull();
  });
  it("keeps the session when the HHH logo opens home and returns directly to the workspace", async () => {
    const { api, harness, router } = await render();
    const user = api.user();
    await harness.navigateByUrl("/app/overview", WorkspaceComponent);
    await harness.fixture.whenStable();
    const menuLinks = [
      ...harness.routeNativeElement!.querySelectorAll<HTMLAnchorElement>(
        'nav[aria-label="Main navigation"] a',
      ),
    ].map((link) => link.getAttribute("href"));
    harness
      .routeNativeElement!.querySelector<HTMLAnchorElement>("aside .brand")!
      .click();
    await vi.waitFor(() => expect(router.url).toBe("/"));
    await harness.fixture.whenStable();
    const home = harness.routeNativeElement!;
    expect(
      [
        ...home.querySelectorAll<HTMLAnchorElement>(
          'nav[aria-label="Main navigation"] a',
        ),
      ].map((link) => link.getAttribute("href")),
    ).toEqual(menuLinks);
    expect(
      home
        .querySelector('nav[aria-label="Main navigation"] a[href="/"]')
        ?.getAttribute("aria-current"),
    ).toBe("page");
    expect(home.querySelector(".landing-session")?.textContent).toContain(
      "Current User",
    );
    expect(home.querySelector('a[href="/login"]')).toBeNull();
    expect(
      home.querySelectorAll('.landing a[href="/app/overview"]').length,
    ).toBe(4);
    home
      .querySelector<HTMLAnchorElement>('.landing-nav a[href="/app/overview"]')!
      .click();
    await vi.waitFor(() => expect(router.url).toBe("/app/overview"));
    await harness.fixture.whenStable();
    expect(
      harness.routeNativeElement!.querySelector('[aria-label="Your account"]'),
    ).not.toBeNull();
    expect(api.user()).toBe(user);
    expect(api.logout).not.toHaveBeenCalled();
    expect(api.restore).not.toHaveBeenCalled();
  });
  it("redirects a signed-in visit to login back to the workspace", async () => {
    const { api, harness, router } = await render();
    await harness.navigateByUrl("/login", WorkspaceComponent);
    expect(router.url).toBe("/app/overview");
    expect(api.restore).toHaveBeenCalledTimes(1);
    expect(api.user()?.id).toBe("current");
    expect(api.logout).not.toHaveBeenCalled();
  });
  it("allows sign-in again when the server reports that the previous session expired", async () => {
    const { api, harness, router } = await render();
    api.restore.mockImplementationOnce(async () => {
      api.user.set(null);
    });
    await harness.navigateByUrl("/login", LoginComponent);
    expect(router.url).toBe("/login");
    expect(harness.routeNativeElement!.textContent).toContain(
      "Sign in to your workspace",
    );
  });
  it("offers sign-in to visitors and protects the workspace", async () => {
    const { api, harness, router } = await render(false);
    await harness.navigateByUrl("/", LandingComponent);
    expect(
      harness.routeNativeElement!.querySelector(
        'nav[aria-label="Main navigation"]',
      ),
    ).toBeNull();
    expect(
      harness.routeNativeElement!.querySelector(".landing-session"),
    ).toBeNull();
    expect(
      harness.routeNativeElement!.querySelector(
        '.landing-nav a[href="/login"]',
      ),
    ).not.toBeNull();
    await harness.navigateByUrl("/app/overview", LoginComponent);
    expect(router.url).toBe("/login");
    expect(api.user()).toBeNull();
  });
  it("keeps sidebar collapse state when moving between the workspace and Home", async () => {
    const { harness, router } = await render();
    await harness.navigateByUrl("/app/overview", WorkspaceComponent);
    harness
      .routeNativeElement!.querySelector<HTMLButtonElement>(
        '[aria-label="Collapse sidebar"]',
      )!
      .click();
    await harness.fixture.whenStable();
    harness
      .routeNativeElement!.querySelector<HTMLAnchorElement>("aside .brand")!
      .click();
    await vi.waitFor(() => expect(router.url).toBe("/"));
    await harness.fixture.whenStable();
    expect(
      harness
        .routeNativeElement!.querySelector(".workspace")
        ?.classList.contains("sidebar-collapsed"),
    ).toBe(true);
    harness
      .routeNativeElement!.querySelector<HTMLAnchorElement>(
        'nav[aria-label="Main navigation"] a[href="/app/settings"]',
      )!
      .click();
    await vi.waitFor(() => expect(router.url).toBe("/app/settings"));
    await harness.fixture.whenStable();
    expect(
      harness
        .routeNativeElement!.querySelector(".workspace")
        ?.classList.contains("sidebar-collapsed"),
    ).toBe(true);
    harness
      .routeNativeElement!.querySelector<HTMLButtonElement>(
        '[aria-label="Expand sidebar"]',
      )!
      .click();
    await harness.fixture.whenStable();
    expect(
      harness
        .routeNativeElement!.querySelector(".workspace")
        ?.classList.contains("sidebar-collapsed"),
    ).toBe(false);
  });
  it("opens the mobile menu on Home, closes it on navigation, and restores focus on Escape", async () => {
    const { harness, router } = await render();
    await harness.navigateByUrl("/", LandingComponent);
    const open = harness.routeNativeElement!.querySelector<HTMLButtonElement>(
      '[aria-label="Open navigation"]',
    )!;
    open.click();
    await harness.fixture.whenStable();
    expect(open.getAttribute("aria-expanded")).toBe("true");
    expect(document.activeElement).toBe(
      harness.routeNativeElement!.querySelector(
        '[aria-label="Close main menu"]',
      ),
    );
    harness
      .routeNativeElement!.querySelector<HTMLAnchorElement>(
        'nav[aria-label="Main navigation"] a[href="/app/settings"]',
      )!
      .click();
    await vi.waitFor(() => expect(router.url).toBe("/app/settings"));
    await harness.fixture.whenStable();
    const trigger =
      harness.routeNativeElement!.querySelector<HTMLButtonElement>(
        '[aria-label="Open navigation"]',
      )!;
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
    trigger.click();
    await harness.fixture.whenStable();
    document.dispatchEvent(
      new KeyboardEvent("keydown", {
        key: "Escape",
        bubbles: true,
        cancelable: true,
      }),
    );
    await harness.fixture.whenStable();
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
    expect(document.activeElement).toBe(trigger);
  });
  it("keeps first-administrator setup available on a new installation", async () => {
    const { api, harness } = await render(false);
    api.setupAvailable.set(true);
    await harness.navigateByUrl("/", LandingComponent);
    const action = harness.routeNativeElement!.querySelector<HTMLAnchorElement>(
      ".hero-actions .btn-primary",
    )!;
    expect(action.getAttribute("href")).toBe("/setup");
    expect(action.textContent).toContain("Set up your organization");
  });
});
