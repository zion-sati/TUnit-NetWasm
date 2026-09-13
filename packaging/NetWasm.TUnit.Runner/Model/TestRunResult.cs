using System.Collections.ObjectModel;

namespace NetWasm.TUnit.Runner.Model;

public sealed class TestRunResult
{
    public TestRunResult(IEnumerable<TestCaseResult> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);

        Cases = new ReadOnlyCollection<TestCaseResult>(new List<TestCaseResult>(cases));
    }

    public IReadOnlyList<TestCaseResult> Cases { get; }

    public TimeSpan Duration { get; init; }

    public int PassedCount => Count(TestOutcome.Passed);

    public int FailedCount => Cases.Count - PassedCount;

    private int Count(TestOutcome outcome)
    {
        var count = 0;
        foreach (var result in Cases)
        {
            if (result.Outcome == outcome)
            {
                count++;
            }
        }

        return count;
    }
}
