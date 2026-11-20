namespace NetWasm.TUnit.Runner.Model;

public sealed record TestCaseResult(
    string StableId,
    string DisplayName,
    TestOutcome Outcome,
    string? Message)
{
    public TimeSpan Duration { get; init; }

    public int AttemptCount { get; init; } = 1;

    // The failure's stack trace, and what the test wrote to standard output and error, as TUnit captured them.
    public string? StackTrace { get; init; }

    public string? StandardOutput { get; init; }

    public string? StandardError { get; init; }
}
