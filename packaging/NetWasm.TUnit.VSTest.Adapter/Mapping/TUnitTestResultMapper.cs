using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using NetWasm.TUnit.VSTest.Adapter.Protocol;

namespace NetWasm.TUnit.VSTest.Adapter.Mapping;

internal sealed class TUnitTestResultMapper : ITUnitTestResultMapper
{
    public TestResult Create(TestCase testCase, TUnitCompletedCase completed)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        ArgumentNullException.ThrowIfNull(completed);

        var result = new TestResult(testCase)
        {
            Duration = completed.Duration,
            Outcome = completed.Outcome switch
            {
                "passed" => TestOutcome.Passed,
                "assertion-failed" or "unexpected-failure" => TestOutcome.Failed,
                "unsupported" or "cancelled" => TestOutcome.Skipped,
                _ => throw new InvalidDataException($"Unknown TUnit test outcome '{completed.Outcome}'."),
            },
        };
        if (completed.Message is not null)
        {
            result.ErrorMessage = completed.Message;
        }
        return result;
    }
}
