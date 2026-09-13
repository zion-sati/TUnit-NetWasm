using NetWasm.TUnit.Runner.Model;
using NetWasm.TUnit.Runner.Reporting;
using TUnit.Core;

namespace NetWasm.TUnit.Runner.Execution;

public interface ITestRunner
{
    ValueTask<TestRunResult> RunAsync(
        ITestEntryCatalog catalog,
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken = default);
}
