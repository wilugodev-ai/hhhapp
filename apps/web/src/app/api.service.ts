import { inject, Injectable, signal } from "@angular/core";
import {
  HttpClient,
  HttpErrorResponse,
  HttpParams,
} from "@angular/common/http";
import { firstValueFrom } from "rxjs";
import { Batch, Dashboard, Filters, Metadata, User } from "./models";
import { ProfitLossDetails, ProfitLossReport } from "./profit-loss.models";
import { MlrConfiguration, MlrReport } from "./mlr.models";
import {
  AdminAudit,
  AdminOrganization,
  AdminUser,
  SetupLink,
  UserDirectory,
} from "./admin.models";

@Injectable({ providedIn: "root" })
export class ApiService {
  private http = inject(HttpClient);
  user = signal<User | null>(null);
  demoEnabled = signal(false);
  async csrf() {
    const config = await firstValueFrom(
      this.http.get<{ demoEnabled: boolean }>("/api/auth/csrf"),
    );
    this.demoEnabled.set(config.demoEnabled);
  }
  async restore() {
    await this.csrf();
    try {
      this.user.set(await firstValueFrom(this.http.get<User>("/api/auth/me")));
    } catch (error) {
      if (!(error instanceof HttpErrorResponse && error.status === 401))
        throw error;
    }
  }
  async login(email: string, password: string, demo = false) {
    await this.csrf();
    await firstValueFrom(
      this.http.post(
        demo ? "/api/auth/demo" : "/api/auth/login",
        demo ? {} : { email, password },
      ),
    );
    await this.csrf();
    this.user.set(await firstValueFrom(this.http.get<User>("/api/auth/me")));
  }
  async logout() {
    try {
      await firstValueFrom(this.http.post("/api/auth/logout", {}));
    } catch (error) {
      // Revoked/expired sessions are already signed out on the server.
      if (!(error instanceof HttpErrorResponse && error.status === 401))
        throw error;
    }
    this.user.set(null);
    await this.csrf();
  }
  metadata() {
    return firstValueFrom(this.http.get<Metadata>("/api/analytics/filters"));
  }
  dashboard(filters: Filters) {
    return firstValueFrom(
      this.http.get<Dashboard>("/api/analytics/dashboard", {
        params: this.params(filters),
      }),
    );
  }
  batches() {
    return firstValueFrom(this.http.get<Batch[]>("/api/data/batches"));
  }
  adminOrganizations() {
    return firstValueFrom(
      this.http.get<AdminOrganization[]>("/api/admin/organizations"),
    );
  }
  adminUsers(org: string, search = "", status = "", page = 1) {
    return firstValueFrom(
      this.http.get<UserDirectory>(`/api/admin/organizations/${org}/users`, {
        params: { search, status, page },
      }),
    );
  }
  adminAudit(org: string, page = 1) {
    return firstValueFrom(
      this.http.get<AdminAudit>(`/api/admin/organizations/${org}/audit`, {
        params: { page },
      }),
    );
  }
  inviteUser(
    org: string,
    body: { email: string; displayName: string; role: string },
  ) {
    return firstValueFrom(
      this.http.post<SetupLink>(`/api/admin/organizations/${org}/users`, body),
    );
  }
  updateUser(
    org: string,
    id: string,
    body: {
      displayName: string;
      role: string;
      isActive: boolean;
      expectedVersion: number;
    },
  ) {
    return firstValueFrom(
      this.http.put<AdminUser>(
        `/api/admin/organizations/${org}/users/${id}`,
        body,
      ),
    );
  }
  adminUserAction(
    org: string,
    id: string,
    action: string,
    expectedVersion: number,
  ) {
    return firstValueFrom(
      this.http.post<SetupLink | AdminUser>(
        `/api/admin/organizations/${org}/users/${id}/${action}`,
        { expectedVersion },
      ),
    );
  }
  createOrganization(body: {
    name: string;
    adminEmail: string;
    adminName: string;
  }) {
    return firstValueFrom(
      this.http.post<{ organizationId: string; invitation: SetupLink }>(
        "/api/admin/organizations",
        body,
      ),
    );
  }
  updateOrganization(
    id: string,
    body: { name: string; isActive: boolean; expectedVersion: number },
  ) {
    return firstValueFrom(
      this.http.put(`/api/admin/organizations/${id}`, body),
    );
  }
  async activateAccount(body: {
    userId: string;
    token: string;
    password: string;
  }) {
    await this.csrf();
    return firstValueFrom(this.http.post("/api/auth/activate", body));
  }
  mlr(filters: Filters) {
    return firstValueFrom(
      this.http.get<MlrReport>("/api/analytics/mlr", {
        params: this.params(filters),
      }),
    );
  }
  mlrConfiguration() {
    return firstValueFrom(
      this.http.get<MlrConfiguration>("/api/mlr/configuration"),
    );
  }
  saveMlrDefinition(body: {
    name: string;
    expectedVersion: number;
    revenueCategories: string[];
    expenseCategories: string[];
  }) {
    return firstValueFrom(
      this.http.put<MlrConfiguration>("/api/mlr/definition", body),
    );
  }
  saveMlrTarget(dimensionId: string, target: number, expectedVersion: number) {
    return firstValueFrom(
      this.http.put<MlrConfiguration>("/api/mlr/targets/" + dimensionId, {
        target,
        expectedVersion,
      }),
    );
  }
  async resetMlrTarget(dimensionId: string, version: number) {
    await firstValueFrom(
      this.http.delete("/api/mlr/targets/" + dimensionId, {
        params: { version },
      }),
    );
  }
  async exportMlr(filters: Filters) {
    const blob = await firstValueFrom(
      this.http.get("/api/reports/mlr.csv", {
        params: this.params(filters),
        responseType: "blob",
      }),
    );
    this.download(blob, "hhh-mlr.csv");
  }
  profitLoss(filters: Filters) {
    return firstValueFrom(
      this.http.get<ProfitLossReport>("/api/analytics/profit-loss", {
        params: this.params(filters),
      }),
    );
  }
  profitLossDetails(filters: Filters) {
    return firstValueFrom(
      this.http.get<ProfitLossDetails>("/api/analytics/profit-loss/details", {
        params: this.params(filters),
      }),
    );
  }
  async exportProfitLoss(filters: Filters) {
    const blob = await firstValueFrom(
      this.http.get("/api/reports/profit-loss.csv", {
        params: this.params(filters),
        responseType: "blob",
      }),
    );
    this.download(blob, "hhh-profit-loss.csv");
  }
  audit() {
    return firstValueFrom(
      this.http.get<{ id: number; action: string; occurredAt: string }[]>(
        "/api/audit",
      ),
    );
  }
  saveSettings(mlrTarget: number) {
    return firstValueFrom(
      this.http.put<{ mlrTarget: number }>("/api/settings", { mlrTarget }),
    );
  }
  async export(filters: Filters) {
    const blob = await firstValueFrom(
      this.http.get("/api/reports/financial.csv", {
        params: this.params(filters),
        responseType: "blob",
      }),
    );
    this.download(blob, "hhh-financial-report.csv");
  }
  private download(blob: Blob, name: string) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = name;
    link.click();
    URL.revokeObjectURL(url);
  }
  private params(filters: Filters) {
    return new HttpParams({
      fromObject: Object.fromEntries(
        Object.entries(filters).filter(([, v]) => v !== ""),
      ),
    });
  }
}

export function errorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 401)
      return "Your session has expired. Sign in again to continue.";
    if (error.status === 403)
      return "Your role does not have permission to perform this action.";
    if (error.status === 429)
      return "Too many attempts. Wait one minute and try again.";
    return (
      error.error?.title ||
      "We could not reach the HHH API. Check that the backend is running and try again."
    );
  }
  return "Something went wrong. Please try again.";
}
