namespace NetWasm.TUnit.Runner.Model;

public enum TestOutcome
{
    Passed,
    AssertionFailed,
    UnexpectedFailure,
    Unsupported,
    Cancelled,
}
