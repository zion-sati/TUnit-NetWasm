using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TUnit.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PsvmAnalyzer : ConcurrentDiagnosticAnalyzer
{
    private const string EnabledProperty = "build_property.EnableTUnitSourceGeneration";
    private const string ModeProperty = "build_property.TUnitSourceGenerationMode";
    private const string ClosedWorldCatalog = "ClosedWorldCatalog";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rules.NoMainMethod);

    protected override void InitializeInternal(AnalysisContext context)
    {
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
    }

    private void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        if (!UsesDesktopGeneratedEntryPoint(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions))
        {
            return;
        }

        if (context.Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (!methodSymbol.IsStatic)
        {
            return;
        }

        if (methodSymbol.Name != "Main")
        {
            return;
        }

        if (!HasKnownReturnType(context, methodSymbol))
        {
            return;
        }

        if (!HasKnownParameters(context, methodSymbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rules.NoMainMethod,
                methodSymbol.Locations.FirstOrDefault())
            );
    }

    private static bool UsesDesktopGeneratedEntryPoint(AnalyzerConfigOptions options)
    {
        if (options.TryGetValue(EnabledProperty, out var enabled) &&
            string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !options.TryGetValue(ModeProperty, out var mode) ||
               !string.Equals(mode, ClosedWorldCatalog, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasKnownReturnType(SymbolAnalysisContext context, IMethodSymbol methodSymbol)
    {
        IEnumerable<INamedTypeSymbol> knownReturnTypes =
            new List<INamedTypeSymbol>
            {
                context.Compilation.GetSpecialType(SpecialType.System_Void),
                context.Compilation.GetSpecialType(SpecialType.System_Int32),
                context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task")!,
                context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1")!.Construct(context.Compilation.GetSpecialType(SpecialType.System_Int32))
            }.AsReadOnly();

        return knownReturnTypes.Any(x => x.Equals(methodSymbol.ReturnType, SymbolEqualityComparer.Default));
    }

    private static bool HasKnownParameters(SymbolAnalysisContext context, IMethodSymbol methodSymbol)
    {
        if (!methodSymbol.Parameters.Any())
        {
            return true;
        }

        return methodSymbol.Parameters.Length == 1
               && methodSymbol.Parameters
                   .First()
                   .Type
                   .Equals(context.Compilation.CreateArrayTypeSymbol(context.Compilation.GetSpecialType(SpecialType.System_String)), SymbolEqualityComparer.Default);
    }
}
