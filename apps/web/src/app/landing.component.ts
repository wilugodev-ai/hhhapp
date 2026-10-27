import { Component, inject, signal } from "@angular/core";
import { Router, RouterLink } from "@angular/router";
import { ApiService, errorMessage } from "./api.service";
import { IconComponent } from "./icon.component";

@Component({
  selector: "hhh-landing",
  standalone: true,
  imports: [RouterLink, IconComponent],
  template: ` <div class="landing">
    <nav class="landing-nav">
      <a class="brand" routerLink="/"
        ><span class="brand-symbol">H<span>↗</span></span
        ><strong>HHH<span>Healthcare Horizon Hub</span></strong></a
      >
      <div class="landing-links">
        <a href="#platform">Platform</a><a href="#approach">Our approach</a
        ><a routerLink="/login" class="btn btn-outline"
          >Sign in <hhh-icon name="arrow"
        /></a>
      </div>
    </nav>
    <main>
      <section class="hero">
        <div class="hero-copy">
          <span class="eyebrow"
            ><span class="status-dot"></span> HEALTHCARE FINANCIAL
            INTELLIGENCE</span
          >
          <h1>
            See beyond<br />the numbers.<br /><em>Predict what’s next.</em>
          </h1>
          <p>
            Turn healthcare financial complexity into a clearer path forward.
            Your performance, your cost drivers, your next opportunity —
            together in one place.
          </p>
          <div class="hero-actions">
            <a
              class="btn btn-primary"
              [routerLink]="api.setupAvailable() ? '/setup' : '/login'"
              >{{
                api.setupAvailable()
                  ? "Set up your organization"
                  : "Open your workspace"
              }}
              <hhh-icon name="arrow" /></a
            ><a class="btn btn-ghost" href="#approach"
              >Discover HHH <hhh-icon name="down"
            /></a>
          </div>
          <div class="hero-proof">
            <span><hhh-icon name="check" /> Built for value-based care</span
            ><span
              ><hhh-icon name="check" /> Financial clarity, every month</span
            >
          </div>
        </div>
        <div
          class="hero-visual"
          aria-label="Illustrative financial intelligence dashboard"
        >
          <div class="orb orb-one"></div>
          <div class="orb orb-two"></div>
          <div class="preview-window">
            <div class="preview-top">
              <span class="preview-dots">● ● ●</span
              ><span>YOUR FINANCIAL HORIZON</span><hhh-icon name="chart" />
            </div>
            <div class="preview-body">
              <span class="eyebrow">A CLEARER PICTURE</span>
              <h2>Performance at a glance</h2>
              <div class="preview-stats">
                <div>
                  <small>Financial visibility</small><strong>360°</strong
                  ><span>Across your organization</span>
                </div>
                <div>
                  <small>One connected view</small><strong>01</strong
                  ><span>Every decision starts here</span>
                </div>
              </div>
              <svg
                class="hero-chart"
                viewBox="0 0 500 190"
                role="img"
                aria-label="Illustrative upward financial trend"
              >
                <defs>
                  <linearGradient id="fill" x1="0" y1="0" x2="0" y2="1">
                    <stop stop-color="#00b8a9" stop-opacity=".25" />
                    <stop offset="1" stop-color="#00b8a9" stop-opacity="0" />
                  </linearGradient>
                </defs>
                <path
                  d="M0 40H500M0 90H500M0 140H500"
                  stroke="#e5edf4"
                  stroke-dasharray="4 5"
                />
                <path
                  d="M0 150 Q25 145 45 130T90 125T140 105T190 110T235 80T280 83T330 55T380 60T425 30T470 22L500 12V190H0Z"
                  fill="url(#fill)"
                />
                <path
                  d="M0 150 Q25 145 45 130T90 125T140 105T190 110T235 80T280 83T330 55T380 60T425 30T470 22L500 12"
                  fill="none"
                  stroke="#00a999"
                  stroke-width="3"
                />
                <path
                  d="M0 170 Q70 155 100 150T200 130T300 115T400 100T500 90"
                  fill="none"
                  stroke="#91a7c2"
                  stroke-width="2"
                  stroke-dasharray="5 5"
                />
              </svg>
              <div class="preview-months">
                <span>UNDERSTAND</span><span>ANALYZE</span
                ><span>LOOK AHEAD</span>
              </div>
            </div>
          </div>
          <div class="floating-note">
            <span class="insight-icon"><hhh-icon name="spark" /></span>
            <div>
              <strong>From insight to foresight</strong
              ><small>Make your next move with clarity.</small>
            </div>
          </div>
          <p class="illustration-note">Illustrative product preview</p>
        </div>
      </section>
      <section id="platform" class="value-section">
        <div class="section-heading">
          <span class="eyebrow">YOUR DATA. A BIGGER PICTURE.</span>
          <h2>Built around the decisions that matter.</h2>
          <p>
            Purpose-built for healthcare finance, from the monthly close to the
            road ahead.
          </p>
        </div>
        <div class="value-grid">
          @for (value of values; track value.title) {
            <article>
              <span class="value-icon"><hhh-icon [name]="value.icon" /></span>
              <h3>{{ value.title }}</h3>
              <p>{{ value.text }}</p>
            </article>
          }
        </div>
      </section>
      <section id="approach" class="approach">
        <div>
          <span class="eyebrow">CLARITY BEFORE COMPLEXITY</span>
          <h2>One foundation.<br />A wider financial horizon.</h2>
        </div>
        <div>
          <p>
            Start with trusted, auditable financial reporting. Understand
            revenue, medical expenses, membership, and operational MLR across
            payers and medical centers.
          </p>
          <p>
            HHH’s roadmap extends that foundation with explainable forecasting,
            scenario modeling, and a permission-aware financial assistant.
          </p>
          <a
            [routerLink]="api.setupAvailable() ? '/setup' : '/login'"
            class="text-link"
            >Open your workspace <hhh-icon name="arrow" /></a
          ><small>Optional starter financial data is clearly labeled.</small>
        </div>
      </section>
    </main>
    <footer>
      <span>© {{ year }} Healthcare Horizon Hub</span
      ><span>Transforming Healthcare Data Into Financial Foresight.</span
      ><a routerLink="/login">Enter workspace <hhh-icon name="arrow" /></a>
    </footer>
  </div>`,
})
export class LandingComponent {
  api = inject(ApiService);
  constructor() {
    document.documentElement.classList.remove("dark");
  }
  year = 2026;
  values = [
    {
      icon: "wallet",
      title: "Financial clarity",
      text: "See how revenue and medical costs shape profitability across your organization.",
    },
    {
      icon: "trend",
      title: "A forward perspective",
      text: "Build toward explainable projections, grounded in measured historical performance.",
    },
    {
      icon: "heart",
      title: "Healthcare context",
      text: "Understand the measures that matter in capitation and value-based arrangements.",
    },
    {
      icon: "grid",
      title: "One connected view",
      text: "Bring your financial performance, membership, and reporting into focus.",
    },
  ];
}
