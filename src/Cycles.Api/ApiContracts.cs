using System.Text.Json;
using System.Text.Json.Serialization;

public static class ApiJson
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    }
}

public static class ApiErrorCodes
{
    public const string AuthenticationRequired = "authenticationRequired";
    public const string Forbidden = "forbidden";
    public const string ValidationFailed = "validationFailed";
    public const string StateConflict = "stateConflict";
    public const string NotFound = "notFound";
}

public static class ApiErrorResponses
{
    public static bool IsHandled(Exception exception) =>
        exception is ApiUnauthorizedException
            or ApiForbiddenException
            or ApiNotFoundException
            or ArgumentException
            or InvalidOperationException;

    public static IResult ToResult(Exception exception, HttpContext? httpContext = null)
    {
        var (statusCode, code) = exception switch
        {
            ApiUnauthorizedException => (StatusCodes.Status401Unauthorized, ApiErrorCodes.AuthenticationRequired),
            ApiForbiddenException => (StatusCodes.Status403Forbidden, ApiErrorCodes.Forbidden),
            ApiNotFoundException => (StatusCodes.Status404NotFound, ApiErrorCodes.NotFound),
            ArgumentException => (StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed),
            InvalidOperationException => (StatusCodes.Status409Conflict, ApiErrorCodes.StateConflict),
            _ => throw new ArgumentOutOfRangeException(nameof(exception), "The exception is not a handled API error.")
        };

        return Results.Json(
            new ErrorResponse(code, exception.Message, Details: null, TraceId: httpContext?.TraceIdentifier),
            statusCode: statusCode);
    }
}

public sealed class ApiErrorMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException) when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new ErrorResponse(
                    ApiErrorCodes.ValidationFailed,
                    "The request body was invalid.",
                    Details: null,
                    TraceId: context.TraceIdentifier),
                context.RequestAborted);
        }
    }
}

public sealed record ErrorResponse(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null,
    string? TraceId = null);

public sealed class ApiNotFoundException(string message) : InvalidOperationException(message);

public sealed class ApiValidationException(string message) : ArgumentException(message);

public sealed class ApiStateConflictException(string message) : InvalidOperationException(message);
