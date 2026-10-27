import { inject } from "@angular/core";
import { CanActivateFn, Router, Routes } from "@angular/router";
import { ApiService } from "./api.service";

const authenticated: CanActivateFn = () =>
  inject(ApiService).user() ? true : inject(Router).createUrlTree(["/login"]);

const signedOutOnly: CanActivateFn = async () => {
  const api = inject(ApiService);
  const router = inject(Router);
  if (api.user()) {
    try {
      await api.restore();
    } catch {
      // Keep the known session when the API is temporarily unavailable.
    }
  }
  return api.user() ? router.createUrlTree(["/app/overview"]) : true;
};

export const routes: Routes = [
  {
    path: "setup",
    loadComponent: () =>
      import("./setup.component").then((m) => m.SetupComponent),
  },
  {
    path: "activate",
    loadComponent: () =>
      import("./activate.component").then((m) => m.ActivateComponent),
  },
  {
    path: "",
    loadComponent: () =>
      import("./landing.component").then((m) => m.LandingComponent),
  },
  {
    path: "login",
    canActivate: [signedOutOnly],
    loadComponent: () =>
      import("./login.component").then((m) => m.LoginComponent),
  },
  {
    path: "app/:section",
    canActivate: [authenticated],
    loadComponent: () =>
      import("./workspace.component").then((m) => m.WorkspaceComponent),
  },
  { path: "app", redirectTo: "app/overview", pathMatch: "full" },
  { path: "**", redirectTo: "" },
];
