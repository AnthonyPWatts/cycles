# Google OIDC Cutover

This runbook moves the hosted Cycles playground from the shared trusted-player selector to per-person Google OIDC identity. The application implementation is designed to land safely while deployed SQL is locked through 31 July 2026; the hosted identity switch and first-login database writes happen only after that lock ends.

## Implemented Boundary

- `Cycles:Authentication:Mode` is explicit. `DevelopmentSelector` is accepted only when `ASPNETCORE_ENVIRONMENT=Development`; hosted OIDC uses the non-Development `Playground` environment.
- Google uses the authorisation-code flow with PKCE and requests only `openid email`. Cycles does not retain Google tokens.
- A first login may bind only an existing active, unbound human Player. It requires Google's `email_verified=true` claim and an edge-trimmed, case-insensitive exact match to a configured invitation containing the target Player ID.
- The binding writes the provider issuer, subject, verified email and login time atomically. It never creates a Player, enrolment, participant, empire or admiral.
- Later logins use the case-sensitive issuer/subject pair. They do not require the invitation to remain configured and cannot rebind the Player through email.
- An invitation may bootstrap the target as administrator in the same transaction. The promotion writes the existing high-severity audit record. Anthony is the only initial administrator; Will remains a Player.
- The canonical interactive origin is `https://cycles.anthonypwatts.co.uk`. Cloudflare authenticates to Azure with a separate shared origin token. The API accepts forwarded host and scheme only with that token, redirects safe direct-origin requests to the canonical host, refuses direct-origin mutations, and leaves `/health` available on Azure.
- Standard-Game manual advancement is administrator-only in OIDC mode. Player-controlled self-paced Training resolution is unchanged.
- The landing and privacy pages remain public. Anonymous dashboard access starts the Google challenge; an expired browser session returns to that challenge rather than exposing the Development selector.

No schema migration is required. The deployed schema already contains the external issuer/subject columns, their binary comparison constraints and the admin audit table. The first intentional deployed database changes are the two first-login bindings after the lock.

## State During The SQL Lock

Keep these GitHub playground variables absent or at their safe defaults:

```text
PLAYGROUND_HOST_ENVIRONMENT=Development
PLAYGROUND_AUTHENTICATION_MODE=DevelopmentSelector
```

The deployment workflow defaults to those values, and automatic deployments do not run database maintenance. Do not configure invitations, remove the shared access code, switch the host environment, or perform a hosted Google login before 1 August 2026.

Code and public edge assets may deploy during the lock. In this state the OIDC services, routes and canonical-origin middleware are not activated, and the existing trusted selector remains the hosted path.

## Inputs Required For Cutover

Keep all of these outside source control and routine logs:

- Anthony's exact Google account email and Will's exact Google account email;
- the dedicated Google OAuth client ID and client secret;
- one newly generated high-entropy origin token of at least 32 characters, stored independently as the Cloudflare `ORIGIN_AUTH_TOKEN` secret and Azure `Cycles__Authentication__ProxySecret` setting;
- retained recovery access for Anthony's Google account, Google Cloud project, GitHub environment, Cloudflare account and Azure subscription.

The existing Player targets are:

| Person | Player ID | First-login role |
| --- | --- | --- |
| Anthony (`Tony`) | `2bbf6b63-b50f-4fe3-bc11-913c2b74aa01` | `Admin` through audited bootstrap |
| Will | `8bf77462-f7ce-4c67-8c20-29e4e1e5bb02` | `Player` |

## Google Setup In Testing

1. Create one dedicated Cycles Google Cloud project and an External OAuth application in Testing status.
2. Configure the application home page as `https://cycles.anthonypwatts.co.uk/` and privacy page as `https://cycles.anthonypwatts.co.uk/privacy.html`.
3. Register only `https://cycles.anthonypwatts.co.uk/signin-oidc` as the hosted redirect URI. Do not register the Azure hostname.
4. Request only `openid` and `email`; do not request offline access or other Google APIs.
5. Add Anthony and Will as the only test users and retain the client ID and secret in the Azure App Service configuration.

