# Trusted Playground Deployment

The hosted playground is a deliberately constrained environment for invited play-testing. As of 22 July 2026 it still runs the shared Development selector while the coordinated Google, Cloudflare and Azure OIDC configuration is prepared. The database is online and available for identity binding. The accepted cutover to the non-Development `Playground` environment is documented in the [Google OIDC cutover runbook](oidc-cutover.md).

## Runtime Shape

- `Cycles.Api` targets .NET 10 LTS and runs on the Azure App Service **F1 Free** plan.
- Authoritative state is stored in Azure SQL database `CyclesDb` on logical server `cycles-sql-b366b760` in France Central. The UK regions do not permit this subscription to provision the selected tier.
- The database uses the Azure SQL free serverless offer: General Purpose Gen5, 2 vCores maximum, 0.5 vCores minimum, the provider-required 60-minute idle auto-pause, 32 GB maximum data size, locally redundant backup storage, and `BillOverUsage` when the monthly free allowance is exhausted.
- `Cycles.Worker` is not deployed. Invited players advance the simulation manually through the Development-only **Close command window and advance** capability.
- The application uses `ASPNETCORE_ENVIRONMENT=Development`, but the API does not implicitly seed an empty database. Initial Development state is provisioned deliberately through the guarded CLI/deployment reseed path; normal deployments preserve and upgrade the existing SQL state.
- GitHub Actions deploys a manually dispatched, verified `main` revision through workload identity federation. No long-lived Azure credential is stored in GitHub.
- A Cloudflare Worker on the Free plan fronts `https://cycles.anthonypwatts.co.uk`. Its static-assets binding serves the public landing shell plus image and video files directly from Cloudflare; all other routes continue through the Worker to the App Service origin. It uses no R2, KV, paid observability, or paid Worker features.
- The landing page, privacy page, their stylesheet, promotional media, atlas art, and interface artwork are public. Until OIDC cutover, the shared application-level access code protects dashboard HTML, JavaScript, CSS, authentication routes, and game APIs. `/health` remains unauthenticated for deployment verification.
- Azure publish output excludes `wwwroot/assets` and `wwwroot/media`, so no video file is uploaded with the website package. Cloudflare uploads the public film from the repository's edge asset source and serves it directly from the custom domain. The repository defines `https://cycles.anthonypwatts.co.uk/media/cycles-promo.mp4` plus `https://cycles.anthonypwatts.co.uk/media/cycles-promo-poster.jpg` as the canonical public contract; consumers omit manual version queries and rely on Cloudflare revalidation and content-derived ETags. Once the coordinated edge revision is deployed, the former duration-based film path redirects permanently to the canonical URL. The deployed API redirects a direct-origin image or video request to the edge domain through `Cycles__EdgeAssetOrigin`; a media request incorrectly proxied from that domain fails with `502` rather than redirecting in a loop. The film is an 11.54 MiB web derivative of the retained 34.26 MiB production master and does not preload before a visitor chooses to play it.

App Service F1 enforces CPU, memory, and bandwidth quotas. If a CPU or bandwidth quota is exhausted, Azure stops the app until the quota resets rather than moving it to a paid compute tier. Azure SQL retains its monthly free allowance but may bill excess serverless usage within the fixed compute and storage envelope. This single-process playground does not deploy `Cycles.Worker`.

The final stopped `/home/data/cycles-state.json` file is retained off-host only as sensitive migration evidence for the cutover checkpoint. It is not updated after the SQL-backed site reopened, is not a rollback target, and must not be used for recovery. Database-native backup and point-in-time restore are authoritative after SQL-backed gameplay resumes.

## Managed-SQL Cutover

The playground was stopped on 2026-07-14 and its final atomic JSON state was converted to transfer format version 1, validated, and imported through the operator CLI. The raw snapshot SHA-256 is `8945C5856BB547D61F15BAA8E7A97C1E983656063A0F6DB83CA1E76FA4E2D665`; the versioned transfer SHA-256 is `559215E1C012A56BDA566084372FF780043654187832A74FFD4934ECB0B30665`. The SQL round trip preserved all 23 persisted collection counts and 166 records. The agreed checkpoint retained 4 players, 4 empires, 7 fleets, 4 orders, 42 events, 1 Chronicle entry, 3 tick logs, an active Cycle at tick 3, and no unresolved recovery. The reopened API passed health, access-gate, Development login, Cycle, fleet, order, and Chronicle checks against SQL.

