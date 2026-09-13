using Microsoft.CodeAnalysis.Diagnostics;

namespace TUnit.Core.SourceGenerator.Utilities;

internal static class SourceGenerationMode
{
    private const string EnabledProperty = "build_property.EnableTUnitSourceGeneration";
    private const string ModeProperty = "build_property.TUnitSourceGenerationMode";
    private const string ClosedWorldCatalog = "ClosedWorldCatalog";

    internal readonly record struct Settings(bool Enabled, bool ClosedWorldCatalog)
    {
        public bool IsDesktop => Enabled && !ClosedWorldCatalog;

        public bool IsClosedWorldCatalog => Enabled && ClosedWorldCatalog;
    }

    public static Settings Read(AnalyzerConfigOptionsProvider options)
    {
        var enabled = !options.GlobalOptions.TryGetValue(EnabledProperty, out var enabledValue) ||
                      !string.Equals(enabledValue, "false", StringComparison.OrdinalIgnoreCase);
        var closedWorldCatalog = options.GlobalOptions.TryGetValue(ModeProperty, out var modeValue) &&
                                 string.Equals(modeValue, ClosedWorldCatalog, StringComparison.OrdinalIgnoreCase);
        return new Settings(enabled, closedWorldCatalog);
    }
}
