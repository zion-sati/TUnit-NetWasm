using System.Collections.Immutable;

namespace NetWasm.TUnit.VSTest.Adapter.Execution;

internal interface ITUnitGuestClient
{
    ValueTask<TUnitGuestRun> RunAsync(
        string source,
        ImmutableArray<string> arguments,
        CancellationToken cancellationToken);
}
