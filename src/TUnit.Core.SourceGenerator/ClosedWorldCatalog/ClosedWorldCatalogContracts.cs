using System.Collections.Immutable;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal interface IClosedWorldCatalogRoslynAdapter
{
    string Emit(CatalogRoslynEmissionRequest request);
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
