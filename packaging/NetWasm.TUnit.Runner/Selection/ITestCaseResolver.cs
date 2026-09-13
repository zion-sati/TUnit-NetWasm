using NetWasm.TUnit.Runner.Model;
using TUnit.Core;

namespace NetWasm.TUnit.Runner.Selection;

public interface ITestCaseResolver
{
    IReadOnlyList<GeneratedTestCase> Resolve(ITestEntryCatalog catalog, TestRunRequest request);
}