Short-term backup retention is seven days. The restore point at `2026-07-14T16:58:31Z` was restored to isolated database `CyclesDbRestoreProof20260714`. Its schema was current with all 14 migrations, and the operator export reproduced all 23 collection counts, 166 records, active tick 3, and zero unresolved recovery. The paid restore target was deleted after verification; the live free-offer database was not modified. The logical server now contains only the live `CyclesDb` user database plus `master`.

Before the playground reopened, rollback could have returned to the frozen JSON checkpoint. SQL-backed login has now written newer state, so the frozen file is no longer a normal rollback target. Use Azure SQL point-in-time restore for recovery, investigate the isolated result, and deliberately decide whether to repair from it or replace the live database. Never silently switch back to stale JSON or dual-write both stores.

### Restore Rehearsal

Azure SQL point-in-time restore creates a new paid database. Use the smallest compatible serverless target, validate it promptly, and delete only that named target after recording evidence:

```powershell
$resourceGroup = "rg-cycles-playground-uks"
$server = "cycles-sql-b366b760"
$source = "CyclesDb"
$destination = "CyclesDbRestoreProof-$(Get-Date -Format yyyyMMddHHmmss)"
$restorePointUtc = "<UTC point at or after earliestRestoreDate>"

az sql db restore `
  --resource-group $resourceGroup `
  --server $server `
  --name $source `
  --dest-name $destination `
  --time $restorePointUtc `
  --edition GeneralPurpose `
  --family Gen5 `
  --capacity 2 `
  --compute-model Serverless `
  --auto-pause-delay 60 `
  --min-capacity 0.5 `
  --backup-storage-redundancy Local `
  --zone-redundant false

# Derive a connection string for the isolated database without printing its secret,
# then verify schema, export through the authoritative store, and validate the export.
dotnet run --project src/Cycles.Cli -- db status "sqlserver:$restoreConnectionString"
dotnet run --project src/Cycles.Cli -- state export "sqlserver:$restoreConnectionString" C:\secure\restore-proof-v1.json
dotnet run --project src/Cycles.Cli -- state validate C:\secure\restore-proof-v1.json

