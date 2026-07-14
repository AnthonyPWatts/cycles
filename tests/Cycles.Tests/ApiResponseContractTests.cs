using Cycles.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Cycles.Tests;

public sealed class ApiResponseContractTests
{
    [Fact]
    public void Public_response_contracts_do_not_expose_domain_entities()
    {
        Type[] responseTypes =
        [
            typeof(LoginResponse),
            typeof(EmpireResponse),
            typeof(GalaxyResponse),
            typeof(SystemPresenceResponse),
            typeof(FleetResponse),
            typeof(FleetDetailResponse),
            typeof(SystemSummaryResponse),
            typeof(SystemDetailResponse),
            typeof(ColonialOutpostResponse),
            typeof(SystemInfluenceResponse),
            typeof(FleetAtSystemResponse),
            typeof(AdmiralSummaryResponse),
            typeof(FleetOrderResponse),
            typeof(LastTickSummaryResponse),
            typeof(CycleResponse),
            typeof(GalaxySystemResponse),
            typeof(SystemLinkResponse),
            typeof(EmpireResourceResponse),
            typeof(EmpirePriorityResponse),
            typeof(FleetDataResponse),
            typeof(EventResponse),
            typeof(BattleResponse),
            typeof(OpeningBriefingResponse),
            typeof(OpeningBriefingObjectivesResponse),
            typeof(OpeningMoveObjectiveResponse),
            typeof(OpeningColoniseObjectiveResponse),
            typeof(OpeningAttackObjectiveResponse),
            typeof(ChronicleEntryResponse),
            typeof(FleetOrderCommandResponse),
            typeof(PriorityCommandResponse),
            typeof(TickCommandResponse),
            typeof(ErrorResponse)
        ];

        var domainAssembly = typeof(GameState).Assembly;
        var leaks = responseTypes
            .SelectMany(responseType => responseType.GetProperties()
                .SelectMany(property => FlattenType(property.PropertyType)
                    .Where(type => type.Assembly == domainAssembly && !type.IsEnum)
                    .Select(type => $"{responseType.Name}.{property.Name}: {type.Name}")))
            .ToArray();

        Assert.Empty(leaks);
    }

    [Fact]
    public void Chronicle_entries_expose_the_source_tick_as_an_optional_value()
    {
        var property = typeof(ChronicleEntryResponse).GetProperty(nameof(ChronicleEntryResponse.TickNumber));

        Assert.NotNull(property);
        Assert.Equal(typeof(int?), property.PropertyType);
    }

    [Fact]
    public void Ordinary_player_fact_contracts_do_not_expose_storage_json()
    {
        Assert.Null(typeof(EventResponse).GetProperty("FactJson"));
        Assert.Null(typeof(BattleResponse).GetProperty("FactJson"));
        Assert.DoesNotContain(
            new[] { typeof(EventResponse), typeof(BattleResponse), typeof(LastTickSummaryResponse) }
                .SelectMany(type => type.GetProperties()),
            property => property.Name.EndsWith("Json", StringComparison.Ordinal));
    }

    [Fact]
    public void Opening_briefing_contract_is_typed_and_empire_scoped()
    {
        var state = GameSeeder.CreateCuratedColdStart(TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var aurelian = state.Empires.Single(empire => empire.EmpireName == "Aurelian Compact");
        var aurelianPlayer = state.Players.Single(player => player.PlayerId == aurelian.PlayerId);
        var aurelianActor = new DevelopmentActor(aurelianPlayer, aurelian);
        var aurelianVisibleSystems = ApiVisibility.GetVisibleSystemIds(state, cycle, aurelianActor);

        var briefing = OpeningBriefingContract.FindVisible(state, cycle, aurelianActor, aurelianVisibleSystems);

        Assert.NotNull(briefing);
        Assert.Equal(GameSeeder.CuratedColdStartScenarioKey, briefing.ScenarioKey);
        Assert.NotEqual(Guid.Empty, briefing.FocusSystemId);
        Assert.NotEqual(Guid.Empty, briefing.Objectives.Move.FleetId);
        Assert.NotEqual(Guid.Empty, briefing.Objectives.Colonise.SystemId);
        Assert.NotEqual(Guid.Empty, briefing.Objectives.Attack.TargetEmpireId);

        var khepri = state.Empires.Single(empire => empire.EmpireName == "Khepri Mandate");
        var khepriPlayer = state.Players.Single(player => player.PlayerId == khepri.PlayerId);
        var khepriActor = new DevelopmentActor(khepriPlayer, khepri);
        var khepriVisibleSystems = ApiVisibility.GetVisibleSystemIds(state, cycle, khepriActor);
        Assert.Null(OpeningBriefingContract.FindVisible(state, cycle, khepriActor, khepriVisibleSystems));
    }

    [Fact]
    public void Api_json_uses_camel_case_string_enums_and_rejects_numeric_values()
    {
        var options = new JsonSerializerOptions();
        ApiJson.Configure(options);

        var json = JsonSerializer.Serialize(new EnumContract(EventSeverity.High), options);

        Assert.Equal("{\"severity\":\"high\"}", json);
        Assert.Equal(EventSeverity.High, JsonSerializer.Deserialize<EnumContract>("{\"severity\":\"high\"}", options)!.Severity);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EnumContract>("{\"severity\":3}", options));
    }

    [Theory]
    [InlineData(typeof(ApiUnauthorizedException), ApiErrorCodes.AuthenticationRequired, StatusCodes.Status401Unauthorized)]
    [InlineData(typeof(ApiForbiddenException), ApiErrorCodes.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(typeof(ApiNotFoundException), ApiErrorCodes.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(typeof(ArgumentException), ApiErrorCodes.ValidationFailed, StatusCodes.Status400BadRequest)]
    [InlineData(typeof(InvalidOperationException), ApiErrorCodes.StateConflict, StatusCodes.Status409Conflict)]
    public async Task Handled_errors_use_stable_codes_and_common_envelope(
        Type exceptionType,
        string expectedCode,
        int expectedStatus)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Safe message")!;
        var result = ApiErrorResponses.ToResult(exception);
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpJsonOptions(options => ApiJson.Configure(options.SerializerOptions));
        context.RequestServices = services.BuildServiceProvider();
        context.Response.Body = body;

        await result.ExecuteAsync(context);
        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
        Assert.Equal("Safe message", document.RootElement.GetProperty("message").GetString());
        Assert.True(document.RootElement.TryGetProperty("details", out _));
        Assert.True(document.RootElement.TryGetProperty("traceId", out _));
        Assert.DoesNotContain("stack", document.RootElement.EnumerateObject().Select(property => property.Name));
    }

    private static IEnumerable<Type> FlattenType(Type type)
    {
        yield return type;
        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nested in FlattenType(argument))
            {
                yield return nested;
            }
        }
    }

    private sealed record EnumContract(EventSeverity Severity);
}
