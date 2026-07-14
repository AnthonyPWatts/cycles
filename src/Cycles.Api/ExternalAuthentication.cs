using System.Security.Claims;
using Cycles.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

public static class CyclesAuthenticationSchemes
{
    public const string Cookie = "Cycles.ExternalCookie";
    public const string OpenIdConnect = "Cycles.ExternalOidc";
}

public static class CyclesClaimTypes
{
    public const string PlayerId = "cycles:player-id";
}

public sealed class ExternalAuthenticationOptions
{
    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string CallbackPath { get; set; } = "/signin-oidc";
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";
    public string[] InvitedIdentities { get; set; } = [];
    public string[] AdminBootstrapIdentities { get; set; } = [];
    public string DeploymentRevision { get; set; } = "unspecified";
    public string[] KnownProxies { get; set; } = [];

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Authority)
            || string.IsNullOrWhiteSpace(ClientId)
            || string.IsNullOrWhiteSpace(ClientSecret))
        {
            throw new InvalidOperationException("Non-Development hosts require Cycles:Authentication Authority, ClientId and ClientSecret configuration.");
        }

        foreach (var identity in InvitedIdentities.Concat(AdminBootstrapIdentities))
        {
            _ = ConfiguredExternalIdentity.Parse(identity);
        }
    }
}

public readonly record struct ConfiguredExternalIdentity(string Issuer, string Subject)
{
    public static ConfiguredExternalIdentity Parse(string value)
    {
        var separator = value.IndexOf('|');
        if (separator <= 0 || separator == value.Length - 1)
        {
            throw new InvalidOperationException("Configured external identities must use the exact 'issuer|subject' format.");
        }

        var issuer = value[..separator].Trim();
        var subject = value[(separator + 1)..].Trim();
        if (issuer.Length > 256 || subject.Length > 256)
        {
            throw new InvalidOperationException("Configured external identity issuer and subject values must each be 256 characters or fewer.");
        }

        return new ConfiguredExternalIdentity(issuer, subject);
    }

    public bool Matches(string issuer, string subject) =>
        string.Equals(Issuer, issuer, StringComparison.Ordinal)
        && string.Equals(Subject, subject, StringComparison.Ordinal);
}