## Cutover Sequence After The Lock

1. Confirm the SQL lock has ended, record the current deployment revision and confirm an Azure SQL point-in-time restore point is available. Do not reseed or run an unrelated galaxy upgrade.
2. Deploy the current public Cloudflare assets, including `/privacy.html`, then set the Worker secret with `npx wrangler secret put ORIGIN_AUTH_TOKEN`. A configured Worker overwrites any client-supplied origin-token header.
3. Add the following Azure App Service settings without printing their values:

```text
Cycles__Authentication__Authority=https://accounts.google.com
Cycles__Authentication__ClientId=<google-client-id>
Cycles__Authentication__ClientSecret=<google-client-secret>
Cycles__Authentication__CanonicalHost=cycles.anthonypwatts.co.uk
Cycles__Authentication__ProxySecret=<same-origin-token-as-cloudflare>
Cycles__Authentication__DeploymentRevision=<deployed-git-revision>
Cycles__Authentication__Invitations__0__PlayerId=2bbf6b63-b50f-4fe3-bc11-913c2b74aa01
Cycles__Authentication__Invitations__0__Email=<anthony-google-email>
Cycles__Authentication__Invitations__0__BootstrapAdmin=true
Cycles__Authentication__Invitations__1__PlayerId=8bf77462-f7ce-4c67-8c20-29e4e1e5bb02
Cycles__Authentication__Invitations__1__Email=<will-google-email>
Cycles__Authentication__Invitations__1__BootstrapAdmin=false
```

4. Set the GitHub playground variables to `PLAYGROUND_HOST_ENVIRONMENT=Playground` and `PLAYGROUND_AUTHENTICATION_MODE=Oidc`. Remove `CYCLES_PLAYGROUND_ACCESS_CODE` and any obsolete trusted-selector setting from Azure, then deploy/restart the API.
5. Verify direct Azure `/health` returns `200`, a direct Azure safe application request redirects to the custom domain, and a direct Azure mutation is refused. Verify the custom-domain landing and privacy pages remain public.
6. Sign in as Anthony through Google. Confirm the session resolves the existing Tony Player, the Player is now Admin, exactly one bootstrap audit row exists for the deployment revision, no Player was inserted, and Standard-Game advancement is available.
7. Sign in as Will. Confirm the session resolves the existing Will Player, his role remains Player, no Player was inserted, Standard-Game advancement is unavailable, and his self-paced Training controls remain available.
8. Confirm an unknown or mismatched Google account receives the safe `403 not admitted` path and causes no Player or identity mutation. Confirm session expiry returns to Google sign-in and POST sign-out clears the Cycles session.
9. Remove both `Cycles__Authentication__Invitations` entries from Azure after both bindings are verified. Repeat both sign-ins to prove issuer/subject authentication no longer depends on email invitation configuration.
10. Complete Google's domain, branding and privacy checks, change the OAuth application from Testing to In production, and repeat the Anthony, Will and denial checks. Publishing the OAuth application does not weaken Cycles-owned admission.

## Rollback And Break Glass

If OIDC or proxy configuration fails before either first login, restore `Development` plus `DevelopmentSelector`, restore the shared access code from the password manager, and redeploy. This is a configuration rollback and does not require a database restore.

If one or both bindings have succeeded, leave those identity fields intact while diagnosing configuration. They are valid durable mappings and do not interfere with a temporary Development-selector rollback. Do not clear or replace issuer/subject values ad hoc. Identity replacement remains an explicit audited operator recovery action based on provider evidence.

If Anthony can authenticate but the admin bootstrap did not complete, use the documented focused admin recovery path only after checking the binding and audit transaction. If database integrity is uncertain, restore to an isolated Azure SQL database and investigate there; do not replace the live database merely to undo an authentication configuration error.

## Cost

This design adds no required paid service: Google identity-only OAuth, the existing Cloudflare Worker Free plan, Azure App Service F1 and the existing Azure SQL free offer remain the selected boundaries. Do not enable Google Identity Platform billing, broader Google APIs, Cloudflare paid features or Azure paid observability as part of this cutover.
