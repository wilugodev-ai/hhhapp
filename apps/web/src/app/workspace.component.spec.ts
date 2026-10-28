import { HttpErrorResponse } from "@angular/common/http";
import { Component, signal } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { provideRouter, Router } from "@angular/router";
import { RouterTestingHarness } from "@angular/router/testing";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiService } from "./api.service";
import { User } from "./models";
import { WorkspaceComponent } from "./workspace.component";

@Component({ template: "Signed out" })
class SignedOutPage {}

async function render() {
  const api = {
    user: signal<User | null>({
      id: "current",
      organizationId: "org-1",
      displayName: "Current User",
      email: "user@example.com",
      organization: "Horizon",
      roles: ["Executive Viewer"],
      mlrTarget: 85,
    }),
    metadata: vi.fn(async () => ({
      dimensions: [],
      months: [],
      reportMonths: [],
      years: [],
    })),
    batches: vi.fn(async () => []),
    dashboard: vi.fn(async () => null),
    logout: vi.fn(async () => {}),
  };
  await TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: "app/:section", component: WorkspaceComponent },
        { path: "login", component: SignedOutPage },
      ]),
      { provide: ApiService, useValue: api },
    ],
  }).compileComponents();
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl("/app/administration", WorkspaceComponent);
  await harness.fixture.whenStable();
  const root = harness.routeNativeElement!;
  return { api, harness, root, router: TestBed.inject(Router) };
}

