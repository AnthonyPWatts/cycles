using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace Cycles.Tests;

public sealed class TypographyContractTests
{
    private const string GalacticStack =
        "\"Bahnschrift\", \"Avenir Next\", \"Century Gothic\", \"Trebuchet MS\", \"Segoe UI\", sans-serif";

    [Theory]
    [InlineData("Dashboard", "styles.css")]
    [InlineData("Landing", "site.css")]
    public void Browser_surfaces_use_one_galactic_sans_serif_family(string fixtureDirectory, string fileName)
    {
        var css = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            fixtureDirectory,
            fileName));

        Assert.Contains($"--font-galactic: {GalacticStack};", css);

        var families = Regex.Matches(css, @"font-family:\s*(?<family>[^;]+);")
            .Select(match => match.Groups["family"].Value.Trim())
            .ToArray();

        Assert.NotEmpty(families);
        Assert.All(families, family => Assert.Equal("var(--font-galactic)", family));
    }

    [Fact]
    public async Task Playground_access_gate_uses_the_same_galactic_sans_serif_family()
    {
        var middleware = new PlaygroundAccessMiddleware(
            _ => Task.CompletedTask,
            "correct-horse-battery-staple-2026");
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/app.html";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var html = await reader.ReadToEndAsync();

        Assert.Contains($"font-family: {GalacticStack};", html);
    }
}
