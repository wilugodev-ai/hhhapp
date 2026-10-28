# HHH on Google Cloud Run and Neon

Live application: **https://hhh-j2uk5d5lkq-uc.a.run.app**

Sign in with the existing administrator email and password. First-administrator setup is already complete. Demo sign-in is disabled. The Angular application and `/api` share one HTTPS origin, and SPA deep links serve the Angular entry point.

## Resources

| Resource | Value |
| --- | --- |
| Google project | `ablest-20261023` |
| Region / Cloud Run service | `us-central1` / `hhh` |
| Artifact Registry | `us-central1-docker.pkg.dev/ablest-20261023/hhh/app` |
| Source bucket | `ablest-20261023-hhh-source` (private) |
| Runtime / build identities | `hhh-runtime` / `hhh-build` |
| Neon database / runtime role | `hhh` / `hhh_app_20261027` |
| Secret Manager resources | `hhh-database-connection`, `hhh-data-protection-cert` |

Google's new-project billing quota prevented billing activation on the initially created `hhh-20261027` project. HHH therefore uses the existing billed project with separate service identities, secrets, image repository, source bucket, and service. Existing Ablest services were not changed. The unused project has no HHH application deployed.

Cloud Run uses one CPU, 512 MiB, request-based billing, zero minimum instances, one maximum instance, concurrency eight, and a 120-second request timeout. The runtime identity can read only HHH's two secrets. The Neon role has table DML and sequence access in `hhh`, no administrative or schema-creation privileges, no inherited roles, and no table access in other application databases. TLS verifies the Neon endpoint certificate; the deployed Npgsql connection also requires channel binding.

## Data transfer completed on 2026-10-27

The operator made a read-only export of `HhhLocal`. It transferred the original administrator, organization, roles, settings, financial records, budgets, and source/audit history: **22 tables, 10,046 records, one account**. Password hashes and security stamps were copied, preserving existing credentials. SQL Server timestamps were normalized to UTC with PostgreSQL microsecond precision; decimal amounts were preserved exactly.

The verified SHA-256 snapshot fingerprint is `93ABE23F24A72A9CA6B0377E83B089809D6CA9866804AC403C7242F2B5D0D4E8`.

An early test run introduced two reference organizations, seven reference accounts, and their dependent fixtures into the local database. The owner chose to keep that database unchanged. `export-hosted` excludes only those two identified fixture trees from the export, including their foreign-key dependents. It performs no local deletes or updates. A permanent seed guard now refuses reference seeding into `HhhLocal`.

Private snapshots, connection strings, generated verification credentials, and the session encryption certificate are held under `.artifacts/cloud/private`, protected by Windows ACLs and excluded from Git, Docker, and Cloud Build uploads. They must remain private. Cloud Build accepts a checked source allowlist; databases, local configuration, operator dependencies, build outputs, and private artifacts are excluded.

## Workstation prerequisites

Use PowerShell, Node.js 24, .NET 10, and an authenticated Google Cloud SDK with the required permissions. Run commands from the repository root. The scripts do not change the active global Google project.

```powershell
# Only needed when gcloud.cmd is not on PATH:
$env:HHH_GCLOUD='C:\intouch\.data\google-cloud\google-cloud-sdk\bin\gcloud.cmd'

npm ci --prefix deploy/cloud-run --ignore-scripts --no-audit --no-fund
dotnet build src/Hhh.Operator/Hhh.Operator.csproj --configuration Release
```

`operator.mjs` uses the workspace-local SDK when present, otherwise `dotnet` from PATH. `HHH_DOTNET` and `HHH_OPERATOR_CONFIGURATION` provide explicit overrides. Provisioning/inspection uses a private `NEON_ADMIN_ENV_FILE` containing `DATABASE_URL`; on this workstation it defaults to the existing administrative configuration at `C:\ablest\apps\api\.env.hosted`. That credential never enters Cloud Run or build source.

## Redeploy an application update

```powershell
.\deploy\cloud-run\deploy.ps1 -BuildOnly
$build=Get-Content .artifacts/cloud/build-result.json -Raw | ConvertFrom-Json
& $env:HHH_GCLOUD builds describe $build.buildId "--project=$($build.projectId)" "--region=$($build.region)" --format='value(status)'
# Proceed only after the build reports SUCCESS:
.\deploy\cloud-run\deploy.ps1 -ExistingImage $build.image
```

