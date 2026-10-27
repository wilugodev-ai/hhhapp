import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiService } from "./api.service";

afterEach(() => {
  TestBed.inject(HttpTestingController).verify();
  TestBed.resetTestingModule();
});
describe("Revoked session sign-out", () => {
  it("clears the local identity and refreshes CSRF when the session was already revoked", async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const api = TestBed.inject(ApiService);
    const http = TestBed.inject(HttpTestingController);
    api.user.set({
      displayName: "Former Session",
      email: "test@example.com",
      organization: "Horizon",
      roles: ["Executive Viewer"],
      mlrTarget: 85,
    });
    const logout = api.logout();
    http.expectOne("/api/auth/csrf").flush({ demoEnabled: false });
    const request = await vi.waitFor(() => http.expectOne("/api/auth/logout"));
    request.flush({}, { status: 401, statusText: "Unauthorized" });
    const refresh = await vi.waitFor(() => http.expectOne("/api/auth/csrf"));
    refresh.flush({ demoEnabled: false });
    await logout;
    expect(api.user()).toBeNull();
  });
  it("refreshes CSRF before signing out and again for the anonymous session", async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const api = TestBed.inject(ApiService);
    const http = TestBed.inject(HttpTestingController);
    const logout = api.logout();
    http.expectNone("/api/auth/logout");
    http.expectOne("/api/auth/csrf").flush({ demoEnabled: false });
    const request = await vi.waitFor(() => http.expectOne("/api/auth/logout"));
    expect(request.request.method).toBe("POST");
    request.flush({});
    const refresh = await vi.waitFor(() => http.expectOne("/api/auth/csrf"));
    refresh.flush({ demoEnabled: false });
    await logout;
    expect(api.user()).toBeNull();
  });
});
