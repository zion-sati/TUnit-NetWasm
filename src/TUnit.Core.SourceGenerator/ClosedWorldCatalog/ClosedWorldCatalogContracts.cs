using System.Collections.Immutable;
using TUnit.Core.SourceGenerator.Models;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal interface ICategoryReader
{
    ImmutableArray<string> Read(TestMethodMetadata testMethod);
}

internal interface IDependencyReader
{
    ImmutableArray<string> Read(TestMethodMetadata testMethod);
}

internal interface IPropertyReader
{
    ImmutableArray<string> Read(TestMethodMetadata testMethod);
}

internal interface IRoslynArgumentRowReader
{
    ImmutableArray<CatalogArgumentRow> Read(RoslynArgumentRowsRequest request);
}

internal interface IRoslynCatalogRowRequestBuilder
{
    CatalogRowRequest Build(RoslynCatalogRowBuildRequest request);
}

internal interface IRoslynInstanceRequestBuilder
{
    InstanceCreationRequest Build(RoslynInstanceRequestInput request);
}

internal interface IRoslynInvocationRequestBuilder
{
    InvocationSource Build(RoslynInvocationRequestInput request);
}

internal interface IRoslynMethodIdentityFormatter
{
    string Format(MethodIdentityRequest request);
}

internal interface IClosedWorldCatalogRoslynAdapter
{
    string Emit(CatalogRoslynEmissionRequest request);
}

internal interface IClosedWorldLifecycleHookReader
{
    ImmutableArray<LifecycleHook> Read(LifecycleRoslynRequest request);
}

internal interface IClosedWorldLifecycleRoslynAdapter
{
    string Format(LifecycleRoslynRequest request);
}

internal interface ICatalogCollectionEmitter
{
    string Emit(CatalogCollectionRequest request);
}

internal interface ICaseEmitter
{
    string Emit(CaseRequest request);
}

internal interface ICatalogOnlyMethodSourceEmitter
{
    string Emit(CatalogOnlyMethodSourceRequest request);
}

internal interface ICatalogRowPlanner
{
    ImmutableArray<CatalogRow> Plan(CatalogRowRequest request);
}

internal interface IEntryPointSourceEmitter
{
    string Emit(EntryPointSourceRequest request);
}

internal interface IInstanceCreationEmitter
{
    string Emit(InstanceCreationRequest request);
}

internal interface IInvocationEmitter
{
    string Emit(InvocationRequest request);
}

internal interface ILifecycleFormatter
{
    string Format(ImmutableArray<LifecycleHook> hooks);
}

internal interface ILifecyclePlanner
{
    ImmutableArray<LifecycleHook> Plan(LifecycleRequest request);
}

internal interface IMethodSourceEmitter
{
    string Emit(MethodSourceRequest request);
}

internal interface IPerClassSourceEmitter
{
    string Emit(PerClassSourceRequest request);
}
