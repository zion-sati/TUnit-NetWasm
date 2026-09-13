using NetWasm.TUnit.Runner.Model;
using TUnit.Assertions.Exceptions;

namespace NetWasm.TUnit.Runner.Failures;

public sealed class TestFailureClassifier : ITestFailureClassifier
{
    public TestFailureClassification Classify(Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return new TestFailureClassification(TestOutcome.Cancelled, exception.Message);
        }

        if (exception is NotSupportedException)
        {
            return new TestFailureClassification(TestOutcome.Unsupported, exception.Message);
        }

        if (exception is AggregateException aggregateException)
        {
            return ClassifyAggregate(aggregateException, cancellationToken);
        }

        return exception is BaseAssertionException
            ? new TestFailureClassification(TestOutcome.AssertionFailed, exception.Message)
            : new TestFailureClassification(TestOutcome.UnexpectedFailure, exception.Message);
    }

    private static TestFailureClassification ClassifyAggregate(
        AggregateException exception,
        CancellationToken cancellationToken)
    {
        var flattened = exception.Flatten();
        var hasAssertionFailure = false;
        var hasCancellation = false;
        var hasUnsupported = false;
        var hasUnexpectedFailure = false;
        foreach (var innerException in flattened.InnerExceptions)
        {
            if (innerException is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                hasCancellation = true;
                continue;
            }

            if (innerException is NotSupportedException)
            {
                hasUnsupported = true;
                continue;
            }

            if (innerException is BaseAssertionException)
            {
                hasAssertionFailure = true;
                continue;
            }

            hasUnexpectedFailure = true;
        }

        var categories = Convert.ToInt32(hasAssertionFailure)
            + Convert.ToInt32(hasCancellation)
            + Convert.ToInt32(hasUnsupported)
            + Convert.ToInt32(hasUnexpectedFailure);
        var outcome = categories != 1 || hasUnexpectedFailure
            ? TestOutcome.UnexpectedFailure
            : hasAssertionFailure
                ? TestOutcome.AssertionFailed
                : hasCancellation
                    ? TestOutcome.Cancelled
                    : TestOutcome.Unsupported;

        return new TestFailureClassification(outcome, exception.Message);
    }
}
