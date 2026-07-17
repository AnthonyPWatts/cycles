using Cycles.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Cycles.Tests;

internal static class TestHttpContextFactory
{
    private static readonly IServiceProvider DevelopmentServices = CreateServices(Environments.Development);
    private static readonly IServiceProvider ProductionServices = CreateServices(Environments.Production);

    public static DefaultHttpContext CreateAuthenticated(Player player)
    {
        var context = CreateDevelopment();
        context.Request.Headers[DevelopmentAuth.HeaderName] = player.PlayerId.ToString("D");
        return context;
    }

    public static DefaultHttpContext CreateDevelopment() =>
        new() { RequestServices = DevelopmentServices };

    public static DefaultHttpContext CreateProduction() =>
        new() { RequestServices = ProductionServices };

    private static IServiceProvider CreateServices(string environmentName)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName));
        return services.BuildServiceProvider();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Cycles.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
