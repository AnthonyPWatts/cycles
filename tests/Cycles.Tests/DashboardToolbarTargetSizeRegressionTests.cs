using System.Text.RegularExpressions;

namespace Cycles.Tests;

public sealed class DashboardToolbarTargetSizeRegressionTests
{
    [Fact]
    public void Shared_header_actions_have_44_pixel_targets()
    {
        var css = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Dashboard",
            "styles.css"));

        Assert.Matches(
            new Regex(
                @"\.toolbar-actions \.toolbar-icon-button\s*\{[^}]*flex:\s*0 0 44px;[^}]*inline-size:\s*44px;[^}]*block-size:\s*44px;[^}]*min-inline-size:\s*44px;[^}]*min-block-size:\s*44px;",
                RegexOptions.Singleline),
            css);
        Assert.DoesNotContain("inline-size: 36px", css);
        Assert.DoesNotContain("block-size: 36px", css);
    }
}
