using NetWasm.TUnit.Runner.Failures;
using NetWasm.TUnit.Runner.Model;
using NetWasm.TUnit.Runner.Reporting;
using NetWasm.TUnit.Runner.Selection;
using TUnit.Core;

namespace NetWasm.TUnit.Runner.Execution;

public sealed class SequentialTestRunner(
    ITestCaseResolver resolver,
    ITestFailureClassifier failureClassifier) : ITestRunner
{
    private readonly ITestCaseResolver _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    private readonly ITestFailureClassifier _failureClassifier = failureClassifier ?? throw new ArgumentNullException(nameof(failureClassifier));

    public async ValueTask<TestRunResult> RunAsync(
        ITestEntryCatalog catalog,
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventSink);

        var cases = _resolver.Resolve(catalog, request);
        var groups = CreateGroups(cases);
        var results = new List<TestCaseResult>(cases.Count);
        var runTimer = Stopwatch.StartNew();
        eventSink.Write(new RunStartedEvent(cases.Count));

        foreach (var testCase in cases)
        {
            eventSink.Write(new TestStartedEvent(testCase.StableId, testCase.DisplayName));
            var caseTimer = Stopwatch.StartNew();
            var group = groups[testCase.GroupIdentity];

            if (!group.SetupAttempted)
            {
                group.SetupAttempted = true;
                group.SetupFailure = await InvokeClassSetupAsync(group.Lifecycle, cancellationToken);
            }

            Exception? failure = group.SetupFailure;
            var unsupported = false;
            if (failure is null)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (testCase.InvocationKind == GeneratedInvocationKind.Unsupported)
                    {
                        unsupported = true;
                    }
                    else
                    {
                        await testCase.ExecuteAsync(cancellationToken);
                    }
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            }

            group.Remaining--;
            if (group.Remaining == 0)
            {
                var teardownFailure = await InvokeClassTeardownAsync(group.Lifecycle);
                if (teardownFailure is not null)
                {
                    failure = failure is null
                        ? teardownFailure
                        : new AggregateException(failure, teardownFailure);
                    unsupported = false;
                }
            }

            var result = CreateResult(testCase, failure, unsupported, cancellationToken) with
            {
                Duration = caseTimer.Elapsed,
            };
            results.Add(result);
            eventSink.Write(new TestCompletedEvent(result));
        }

        var runResult = new TestRunResult(results)
        {
            Duration = runTimer.Elapsed,
        };
        eventSink.Write(new RunCompletedEvent(runResult));
        return runResult;
    }

    private TestCaseResult CreateResult(
        GeneratedTestCase testCase,
        Exception? failure,
        bool unsupported,
        CancellationToken cancellationToken)
    {
        if (failure is null)
        {
            return new TestCaseResult(
                testCase.StableId,
                testCase.DisplayName,
                unsupported ? TestOutcome.Unsupported : TestOutcome.Passed,
                unsupported ? "The generated invocation shape is not supported." : null);
        }

        var classification = _failureClassifier.Classify(failure, cancellationToken);
        return new TestCaseResult(
            testCase.StableId,
            testCase.DisplayName,
            classification.Outcome,
            classification.Message);
    }

    private static Dictionary<string, GroupExecutionState> CreateGroups(
        IReadOnlyList<GeneratedTestCase> cases)
    {
        var groups = new Dictionary<string, GroupExecutionState>(StringComparer.Ordinal);
        foreach (var testCase in cases)
        {
            if (groups.TryGetValue(testCase.GroupIdentity, out var group))
            {
                group.Remaining++;
            }
            else
            {
                groups.Add(testCase.GroupIdentity, new GroupExecutionState(testCase.Lifecycle));
            }
        }

        return groups;
    }

    private static async ValueTask<Exception?> InvokeClassSetupAsync(
        GeneratedLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        foreach (var action in lifecycle.Actions)
        {
            if (action.Stage != GeneratedLifecycleStage.ClassSetup)
            {
                continue;
            }

            try
            {
                await action.Invoke(null, cancellationToken);
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        return null;
    }

    private static async ValueTask<Exception?> InvokeClassTeardownAsync(GeneratedLifecycle lifecycle)
    {
        List<Exception>? failures = null;
        foreach (var action in lifecycle.Actions)
        {
            if (action.Stage != GeneratedLifecycleStage.ClassTeardown)
            {
                continue;
            }

            try
            {
                await action.Invoke(null, CancellationToken.None);
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        return failures?.Count switch
        {
            null => null,
            1 => failures[0],
            _ => new AggregateException(failures),
        };
    }

    private sealed class GroupExecutionState(GeneratedLifecycle lifecycle)
    {
        public GeneratedLifecycle Lifecycle { get; } = lifecycle;

        public int Remaining { get; set; } = 1;

        public bool SetupAttempted { get; set; }

        public Exception? SetupFailure { get; set; }
    }
}
