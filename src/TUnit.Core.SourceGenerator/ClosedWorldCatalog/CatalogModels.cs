using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using TUnit.Core.SourceGenerator.Generators;
using TUnit.Core.SourceGenerator.Models;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

// A row value's code, identity and display; an [Arguments] constant also carries its type.
internal sealed record CatalogValue(string Code, string Canonical, string Display, ITypeSymbol? ConstantType = null);

internal sealed record CatalogArgumentRow(
    ImmutableArray<CatalogValue> Values,
    string? DisplayName = null,
    string? SkipReason = null,
    ImmutableArray<string> Categories = default);

internal sealed record CatalogRowRequest(
    string MethodIdentity,
    string TestName,
    ImmutableArray<CatalogArgumentRow> MethodRows,
    ImmutableArray<CatalogArgumentRow> ConstructorRows,
    bool HasRuntimeDataSource,
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

internal sealed record ParallelConstraintRequest(
    bool NotInParallel,
    ImmutableArray<string> ConstraintKeys,
    int NotInParallelOrder,
    string? ParallelGroup,
    int ParallelGroupOrder);

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
    bool LazilyMaterializeConstructorArguments = false,
    ParallelConstraintRequest? ParallelConstraint = null);

internal sealed record CatalogCollectionGroup(int Index, ImmutableArray<string> CaseBodies);

internal sealed record CatalogCollectionRequest(
    ImmutableArray<CatalogCollectionGroup> Groups,
    bool IncludeGroupMethods = true);

internal sealed record CatalogRoslynEmissionRequest(
    TestMethodMetadata TestMethod,
    string ConcreteClassName,
    string GroupIdentity,
    string FullyQualifiedName,
    string GeneratedIdentitySuffix,
    ITypeSymbol[] ClassTypeArguments,
    ITypeSymbol[] MethodTypeArguments,
    TestMetadataGenerator.ConcreteInstantiation? Instantiation,
    string LifecycleName);
