using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace NetWasm.TUnit.VSTest.Adapter.Execution;

internal interface ITestExecutionApplication
{
    void Run(IEnumerable<TestCase> tests, IFrameworkHandle frameworkHandle);

    void Run(
        IEnumerable<string> sources,
        IRunContext runContext,
        IFrameworkHandle frameworkHandle);

    void Cancel();
}
