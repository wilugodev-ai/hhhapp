import { TestBed } from "@angular/core/testing";
import { ActivatedRoute, Router } from "@angular/router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiService } from "./api.service";
import { ActivateComponent } from "./activate.component";

afterEach(() => TestBed.resetTestingModule());
describe("Account setup", () => {
  it("removes the fragment and requires matching passwords before submitting", async () => {
    const api = { activateAccount: vi.fn(async () => ({})) };
    const router = { navigate: vi.fn(async () => true) };
    await TestBed.configureTestingModule({
      imports: [ActivateComponent],
      providers: [
        { provide: ApiService, useValue: api },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { fragment: "user=test-user&token=setup-secret" },
          },
        },
        { provide: Router, useValue: router },
      ],
    }).compileComponents();
    // Exercise the setup flow directly; RouterLink rendering is covered by the production build.
    const component = TestBed.runInInjectionContext(
      () => new ActivateComponent(),
    );
    expect(router.navigate).toHaveBeenCalledWith(
      [],
      expect.objectContaining({ replaceUrl: true }),
    );
    component.password = "Valid-Test-Password1!";
    component.confirmation = "Different-Password1!";
    await component.submit();
    expect(api.activateAccount).not.toHaveBeenCalled();
    expect(component.error()).toContain("match");
    component.confirmation = component.password;
    await component.submit();
    expect(api.activateAccount).toHaveBeenCalledWith({
      userId: "test-user",
      token: "setup-secret",
      password: "Valid-Test-Password1!",
    });
    expect(component.complete()).toBe(true);
    expect(component.password).toBe("");
    expect(component.token).toBe("");
  });
});
