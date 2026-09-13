using System.Collections.Immutable;

namespace NetWasm.TUnit.VSTest.Adapter.Protocol;

internal sealed record TUnitProtocolSession(
    ImmutableArray<TUnitCatalogCase> Catalog,
    ImmutableArray<string> StartedStableIds,
    ImmutableArray<TUnitCompletedCase> CompletedCases,
    int? HostStatus,
    string? HostMessage,
    string UserOutput);

internal sealed record TUnitCatalogCase(
    string StableId,
    string DisplayName,
    string FullyQualifiedName,
    string FilePath,
    int LineNumber,
    ImmutableArray<TUnitTrait> Traits);

internal sealed record TUnitTrait(string Name, string Value);

internal sealed record TUnitCompletedCase(
    string StableId,
    string Outcome,
    TimeSpan Duration,
    string? Message);
