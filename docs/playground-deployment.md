# Trusted Playground Deployment

The hosted playground is a deliberately constrained development environment for invited play-testing. It is not the production hosting, authentication, Worker, monitoring, or backup design.

## Runtime Shape

- `Cycles.Api` targets .NET 10 LTS and runs on the Azure App Service **F1 Free** plan.
- Authoritative state is stored in Azure SQL database `CyclesDb` on logical server `cycles-sql-b366b760` in France Central. The UK regions do not permit this subscription to provision the selected tier.
- The database uses the Azure SQL free serverless offer: General Purpose Gen5, 2 vCores maximum, 0.5 vCores minimum, the provider-required 60-minute auto-pause, 32 GB maximum data size, locally redundant backup storage, and `AutoPause` when the monthly free allowance is exhausted.
- `Cycles.Worker` is not deployed. Invited players advance the simulation manually through the Development-only **Advance turn** capability.
- The application uses `ASPNETCORE_ENVIRONMENT=Development` so an empty store receives the canonical 8-sector, 64-system Day One seed.
- GitHub Actions deploys a successful `main` build through workload identity federation. No long-lived Azure credential is stored in GitHub.
- A Cloudflare Worker on the Free plan proxies `https://cycles.anthonypwatts.co.uk` to the App Service origin. The Worker has no bindings, storage, observability, or paid features.
- The landing page, its stylesheet and promotional media remain public on both the custom domain and direct Azure origin. The shared application-level access code protects the dashboard, application assets, authentication routes, and game APIs. `/health` also remains unauthenticated for deployment verification.

App Service F1 enforces CPU, memory, and bandwidth quotas. If a CPU or bandwidth quota is exhausted, Azure stops the app until the quota resets rather than moving it to a paid compute tier. Azure SQL is separately configured to stop at its free monthly allowance rather than bill for overage. This single-process playground does not deploy `Cycles.Worker`.

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

The `Deploy playground` workflow runs after a successful `CI` push build on `main`. It can also be invoked manually.

Required GitHub environment variables on the `playground` environment:

- `AZURE_CLIENT_ID`;
- `AZURE_TENANT_ID`;
- `AZURE_SUBSCRIPTION_ID`;
- `AZURE_WEBAPP_NAME`.

`AZURE_WEBAPP_DEPLOY_ENABLED` is a repository variable, set to `true` only while the access-restricted playground is intended to be online. It cannot be environment-scoped because GitHub evaluates the job-level deployment condition before attaching the `playground` environment and its variables.

The workflow publishes `src/Cycles.Api` and restores the operator CLI, signs in to Azure through OpenID Connect, then stops the app while it applies pending migrations and prepares the canonical galaxy. Normal automatic deployments use the guarded topology upgrade; a manually dispatched deployment must explicitly select `reseed` before the authoritative seed command replaces the disposable Development state. Migration connection attempts are retried with a short back-off because the serverless database may need to wake from auto-pause. It deploys the published output, attempts to restart the app even when maintenance or deployment fails, and verifies `/health` after a successful path.

The 15 July 2026 deployment upgraded the preserved tick-3 opening in place: migration `015_add_galaxy_sectors` was applied, 16 sectors and 256 systems were added, 36 superseded routes were replaced, and the deployed API then reported 16 sectors, 280 systems, 296 routes, 32 gateways, sector sizes from 12 to 24, exactly two inter-sector routes per sector, and at most one bridge per gateway. Both the direct Azure health endpoint and the custom-domain health endpoint returned `200` after deployment.

Later on 15 July, manual workflow run `29446689039` used the explicit `reseed` input to replace that disposable crown with the compact territorial graph and deploy revision `a4242a0`. Independent custom-domain verification reported `200` from `/health`, `401` from an unauthenticated root request, successful access exchange and Development login, and `200` from the protected dashboard. The live `/galaxy` response contained 8 sectors, 64 systems, 93 routes, 13 inter-sector bridges, 16 gateway systems, and exactly 8 systems per sector. Nine gateways served more than one bridge and the maximum gateway fan-out was three. A deployed-browser render showed the three-range toolbar, 8 visible Galaxy territories, 13 visible bridges, and no console errors.

