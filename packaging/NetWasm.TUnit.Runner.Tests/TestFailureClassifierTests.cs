using NetWasm.TUnit.Runner.Failures;
using NetWasm.TUnit.Runner.Model;
using TUnit.Assertions.Exceptions;

namespace NetWasm.TUnit.Runner.Tests;

public sealed class TestFailureClassifierTests
{
    private readonly ITestFailureClassifier _classifier = new TestFailureClassifier();

    [Fact]
    public void ClassifyRecognizesAssertionFailure()
    {
        var result = _classifier.Classify(new AssertionException("assertion"), CancellationToken.None);

        XunitAssert.Equal(TestOutcome.AssertionFailed, result.Outcome);
        XunitAssert.Equal("assertion", result.Message);
    }

    [Fact]
    public void ClassifyRecognizesUnexpectedFailure()
    {
        var result = _classifier.Classify(new InvalidOperationException("unexpected"), CancellationToken.None);

        XunitAssert.Equal(TestOutcome.UnexpectedFailure, result.Outcome);
    }

    [Fact]
    public void ClassifyRecognizesUnsupportedFailure()
    {
        var result = _classifier.Classify(new NotSupportedException("unsupported"), CancellationToken.None);

        XunitAssert.Equal(TestOutcome.Unsupported, result.Outcome);
    }

    [Fact]
    public void ClassifyRecognizesCancellationFromTheRunToken()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        var result = _classifier.Classify(new OperationCanceledException(source.Token), source.Token);

        XunitAssert.Equal(TestOutcome.Cancelled, result.Outcome);
    }

    [Fact]
    public void ClassifyRecognizesUniformAssertionAggregate()
    {
        var result = _classifier.Classify(
            new AggregateException(new AssertionException("first"), new AssertionException("second")),
            CancellationToken.None);

        XunitAssert.Equal(TestOutcome.AssertionFailed, result.Outcome);
    }

    [Fact]
    public void ClassifyTreatsMixedAggregateAsUnexpected()
    {
        var result = _classifier.Classify(
            new AggregateException(new AssertionException("assertion"), new InvalidOperationException("other")),
            CancellationToken.None);

        XunitAssert.Equal(TestOutcome.UnexpectedFailure, result.Outcome);
    }
}
