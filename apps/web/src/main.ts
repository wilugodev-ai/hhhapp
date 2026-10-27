import { Component, inject, provideAppInitializer } from "@angular/core";
import { bootstrapApplication } from "@angular/platform-browser";
import { provideHttpClient, withXsrfConfiguration } from "@angular/common/http";
import { provideRouter, RouterOutlet } from "@angular/router";
import { ApiService } from "./app/api.service";
import { routes } from "./app/app.routes";
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
