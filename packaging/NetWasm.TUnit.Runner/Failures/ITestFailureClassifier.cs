namespace NetWasm.TUnit.Runner.Failures;

public interface ITestFailureClassifier
{
    TestFailureClassification Classify(Exception exception, CancellationToken cancellationToken);
}
