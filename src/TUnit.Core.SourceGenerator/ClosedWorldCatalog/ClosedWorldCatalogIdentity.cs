using Microsoft.CodeAnalysis;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal static class ClosedWorldCatalogIdentity
{
    internal static string Format(ITypeSymbol[] classTypeArguments, ITypeSymbol[] methodTypeArguments)
    {
        var arguments = classTypeArguments.Concat(methodTypeArguments)
            .Select(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToArray();
        return arguments.Length == 0 ? string.Empty : $"#generic<{string.Join(",", arguments)}>";
    }

    internal static string FormatFullyQualifiedName(
        string namespaceName,
        string simpleClassName,
        string testName,
        ITypeSymbol[] classTypeArguments)
    {
        var displayClassName = classTypeArguments.Length == 0
            ? simpleClassName
            : $"{simpleClassName}<{string.Join(",", classTypeArguments.Select(static type => type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)))}>";
        return string.IsNullOrEmpty(namespaceName)
            ? $"{displayClassName}.{testName}"
            : $"{namespaceName}.{displayClassName}.{testName}";
    }
}
