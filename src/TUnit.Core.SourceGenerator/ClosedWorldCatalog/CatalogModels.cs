using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using TUnit.Core.SourceGenerator.Models;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed record CatalogValue(string Code, string Canonical, string Display);

internal sealed record CatalogArgumentRow(
    ImmutableArray<CatalogValue> Values,
    string? DisplayName = null,
    string? Identity = null);

internal sealed record CatalogRowRequest(
    string MethodIdentity,
    string MethodName,
    ImmutableArray<CatalogArgumentRow> MethodRows,
    ImmutableArray<CatalogArgumentRow> ConstructorRows,
    bool HasRuntimeDataSource,
    string? SelectedMethodRowIdentity = null,
    string? MethodDisplayName = null,
    ImmutableArray<string> MethodParameterNames = default,
    string? ClassDisplayName = null,
    ImmutableArray<string> ConstructorParameterNames = default);

internal sealed record CatalogRow(
    ImmutableArray<CatalogValue> MethodValues,
    ImmutableArray<CatalogValue> ConstructorValues,
    string StableId,
    string DisplayName);

internal sealed record CaseRequest(
    string TypeName,
    string MethodName,
    string FullyQualifiedName,
    string GroupIdentity,
    string FilePath,
    int LineNumber,
    string InvocationKind,
    InstanceCreationRequest InstanceCreation,
    InvocationRequest Invocation,
    ImmutableArray<string> Categories,
    ImmutableArray<string> Properties,
    ImmutableArray<string> Dependencies,
    CatalogRow Row,
    string LifecycleName);

internal sealed record CatalogCollectionGroup(int Index, ImmutableArray<string> CaseBodies);

internal sealed record CatalogCollectionRequest(
    ImmutableArray<CatalogCollectionGroup> Groups,
    bool IncludeGroupMethods = true);

internal sealed record RoslynArgumentRowsRequest(ImmutableArray<AttributeData> Attributes);

internal sealed record RoslynCatalogRowBuildRequest(
    TestMethodMetadata TestMethod,
    string MethodIdentity,
    string MethodName,
    AttributeData? SelectedArgumentsAttribute);

internal sealed record CatalogRoslynEmissionRequest(
    TestMethodMetadata TestMethod,
    string ConcreteClassName,
    string GroupIdentity,
    string FullyQualifiedName,
    string GeneratedIdentitySuffix,
    ITypeSymbol[] ClassTypeArguments,
    ITypeSymbol[] MethodTypeArguments,
    AttributeData? SelectedArgumentsAttribute,
    string LifecycleName);
