import { Component, inject, provideAppInitializer } from "@angular/core";
import { bootstrapApplication } from "@angular/platform-browser";
import { provideHttpClient, withXsrfConfiguration } from "@angular/common/http";
import {
  CanActivateFn,
  provideRouter,
  Router,
  RouterOutlet,
  Routes,
} from "@angular/router";
import { ApiService } from "./app/api.service";
const authenticated: CanActivateFn = () =>
  inject(ApiService).user() ? true : inject(Router).createUrlTree(["/login"]);
const routes: Routes = [
  {
    path: "setup",
    loadComponent: () =>
      import("./app/setup.component").then((m) => m.SetupComponent),
  },
  {
    path: "activate",
    loadComponent: () =>
      import("./app/activate.component").then((m) => m.ActivateComponent),
  },
  {
    path: "",
    loadComponent: () =>
      import("./app/landing.component").then((m) => m.LandingComponent),
  },
  {
    path: "login",
    loadComponent: () =>
      import("./app/login.component").then((m) => m.LoginComponent),
  },
  {
    path: "app/:section",
    canActivate: [authenticated],
    loadComponent: () =>
      import("./app/workspace.component").then((m) => m.WorkspaceComponent),
  },
  { path: "app", redirectTo: "app/overview", pathMatch: "full" },
  { path: "**", redirectTo: "" },
];
@Component({
  selector: "hhh-root",
  standalone: true,
  imports: [RouterOutlet],
  template: "<router-outlet/>",
})
class AppComponent {}
bootstrapApplication(AppComponent, {
  providers: [
    provideRouter(routes),
    provideHttpClient(
      withXsrfConfiguration({
        cookieName: "XSRF-TOKEN",
        headerName: "X-XSRF-TOKEN",
      }),
    ),
    provideAppInitializer(async () => {
      try {
        await inject(ApiService).restore();
      } catch {
        /* Sign-in page exposes connection errors on interaction. */
      }
    }),
  ],
}).catch(console.error);
