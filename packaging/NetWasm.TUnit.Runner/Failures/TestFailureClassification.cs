using NetWasm.TUnit.Runner.Model;

namespace NetWasm.TUnit.Runner.Failures;

public sealed record TestFailureClassification(TestOutcome Outcome, string? Message);
