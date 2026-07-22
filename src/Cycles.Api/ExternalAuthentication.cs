using System.Security.Claims;
using Cycles.Application;
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

public static class ExternalAuthenticationFailureCodes
{
    public const string AccessDenied = "accessDenied";
    public const string ExternalAuthenticationFailed = "externalAuthenticationFailed";
    public const string NotAdmitted = "notAdmitted";
    public const string TemporarilyBusy = "temporarilyBusy";

    private const string FailureCodeItemKey = "Cycles.ExternalAuthentication.FailureCode";

    public static void MarkTemporarilyBusy(HttpContext httpContext) =>
        httpContext.Items[FailureCodeItemKey] = TemporarilyBusy;

    public static void MarkNotAdmitted(HttpContext httpContext) =>
        httpContext.Items[FailureCodeItemKey] = NotAdmitted;

    public static string ResolveRemoteFailure(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(FailureCodeItemKey, out var value)
            && value is string code
            ? code
            : ExternalAuthenticationFailed;
}

public sealed class ExternalAuthenticationOptions
{
    public string Authority { get; set; } = "https://accounts.google.com";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string CallbackPath { get; set; } = "/signin-oidc";
    public string CanonicalHost { get; set; } = "";
    public string ProxySecret { get; set; } = "";
    public ExternalAuthenticationInvitation[] Invitations { get; set; } = [];
    public string DeploymentRevision { get; set; } = "unspecified";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Authority)
            || string.IsNullOrWhiteSpace(ClientId)
            || string.IsNullOrWhiteSpace(ClientSecret))
        {
            throw new InvalidOperationException("Non-Development hosts require Cycles:Authentication Authority, ClientId and ClientSecret configuration.");
        }

        foreach (var invitation in Invitations)
        {
            invitation.Validate();
        }

        if (!Uri.TryCreate($"https://{CanonicalHost}", UriKind.Absolute, out var canonicalUri)
            || !string.Equals(canonicalUri.Authority, CanonicalHost, StringComparison.OrdinalIgnoreCase)
            || canonicalUri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(canonicalUri.Query)
            || !string.IsNullOrEmpty(canonicalUri.Fragment))
        {
            throw new InvalidOperationException(
                "Cycles:Authentication:CanonicalHost must be a hostname without a scheme or path.");
        }

        if (ProxySecret.Length < 32)
        {
            throw new InvalidOperationException(
                "Cycles:Authentication:ProxySecret must contain at least 32 characters.");
        }

        if (Invitations.GroupBy(item => item.PlayerId).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("Cycles:Authentication:Invitations contains a duplicate PlayerId.");
        }

        if (Invitations
            .GroupBy(item => item.Email.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("Cycles:Authentication:Invitations contains a duplicate email address.");
        }
    }
}

public sealed class ExternalAuthenticationInvitation
{
    public Guid PlayerId { get; set; }

    public string Email { get; set; } = "";

    public bool BootstrapAdmin { get; set; }

    public void Validate()
    {
        if (PlayerId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Each Cycles:Authentication:Invitations entry requires a PlayerId.");
        }

        var email = Email.Trim();
        if (email.Length == 0 || email.Length > 256)
        {
            throw new InvalidOperationException(
                "Each Cycles:Authentication:Invitations entry requires an email address of 256 characters or fewer.");
        }
    }

    public bool MatchesVerifiedEmail(string email) =>
        string.Equals(Email.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase);
}

public static class ExternalIdentityAdmission
{
    public static PlayerAccountSnapshot SignIn(
        IPlayerAccountCommandStore accounts,
        string issuer,
        string subject,
        string? email,
        bool emailVerified,
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

        var invitation = emailVerified && !string.IsNullOrWhiteSpace(email)
            ? options.Invitations.SingleOrDefault(item => item.MatchesVerifiedEmail(email))
            : null;
        var binding = invitation is null
            ? null
            : new ExternalPlayerBinding(
                invitation.PlayerId,
                email!,
                invitation.BootstrapAdmin
                    ? new ConfiguredAdminBootstrap($"configuration:{options.DeploymentRevision}")
                    : null);
        var result = accounts.SignInExternal(new ExternalPlayerSignInCommand(
            issuer,
            subject,
            binding,
            now));
        return result switch
        {
            AccountCommandResult<ExternalPlayerSignInSnapshot>.Success success => success.Value.Player,
            AccountCommandResult<ExternalPlayerSignInSnapshot>.Unavailable =>
                throw new ApiForbiddenException("The mapped Cycles player is not available for sign-in."),
            AccountCommandResult<ExternalPlayerSignInSnapshot>.Busy =>
                throw new ApiStateConflictException("Player account sign-in is temporarily busy. Try again."),
            _ => throw new InvalidOperationException("The player account store returned an unsupported sign-in result.")
        };
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
                oidc.ResponseType = OpenIdConnectResponseType.Code;
                oidc.Prompt = "select_account";
                oidc.UsePkce = true;
                oidc.SaveTokens = false;
                oidc.MapInboundClaims = false;
                oidc.Scope.Clear();
                oidc.Scope.Add("openid");
                oidc.Scope.Add("email");
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
                            var accounts = context.HttpContext.RequestServices.GetRequiredService<IPlayerAccountCommandStore>();
                            var player = ExternalIdentityAdmission.SignIn(
                                accounts,
                                issuer,
                                subject,
                                context.Principal?.FindFirstValue("email"),
                                string.Equals(
                                    context.Principal?.FindFirstValue("email_verified"),
                                    bool.TrueString,
                                    StringComparison.OrdinalIgnoreCase),
                                cyclesOptions,
                                DateTimeOffset.UtcNow);
                            ((ClaimsIdentity)context.Principal!.Identity!).AddClaim(
                                new Claim(CyclesClaimTypes.PlayerId, player.PlayerId.ToString("D")));
                        }
                        catch (ApiStateConflictException exception)
                        {
                            ExternalAuthenticationFailureCodes.MarkTemporarilyBusy(context.HttpContext);
                            context.Fail(exception.Message);
                        }
                        catch (ApiForbiddenException exception)
                        {
                            ExternalAuthenticationFailureCodes.MarkNotAdmitted(context.HttpContext);
                            context.Fail(exception.Message);
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
                        var code = ExternalAuthenticationFailureCodes.ResolveRemoteFailure(context.HttpContext);
                        context.Response.Redirect($"/auth/error?code={code}");
                        return Task.CompletedTask;
                    },
                    OnAccessDenied = context =>
                    {
                        context.HandleResponse();
                        context.Response.Redirect($"/auth/error?code={ExternalAuthenticationFailureCodes.AccessDenied}");
                        return Task.CompletedTask;
                    }
                };
            });
        services.AddAuthorization();
        return cyclesOptions;
    }
}
