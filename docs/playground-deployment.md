# Trusted Playground Deployment

The hosted playground is a deliberately constrained development environment for invited play-testing. It is not the production hosting, authentication, Worker, monitoring, or backup design.

## Runtime Shape

- `Cycles.Api` targets .NET 10 LTS and runs on the Azure App Service **F1 Free** plan.
- State is stored in `/home/data/cycles-state.json` on App Service's persistent filesystem.
- `Cycles.Worker` is not deployed. Invited players advance the simulation manually through the Development-only **Advance turn** capability.
- The application uses `ASPNETCORE_ENVIRONMENT=Development` so an empty store receives the curated Day One seed.
- GitHub Actions deploys a successful `main` build through workload identity federation. No long-lived Azure credential is stored in GitHub.
- A Cloudflare Worker on the Free plan proxies `https://cycles.anthonypwatts.co.uk` to the App Service origin. The Worker has no bindings, storage, observability, or paid features.
- Both the custom domain and the direct Azure origin are protected by the same application-level access code. `/health` remains unauthenticated for deployment verification.

App Service F1 enforces CPU, memory, bandwidth, and filesystem quotas. If a CPU or bandwidth quota is exhausted, Azure stops the app until the quota resets rather than moving it to a paid compute tier. The 1 GB filesystem quota bounds the JSON store. This single-process playground does not deploy `Cycles.Worker` or share the file with another process.

JSON is a deliberate playground exception, not a reversal of the SQL-backed runtime direction. It removes database and inter-region transfer cost exposure while keeping the trusted manual-turn test loop persistent across App Service restarts and deployments.

## Accepted Managed-SQL Cutover

The JSON exception describes the currently deployed state, but it is no longer the accepted destination for further tester invitations. Q116 requires the playground to move its existing game to managed SQL, retain at least seven days of database-native point-in-time recovery, and prove one isolated restore. [GitHub issue #125](https://github.com/AnthonyPWatts/cycles/issues/125) tracks the cutover.

The cutover must intentionally revise the cost guardrails below rather than bypass them. Q117 selects the existing SQL Server provider on managed Azure SQL, subject to a compatibility smoke test. Until that choice is implemented and verified, the current SQL-resource deny policy remains enforced and the JSON-backed deployment remains the truthful runtime description.

## Deployment

The `Deploy playground` workflow runs after a successful `CI` push build on `main`. It can also be invoked manually.

Required GitHub environment variables on the `playground` environment:

- `AZURE_CLIENT_ID`;
- `AZURE_TENANT_ID`;
- `AZURE_SUBSCRIPTION_ID`;
- `AZURE_WEBAPP_NAME`.

`AZURE_WEBAPP_DEPLOY_ENABLED` is a repository variable, set to `true` only while the access-restricted playground is intended to be online. It cannot be environment-scoped because GitHub evaluates the job-level deployment condition before attaching the `playground` environment and its variables.

The workflow publishes `src/Cycles.Api`, signs in to Azure through OpenID Connect, deploys the published output, explicitly starts a stopped app, and verifies `/health`.

Set `AZURE_WEBAPP_DEPLOY_ENABLED=false` and stop the web app while the edge-access restriction is absent or under maintenance. Successful CI runs then skip deployment rather than restarting a public Development-auth origin.

The Cloudflare proxy is defined under `deploy/cloudflare`. It is deliberately separate from the normal application deployment because the proxy is stable and its deployment requires a short-lived Cloudflare token. Create a token with only `Workers Scripts: Write` and `Workers Routes: Write`, deploy with Wrangler, and delete the token immediately afterwards. Do not store a Cloudflare token in GitHub.

## Invited Access

`CYCLES_PLAYGROUND_ACCESS_CODE` enables the access gate. Keep the generated value only in the Azure App Service setting and a password manager; never commit it or add it to GitHub. Anthony and Will use the same code and receive separate seven-day, secure, HTTP-only browser cookies. Rotate the setting to revoke every existing playground cookie.

This shared-code gate is a trusted-playground exception, not production identity. Cloudflare Zero Trust was considered but not activated because its checkout required a payment card and offered authorisation for usage over the free allowance. The hard-spend requirement takes precedence over per-email sign-in for this environment.

## Cost Guardrails

- Keep the App Service plan on SKU `F1`.
- Keep `WEBSITES_ENABLE_APP_SERVICE_STORAGE=true` and `CYCLES_STATE_PATH=/home/data/cycles-state.json`.
- Do not add a database, continuously running Worker, Container Apps environment, Azure Container Registry, private endpoint, Application Insights resource, Log Analytics workspace, or paid App Service feature to this playground.
- Treat Azure budgets as notifications only; the enforced F1 quotas are the actual spend controls.
- Keep the `cycles-f1-read-only` resource lock on the App Service plan. Remove it only for an intentional, reviewed plan change or teardown.
- Keep the `cycles-playground-free-only` policy assignment enforced on the resource group. It denies SQL, Container Apps, container registry, Application Insights, and Log Analytics resources in this scope.
- Keep access restricted at the edge. This environment uses development authentication and is not suitable for untrusted public access.
- Do not invite further testers until the accepted managed-SQL cutover, backup retention, and restore rehearsal are complete.
- Keep the Cloudflare Workers subscription on Free. Do not enable a paid Workers plan, Zero Trust subscription, paid observability, or usage-overage authorisation for this playground.

## Verification

```powershell
az appservice plan show `
  --resource-group rg-cycles-playground-uks `
  --name asp-cycles-playground-ukw `
  --query '{sku:sku.name,tier:sku.tier}'

az webapp config appsettings list `
  --resource-group rg-cycles-playground-uks `
  --name cycles-play-b366b760 `
  --query "[?name=='CYCLES_STATE_PATH' || name=='WEBSITES_ENABLE_APP_SERVICE_STORAGE'].{name:name,value:value}"

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

The checks must continue to report `F1`, `Free`, `/home/data/cycles-state.json`, `true`, an access-code setting without displaying its value, a `ReadOnly` lock, and an enforced policy assignment respectively. Public verification should report `200` from `https://cycles.anthonypwatts.co.uk/health`, `401` from an unauthenticated request to the root, and `200` after exchanging the access code for the secure cookie.
