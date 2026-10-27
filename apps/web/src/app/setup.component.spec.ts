import { TestBed } from "@angular/core/testing";
import { HttpErrorResponse } from "@angular/common/http";
import { signal } from "@angular/core";
import { provideRouter } from "@angular/router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiService } from "./api.service";
import { SetupComponent } from "./setup.component";
import { LoginComponent } from "./login.component";

async function render(available = true) {
  const api = {
    setupAvailable: signal(available),
    demoEnabled: signal(false),
    csrf: vi.fn(async () => ({})),
    completeAdministratorSetup: vi.fn(async (_body: unknown) => {
      api.setupAvailable.set(false);
    }),
  };
  await TestBed.configureTestingModule({
    imports: [SetupComponent, LoginComponent],
    providers: [provideRouter([]), { provide: ApiService, useValue: api }],
  }).compileComponents();
  const fixture = TestBed.createComponent(SetupComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return {
    api,
    fixture,
    component: fixture.componentInstance,
    root: fixture.nativeElement as HTMLElement,
  };
}
function fill(component: SetupComponent) {
  component.displayName = "Taylor Owner";
  component.email = "owner@example.com";
  component.organizationName = "Horizon Partners";
  component.setupKey = "test-only-installation-key-1234567890123456";
  component.password = "Strong-Test-Password1!";
  component.confirmation = component.password;
}
afterEach(() => {
  TestBed.resetTestingModule();
  document.documentElement.classList.remove("dark");
});
describe("First administrator setup", () => {
  it("hides the form when setup is unavailable", async () => {
    const { root, api, component } = await render(false);
    expect(root.textContent).toContain("Administrator setup is unavailable");
    expect(root.querySelector('input[name="setupKey"]')).toBeNull();
    await component.submit();
    expect(api.completeAdministratorSetup).not.toHaveBeenCalled();
  });
  it("shows a protected form and submits only first-admin profile and credentials", async () => {
    const { root, api, component, fixture } = await render();
    fill(component);
    expect(
      root.querySelector<HTMLInputElement>('input[name="setupKey"]')?.type,
    ).toBe("password");
    await component.submit();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(api.completeAdministratorSetup).toHaveBeenCalledWith({
      setupKey: "test-only-installation-key-1234567890123456",
      email: "owner@example.com",
      displayName: "Taylor Owner",
      organizationName: "Horizon Partners",
      password: "Strong-Test-Password1!",
    });
    expect(root.textContent).toContain("Your administrator account is ready");
    expect(root.querySelector('input[name="setupKey"]')).toBeNull();
    expect(component.setupKey).toBe("");
    expect(component.password).toBe("");
    expect(component.confirmation).toBe("");
  });
  it("rejects mismatched passwords before sending the setup key", async () => {
    const { component, api } = await render();
    fill(component);
    component.confirmation = "Different-Test-Password1!";
    await component.submit();
    expect(component.error()).toBe("Passwords must match.");
    expect(api.completeAdministratorSetup).not.toHaveBeenCalled();
  });
  it("explains invalid setup keys without confusing them with role permissions", async () => {
    const { component, api } = await render();
    fill(component);
    api.completeAdministratorSetup.mockRejectedValueOnce(
      new HttpErrorResponse({ status: 403 }),
    );
    await component.submit();
    expect(component.error()).toContain("setup key is not valid");
    expect(api.setupAvailable()).toBe(true);
    expect(component.complete()).toBe(false);
  });
  it("closes a stale form when another owner finishes setup first", async () => {
    const { component, api, fixture, root } = await render();
    fill(component);
    api.completeAdministratorSetup.mockRejectedValueOnce(
      new HttpErrorResponse({
        status: 404,
        error: { title: "First-administrator setup is unavailable." },
      }),
    );
    await component.submit();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(root.textContent).toContain("Administrator setup is unavailable");
    expect(component.setupKey).toBe("");
    expect(component.password).toBe("");
  });
  it("offers the login-page setup link only while the server permits installation", async () => {
    const { api } = await render();
    const login = TestBed.createComponent(LoginComponent);
    login.detectChanges();
    await login.whenStable();
    expect(
      login.nativeElement.querySelector('a[href="/setup"]'),
    ).not.toBeNull();
    api.setupAvailable.set(false);
    login.detectChanges();
    await login.whenStable();
    expect(login.nativeElement.querySelector('a[href="/setup"]')).toBeNull();
  });
});