# Destructive: verify the exact temporary name before running this cleanup.
az sql db delete --resource-group $resourceGroup --server $server --name $destination --yes
```

Compare every persisted collection count plus the active Cycle/tick, players, empires, fleets, orders, events, Chronicle entries, tick logs, and unresolved recovery state. Do not connect the application to the rehearsal database.

## Deployment

The `Deploy playground` workflow is manual and accepts only `main`. CI continues to build and test every push, while verified revisions are batched into deliberate release windows rather than publishing every commit.

Required GitHub environment variables on the `playground` environment:

- `AZURE_CLIENT_ID`;
- `AZURE_TENANT_ID`;
- `AZURE_SUBSCRIPTION_ID`;
- `AZURE_WEBAPP_NAME`.

Authentication rollout uses two additional repository/environment variables. Leave them absent for the safe Development-selector defaults until the coordinated cutover; set them to `Playground` and `Oidc` together at cutover:

- `PLAYGROUND_HOST_ENVIRONMENT`;
- `PLAYGROUND_AUTHENTICATION_MODE`.

`AZURE_WEBAPP_DEPLOY_ENABLED` is a repository variable, set to `true` only while the access-restricted playground is intended to be online. It cannot be environment-scoped because GitHub evaluates the job-level deployment condition before attaching the `playground` environment and its variables.

Every dispatch selects one `database_action`:

- `none` publishes the API without restoring the operator CLI, stopping the API, retrieving the SQL connection string, or connecting to the database;
- `migrate` applies pending schema migrations before the API is published;
- `migrate-and-upgrade` applies migrations and then runs the guarded legacy-Game topology upgrade;
- `reseed` applies migrations, replaces the disposable Development state with the canonical opening, and prints the resulting state.

Use `none` for ordinary application-only releases. Select `migrate` when a batched revision introduces a database migration, and reserve the other two actions for their explicit operator purposes. Database actions are queued rather than cancelled by a later dispatch so an in-progress maintenance window cannot be abandoned with the API stopped.

Before any database action, the workflow reads Azure Monitor's `free_amount_remaining` metric without opening a SQL connection. It budgets 75,000 of the 100,000 monthly free vCore-seconds for engineering work, paced by calendar day, and reserves 25,000 for invited play. Falling ahead of that pace, entering the reserve, or receiving no usable allowance metric blocks maintenance before the connection string is retrieved. `override_sql_budget` is a one-dispatch acknowledgement for deliberate exceptional work; with `BillOverUsage`, using it may incur charges.

The workflow configures the edge-asset origin plus the host environment and authentication mode from `PLAYGROUND_HOST_ENVIRONMENT` and `PLAYGROUND_AUTHENTICATION_MODE`. Their safe fallbacks are `Development` and `DevelopmentSelector`. At OIDC cutover they change together to `Playground` and `Oidc`; Google credentials, invitations and the origin secret remain protected Azure settings rather than workflow text. The deployment attempts to restart the app even when explicit maintenance or deployment fails and verifies direct-origin `/health` after a successful path.

The 15 July 2026 deployment upgraded the preserved tick-3 opening in place: migration `015_add_galaxy_sectors` was applied, 16 sectors and 256 systems were added, 36 superseded routes were replaced, and the deployed API then reported 16 sectors, 280 systems, 296 routes, 32 gateways, sector sizes from 12 to 24, exactly two inter-sector routes per sector, and at most one bridge per gateway. Both the direct Azure health endpoint and the custom-domain health endpoint returned `200` after deployment.

Later on 15 July, manual workflow run `29446689039` used the explicit `reseed` input to replace that disposable crown with the compact territorial graph and deploy revision `a4242a0`. Independent custom-domain verification reported `200` from `/health`, `401` from an unauthenticated root request, successful access exchange and Development login, and `200` from the protected dashboard. The live `/galaxy` response contained 8 sectors, 64 systems, 93 routes, 13 inter-sector bridges, 16 gateway systems, and exactly 8 systems per sector. Nine gateways served more than one bridge and the maximum gateway fan-out was three. A deployed-browser render showed the three-range toolbar, 8 visible Galaxy territories, 13 visible bridges, and no console errors.

Set `AZURE_WEBAPP_DEPLOY_ENABLED=false` and stop the web app while the edge-access restriction is absent or under maintenance. Manual dispatches then skip deployment rather than restarting a public Development-auth origin.

The Cloudflare edge is defined under `deploy/cloudflare`. Before development or deployment, Wrangler runs `build-public-assets.js` and recreates the ignored `.public-assets` staging directory from a narrow source allowlist. The bundle contains only `index.html`, `privacy.html`, `site.css`, the canonical film and poster, and approved artwork directories and extensions. It cannot contain dashboard HTML, JavaScript or CSS, the admiral catalogue, Markdown production notes, or another repository file merely because that file exists under `wwwroot`.

Wrangler points its static-assets binding only at that generated bundle and uses asset-first routing. A matching public file is therefore served without invoking the Worker. Files absent from the bundle, authentication and API requests fall through to `worker.js` and the Azure proxy. The Worker retains its public-path fallback so a missing approved edge asset fails at Cloudflare instead of consuming Azure bandwidth. The generated bundle remains a deployment staging artefact and must not be edited or committed.

Cloudflare deployment remains separate from the normal application deployment because it requires a short-lived Cloudflare token. Create a token with only `Workers Scripts: Write` and `Workers Routes: Write`, deploy from `deploy/cloudflare`, and delete the token immediately afterwards. Do not store a Cloudflare deployment token in GitHub. The long-lived `ORIGIN_AUTH_TOKEN` Worker secret is a different high-entropy value used only to authenticate Cloudflare to Azure in OIDC mode:

```powershell
npm test
npx wrangler deploy --dry-run
npx wrangler deploy
```

The custom Wrangler build runs for both dry runs and deployments. To add a public asset, place it in an existing approved source directory and extension, or make the smallest explicit addition to `publicFiles` or `publicDirectoryRules` in `build-public-assets.js`; extend `public-assets.test.js` with an expected public path and a representative forbidden neighbour. Do not broaden a rule to the whole `assets`, `media`, or `wwwroot` tree.

Deploy Cloudflare before publishing an API revision that changes or removes edge assets, and before publishing an external consumer of a new canonical asset URL. Verify the canonical film and poster paths before updating the consumer. The Azure package deliberately has no media fallback, so this ordering prevents a missing-asset window while retaining the hard bandwidth boundary.

The 17 July 2026 media refresh deployed Cloudflare Worker version `e0a90e57-4996-4f5b-97d1-c4e3d003311c`. The public root returned `200`, unauthenticated `/app.html` returned the access form with `401`, the refreshed video returned `200` with `CF-Cache-Status: HIT`, and the direct Azure video URL returned `307` to the same custom-domain asset. Live browser playback reached ready state 4, and the landing page loaded the hero image once through its matching preload and CSS URL without console errors. A Release website publish contained zero video, `assets`, or `media` files.

The stable promo contract was subsequently deployed as Cloudflare Worker version `a190e52e-979f-4ee5-a976-3c62edad5aaf`. Unauthenticated GETs returned `200` for the canonical 12,101,132-byte `video/mp4` film and 441,039-byte `image/jpeg` poster; both responses reported `CF-Cache-Status: HIT`, `Cache-Control: public, max-age=0, must-revalidate`, and content-derived ETags. GET and HEAD requests for `/media/cycles-promo-30s.mp4` returned `308` with `Location: /media/cycles-promo.mp4`, and the direct Azure canonical request returned `307` to the full custom-domain URL. The public landing page returned `200` and referenced the canonical film and poster without query strings. Chromium loaded the film from that URL, reached ready state 4, advanced playback, and reported no media or console error.

## Invited Access

`CYCLES_PLAYGROUND_ACCESS_CODE` enables the access gate. Keep the generated value only in the Azure App Service setting and a password manager; never commit it or add it to GitHub. Anthony and Will use the same code and receive separate seven-day, secure, HTTP-only browser cookies. Rotate the setting to revoke every existing playground cookie.

Anonymous visitors may read the public landing page, play its Cloudflare-served film, and view static artwork. Following **Enter the Build** opens `/app.html`, which requires the shared code before any dashboard HTML, JavaScript, CSS, authentication route, or game API is served. A successful code exchange redirects directly to `/app.html`. The **trusted playground** label applies to this gated application surface, not to the open landing page.

This shared-code gate is a trusted-playground exception, not production identity. Cloudflare Zero Trust was considered but not activated because its checkout required a payment card and offered authorisation for usage over the free allowance. The later OIDC decision replaces this shared gate directly rather than adding another paid perimeter.

At cutover, direct Google OIDC replaces both this shared access code and the hosted selector. Anthony and Will then authenticate separately and bind to their existing Players; the public landing, privacy and health boundaries remain unchanged. See the [Google OIDC cutover runbook](oidc-cutover.md).

## Cost Guardrails

- Keep the App Service plan on SKU `F1`.
- App Service F1 enforces a daily outbound-data allowance. Static media should not contribute to it after the edge cutover. If Azure reports the site state as `QuotaExceeded`, inspect the plan's `/usages` resource, leave paid scaling disabled, and wait for the reported `nextResetTime`. Pause manual deployment while the quota is exhausted, then investigate direct-origin or unexpectedly proxied traffic before re-enabling deployment.
- Keep `CyclesDb` on the free serverless offer with 2 vCores maximum, 0.5 vCores minimum, 32 GB maximum size, locally redundant backups, provider-default idle auto-pause, and free-limit exhaustion behaviour `BillOverUsage`. The paid-overage choice is permanent for this database; do not enlarge its compute or storage envelope.
- Treat 75,000 free vCore-seconds as the monthly engineering ceiling and preserve the remaining 25,000 for invited play. The deployment preflight enforces both the reserve and calendar pace for operator SQL work; do not use its override for routine releases.
- Run broad SQL suites and gameplay smoke checks against the disposable local/CI SQL Server. When deployed verification is necessary, perform migration, smoke, browser and OIDC checks together after one batched release rather than spreading them across separate wake windows.
- Do not add another persistent database, continuously running Worker, Container Apps environment, Azure Container Registry, private endpoint, Application Insights resource, Log Analytics workspace, or paid App Service feature to this playground. A restore-rehearsal database must be isolated, validated, and deleted immediately after evidence is recorded.
- Treat Azure budgets as notifications only. The requested one-unit monthly resource-group budget could not be created because the current Azure identity lacks budget-write authority. F1 quotas, SQL serverless auto-pause, fixed capacity and management locks remain the available controls.
- Keep the `cycles-f1-read-only` resource lock on the App Service plan. Remove it only for an intentional, reviewed plan change or teardown.
- Keep the `cycles-sql-config-read-only` resource lock on `CyclesDb`. It does not block application data access; remove it only for an intentional, reviewed database configuration change or teardown.
- Keep the `cycles-playground-free-only` policy assignment enforced on the resource group. It permits the approved Azure SQL resources but continues to deny Container Apps, container registry, Application Insights, and Log Analytics resources in this scope.
- Keep the playable application surface restricted. This environment uses development authentication and is not suitable for untrusted application access even though its non-sensitive landing page is public.
- The database cutover and restore gate is complete; later tester scope remains governed by the guided-play, Worker-operation, and security gates in the project backlog.
- Keep temporary restore-rehearsal databases isolated and delete them as soon as their recorded evidence is complete. No restore-proof database should remain during normal playground operation.
- Keep the Cloudflare Workers subscription on Free. Static assets must remain below the Free plan's 25 MiB per-file limit. Do not enable R2, a paid Workers plan, Zero Trust subscription, paid observability, or usage-overage authorisation for this playground.

## Verification

```powershell
az appservice plan show `
  --resource-group rg-cycles-playground-uks `
  --name asp-cycles-playground-ukw `
  --query '{sku:sku.name,tier:sku.tier}'

az sql db show `
  --resource-group rg-cycles-playground-uks `
  --server cycles-sql-b366b760 `
  --name CyclesDb `
  --query '{status:status,location:location,sku:sku.name,capacity:sku.capacity,minCapacity:minCapacity,autoPauseDelay:autoPauseDelay,maxSizeBytes:maxSizeBytes,useFreeLimit:useFreeLimit,freeLimitExhaustionBehavior:freeLimitExhaustionBehavior,backupStorage:currentBackupStorageRedundancy}'

az sql db str-policy show `
  --resource-group rg-cycles-playground-uks `
  --server cycles-sql-b366b760 `
  --name CyclesDb `
  --query '{retentionDays:retentionDays,diffBackupIntervalInHours:diffBackupIntervalInHours}'

az webapp config connection-string list `
  --resource-group rg-cycles-playground-uks `
  --name cycles-play-b366b760 `
  --query '[].name'

