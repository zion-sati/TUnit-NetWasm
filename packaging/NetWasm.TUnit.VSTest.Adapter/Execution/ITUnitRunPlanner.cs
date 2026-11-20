using System.Collections.Immutable;

namespace NetWasm.TUnit.VSTest.Adapter.Execution;

internal interface ITUnitRunPlanner
{
    // The guest runs that execute the selected tests, each with the arguments the guest takes and the stable IDs it
    // completes. catalogCount is the number of tests the source has, or null when the caller does not know it.
    ImmutableArray<TUnitGuestRunPlan> Plan(IReadOnlyList<string> selectedStableIds, int? catalogCount);
}

internal sealed record TUnitGuestRunPlan(ImmutableArray<string> Arguments, ImmutableArray<string> StableIds);
