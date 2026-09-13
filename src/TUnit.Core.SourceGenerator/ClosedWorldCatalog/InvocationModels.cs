using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using TUnit.Core.SourceGenerator.Models;

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
    ImmutableArray<RequiredProperty> RequiredProperties);

internal sealed record InvocationRequest(string MethodCall, InvocationReturnKind ReturnKind);

internal enum InvocationReturnKind
{
    Sync,
    ValueTask,
    ValueTaskOfT,
    Task,
    Unsupported,
}

internal sealed record InvocationSource(InvocationRequest Request, string InvocationKind);

internal sealed record RoslynInstanceRequestInput(
    ITypeSymbol Type,
    ImmutableArray<CatalogValue> ConstructorArguments);

internal sealed record RoslynInvocationRequestInput(
    TestMethodMetadata TestMethod,
    ITypeSymbol[] MethodTypeArguments,
    ITypeSymbol[] ClassTypeArguments,
    ImmutableArray<CatalogValue> Values);

/// <summary>
/// A closed-world test method paired with the compile-time type arguments that instantiate it.
/// The same passive value is also consumed by the desktop compatibility path.
/// </summary>
internal sealed class ConcreteInstantiation
{
    public required string ConcreteClassName { get; init; }
    public required ITypeSymbol[] TypeArguments { get; init; }
    public required ITypeSymbol[] ClassTypeArgs { get; init; }
    public required ITypeSymbol[] MethodTypeArgs { get; init; }
    public required string TestName { get; init; }
    public required string MethodName { get; init; }
    public AttributeData? SpecificArgumentsAttribute { get; init; }
}