public static class ExternalIdentityAdmission
{
    public static Player SignIn(
        GameState state,
        string issuer,
        string subject,
        string? preferredUsername,
        string? email,
        ExternalAuthenticationOptions options,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
        {
            throw new ApiForbiddenException("The identity provider did not supply a stable issuer and subject.");
        }

        if (issuer.Length > 256 || subject.Length > 256)
        {
            throw new ApiForbiddenException("The identity provider supplied an issuer or subject longer than the supported identity key.");
        }

        var invited = options.InvitedIdentities
            .Select(ConfiguredExternalIdentity.Parse)
            .Any(identity => identity.Matches(issuer, subject));
        var bootstrapped = options.AdminBootstrapIdentities
            .Select(ConfiguredExternalIdentity.Parse)
            .Any(identity => identity.Matches(issuer, subject));
        if (!invited && !bootstrapped)
        {
            throw new ApiForbiddenException("This external identity has not been invited to Cycles.");
        }

        var matches = state.Players
            .Where(player => string.Equals(player.ExternalIssuer, issuer, StringComparison.Ordinal)
                             && string.Equals(player.ExternalSubject, subject, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidOperationException("More than one local player is mapped to the same external identity.");
        }

        var player = matches.SingleOrDefault();
        if (player is null)
        {
            var safeUsername = NormaliseUsername(state, preferredUsername, subject);
            player = new Player
            {
                Username = safeUsername,
                Email = NormaliseEmail(email),
                PasswordHash = "",
                ExternalIssuer = issuer,
                ExternalSubject = subject,
                Role = PlayerRole.Player,
                CreatedAt = now,
                LastLoginAt = now,
                Status = PlayerStatus.Active
            };
            state.Players.Add(player);

            var cycle = state.GetActiveCycle()
                ?? throw new InvalidOperationException("No active Cycle exists for invited-player provisioning.");
            PlayerProvisioning.AddEmpireForPlayer(state, cycle, player, null, now);
        }

        if (player.Status != PlayerStatus.Active)
        {
            throw new ApiForbiddenException("The mapped Cycles player is not active.");
        }

        player.LastLoginAt = now;
        if (bootstrapped)
        {
            AdminRoleService.ApplyBootstrap(
                state,
                player,
                $"configuration:{options.DeploymentRevision}",
                "Applied explicitly configured initial administrator identity.",
                now);
        }

        return player;
    }

    private static string NormaliseUsername(GameState state, string? preferredUsername, string subject)
    {
        var candidate = string.IsNullOrWhiteSpace(preferredUsername)
            ? $"external-{subject[..Math.Min(subject.Length, 12)]}"
            : preferredUsername.Trim();
        candidate = candidate[..Math.Min(candidate.Length, 80)];
        if (!state.Players.Any(player => string.Equals(player.Username, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            return candidate;
        }

        var suffix = $"-{subject[^Math.Min(subject.Length, 8)..]}";
        var prefixLength = Math.Max(1, 80 - suffix.Length);
        return $"{candidate[..Math.Min(candidate.Length, prefixLength)]}{suffix}";
    }

    private static string NormaliseEmail(string? email)
    {
        var candidate = email?.Trim() ?? "";
        return candidate[..Math.Min(candidate.Length, 256)];
    }
}

public static class ExternalAuthenticationExtensions
{
    public static ExternalAuthenticationOptions AddExternalCyclesAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var cyclesOptions = configuration
            .GetSection("Cycles:Authentication")
            .Get<ExternalAuthenticationOptions>()
            ?? new ExternalAuthenticationOptions();
        cyclesOptions.Validate();
        services.AddSingleton(cyclesOptions);

        services
            .AddAuthentication(authentication =>
            {
                authentication.DefaultScheme = CyclesAuthenticationSchemes.Cookie;
                authentication.DefaultAuthenticateScheme = CyclesAuthenticationSchemes.Cookie;
                authentication.DefaultSignInScheme = CyclesAuthenticationSchemes.Cookie;
                authentication.DefaultChallengeScheme = CyclesAuthenticationSchemes.OpenIdConnect;
            })
            .AddCookie(CyclesAuthenticationSchemes.Cookie, cookie =>
            {
                cookie.Cookie.Name = "__Host-CyclesSession";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.Path = "/";
                cookie.ExpireTimeSpan = TimeSpan.FromHours(12);
                cookie.SlidingExpiration = true;
            })
            .AddOpenIdConnect(CyclesAuthenticationSchemes.OpenIdConnect, oidc =>
            {
                oidc.Authority = cyclesOptions.Authority;
                oidc.ClientId = cyclesOptions.ClientId;
                oidc.ClientSecret = cyclesOptions.ClientSecret;
                oidc.CallbackPath = cyclesOptions.CallbackPath;
                oidc.SignedOutCallbackPath = cyclesOptions.SignedOutCallbackPath;
                oidc.ResponseType = OpenIdConnectResponseType.Code;
                oidc.UsePkce = true;
                oidc.SaveTokens = false;
                oidc.MapInboundClaims = false;
                oidc.TokenValidationParameters.NameClaimType = "name";
                oidc.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = context =>
                    {
                        var issuer = context.SecurityToken.Issuer;
                        var subject = context.Principal?.FindFirstValue("sub");
                        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
                        {
                            context.Fail("The provider identity did not contain issuer and subject claims.");
                            return Task.CompletedTask;
                        }

                        try
                        {
                            var store = context.HttpContext.RequestServices.GetRequiredService<IGameStateStore>();
                            var player = store.Update(state => ExternalIdentityAdmission.SignIn(
                                state,
                                issuer,
                                subject,
                                context.Principal?.FindFirstValue("preferred_username")
                                    ?? context.Principal?.Identity?.Name,
                                context.Principal?.FindFirstValue("email"),
                                cyclesOptions,
                                DateTimeOffset.UtcNow));
                            ((ClaimsIdentity)context.Principal!.Identity!).AddClaim(
                                new Claim(CyclesClaimTypes.PlayerId, player.PlayerId.ToString("D")));
                        }
                        catch (Exception exception) when (ApiErrorResponses.IsHandled(exception))
                        {
                            context.Fail(exception.Message);
                        }

                        return Task.CompletedTask;
                    },
                    OnRemoteFailure = context =>
                    {
                        context.HandleResponse();
                        context.Response.Redirect("/auth/error?code=externalAuthenticationFailed");
                        return Task.CompletedTask;
                    },
                    OnAccessDenied = context =>
                    {
                        context.HandleResponse();
                        context.Response.Redirect("/auth/error?code=accessDenied");
                        return Task.CompletedTask;
                    }
                };
            });
        services.AddAuthorization();
        return cyclesOptions;
    }
}
