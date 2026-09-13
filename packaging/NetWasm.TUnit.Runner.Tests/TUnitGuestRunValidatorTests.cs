using System.Collections.Immutable;
using NetWasm.Hosting.Execution;
using NetWasm.TUnit.VSTest.Adapter.Execution;
using NetWasm.TUnit.VSTest.Adapter.Protocol;

namespace NetWasm.TUnit.Runner.Tests;

public sealed class TUnitGuestRunValidatorTests
{
    [Fact]
    public void DiscoveryAcceptsNormalHostResultAndCatalog()
    {
        var run = CreateRun(
            exitCode: 0,
            catalog: [new TUnitCatalogCase("id", "Display", "Tests.Case", "Tests.cs", 1, [])],
            hostStatus: 0);

        TUnitGuestRunValidator.ValidateDiscovery(run);
    }

    [Fact]
    public void ExecutionAcceptsExitOneWhenEverySelectedTestCompleted()
    {
        var run = CreateRun(
            exitCode: 1,
            completed: [new TUnitCompletedCase("id", "assertion-failed", TimeSpan.Zero, "failure")]);

        TUnitGuestRunValidator.ValidateExecution(run, 1);
    }

    [Fact]
    public void ExecutionRejectsInfrastructureFailure()
    {
        var run = CreateRun(
            exitCode: null,
            completionKind: NetWasmCompletionKind.HostFailure,
            failure: new NetWasmExecutionFailure(
                NetWasmFailurePhase.Execution,
                "NW-HOST",
                "failed"));

        XunitAssert.Throws<InvalidDataException>(() => TUnitGuestRunValidator.ValidateExecution(run, 0));
    }

    private static TUnitGuestRun CreateRun(
        int? exitCode,
        NetWasmCompletionKind completionKind = NetWasmCompletionKind.Normal,
        NetWasmExecutionFailure? failure = null,
        ImmutableArray<TUnitCatalogCase> catalog = default,
        ImmutableArray<TUnitCompletedCase> completed = default,
        int? hostStatus = null)
    {
        return new TUnitGuestRun(
            new NetWasmExecutionResult(
                1,
                completionKind,
                exitCode,
                failure,
                ImmutableArray<NetWasmExecutionFailure>.Empty),
            new TUnitProtocolSession(
                catalog.IsDefault ? [] : catalog,
                [],
                completed.IsDefault ? [] : completed,
                hostStatus,
                null,
                string.Empty),
            string.Empty,
            string.Empty);
    }
}
