import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { afterEach, describe, expect, it } from "vitest";
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
    http
      .expectOne("/api/auth/logout")
      .flush({}, { status: 401, statusText: "Unauthorized" });
    await Promise.resolve();
    http.expectOne("/api/auth/csrf").flush({ demoEnabled: true });
    await logout;
    expect(api.user()).toBeNull();
  });
});
