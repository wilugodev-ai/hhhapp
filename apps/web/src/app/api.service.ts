import { inject, Injectable, signal } from "@angular/core";
import {
  HttpClient,
  HttpErrorResponse,
  HttpParams,
} from "@angular/common/http";
import { firstValueFrom } from "rxjs";
import { Batch, Dashboard, Filters, Metadata, User } from "./models";

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
    await firstValueFrom(this.http.post("/api/auth/logout", {}));
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
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "hhh-financial-report.csv";
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
