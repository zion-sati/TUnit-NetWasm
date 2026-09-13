namespace NetWasm.TUnit.Runner.Model;

public sealed record TestCaseResult(
    string StableId,
    string DisplayName,
    TestOutcome Outcome,
    string? Message)
{
    public TimeSpan Duration { get; init; }
}
