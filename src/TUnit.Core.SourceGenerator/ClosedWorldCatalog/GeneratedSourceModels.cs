using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using TUnit.Core.SourceGenerator.Models;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed record EntryPointSourceRequest(ImmutableArray<string> SourceNames);

internal sealed record MethodSourceGroup(
    string LifecycleCode,
    ImmutableArray<string> CaseBodies);

internal sealed record MethodSourceRequest(
    string SourceName,
    ImmutableArray<MethodSourceGroup> Groups);

internal sealed record PerClassSourceRequest(
    string SourceName,
    string LifecycleCode,
    ImmutableArray<string> CaseBodies);

internal sealed record CatalogOnlyMethodSourceRequest(
    TestMethodMetadata TestMethod,
    ImmutableArray<ConcreteInstantiation> Instantiations);

internal sealed record MethodIdentityRequest(IMethodSymbol Method, string FullyQualifiedName);