afterEach(() => TestBed.resetTestingModule());
describe("Workspace account controls", () => {
  it("opens the profile button, shows the current account, and supports Escape and outside clicks", async () => {
    const { root, harness } = await render();
    const profile = root.querySelector<HTMLButtonElement>(
      '[aria-label="Your account"]',
    )!;
    expect(profile.getAttribute("aria-expanded")).toBe("false");
    profile.focus();
    profile.click();
    await harness.fixture.whenStable();
    const menu = root.querySelector("#user-account-menu")!;
    expect(profile.getAttribute("aria-expanded")).toBe("true");
    expect(menu.textContent).toContain("Current User");
    expect(menu.textContent).toContain("user@example.com");
    expect(menu.textContent).toContain("Horizon");
    expect(menu.textContent).toContain("Executive Viewer");
    expect(menu.querySelector('a[href="/app/settings"]')).not.toBeNull();
    expect(menu.querySelector('a[href="/app/administration"]')).toBeNull();
    menu.querySelector<HTMLAnchorElement>("a")!.focus();
    document.dispatchEvent(
      new KeyboardEvent("keydown", {
        key: "Escape",
        bubbles: true,
        cancelable: true,
      }),
    );
    await harness.fixture.whenStable();
    expect(root.querySelector("#user-account-menu")).toBeNull();
    expect(document.activeElement).toBe(profile);
    profile.click();
    await harness.fixture.whenStable();
    document.body.click();
    await harness.fixture.whenStable();
    expect(root.querySelector("#user-account-menu")).toBeNull();
  });
  it.each(["System Administrator", "Organization Administrator"])(
    "offers Administration to %s and closes the menu on navigation",
    async (role) => {
      const { api, root, harness, router } = await render();
      api.user.update((user) => ({ ...user!, roles: [role] }));
      root
        .querySelector<HTMLButtonElement>('[aria-label="Your account"]')!
        .click();
      await harness.fixture.whenStable();
      const link = root.querySelector<HTMLAnchorElement>(
        '#user-account-menu a[href="/app/administration"]',
      );
      expect(link).not.toBeNull();
      link!.click();
      await harness.fixture.whenStable();
      expect(router.url).toBe("/app/administration");
      expect(root.querySelector("#user-account-menu")).toBeNull();
    },
  );
  it("closes the menu when keyboard focus moves past its actions", async () => {
    const { root, harness } = await render();
    const outside = root.querySelector<HTMLButtonElement>(
      '[aria-label="View MLR alerts"]',
    )!;
    const profile = root.querySelector<HTMLButtonElement>(
      '[aria-label="Your account"]',
    )!;
    profile.focus();
    profile.click();
    await harness.fixture.whenStable();
    root.querySelector<HTMLAnchorElement>("#user-account-menu a")!.focus();
    outside.focus();
    await harness.fixture.whenStable();
    expect(root.querySelector("#user-account-menu")).toBeNull();
    expect(document.activeElement).toBe(outside);
  });
  it("signs out from the account menu", async () => {
    const { api, root, harness, router } = await render();
    root
      .querySelector<HTMLButtonElement>('[aria-label="Your account"]')!
      .click();
    await harness.fixture.whenStable();
    root.querySelector<HTMLButtonElement>("#user-account-menu button")!.click();
    await vi.waitFor(() => expect(router.url).toBe("/login"));
    expect(api.logout).toHaveBeenCalledTimes(1);
  });
  it("opens Settings from the account menu", async () => {
    const { root, harness, router } = await render();
    root
      .querySelector<HTMLButtonElement>('[aria-label="Your account"]')!
      .click();
    await harness.fixture.whenStable();
    root
      .querySelector<HTMLAnchorElement>(
        '#user-account-menu a[href="/app/settings"]',
      )!
      .click();
    await vi.waitFor(() => expect(router.url).toBe("/app/settings"));
    await harness.fixture.whenStable();
    expect(root.querySelector("#user-account-menu")).toBeNull();
    expect(root.textContent).toContain("Appearance");
  });
  it("shows progress, prevents duplicate clicks, and navigates to sign in", async () => {
    const { api, harness, root, router } = await render();
    let finish!: () => void;
    api.logout.mockImplementationOnce(
      () =>
        new Promise<void>((resolve) => {
          finish = resolve;
        }),
    );
    expect(root.querySelector(".export-button")).toBeNull();
    const profile = root.querySelector<HTMLButtonElement>(
      '[aria-label="Your account"]',
    )!;
    profile.click();
    await harness.fixture.whenStable();
    root.querySelector<HTMLButtonElement>("#user-account-menu button")!.click();
    harness.detectChanges();
    expect(root.querySelector('[role="status"]')?.textContent).toContain(
      "Signing out",
    );
    profile.click();
    harness.detectChanges();
    const button = root.querySelector<HTMLButtonElement>(
      "#user-account-menu button",
    )!;
    expect(button.disabled).toBe(true);
    button.click();
    expect(api.logout).toHaveBeenCalledTimes(1);
    finish();
    await vi.waitFor(() => expect(router.url).toBe("/login"));
    expect(harness.routeNativeElement?.textContent).toContain("Signed out");
  });
  it("shows a failed sign out and allows another attempt", async () => {
    const { api, harness, root, router } = await render();
    api.logout.mockRejectedValueOnce(new HttpErrorResponse({ status: 503 }));
    root
      .querySelector<HTMLButtonElement>('[aria-label="Your account"]')!
      .click();
    await harness.fixture.whenStable();
    root.querySelector<HTMLButtonElement>("#user-account-menu button")!.click();
    await vi.waitFor(() => {
      harness.detectChanges();
      expect(root.querySelector('[role="status"]')?.textContent).toContain(
        "HHH API",
      );
    });
    expect(router.url).toBe("/app/administration");
    expect(root.querySelector("#user-account-menu")).toBeNull();
    root
      .querySelector<HTMLButtonElement>('[aria-label="Your account"]')!
      .click();
    await harness.fixture.whenStable();
    const button = root.querySelector<HTMLButtonElement>(
      "#user-account-menu button",
    )!;
    expect(button.disabled).toBe(false);
    button.click();
    await vi.waitFor(() => expect(router.url).toBe("/login"));
    expect(api.logout).toHaveBeenCalledTimes(2);
  });
});
