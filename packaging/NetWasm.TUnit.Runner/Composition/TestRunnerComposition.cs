using NetWasm.TUnit.Runner.Execution;
using NetWasm.TUnit.Runner.Failures;
using NetWasm.TUnit.Runner.Selection;

namespace NetWasm.TUnit.Runner.Composition;

public static class TestRunnerComposition
{
    public static ITestRunner Create() =>
        new SequentialTestRunner(
            new TestCaseResolver(),
            new TestFailureClassifier());
}
