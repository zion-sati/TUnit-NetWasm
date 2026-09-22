using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using TUnit.Core.SourceGenerator.Models;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed record CatalogValue(string Code, string Canonical, string Display);

internal sealed record CatalogArgumentRow(
    ImmutableArray<CatalogValue> Values,
    string? DisplayName = null,
    string? Identity = null,
    string? SkipReason = null,
    ImmutableArray<string> Categories = default);

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
    ImmutableArray<string> ConstructorParameterNames = default,
    int RepeatCount = 0);


internal sealed record CatalogRow(
    ImmutableArray<CatalogValue> MethodValues,
    ImmutableArray<CatalogValue> ConstructorValues,
    string StableId,
    string DisplayName,
    string? SkipReason = null,
    ImmutableArray<string> Categories = default,
    int RepeatIndex = 0);

internal sealed record RetryPolicyRequest(
    int MaxRetries,
    int BackoffMilliseconds,
    double BackoffMultiplier,
    ImmutableArray<string> ExceptionTypeNames);

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
    string LifecycleName,
    int? TimeoutMilliseconds = null,
    RetryPolicyRequest? RetryPolicy = null,
    string? SkipReason = null,
    int ExecutionPriority = 2,
    bool IsExplicit = false,
    bool IsNotDiscoverable = false,
    string? StableIdExpression = null,
    string? DisplayNameExpression = null,
    bool CapturesRuntimeValues = false,
    string? DisposeDataExpression = null,
    string? RepeatIndexExpression = null,
    bool LazilyMaterializeRowArguments = false,
    bool LazilyMaterializeConstructorArguments = false);

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
