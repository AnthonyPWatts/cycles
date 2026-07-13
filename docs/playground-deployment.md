# Trusted Playground Deployment

The hosted playground is a deliberately constrained development environment for invited play-testing. It is not the production hosting, authentication, Worker, monitoring, or backup design.

## Runtime Shape

- `Cycles.Api` targets .NET 10 LTS and runs on the Azure App Service **F1 Free** plan.
- State is stored in `/home/data/cycles-state.json` on App Service's persistent filesystem.
- `Cycles.Worker` is not deployed. Invited players advance the simulation manually through the Development-only **Advance turn** capability.
- The application uses `ASPNETCORE_ENVIRONMENT=Development` so an empty store receives the curated Day One seed.
- GitHub Actions deploys a successful `main` build through workload identity federation. No long-lived Azure credential is stored in GitHub.

App Service F1 enforces CPU, memory, bandwidth, and filesystem quotas. If a CPU or bandwidth quota is exhausted, Azure stops the app until the quota resets rather than moving it to a paid compute tier. The 1 GB filesystem quota bounds the JSON store. This single-process playground does not deploy the Worker or share the file with another process.

JSON is a deliberate playground exception, not a reversal of the SQL-backed runtime direction. It removes database and inter-region transfer cost exposure while keeping the trusted manual-turn test loop persistent across App Service restarts and deployments.

## Deployment

The `Deploy playground` workflow runs after a successful `CI` push build on `main`. It can also be invoked manually.

Required GitHub environment variables on the `playground` environment:

- `AZURE_CLIENT_ID`;
- `AZURE_TENANT_ID`;
- `AZURE_SUBSCRIPTION_ID`;
- `AZURE_WEBAPP_NAME`.

The workflow publishes `src/Cycles.Api`, signs in to Azure through OpenID Connect, deploys the published output, and verifies `/health`.

## Cost Guardrails

- Keep the App Service plan on SKU `F1`.
- Keep `WEBSITES_ENABLE_APP_SERVICE_STORAGE=true` and `CYCLES_STATE_PATH=/home/data/cycles-state.json`.
- Do not add a database, continuously running Worker, Container Apps environment, Azure Container Registry, private endpoint, Application Insights resource, Log Analytics workspace, or paid App Service feature to this playground.
- Treat Azure budgets as notifications only; the enforced F1 quotas are the actual spend controls.
- Keep the `cycles-f1-read-only` resource lock on the App Service plan. Remove it only for an intentional, reviewed plan change or teardown.
- Keep the `cycles-playground-free-only` policy assignment enforced on the resource group. It denies SQL, Container Apps, container registry, Application Insights, and Log Analytics resources in this scope.
- Keep access restricted at the edge. This environment uses development authentication and is not suitable for untrusted public access.

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

az lock show `
  --resource-group rg-cycles-playground-uks `
  --resource-type Microsoft.Web/serverfarms `
  --name cycles-f1-read-only

az policy assignment show `
  --resource-group rg-cycles-playground-uks `
  --name cycles-playground-free-only `
  --query '{enforcementMode:enforcementMode,scope:scope}'
```

The checks must continue to report `F1`, `Free`, `/home/data/cycles-state.json`, `true`, a `ReadOnly` lock, and an enforced policy assignment respectively.
