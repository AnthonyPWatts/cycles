# Agent Notes

Follow `AGENTS.md` and the maintained documents under `docs/`.

## Deploy Configuration (configured by /setup-deploy)

- Platform: .NET 10 LTS on Azure App Service F1 through GitHub Actions.
- Environment: `playground`.
- Azure application: `cycles-play-b366b760` in `rg-cycles-playground-uks`.
- Deploy workflow: `.github/workflows/deploy-playground.yml`.
- Deploy trigger: a successful `CI` push run on `main`, or a manual workflow dispatch.
- Deploy enable switch: GitHub repository variable `AZURE_WEBAPP_DEPLOY_ENABLED`; keep it `false` whenever the edge-access restriction is not verified. The Azure identity values remain scoped to the `playground` environment.
- Deploy status command: `gh run list --workflow deploy-playground.yml --limit 1`.
- Public URL: `https://cycles.anthonypwatts.co.uk` through a Cloudflare Worker on the Free plan.
- Current origin URL: `https://cycles-play-b366b760.azurewebsites.net`.
- Post-deploy health check: `https://cycles-play-b366b760.azurewebsites.net/health`.
- Pre-deploy verification: `.\eng\test.ps1`.

Both the public URL and direct origin require the application-level playground access code, except for `/health`. Preserve that gate, the Cloudflare Workers Free plan, the F1 plan, resource lock, paid-resource policy deny list, persistent JSON path, and no-simulation-Worker shape documented in `docs/playground-deployment.md`.
