namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal static class ClosedWorldCatalogComposition
{
    internal static IInstanceCreationEmitter CreateInstanceCreationEmitter() => new DirectInstanceCreationEmitter();

    internal static ICatalogRowPlanner CreateRowPlanner() => new CatalogRowPlanner();

    internal static ILifecyclePlanner CreateLifecyclePlanner() => new LifecyclePlanner();

    internal static ILifecycleFormatter CreateLifecycleFormatter() => new LifecycleFormatter();

    internal static IInvocationEmitter CreateInvocationEmitter() => new DirectInvocationEmitter();

    internal static ICaseEmitter CreateCaseEmitter() => new CatalogCaseEmitter(CreateInstanceCreationEmitter(), CreateInvocationEmitter());

    internal static IPerClassSourceEmitter CreatePerClassSourceEmitter() => new PerClassSourceEmitter();

    internal static ICatalogCollectionEmitter CreateCatalogCollectionEmitter() => new CatalogCollectionEmitter();

    internal static IMethodSourceEmitter CreateMethodSourceEmitter() => new MethodSourceEmitter();

    internal static IEntryPointSourceEmitter CreateEntryPointSourceEmitter() => new EntryPointSourceEmitter();

    internal static IClosedWorldCatalogRoslynAdapter CreateCatalogRoslynAdapter() =>
        new ClosedWorldCatalogRoslynAdapter(CreateRowPlanner(), CreateCaseEmitter());

    internal static IClosedWorldLifecycleRoslynAdapter CreateLifecycleRoslynAdapter() =>
        new ClosedWorldLifecycleRoslynAdapter(CreateLifecyclePlanner(), CreateLifecycleFormatter());

    internal static ICatalogOnlyMethodSourceEmitter CreateCatalogOnlyMethodSourceEmitter() =>
        new CatalogOnlyMethodSourceEmitter(
            CreateCatalogRoslynAdapter(),
            CreateLifecycleRoslynAdapter(),
            CreateMethodSourceEmitter());
}
