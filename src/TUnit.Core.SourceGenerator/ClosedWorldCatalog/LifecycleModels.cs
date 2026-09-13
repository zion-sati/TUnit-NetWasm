using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed record LifecycleHook(
    string Stage,
    int Depth,
    int Order,
    int Line,
    string MethodName,
    string Invocation,
    string ReturnType);

internal sealed record LifecycleRequest(ImmutableArray<LifecycleHook> Hooks);

internal sealed record LifecycleRoslynRequest(INamedTypeSymbol TypeSymbol, string ConcreteClassName);
