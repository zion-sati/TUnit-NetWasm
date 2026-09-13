using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using NetWasm.TUnit.VSTest.Adapter.Execution;

namespace NetWasm.TUnit.VSTest.Adapter;

[ExtensionUri(TUnitNetWasmAdapterConstants.ExecutorUri)]
public sealed class TUnitNetWasmTestExecutor : ITestExecutor
{
    private readonly ITestExecutionApplication _application;

    public TUnitNetWasmTestExecutor()
        : this(TUnitNetWasmAdapterComposition.CreateExecutionApplication())
    {
    }

    internal TUnitNetWasmTestExecutor(ITestExecutionApplication application)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
    }

    public void RunTests(
        IEnumerable<TestCase>? tests,
        IRunContext? runContext,
        IFrameworkHandle? frameworkHandle)
    {
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(runContext);
        ArgumentNullException.ThrowIfNull(frameworkHandle);
        _application.Run(tests, frameworkHandle);
    }

    public void RunTests(
        IEnumerable<string>? sources,
        IRunContext? runContext,
        IFrameworkHandle? frameworkHandle)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(runContext);
        ArgumentNullException.ThrowIfNull(frameworkHandle);
        _application.Run(sources, runContext, frameworkHandle);
    }

    public void Cancel() => _application.Cancel();
}
