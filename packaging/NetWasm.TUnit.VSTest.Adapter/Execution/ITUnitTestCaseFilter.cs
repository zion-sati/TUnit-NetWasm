using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace NetWasm.TUnit.VSTest.Adapter.Execution;

internal interface ITUnitTestCaseFilter
{
    IReadOnlyCollection<TestCase> Apply(
        IEnumerable<TestCase> testCases,
        IRunContext runContext);
}
