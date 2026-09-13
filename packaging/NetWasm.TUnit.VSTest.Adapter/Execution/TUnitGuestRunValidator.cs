using NetWasm.Hosting.Execution;
using NetWasm.TUnit.VSTest.Adapter.Protocol;

namespace NetWasm.TUnit.VSTest.Adapter.Execution;

internal static class TUnitGuestRunValidator
{
    public static TUnitProtocolSession ValidateDiscovery(TUnitGuestRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        ValidateInfrastructure(run);
        var protocol = RequireProtocol(run);
        if (run.Execution.ExitCode != 0 || protocol.HostStatus != 0)
        {
            throw new InvalidDataException(
                $"TUnit guest discovery failed with exit {run.Execution.ExitCode} and host status {protocol.HostStatus}.");
        }
        if (protocol.Catalog.IsEmpty)
        {
            throw new InvalidDataException("TUnit guest discovery returned an empty catalog.");
        }
        return protocol;
    }

    public static TUnitProtocolSession ValidateExecution(TUnitGuestRun run, int expectedCount)
    {
        ArgumentNullException.ThrowIfNull(run);
        ValidateInfrastructure(run);
        var protocol = RequireProtocol(run);
        if (protocol.HostStatus is not null)
        {
            throw new InvalidDataException(
                $"TUnit guest execution reported host status {protocol.HostStatus}: {protocol.HostMessage}");
        }
        if (run.Execution.ExitCode is not (0 or 1))
        {
            throw new InvalidDataException($"TUnit guest execution failed with exit {run.Execution.ExitCode}.");
        }
        if (protocol.CompletedCases.Length != expectedCount)
        {
            throw new InvalidDataException(
                $"TUnit guest execution completed {protocol.CompletedCases.Length} tests; expected {expectedCount}.");
        }
        return protocol;
    }

    private static void ValidateInfrastructure(TUnitGuestRun run)
    {
        if (run.Execution.CompletionKind != NetWasmCompletionKind.Normal)
        {
            var failure = run.Execution.PrimaryFailure;
            var detail = failure is null
                ? run.Execution.CompletionKind.ToString()
                : $"{failure.Code}: {failure.Message}";
            throw new InvalidDataException($"NetWasm test infrastructure failed: {detail}");
        }
    }

    private static TUnitProtocolSession RequireProtocol(TUnitGuestRun run) =>
        run.Protocol ?? throw new InvalidDataException("The TUnit protocol output is missing.");
}
