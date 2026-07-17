using Cycles.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;
using System.Security.Cryptography;

public static class DevelopmentAuth
{
    public const string CookieName = "cycles.dev.player";
    public const string HeaderName = "X-Cycles-Dev-Player";
    private const string CookiePurpose = "Cycles.TrustedPlayerSession.v1";

    public static void SignIn(HttpContext httpContext, Player player)
    {
        var environment = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        var protectedPlayerId = GetProtector(httpContext).Protect(player.PlayerId.ToString("D"));
        httpContext.Response.Cookies.Append(
            CookieName,
            protectedPlayerId,
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = !environment.IsDevelopment(),
                Path = "/",
                MaxAge = TimeSpan.FromHours(12)
            });
    }

    public static void SignOut(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(CookieName);
    }

    public static DevelopmentActor RequireActor(HttpContext httpContext, GameState state)
    {
        var player = RequirePlayer(httpContext, state);
        var cycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("No active cycle exists.");
        var participant = state.GetParticipant(cycle.CycleId, player.PlayerId);
        var empire = participant is null
            ? null
            : state.Empires.Single(item => item.EmpireId == participant.EmpireId);

        if (player.Role != PlayerRole.Admin && empire is null)
        {
            throw new ApiForbiddenException("The authenticated player has no empire in the active cycle.");
        }

        return new DevelopmentActor(player, empire, participant);
    }

    public static Fleet RequireCommandableFleet(GameState state, DevelopmentActor actor, Guid fleetId)
    {
        var cycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("No active cycle exists.");
        var fleet = state.Fleets.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.FleetId == fleetId)
            ?? throw new ApiNotFoundException("Fleet does not exist in the active cycle.");

        _ = RequireCommandableEmpire(state, actor);

        if (!actor.IsAdmin && fleet.EmpireId != RequirePlayerEmpireId(actor))
        {
            throw new ApiForbiddenException("The authenticated player cannot command this fleet.");
        }

        return fleet;
    }

    public static Empire? RequireCommandableEmpire(GameState state, DevelopmentActor actor)
    {
        if (actor.IsAdmin)
        {
            return actor.Empire;
        }

        var cycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("No active cycle exists.");
        try
        {
            return state.RequireCommandableEmpire(cycle.CycleId, actor.Player.PlayerId);
        }
        catch (InvalidOperationException exception)
        {
            throw new ApiForbiddenException(exception.Message);
        }
    }

    public static Guid ResolveEmpireId(GameState state, DevelopmentActor actor, Guid? requestedEmpireId = null)
    {
        var cycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("No active cycle exists.");

        if (actor.IsAdmin)
        {
            var empireId = requestedEmpireId ?? actor.Empire?.EmpireId
                ?? throw new InvalidOperationException("Admin requests must identify an empire.");
            EnsureEmpireExists(state, cycle.CycleId, empireId);
            return empireId;
        }

        var playerEmpireId = RequirePlayerEmpireId(actor);
        if (requestedEmpireId.HasValue && requestedEmpireId.Value != playerEmpireId)
        {
            throw new ApiForbiddenException("The authenticated player cannot act for another empire.");
        }

        return playerEmpireId;
    }

    public static Guid ResolveOrderOwnerEmpireId(GameState state, DevelopmentActor actor, Guid fleetOrderId)
    {
        var cycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("No active cycle exists.");
        var order = state.FleetOrders.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.FleetOrderId == fleetOrderId)
            ?? throw new ApiNotFoundException("Fleet order does not exist in the active cycle.");
        var fleet = state.Fleets.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.FleetId == order.FleetId)
            ?? throw new InvalidOperationException("Fleet for order does not exist in the active cycle.");

        if (!actor.IsAdmin && fleet.EmpireId != RequirePlayerEmpireId(actor))
        {
            throw new ApiForbiddenException("The authenticated player cannot cancel another empire's order.");
        }

        if (!actor.IsAdmin)
        {
            _ = RequireCommandableEmpire(state, actor);
        }

        return fleet.EmpireId;
    }

    private static Player RequirePlayer(HttpContext httpContext, GameState state)
    {
        var playerId = ReadPlayerId(httpContext)
            ?? throw new ApiUnauthorizedException("Login required.");
        var player = state.Players.SingleOrDefault(item => item.PlayerId == playerId)
            ?? throw new ApiUnauthorizedException("Login required.");

        if (player.Status != PlayerStatus.Active)
        {
            throw new ApiForbiddenException("The authenticated player is not active.");
        }

        if (player.Kind != PlayerKind.Human)
        {
            throw new ApiForbiddenException("The authenticated player is not available for human sign-in.");
        }

        return player;
    }

    private static Guid? ReadPlayerId(HttpContext httpContext)
    {
        if (Guid.TryParse(httpContext.User.FindFirstValue(CyclesClaimTypes.PlayerId), out var authenticatedPlayerId))
        {
            return authenticatedPlayerId;
        }

        var requestServices = httpContext.Features.Get<IServiceProvidersFeature>()?.RequestServices;
        var environment = requestServices?.GetService<IHostEnvironment>();
        if (environment?.IsDevelopment() == true
            && httpContext.Request.Headers.TryGetValue(HeaderName, out var headerValues)
            && Guid.TryParse(headerValues.FirstOrDefault(), out var headerPlayerId))
        {
            return headerPlayerId;
        }

        if (!httpContext.Request.Cookies.TryGetValue(CookieName, out var cookieValue))
        {
            return null;
        }

        try
        {
            var unprotectedPlayerId = GetProtector(httpContext).Unprotect(cookieValue);
            return Guid.TryParse(unprotectedPlayerId, out var cookiePlayerId)
                ? cookiePlayerId
                : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static IDataProtector GetProtector(HttpContext httpContext) =>
        httpContext.RequestServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(CookiePurpose);

    private static Guid RequirePlayerEmpireId(DevelopmentActor actor) =>
        actor.Empire?.EmpireId
            ?? throw new ApiForbiddenException("The authenticated player has no empire in the active cycle.");

    private static void EnsureEmpireExists(GameState state, Guid cycleId, Guid empireId)
    {
        if (!state.Empires.Any(item => item.CycleId == cycleId && item.EmpireId == empireId))
        {
            throw new ApiNotFoundException("Empire does not exist in the active cycle.");
        }
    }
}

public sealed record DevelopmentActor(Player Player, Empire? Empire, MatchParticipant? Participant = null)
{
    public bool IsAdmin => Player.Role == PlayerRole.Admin;
}

public sealed class ApiUnauthorizedException(string message) : InvalidOperationException(message);

public sealed class ApiForbiddenException(string message) : InvalidOperationException(message);
