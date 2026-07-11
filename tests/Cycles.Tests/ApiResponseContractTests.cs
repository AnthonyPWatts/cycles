using Cycles.Core;

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
}
