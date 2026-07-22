internal enum CyclesAuthenticationMode
{
    DevelopmentSelector,
    Oidc
}

internal static class CyclesAuthenticationModeConfiguration
{
    public static CyclesAuthenticationMode Resolve(
        IConfiguration configuration,
        bool isDevelopment)
    {
        var configuredMode = configuration["Cycles:Authentication:Mode"];
        if (!Enum.TryParse<CyclesAuthenticationMode>(configuredMode, ignoreCase: true, out var mode)
            || !string.Equals(configuredMode, mode.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Cycles:Authentication:Mode must be DevelopmentSelector or Oidc.");
        }

        if (mode == CyclesAuthenticationMode.DevelopmentSelector && !isDevelopment)
        {
            throw new InvalidOperationException(
                "Cycles:Authentication:Mode DevelopmentSelector is available only in the Development environment.");
        }

        return mode;
    }
}
