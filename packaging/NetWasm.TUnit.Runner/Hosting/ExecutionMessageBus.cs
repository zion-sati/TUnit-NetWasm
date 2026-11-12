using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using NetWasm.TUnit.Runner.Failures;
using NetWasm.TUnit.Runner.Model;
using NetWasm.TUnit.Runner.Reporting;

namespace NetWasm.TUnit.Runner.Hosting;

/// <summary>
/// Writes the engine's progress as protocol records: a test's first in-progress state as test-started, and its final
/// state as test-completed with the outcome the failure classifier assigns to its exception.
/// </summary>
public sealed class ExecutionMessageBus(
    ITestEventSink sink,
    ITestFailureClassifier classifier,
    CancellationToken cancellationToken) : IMessageBus
{
    private readonly HashSet<string> _started = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TestCaseResult> _completed = new(StringComparer.Ordinal);

    public IReadOnlyCollection<TestCaseResult> Results => _completed.Values;

    public bool HasCompleted(string uid) => _completed.ContainsKey(uid);

    public Task PublishAsync(IDataProducer dataProducer, IData data)
    {
        if (data is not TestNodeUpdateMessage { TestNode: var node })
        {
            return Task.CompletedTask;
        }

        var uid = node.Uid.Value;
        var state = node.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (state is InProgressTestNodeStateProperty)
        {
            if (_started.Add(uid))
            {
                sink.Write(new TestStartedEvent(uid, node.DisplayName));
            }

            return Task.CompletedTask;
        }

        if (state is null or DiscoveredTestNodeStateProperty || _completed.ContainsKey(uid))
        {
            return Task.CompletedTask;
        }

        var (outcome, message) = Classify(state);
        var result = new TestCaseResult(uid, node.DisplayName, outcome, message)
        {
            Duration = node.Properties.SingleOrDefault<TimingProperty>()?.GlobalTiming.Duration ?? TimeSpan.Zero,
        };
        _completed.Add(uid, result);
        sink.Write(new TestCompletedEvent(result));
        return Task.CompletedTask;
    }

    private (TestOutcome Outcome, string? Message) Classify(TestNodeStateProperty state)
    {
        switch (state)
        {
            case PassedTestNodeStateProperty:
                return (TestOutcome.Passed, null);
            case SkippedTestNodeStateProperty skipped:
                return (TestOutcome.Skipped, skipped.Explanation);
            case TimeoutTestNodeStateProperty timeout:
                return (TestOutcome.TimedOut, timeout.Explanation ?? timeout.Exception?.Message);
#pragma warning disable CS0618, MTP0001 // TUnit still reports cancellation with this state.
            case CancelledTestNodeStateProperty cancelled:
                return (TestOutcome.Cancelled, cancelled.Explanation ?? cancelled.Exception?.Message);
#pragma warning restore CS0618, MTP0001
            case FailedTestNodeStateProperty { Exception: { } exception }:
                return FromException(exception);
            case ErrorTestNodeStateProperty { Exception: { } exception }:
                return FromException(exception);
            default:
                return (TestOutcome.UnexpectedFailure, state.Explanation);
        }
    }

    private (TestOutcome, string?) FromException(Exception exception)
    {
        var classification = classifier.Classify(EngineExceptions.Unwrap(exception), cancellationToken);
        return (classification.Outcome, classification.Message);
    }
}
