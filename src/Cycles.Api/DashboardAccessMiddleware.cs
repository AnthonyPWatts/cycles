using Cycles.Application;
using Microsoft.AspNetCore.Authentication;

internal static class DashboardAccessExtensions
{
    public static IApplicationBuilder UsePrivateDashboard(this IApplicationBuilder app, bool isDevelopment) =>
        app.UseMiddleware<DashboardAccessMiddleware>(!isDevelopment);
}

internal sealed class DashboardAccessMiddleware(RequestDelegate next, bool protectDashboard)
{
    public async Task InvokeAsync(HttpContext context, IPlayerAccountQuery accounts)
    {
        if (!protectDashboard
            || !context.Request.Path.Equals("/app.html", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await context.ChallengeAsync(
                CyclesAuthenticationSchemes.OpenIdConnect,
                new AuthenticationProperties { RedirectUri = "/app.html" });
            return;
        }

        try
        {
            _ = DevelopmentAuth.RequireAccount(context, accounts);
            await next(context);
        }
        catch (Exception exception) when (exception is ApiUnauthorizedException or ApiForbiddenException)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("This authenticated identity is not admitted to the Cycles dashboard.", context.RequestAborted);
        }
    }
}