az webapp config appsettings list `
  --resource-group rg-cycles-playground-uks `
  --name cycles-play-b366b760 `
  --query "contains([].name, 'CYCLES_PLAYGROUND_ACCESS_CODE')"

az lock list `
  --resource-group rg-cycles-playground-uks `
  --query "[?name=='cycles-f1-read-only'].{name:name,level:level}"

az policy assignment show `
  --resource-group rg-cycles-playground-uks `
  --name cycles-playground-free-only `
  --query '{enforcementMode:enforcementMode,scope:scope}'
```

The checks must continue to report `F1`/`Free`; an online or auto-paused `GP_S_Gen5` `CyclesDb` database with capacity 2, minimum 0.5, auto-pause 60, 32 GB maximum size, free-limit use enabled, `BillOverUsage` exhaustion behaviour, and local backup storage; no exceeded App Service quota after the daily reset; no leftover restore-proof user database; seven retention days; a single `Cycles` connection-string name without displaying its value; no obsolete state-path or SQL-activation setting; an access-code setting without displaying its value; a `ReadOnly` lock; and an enforced policy assignment. Public verification should report `200` plus `video/mp4` and a cache validator from `https://cycles.anthonypwatts.co.uk/media/cycles-promo.mp4`; `200` plus `image/jpeg` and a cache validator from the canonical poster; `308` from the former duration-based film path to the canonical film; unchanged Azure Data Out on repeated media requests; `200` from the direct Azure `/health`; `307` from a direct-origin image/video request to the matching custom-domain URL; `401` from an unauthenticated `/app.html`; and `200` from `/app.html` after exchanging the access code for the secure cookie.
