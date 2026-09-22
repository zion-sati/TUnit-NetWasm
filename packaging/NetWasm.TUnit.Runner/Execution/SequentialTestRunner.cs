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
        var completedResults = new Dictionary<string, TestCaseResult>(StringComparer.Ordinal);
        var runTimer = Stopwatch.StartNew();
        eventSink.Write(new RunStartedEvent(cases.Count));

        foreach (var testCase in cases)
        {
            eventSink.Write(new TestStartedEvent(testCase.StableId, testCase.DisplayName));
            var caseTimer = Stopwatch.StartNew();
            var group = groups[testCase.GroupIdentity];
            Exception? failure = null;
            var unsupported = false;
            var skipped = false;
            var skipReason = testCase.SkipReason;
            var attemptCount = 0;

            if (skipReason is not null)
            {
                skipped = true;
            }
            else if (TryGetBlockingDependency(testCase, cases, completedResults, out var dependencyReason))
            {
                skipped = true;
                skipReason = dependencyReason;
            }
            else
            {
                if (!group.SetupAttempted)
                {
                    group.SetupAttempted = true;
                    var setup = await InvokeClassSetupAsync(group.Lifecycle, cancellationToken);
                    group.SetupFailure = setup.Failure;
                }

                failure = group.SetupFailure;
                if (failure is null)
                {
                    if (testCase.InvocationKind == GeneratedInvocationKind.Unsupported)
                    {
                        failure = cancellationToken.IsCancellationRequested
                            ? new OperationCanceledException(cancellationToken)
                            : null;
                        unsupported = failure is null;
                    }
                    else
                    {
                        var execution = await ExecuteWithRetryAsync(testCase, cancellationToken);
                        failure = execution.Failure;
                        attemptCount = execution.AttemptCount;
                    }
                }
            }

            Task? abandonedCaseExecution = null;
            if (TryGetAbandonedExecution(failure, out abandonedCaseExecution))
            {
                group.PendingExecutions.Add(abandonedCaseExecution);
            }

            group.Remaining--;
            Task? deferredDataBoundary = null;
            if (group.Remaining == 0)
            {
                Exception? teardownFailure = null;
                if (group.SetupAttempted)
                {
                    if (group.PendingExecutions.Count > 0)
                    {
                        deferredDataBoundary = CompleteDeferredClassTeardownAsync(
                            group.Lifecycle,
                            group.PendingExecutions,
                            0);
                    }
                    else
                    {
                        teardownFailure = await InvokeClassTeardownAsync(group.Lifecycle);
                        if (TryGetAbandonedExecution(teardownFailure, out var abandonedTeardown))
                        {
                            deferredDataBoundary = abandonedTeardown;
                        }
                    }
                }
                if (teardownFailure is not null)
                {
                    failure = failure is null
                        ? teardownFailure
                        : new AggregateException(failure, teardownFailure);
                    unsupported = false;
                    skipped = false;
                }
            }

            var dataBoundary = deferredDataBoundary ?? abandonedCaseExecution;
            if (dataBoundary is not null)
            {
                _ = DisposeCaseDataAfterAsync(testCase, dataBoundary);
            }
            else
            {
                try
                {
                    await testCase.DisposeDataAsync();
                }
                catch (Exception exception)
                {
                    failure = failure is null
                        ? exception
                        : new AggregateException(failure, exception);
                    unsupported = false;
                    skipped = false;
                }
            }

            var result = CreateResult(testCase, failure, unsupported, skipped, skipReason, cancellationToken) with
            {
                Duration = caseTimer.Elapsed,
                AttemptCount = attemptCount,
            };
            results.Add(result);
            completedResults.Add(testCase.StableId, result);
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
        bool skipped,
        string? skipReason,
        CancellationToken cancellationToken)
    {
        if (failure is null)
        {
            return new TestCaseResult(
                testCase.StableId,
                testCase.DisplayName,
                skipped ? TestOutcome.Skipped : unsupported ? TestOutcome.Unsupported : TestOutcome.Passed,
                skipped ? skipReason : unsupported ? "The generated invocation shape is not supported." : null);
        }

        var classification = _failureClassifier.Classify(failure, cancellationToken);
        return new TestCaseResult(
            testCase.StableId,
            testCase.DisplayName,
            classification.Outcome,
            classification.Message);
    }

    private static async ValueTask<AttemptExecution> ExecuteWithRetryAsync(
        GeneratedTestCase testCase,
        CancellationToken cancellationToken)
    {
        Exception? failure = null;
        var attemptCount = 0;
        for (var retryIndex = 0; retryIndex <= testCase.RetryPolicy.MaxRetries; retryIndex++)
        {
            attemptCount++;
            failure = await ExecuteAttemptAsync(testCase, cancellationToken);
            if (failure is null)
            {
                return new AttemptExecution(null, attemptCount);
            }

            if (cancellationToken.IsCancellationRequested ||
                retryIndex == testCase.RetryPolicy.MaxRetries ||
                TryGetAbandonedExecution(failure, out _) ||
                !testCase.RetryPolicy.Accepts(failure))
            {
                break;
            }

            var delay = testCase.RetryPolicy.GetDelay(retryIndex + 1);
            if (delay > TimeSpan.Zero)
            {
                var delayFailure = await DelayBeforeRetryAsync(delay, cancellationToken);
                if (delayFailure is not null)
                {
                    failure = delayFailure;
                    break;
                }
            }
        }

        return new AttemptExecution(failure, attemptCount);
    }

    private static async ValueTask<Exception?> DelayBeforeRetryAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async ValueTask<Exception?> ExecuteAttemptAsync(
        GeneratedTestCase testCase,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await testCase.ExecuteAsync(cancellationToken);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static bool TryGetBlockingDependency(
        GeneratedTestCase testCase,
        IReadOnlyList<GeneratedTestCase> cases,
        IReadOnlyDictionary<string, TestCaseResult> completedResults,
        out string? reason)
    {
        foreach (var dependency in testCase.Dependencies)
        {
            var separator = dependency.IndexOf(':');
            var className = separator < 0 ? string.Empty : dependency[..separator];
            var methodName = separator < 0 ? dependency : dependency[(separator + 1)..];
            foreach (var candidate in cases)
            {
                if (!string.Equals(candidate.MethodName, methodName, StringComparison.Ordinal) ||
                    (className.Length == 0
                        ? !string.Equals(candidate.GroupIdentity, testCase.GroupIdentity, StringComparison.Ordinal)
                        : !candidate.GroupIdentity.EndsWith(className, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (!completedResults.TryGetValue(candidate.StableId, out var result) ||
                    result.Outcome != TestOutcome.Passed)
                {
                    reason = $"Dependency '{candidate.DisplayName}' did not pass.";
                    return true;
                }
            }
        }

        reason = null;
        return false;
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

    private static async ValueTask<ClassSetupExecution> InvokeClassSetupAsync(
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
                await action.InvokeAsync(null, cancellationToken);
            }
            catch (Exception exception)
            {
                return new ClassSetupExecution(exception);
            }
        }

        return new ClassSetupExecution(null);
    }

    private static async ValueTask<Exception?> InvokeClassTeardownAsync(GeneratedLifecycle lifecycle)
    {
        List<Exception>? failures = null;
        for (var actionIndex = 0; actionIndex < lifecycle.Actions.Count; actionIndex++)
        {
            var action = lifecycle.Actions[actionIndex];
            if (action.Stage != GeneratedLifecycleStage.ClassTeardown)
            {
                continue;
            }

            try
            {
                await action.InvokeAsync(null, CancellationToken.None);
            }
            catch (Exception exception)
            {
                if (TryGetAbandonedExecution(exception, out var teardownExecution))
                {
                    var cleanupCompletion = CompleteDeferredClassTeardownAsync(
                        lifecycle,
                        new[] { teardownExecution },
                        actionIndex + 1);
                    (failures ??= []).Add(ReplaceExecutionCompletion(exception, cleanupCompletion));
                    break;
                }

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

    private static async Task CompleteDeferredClassTeardownAsync(
        GeneratedLifecycle lifecycle,
        IReadOnlyList<Task> abandonedExecutions,
        int teardownStartIndex)
    {
        foreach (var abandonedExecution in abandonedExecutions)
        {
            await ObserveCleanupCompletionAsync(abandonedExecution);
        }

        for (var actionIndex = teardownStartIndex; actionIndex < lifecycle.Actions.Count; actionIndex++)
        {
            var action = lifecycle.Actions[actionIndex];
            if (action.Stage != GeneratedLifecycleStage.ClassTeardown)
            {
                continue;
            }

            var exception = await InvokeDeferredClassTeardownActionAsync(action);
            if (exception is not null && TryGetAbandonedExecution(exception, out var teardownExecution))
            {
                await ObserveCleanupCompletionAsync(teardownExecution);
            }
        }
    }

    private static async ValueTask<Exception?> InvokeDeferredClassTeardownActionAsync(
        GeneratedLifecycleAction action)
    {
        try
        {
            await action.InvokeAsync(null, CancellationToken.None);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async ValueTask ObserveCleanupCompletionAsync(Task execution)
    {
        try
        {
            await execution;
        }
        catch
        {
            // The earlier timeout or cancellation remains the reported outcome.
        }
    }

    private static bool TryGetAbandonedExecution(Exception? exception, out Task execution)
    {
        if (exception is GeneratedTimeoutException { ExecutionWasAbandoned: true } timeout)
        {
            execution = timeout.ExecutionCompletion;
            return true;
        }

        if (exception is GeneratedCancellationException { ExecutionWasAbandoned: true } cancellation)
        {
            execution = cancellation.ExecutionCompletion;
            return true;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (var innerException in aggregate.InnerExceptions)
            {
                if (TryGetAbandonedExecution(innerException, out execution))
                {
                    return true;
                }
            }
        }

        execution = null!;
        return false;
    }

    private static Exception ReplaceExecutionCompletion(Exception exception, Task executionCompletion) =>
        exception switch
        {
            GeneratedTimeoutException timeout => new GeneratedTimeoutException(
                timeout.Message,
                executionCompletion,
                executionWasAbandoned: true),
            GeneratedCancellationException cancellation => new GeneratedCancellationException(
                cancellation.CancellationToken,
                executionCompletion,
                executionWasAbandoned: true),
            _ => exception,
        };

    private static async Task DisposeCaseDataAfterAsync(GeneratedTestCase testCase, Task execution)
    {
        try
        {
            await execution;
        }
        catch
        {
            // The timeout is the reported failure.
        }

        try
        {
            await testCase.DisposeDataAsync();
        }
        catch
        {
            // Late disposal cannot replace the already reported timeout.
        }
    }

    private sealed class GroupExecutionState(GeneratedLifecycle lifecycle)
    {
        public GeneratedLifecycle Lifecycle { get; } = lifecycle;

        public int Remaining { get; set; } = 1;

        public bool SetupAttempted { get; set; }

        public Exception? SetupFailure { get; set; }

        public List<Task> PendingExecutions { get; } = [];
    }

    private readonly record struct ClassSetupExecution(Exception? Failure);

    private readonly record struct AttemptExecution(Exception? Failure, int AttemptCount);
}