`deploy.ps1` pins secret versions to `1`, configures exact generated Google hostnames in `AllowedHosts`, and keeps migration/demo flags disabled. It records the URL and image in `.artifacts/cloud/deployment-result.json`. For a new schema change, review and apply the matching PostgreSQL migration with the operator before deploying compatible application code:

```powershell
node deploy/cloud-run/operator.mjs migrate
```

The SQL Server context and migrations remain the local defaults. Specify `--context HhhDbContext` or `--context PostgresHhhDbContext` when generating EF migrations to avoid ambiguity.

## Initial provisioning and transfer

These steps were completed for this deployment. **Do not rerun import against the live database.** The import refuses populated application tables and verifies the full canonical snapshot inside its transaction before committing.

1. `prepare.ps1` prepares HHH-only Google resources in an already billed project and protects the private local artifact directory.
2. `dotnet run --project src/Hhh.Operator --configuration Release -- certificate .artifacts/cloud/private/session-encryption.pfx` creates the private encryption certificate once and refuses to overwrite it.
3. `node deploy/cloud-run/neon.mjs` creates the separate database/role and uploads the runtime connection and certificate to HHH's secrets. Existing private deployment metadata is required for safe retries against an existing database.
4. `node deploy/cloud-run/operator.mjs migrate` creates the PostgreSQL schema using the private operator connection.
5. `dotnet run --project src/Hhh.Operator --configuration Release -- export-hosted .artifacts/cloud/private/hosted-transfer.json` exports the normal local installation without changing it.
6. `node deploy/cloud-run/operator.mjs import .artifacts/cloud/private/hosted-transfer.json` imports only into the empty dedicated `hhh` database.
7. Build and deploy with `deploy.ps1` as above.

`export` retains every local row; `export-hosted` applies the specific reference-fixture exclusion described above. Both use a consistent read-only SQL transaction. Neither command cleans or resets the source database.

## Verification

Completed checks: 61 backend tests, 63 Angular tests, the production Angular build, and the actual cloud Docker build. Live HTTPS checks verified static assets and deep links, protected APIs, sign-in/out, secure cookies, financial/report reconciliation, approved budget matching, MLR, data sources, tenant isolation, administrator directory, organization updates, stale-write conflicts, and audit records. A saved session survived a fresh Cloud Run revision. Runtime privilege and encrypted-key checks passed. Temporary hosted accounts and their records were removed after checking, and the original transfer was verified again.

To repeat hosted checks, create fresh temporary verification metadata at a new private path, or archive the previous generated verifier file after confirming its records were removed. `verify-hosted.mjs` reads `.artifacts/cloud/private/hosted-verifier.json`. Never substitute an original account into that file.

```powershell
node deploy/cloud-run/operator.mjs create-verifier .artifacts/cloud/private/hosted-verifier.json
node deploy/cloud-run/verify-hosted.mjs
# Replace the Cloud Run container with a new revision before this check:
node deploy/cloud-run/verify-hosted.mjs --check-restart
node deploy/cloud-run/operator.mjs promote-verifier .artifacts/cloud/private/hosted-verifier.json
node deploy/cloud-run/verify-hosted.mjs --check-administration
node deploy/cloud-run/operator.mjs remove-verifier .artifacts/cloud/private/hosted-verifier.json
node deploy/cloud-run/inspect-hosted.mjs
```

Only generated verifier IDs with the `hhh-deploy-check-` prefix and the matching temporary organization can be removed by that command. It targets PostgreSQL database `hhh`; it cannot operate on `HhhLocal`. The `verify` operator compares all business/account/audit rows to the supplied snapshot; after legitimate user activity it will differ from the original transfer snapshot, so use it only when such changes are not expected. Browser visual testing remains separate from these HTTP and component checks.

## Sessions, recovery, and backups

Data Protection keys persist in Neon and are encrypted with the certificate from `hhh-data-protection-cert:1`. Keep that certificate version and its private backup: replacing it without retaining decryption support would invalidate existing sessions and protected tokens. A container restart or new revision reuses the same keys. Local and hosted browser sessions are separate; sign in once on the hosted origin using the same credentials.

Neon is now the hosted application's live database. Local and hosted databases do not synchronize after this initial transfer. Keep private export backups and use the existing Neon recovery/backup facilities before destructive maintenance; deployment does not change the Neon plan or purchase a backup tier. Account invitation/recovery links remain administrator-managed; automatic email delivery, SSO, and MFA are separate unfinished integrations.

For an application rollback, redeploy a previously verified HHH image with `deploy.ps1 -ExistingImage <image>`. The script preserves the database and secret references. Schema changes require their own compatibility and recovery review; image rollback does not undo database migrations.