Set `AZURE_WEBAPP_DEPLOY_ENABLED=false` and stop the web app while the edge-access restriction is absent or under maintenance. Successful CI runs then skip deployment rather than restarting a public Development-auth origin.

The Cloudflare proxy is defined under `deploy/cloudflare`. It is deliberately separate from the normal application deployment because the proxy is stable and its deployment requires a short-lived Cloudflare token. Create a token with only `Workers Scripts: Write` and `Workers Routes: Write`, deploy with Wrangler, and delete the token immediately afterwards. Do not store a Cloudflare token in GitHub.

## Invited Access

`CYCLES_PLAYGROUND_ACCESS_CODE` enables the access gate. Keep the generated value only in the Azure App Service setting and a password manager; never commit it or add it to GitHub. Anthony and Will use the same code and receive separate seven-day, secure, HTTP-only browser cookies. Rotate the setting to revoke every existing playground cookie.

Anonymous visitors may read the public landing page and promotional media. Following **Enter the Build** opens `/app.html`, which requires the shared code before any dashboard, application asset, authentication route, or game API is served. A successful code exchange redirects directly to `/app.html`.

This shared-code gate is a trusted-playground exception, not production identity. Cloudflare Zero Trust was considered but not activated because its checkout required a payment card and offered authorisation for usage over the free allowance. The hard-spend requirement takes precedence over per-email sign-in for this environment.

## Cost Guardrails

- Keep the App Service plan on SKU `F1`.
- Keep `CyclesDb` on the free serverless offer with 2 vCores maximum, 0.5 vCores minimum, 32 GB maximum size, locally redundant backups, provider-default auto-pause, and free-limit exhaustion behaviour `AutoPause`. Do not select `BillOverUsage`.
- Do not add another persistent database, continuously running Worker, Container Apps environment, Azure Container Registry, private endpoint, Application Insights resource, Log Analytics workspace, or paid App Service feature to this playground. A restore-rehearsal database must be isolated, validated, and deleted immediately after evidence is recorded.
- Treat Azure budgets as notifications only; the F1 quotas and Azure SQL free-limit exhaustion setting are the enforced spend controls.
- Keep the `cycles-f1-read-only` resource lock on the App Service plan. Remove it only for an intentional, reviewed plan change or teardown.
- Keep the `cycles-playground-free-only` policy assignment enforced on the resource group. It permits the approved Azure SQL resources but continues to deny Container Apps, container registry, Application Insights, and Log Analytics resources in this scope.
- Keep the playable application surface restricted. This environment uses development authentication and is not suitable for untrusted application access even though its non-sensitive landing page is public.
- The database cutover and restore gate is complete; later tester scope remains governed by the guided-play, Worker-operation, and security gates in the project backlog.
- Keep temporary restore-rehearsal databases isolated and delete them as soon as their recorded evidence is complete. No restore-proof database should remain during normal playground operation.
- Keep the Cloudflare Workers subscription on Free. Do not enable a paid Workers plan, Zero Trust subscription, paid observability, or usage-overage authorisation for this playground.

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

The checks must continue to report `F1`/`Free`; an online or auto-paused `GP_S_Gen5` `CyclesDb` database with capacity 2, minimum 0.5, auto-pause 60, 32 GB maximum, free-limit use enabled, `AutoPause` exhaustion behaviour, and local backup storage; no leftover restore-proof user database; seven retention days; a single `Cycles` connection-string name without displaying its value; no obsolete state-path or SQL-activation setting; an access-code setting without displaying its value; a `ReadOnly` lock; and an enforced policy assignment. Public verification should report `200` from both `https://cycles.anthonypwatts.co.uk/` and `/health`, `401` from an unauthenticated request to `/app.html`, and `200` from `/app.html` after exchanging the access code for the secure cookie.
