using NetWasm.Hosting.Execution;
using NetWasm.TUnit.VSTest.Adapter.Protocol;

namespace NetWasm.TUnit.VSTest.Adapter.Execution;

internal sealed record TUnitGuestRun(
    NetWasmExecutionResult Execution,
    TUnitProtocolSession? Protocol,
    string StandardOutput,
    string StandardError);
