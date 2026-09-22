namespace NetWasm.TUnit.Runner.Model;

public enum TestOutcome
{
    Passed,
    Skipped,
    AssertionFailed,
    UnexpectedFailure,
    Unsupported,
    Cancelled,
    TimedOut,
}
