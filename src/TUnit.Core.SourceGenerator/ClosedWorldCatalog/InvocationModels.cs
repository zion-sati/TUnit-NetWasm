using System.Collections.Immutable;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed record ConstructorParameter(
    string TypeName,
    bool IsTrailingCollection,
    string? ElementTypeName,
    string? DefaultValue);

internal sealed record RequiredProperty(string Name, string DefaultValue);

internal sealed record InstanceCreationRequest(
    string TypeName,
    bool HasClassConstructor,
    ImmutableArray<ConstructorParameter> ConstructorParameters,
    ImmutableArray<string> ConstructorArguments,
    ImmutableArray<RequiredProperty> RequiredProperties,
    string? FailureExpression = null);

internal sealed record InvocationRequest(string MethodCall, InvocationReturnKind ReturnKind);

internal enum InvocationReturnKind
{
    Sync,
    ValueTask,
    ValueTaskOfT,
    Task,
    Unsupported,
}
