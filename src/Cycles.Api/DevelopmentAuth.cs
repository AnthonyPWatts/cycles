using Cycles.Core;
using Microsoft.AspNetCore.Http;

public static class DevelopmentAuth
{
    public const string CookieName = "cycles.dev.player";
    public const string HeaderName = "X-Cycles-Dev-Player";

    public static void SignIn(HttpContext httpContext, Player player)
    {
        httpContext.Response.Cookies.Append(
            CookieName,
            player.PlayerId.ToString("D"),
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = false
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
        var empire = GetPlayerEmpire(state, cycle.CycleId, player.PlayerId);

        if (player.Role != PlayerRole.Admin && empire is null)
        {
            throw new ApiForbiddenException("The authenticated player has no empire in the active cycle.");
        }

        return new DevelopmentActor(player, empire);
    }

    public static Fleet RequireCommandableFleet(GameState state, DevelopmentActor actor, Guid fleetId)
    {
        var cycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("No active cycle exists.");
        var fleet = state.Fleets.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.FleetId == fleetId)
            ?? throw new InvalidOperationException("Fleet does not exist in the active cycle.");

        if (!actor.IsAdmin && fleet.EmpireId != RequirePlayerEmpireId(actor))
        {
            throw new ApiForbiddenException("The authenticated player cannot command this fleet.");
        }

        return fleet;
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
            ?? throw new InvalidOperationException("Fleet order does not exist in the active cycle.");
        var fleet = state.Fleets.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.FleetId == order.FleetId)
            ?? throw new InvalidOperationException("Fleet for order does not exist in the active cycle.");

        if (!actor.IsAdmin && fleet.EmpireId != RequirePlayerEmpireId(actor))
        {
            throw new ApiForbiddenException("The authenticated player cannot cancel another empire's order.");
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

        return player;
    }

    private static Guid? ReadPlayerId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(HeaderName, out var headerValues)
            && Guid.TryParse(headerValues.FirstOrDefault(), out var headerPlayerId))
        {
            return headerPlayerId;
        }

        return httpContext.Request.Cookies.TryGetValue(CookieName, out var cookieValue)
            && Guid.TryParse(cookieValue, out var cookiePlayerId)
                ? cookiePlayerId
                : null;
    }

    private static Empire? GetPlayerEmpire(GameState state, Guid cycleId, Guid playerId)
    {
        var empires = state.Empires
            .Where(item => item.CycleId == cycleId && item.PlayerId == playerId)
            .ToArray();

        return empires.Length switch
        {
            0 => null,
            1 => empires[0],
            _ => throw new InvalidOperationException("A player is assigned to more than one empire in the active cycle.")
        };
    }

    private static Guid RequirePlayerEmpireId(DevelopmentActor actor) =>
        actor.Empire?.EmpireId
            ?? throw new ApiForbiddenException("The authenticated player has no empire in the active cycle.");

    private static void EnsureEmpireExists(GameState state, Guid cycleId, Guid empireId)
    {
        if (!state.Empires.Any(item => item.CycleId == cycleId && item.EmpireId == empireId))
        {
            throw new InvalidOperationException("Empire does not exist in the active cycle.");
        }
    }
}

public sealed record DevelopmentActor(Player Player, Empire? Empire)
{
    public bool IsAdmin => Player.Role == PlayerRole.Admin;
}

public sealed class ApiUnauthorizedException(string message) : InvalidOperationException(message);

public sealed class ApiForbiddenException(string message) : InvalidOperationException(message);
