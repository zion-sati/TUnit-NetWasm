using NetWasm.TUnit.Runner.Composition;
using NetWasm.TUnit.Runner.Execution;
using NetWasm.TUnit.Runner.Model;
using TUnit.Assertions.Exceptions;
using TUnit.Core;

namespace NetWasm.TUnit.Runner.Tests;

public sealed class SequentialTestRunnerTests
{
    private readonly ITestRunner _runner = TestRunnerComposition.Create();

    [Fact]
    public async Task RunAsyncExecutesSelectedCasesInCatalogOrderAndReportsEvents()
    {
        var invoked = new List<string>();
        var catalog = new SourceGeneratedTestCatalog([
            Case("b", GeneratedInvocationKind.Task),
            Case("a", GeneratedInvocationKind.Sync),
            Case("c", GeneratedInvocationKind.ValueTask),
        ]);
        var sink = new RecordingEventSink();

        var result = await _runner.RunAsync(catalog, new TestRunRequest(["c", "a"]), sink, Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(["a", "c"], invoked);
        XunitAssert.Equal(2, result.PassedCount);
        XunitAssert.Equal(0, result.FailedCount);
        XunitAssert.All(result.Cases, testCase => XunitAssert.True(testCase.Duration >= TimeSpan.Zero));
        XunitAssert.True(result.Duration >= TimeSpan.Zero);
        XunitAssert.Collection(
            sink.Events,
            item => XunitAssert.IsType<RunStartedEvent>(item),
            item => XunitAssert.Equal("a", XunitAssert.IsType<TestStartedEvent>(item).StableId),
            item => XunitAssert.Equal("a", XunitAssert.IsType<TestCompletedEvent>(item).Result.StableId),
            item => XunitAssert.Equal("c", XunitAssert.IsType<TestStartedEvent>(item).StableId),
            item => XunitAssert.Equal("c", XunitAssert.IsType<TestCompletedEvent>(item).Result.StableId),
            item => XunitAssert.IsType<RunCompletedEvent>(item));

        GeneratedTestCase Case(string id, GeneratedInvocationKind kind) =>
            TestCaseFactory.Create(
                id,
                invocationKind: kind,
                invoke: (_, _) =>
                {
                    invoked.Add(id);
                    return ValueTask.CompletedTask;
                });
    }

    [Fact]
    public async Task RunAsyncScopesClassLifecycleOnceAroundTheCompleteGroup()
    {
        var sequence = new List<string>();
        var lifecycle = new GeneratedLifecycle([
            Action(GeneratedLifecycleStage.ClassSetup, "class-setup"),
            Action(GeneratedLifecycleStage.TestSetup, "test-setup"),
            Action(GeneratedLifecycleStage.TestTeardown, "test-teardown"),
            Action(GeneratedLifecycleStage.ClassTeardown, "class-teardown"),
        ]);
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create("b", invoke: Body("body-b"), lifecycle: lifecycle),
            TestCaseFactory.Create("a", invoke: Body("body-a"), lifecycle: lifecycle),
        ]);

        var result = await _runner.RunAsync(catalog, TestRunRequest.All, new RecordingEventSink(), Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(2, result.PassedCount);
        XunitAssert.Equal(
            [
                "class-setup",
                "test-setup",
                "body-a",
                "test-teardown",
                "test-setup",
                "body-b",
                "test-teardown",
                "class-teardown",
            ],
            sequence);

        GeneratedLifecycleAction Action(GeneratedLifecycleStage stage, string value) =>
            new(stage, 0, (_, _) =>
            {
                sequence.Add(value);
                return ValueTask.CompletedTask;
            });

        Func<TestInstance, CancellationToken, ValueTask> Body(string value) => (_, _) =>
        {
            sequence.Add(value);
            return ValueTask.CompletedTask;
        };
    }

    [Fact]
    public async Task RunAsyncClassifiesAssertionUnexpectedAndUnsupportedCases()
    {
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create("assertion", invoke: static (_, _) => throw new AssertionException("assertion")),
            TestCaseFactory.Create("unexpected", invoke: static (_, _) => throw new InvalidOperationException("unexpected")),
            TestCaseFactory.Create("unsupported", invocationKind: GeneratedInvocationKind.Unsupported),
        ]);

        var result = await _runner.RunAsync(catalog, TestRunRequest.All, new RecordingEventSink(), Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(
            [
                TestOutcome.AssertionFailed,
                TestOutcome.UnexpectedFailure,
                TestOutcome.Unsupported,
            ],
            result.Cases.Select(testCase => testCase.Outcome));
    }

    [Fact]
    public async Task RunAsyncClassifiesCancellationFromTheRunToken()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var catalog = new SourceGeneratedTestCatalog([TestCaseFactory.Create("cancelled")]);

        var result = await _runner.RunAsync(
            catalog,
            TestRunRequest.All,
            new RecordingEventSink(),
            source.Token);

        XunitAssert.Equal(TestOutcome.Cancelled, XunitAssert.Single(result.Cases).Outcome);
    }

    [Fact]
    public async Task RunAsyncClassifiesCancellationBeforeUnsupportedInvocation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create("unsupported", invocationKind: GeneratedInvocationKind.Unsupported)]);

        var result = await _runner.RunAsync(
            catalog,
            TestRunRequest.All,
            new RecordingEventSink(),
            source.Token);

        XunitAssert.Equal(TestOutcome.Cancelled, XunitAssert.Single(result.Cases).Outcome);
    }

    [Fact]
    public async Task RunAsyncDoesNotInvokeCasesAfterClassSetupFailureAndStillRunsTeardown()
    {
        var bodyCount = 0;
        var teardownCount = 0;
        var lifecycle = new GeneratedLifecycle([
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.ClassSetup,
                0,
                static (_, _) => throw new InvalidOperationException("setup")),
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.ClassTeardown,
                0,
                (_, _) =>
                {
                    teardownCount++;
                    return ValueTask.CompletedTask;
                }),
        ]);
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create("a", invoke: Body, lifecycle: lifecycle),
            TestCaseFactory.Create("b", invoke: Body, lifecycle: lifecycle),
        ]);

        var result = await _runner.RunAsync(catalog, TestRunRequest.All, new RecordingEventSink(), Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(0, bodyCount);
        XunitAssert.Equal(1, teardownCount);
        XunitAssert.All(result.Cases, testCase => XunitAssert.Equal(TestOutcome.UnexpectedFailure, testCase.Outcome));

        ValueTask Body(TestInstance _, CancellationToken __)
        {
            bodyCount++;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task RunAsyncMakesClassTeardownFailureFailTheLastCase()
    {
        var lifecycle = new GeneratedLifecycle([
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.ClassTeardown,
                0,
                static (_, _) => throw new InvalidOperationException("teardown")),
        ]);
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create("a", lifecycle: lifecycle),
            TestCaseFactory.Create("b", lifecycle: lifecycle),
        ]);

        var result = await _runner.RunAsync(catalog, TestRunRequest.All, new RecordingEventSink(), Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(TestOutcome.Passed, result.Cases[0].Outcome);
        XunitAssert.Equal(TestOutcome.UnexpectedFailure, result.Cases[1].Outcome);
    }

    [Fact]
    public async Task RunAsyncSkipsWithoutConstructingOrRunningLifecycle()
    {
        var operationCount = 0;
        var lifecycle = new GeneratedLifecycle([
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.TestSetup,
                0,
                (_, _) =>
                {
                    operationCount++;
                    return ValueTask.CompletedTask;
                }),
        ]);
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create(
                "skipped",
                createInstance: () =>
                {
                    operationCount++;
                    return new TestInstance();
                },
                invoke: (_, _) =>
                {
                    operationCount++;
                    return ValueTask.CompletedTask;
                },
                lifecycle: lifecycle,
                skipReason: "not today"),
        ]);

        var result = await _runner.RunAsync(catalog, TestRunRequest.All, new RecordingEventSink(), Xunit.TestContext.Current.CancellationToken);

        var testResult = XunitAssert.Single(result.Cases);
        XunitAssert.Equal(TestOutcome.Skipped, testResult.Outcome);
        XunitAssert.Equal("not today", testResult.Message);
        XunitAssert.Equal(0, testResult.AttemptCount);
        XunitAssert.Equal(0, operationCount);
    }

    [Fact]
    public async Task RunAsyncRetriesWithFreshFixtureLifecycleAndAsyncDisposal()
    {
        var nextInstanceId = 0;
        var bodyAttempts = 0;
        var setupInstances = new List<int>();
        var teardownInstances = new List<int>();
        var asyncDisposedInstances = new List<int>();
        var syncDisposeCount = 0;
        var lifecycle = new GeneratedLifecycle([
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.TestSetup,
                0,
                (instance, _) =>
                {
                    setupInstances.Add(((RecordingTestInstance)instance!).Id);
                    return ValueTask.CompletedTask;
                }),
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.TestTeardown,
                0,
                (instance, _) =>
                {
                    teardownInstances.Add(((RecordingTestInstance)instance!).Id);
                    return ValueTask.CompletedTask;
                }),
        ]);
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create(
                "retry",
                createInstance: () => new RecordingTestInstance(
                    ++nextInstanceId,
                    id => asyncDisposedInstances.Add(id),
                    () => syncDisposeCount++),
                invoke: (_, _) =>
                {
                    bodyAttempts++;
                    return bodyAttempts < 3
                        ? ValueTask.FromException(new InvalidOperationException("retry"))
                        : ValueTask.CompletedTask;
                },
                lifecycle: lifecycle,
                retryPolicy: new GeneratedRetryPolicy(2)),
        ]);

        var result = await _runner.RunAsync(catalog, TestRunRequest.All, new RecordingEventSink(), Xunit.TestContext.Current.CancellationToken);

        var testResult = XunitAssert.Single(result.Cases);
        XunitAssert.Equal(TestOutcome.Passed, testResult.Outcome);
        XunitAssert.Equal(3, testResult.AttemptCount);
        XunitAssert.Equal([1, 2, 3], setupInstances);
        XunitAssert.Equal([1, 2, 3], teardownInstances);
        XunitAssert.Equal([1, 2, 3], asyncDisposedInstances);
        XunitAssert.Equal(0, syncDisposeCount);
    }

    [Fact]
    public async Task RunAsyncReusesCaseDataAcrossRetriesAndDoesNotCreateSkippedData()
    {
        var initialCreatedCount = RecordingCaseData.CreatedCount;
        var retryData = new GeneratedCaseData<RecordingCaseData>(() => new RecordingCaseData());
        var skippedData = new GeneratedCaseData<RecordingCaseData>(() => new RecordingCaseData());
        var attempts = 0;
        var observed = new List<RecordingCaseData>();
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create(
                "retry-data",
                invoke: (_, _) =>
                {
                    observed.Add(retryData.Get());
                    attempts++;
                    return attempts < 3
                        ? ValueTask.FromException(new InvalidOperationException("retry"))
                        : ValueTask.CompletedTask;
                },
                retryPolicy: new GeneratedRetryPolicy(2),
                disposeData: retryData.DisposeAsync),
            TestCaseFactory.Create(
                "skipped-data",
                invoke: (_, _) =>
                {
                    skippedData.Get();
                    return ValueTask.CompletedTask;
                },
                skipReason: "skip",
                disposeData: skippedData.DisposeAsync),
        ]);

        var result = await _runner.RunAsync(catalog, TestRunRequest.All, new RecordingEventSink(), Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(TestOutcome.Passed, result.Cases[0].Outcome);
        XunitAssert.Equal(TestOutcome.Skipped, result.Cases[1].Outcome);
        XunitAssert.Equal(3, result.Cases[0].AttemptCount);
        XunitAssert.Equal(3, observed.Count);
        XunitAssert.True(observed.All(item => ReferenceEquals(item, observed[0])));
        XunitAssert.Equal(1, observed[0].DisposeCount);
        XunitAssert.Equal(initialCreatedCount + 1, RecordingCaseData.CreatedCount);
    }

    [Fact]
    public async Task RunAsyncHonorsRetryExceptionFilter()
    {
        var attempts = 0;
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create(
                "retry-filter",
                invoke: (_, _) =>
                {
                    attempts++;
                    return ValueTask.FromException(new InvalidOperationException("stop"));
                },
                retryPolicy: new GeneratedRetryPolicy(
                    3,
                    shouldRetry: static exception => exception is ArgumentException)),
        ]);

        var result = await _runner.RunAsync(catalog, TestRunRequest.All, new RecordingEventSink(), Xunit.TestContext.Current.CancellationToken);

        var testResult = XunitAssert.Single(result.Cases);
        XunitAssert.Equal(TestOutcome.UnexpectedFailure, testResult.Outcome);
        XunitAssert.Equal(1, testResult.AttemptCount);
        XunitAssert.Equal(1, attempts);
    }

    [Fact]
    public async Task RunAsyncReportsCancellationDuringRetryBackoffAndStillCleansUp()
    {
        using var cancellation = new CancellationTokenSource();
        var firstAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var teardownCount = 0;
        var dataDisposeCount = 0;
        var lifecycle = new GeneratedLifecycle([
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.ClassTeardown,
                0,
                (_, _) =>
                {
                    teardownCount++;
                    return ValueTask.CompletedTask;
                }),
        ]);
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create(
                "retry-cancellation",
                invoke: (_, _) =>
                {
                    firstAttempt.TrySetResult();
                    return ValueTask.FromException(new InvalidOperationException("retry"));
                },
                lifecycle: lifecycle,
                retryPolicy: new GeneratedRetryPolicy(2, backoffMilliseconds: 30_000),
                disposeData: () =>
                {
                    dataDisposeCount++;
                    return ValueTask.CompletedTask;
                }),
        ]);
        var sink = new RecordingEventSink();

        var execution = _runner.RunAsync(catalog, TestRunRequest.All, sink, cancellation.Token).AsTask();
        await firstAttempt.Task;
        cancellation.Cancel();
        var result = await execution;

        var testResult = XunitAssert.Single(result.Cases);
        XunitAssert.Equal(TestOutcome.Cancelled, testResult.Outcome);
        XunitAssert.Equal(1, testResult.AttemptCount);
        XunitAssert.Equal(1, teardownCount);
        XunitAssert.Equal(1, dataDisposeCount);
        XunitAssert.IsType<TestCompletedEvent>(sink.Events[^2]);
        XunitAssert.IsType<RunCompletedEvent>(sink.Events[^1]);
    }

    [Fact]
    public async Task RunAsyncAppliesTimeoutOnlyToTheTestBody()
    {
        var lifecycle = new GeneratedLifecycle([
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.TestSetup,
                0,
                async (_, cancellationToken) => await Task.Delay(30, cancellationToken)),
        ]);
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create(
                "body-timeout-only",
                lifecycle: lifecycle,
                timeout: TimeSpan.FromMilliseconds(10)),
        ]);

        var result = await _runner.RunAsync(catalog, TestRunRequest.All, new RecordingEventSink(), Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(TestOutcome.Passed, XunitAssert.Single(result.Cases).Outcome);
    }

    [Fact]
    public async Task RunAsyncDefersCleanupForAnAbandonedTimedOutBodyAndDoesNotRetry()
    {
        var bodyCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var teardownCount = 0;
        var classTeardownCount = 0;
        var disposeCount = 0;
        var lifecycle = new GeneratedLifecycle([
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.TestTeardown,
                0,
                (_, _) =>
                {
                    teardownCount++;
                    return ValueTask.CompletedTask;
                }),
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.ClassTeardown,
                0,
                (_, _) =>
                {
                    classTeardownCount++;
                    return ValueTask.CompletedTask;
                }),
        ]);
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create(
                "timeout",
                createInstance: () => new RecordingTestInstance(
                    1,
                    _ => disposeCount++),
                invoke: (_, _) =>
                {
                    attempts++;
                    return new ValueTask(bodyCompletion.Task);
                },
                lifecycle: lifecycle,
                timeout: TimeSpan.FromMilliseconds(10),
                retryPolicy: new GeneratedRetryPolicy(2),
                disposeData: () =>
                {
                    cleanupCompletion.TrySetResult();
                    return ValueTask.CompletedTask;
                }),
        ]);

        var result = await _runner.RunAsync(catalog, TestRunRequest.All, new RecordingEventSink(), Xunit.TestContext.Current.CancellationToken);

        var testResult = XunitAssert.Single(result.Cases);
        XunitAssert.Equal(TestOutcome.TimedOut, testResult.Outcome);
        XunitAssert.Equal(1, testResult.AttemptCount);
        XunitAssert.Equal(1, attempts);
        XunitAssert.Equal(0, teardownCount);
        XunitAssert.Equal(0, classTeardownCount);
        XunitAssert.Equal(0, disposeCount);

        bodyCompletion.SetResult();
        var cleanupRace = await Task.WhenAny(
            cleanupCompletion.Task,
            Task.Delay(TimeSpan.FromSeconds(2), Xunit.TestContext.Current.CancellationToken));
        XunitAssert.Same(cleanupCompletion.Task, cleanupRace);
        XunitAssert.Equal(1, teardownCount);
        XunitAssert.Equal(1, classTeardownCount);
        XunitAssert.Equal(1, disposeCount);
    }

    [Fact]
    public async Task RunAsyncDefersClassResourcesWhenCancellationAbandonsClassSetup()
    {
        using var cancellation = new CancellationTokenSource();
        var setupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var setupCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var classTeardownCount = 0;
        var dataDisposeCount = 0;
        var lifecycle = new GeneratedLifecycle([
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.ClassSetup,
                0,
                (_, _) =>
                {
                    setupStarted.TrySetResult();
                    return new ValueTask(setupCompletion.Task);
                },
                TimeSpan.FromMinutes(1)),
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.ClassTeardown,
                0,
                (_, _) =>
                {
                    classTeardownCount++;
                    return ValueTask.CompletedTask;
                }),
        ]);
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create(
                "cancelled-class-setup",
                lifecycle: lifecycle,
                disposeData: () =>
                {
                    dataDisposeCount++;
                    cleanupCompletion.TrySetResult();
                    return ValueTask.CompletedTask;
                }),
        ]);

        var execution = _runner.RunAsync(
            catalog,
            TestRunRequest.All,
            new RecordingEventSink(),
            cancellation.Token).AsTask();
        await setupStarted.Task;
        cancellation.Cancel();
        var result = await execution;

        XunitAssert.Equal(TestOutcome.Cancelled, XunitAssert.Single(result.Cases).Outcome);
        XunitAssert.Equal(0, classTeardownCount);
        XunitAssert.Equal(0, dataDisposeCount);

        setupCompletion.SetResult();
        var cleanupRace = await Task.WhenAny(
            cleanupCompletion.Task,
            Task.Delay(TimeSpan.FromSeconds(2), Xunit.TestContext.Current.CancellationToken));
        XunitAssert.Same(cleanupCompletion.Task, cleanupRace);
        XunitAssert.Equal(1, classTeardownCount);
        XunitAssert.Equal(1, dataDisposeCount);
    }

    [Fact]
    public async Task RunAsyncPreservesOwnershipWhenTimeoutCancellationCallbackThrows()
    {
        var bodyCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixtureDisposeCount = 0;
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create(
                "throwing-cancellation-callback",
                createInstance: () => new RecordingTestInstance(1, _ => fixtureDisposeCount++),
                invoke: (_, cancellationToken) =>
                {
                    cancellationToken.Register(static () => throw new InvalidOperationException("callback"));
                    return new ValueTask(bodyCompletion.Task);
                },
                timeout: TimeSpan.FromMilliseconds(10),
                disposeData: () =>
                {
                    cleanupCompletion.TrySetResult();
                    return ValueTask.CompletedTask;
                }),
        ]);

        var result = await _runner.RunAsync(
            catalog,
            TestRunRequest.All,
            new RecordingEventSink(),
            Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(TestOutcome.TimedOut, XunitAssert.Single(result.Cases).Outcome);
        XunitAssert.Equal(0, fixtureDisposeCount);

        bodyCompletion.SetResult();
        var cleanupRace = await Task.WhenAny(
            cleanupCompletion.Task,
            Task.Delay(TimeSpan.FromSeconds(2), Xunit.TestContext.Current.CancellationToken));
        XunitAssert.Same(cleanupCompletion.Task, cleanupRace);
        XunitAssert.Equal(1, fixtureDisposeCount);
    }

    [Fact]
    public async Task RunAsyncDoesNotRetryAnAggregateContainingAbandonedTeardown()
    {
        var teardownCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var dataDisposeCount = 0;
        var lifecycle = new GeneratedLifecycle([
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.TestTeardown,
                0,
                (_, _) => new ValueTask(teardownCompletion.Task),
                TimeSpan.FromMilliseconds(10)),
        ]);
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create(
                "aggregate-timeout",
                invoke: (_, _) =>
                {
                    attempts++;
                    return ValueTask.FromException(new InvalidOperationException("body"));
                },
                lifecycle: lifecycle,
                retryPolicy: new GeneratedRetryPolicy(2),
                disposeData: () =>
                {
                    dataDisposeCount++;
                    cleanupCompletion.TrySetResult();
                    return ValueTask.CompletedTask;
                }),
        ]);

        var result = await _runner.RunAsync(
            catalog,
            TestRunRequest.All,
            new RecordingEventSink(),
            Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(1, attempts);
        XunitAssert.Equal(1, XunitAssert.Single(result.Cases).AttemptCount);
        XunitAssert.Equal(0, dataDisposeCount);

        teardownCompletion.SetResult();
        var cleanupRace = await Task.WhenAny(
            cleanupCompletion.Task,
            Task.Delay(TimeSpan.FromSeconds(2), Xunit.TestContext.Current.CancellationToken));
        XunitAssert.Same(cleanupCompletion.Task, cleanupRace);
        XunitAssert.Equal(1, dataDisposeCount);
    }

    [Fact]
    public async Task RunAsyncKeepsOuterResourcesUntilCancelledBodyAndTestCleanupComplete()
    {
        using var cancellation = new CancellationTokenSource();
        var bodyStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bodyCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var teardownStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var teardownCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixtureDisposeCount = 0;
        var classTeardownCount = 0;
        var dataDisposeCount = 0;
        var lifecycle = new GeneratedLifecycle([
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.TestTeardown,
                0,
                (_, _) =>
                {
                    teardownStarted.TrySetResult();
                    return new ValueTask(teardownCompletion.Task);
                }),
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.ClassTeardown,
                0,
                (_, _) =>
                {
                    classTeardownCount++;
                    return ValueTask.CompletedTask;
                }),
        ]);
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create(
                "cancelled-body",
                createInstance: () => new RecordingTestInstance(1, _ => fixtureDisposeCount++),
                invoke: (_, _) =>
                {
                    bodyStarted.TrySetResult();
                    return new ValueTask(bodyCompletion.Task);
                },
                lifecycle: lifecycle,
                timeout: TimeSpan.FromMinutes(1),
                disposeData: () =>
                {
                    dataDisposeCount++;
                    cleanupCompletion.TrySetResult();
                    return ValueTask.CompletedTask;
                }),
        ]);

        var execution = _runner.RunAsync(
            catalog,
            TestRunRequest.All,
            new RecordingEventSink(),
            cancellation.Token).AsTask();
        await bodyStarted.Task;
        cancellation.Cancel();
        var result = await execution;

        XunitAssert.Equal(TestOutcome.Cancelled, XunitAssert.Single(result.Cases).Outcome);
        XunitAssert.Equal(0, fixtureDisposeCount);
        XunitAssert.Equal(0, classTeardownCount);
        XunitAssert.Equal(0, dataDisposeCount);

        bodyCompletion.SetResult();
        await teardownStarted.Task;
        XunitAssert.Equal(0, fixtureDisposeCount);
        XunitAssert.Equal(0, classTeardownCount);
        XunitAssert.Equal(0, dataDisposeCount);

        teardownCompletion.SetResult();
        var cleanupRace = await Task.WhenAny(
            cleanupCompletion.Task,
            Task.Delay(TimeSpan.FromSeconds(2), Xunit.TestContext.Current.CancellationToken));
        XunitAssert.Same(cleanupCompletion.Task, cleanupRace);
        XunitAssert.Equal(1, fixtureDisposeCount);
        XunitAssert.Equal(1, classTeardownCount);
        XunitAssert.Equal(1, dataDisposeCount);
    }

    [Fact]
    public async Task RunAsyncWaitsForNestedDeferredTeardownBeforeDisposal()
    {
        var bodyCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstTeardownStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstTeardownCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondTeardownCount = 0;
        var fixtureDisposeCount = 0;
        var dataDisposeCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycle = new GeneratedLifecycle([
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.TestTeardown,
                0,
                (_, _) =>
                {
                    firstTeardownStarted.TrySetResult();
                    return new ValueTask(firstTeardownCompletion.Task);
                },
                TimeSpan.FromMilliseconds(10)),
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.TestTeardown,
                1,
                (_, _) =>
                {
                    secondTeardownCount++;
                    return ValueTask.CompletedTask;
                }),
        ]);
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create(
                "nested-timeout",
                createInstance: () => new RecordingTestInstance(1, _ => fixtureDisposeCount++),
                invoke: (_, _) => new ValueTask(bodyCompletion.Task),
                lifecycle: lifecycle,
                timeout: TimeSpan.FromMilliseconds(10),
                disposeData: () =>
                {
                    dataDisposeCompletion.TrySetResult();
                    return ValueTask.CompletedTask;
                }),
        ]);

        await _runner.RunAsync(
            catalog,
            TestRunRequest.All,
            new RecordingEventSink(),
            Xunit.TestContext.Current.CancellationToken);
        bodyCompletion.SetResult();
        await firstTeardownStarted.Task;
        await Task.Delay(30, Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(0, secondTeardownCount);
        XunitAssert.Equal(0, fixtureDisposeCount);
        XunitAssert.False(dataDisposeCompletion.Task.IsCompleted);

        firstTeardownCompletion.SetResult();
        var cleanupRace = await Task.WhenAny(
            dataDisposeCompletion.Task,
            Task.Delay(TimeSpan.FromSeconds(2), Xunit.TestContext.Current.CancellationToken));
        XunitAssert.Same(dataDisposeCompletion.Task, cleanupRace);
        XunitAssert.Equal(1, secondTeardownCount);
        XunitAssert.Equal(1, fixtureDisposeCount);
    }

    [Fact]
    public async Task RunAsyncDefersClassTeardownUntilAnAbandonedClassSetupCompletes()
    {
        var setupCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var teardownCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var teardownCount = 0;
        var lifecycle = new GeneratedLifecycle([
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.ClassSetup,
                0,
                (_, _) => new ValueTask(setupCompletion.Task),
                TimeSpan.FromMilliseconds(10)),
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.ClassTeardown,
                0,
                (_, _) =>
                {
                    teardownCount++;
                    teardownCompletion.TrySetResult();
                    return ValueTask.CompletedTask;
                }),
        ]);
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create("class-timeout", lifecycle: lifecycle),
        ]);

        var result = await _runner.RunAsync(catalog, TestRunRequest.All, new RecordingEventSink(), Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(TestOutcome.TimedOut, XunitAssert.Single(result.Cases).Outcome);
        XunitAssert.Equal(0, teardownCount);

        setupCompletion.SetResult();
        var teardownRace = await Task.WhenAny(
            teardownCompletion.Task,
            Task.Delay(TimeSpan.FromSeconds(2), Xunit.TestContext.Current.CancellationToken));
        XunitAssert.Same(teardownCompletion.Task, teardownRace);
        XunitAssert.Equal(1, teardownCount);
    }

    [Fact]
    public async Task RunAsyncSkipsDependentsWhenAnyDependencyRowFails()
    {
        var dependentInvocations = 0;
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create(
                "dependency-row-1",
                methodName: "Dependency",
                invoke: static (_, _) => ValueTask.CompletedTask),
            TestCaseFactory.Create(
                "dependency-row-2",
                methodName: "Dependency",
                invoke: static (_, _) => ValueTask.FromException(new InvalidOperationException("failed"))),
            TestCaseFactory.Create(
                "dependent",
                methodName: "Dependent",
                dependencies: ["Dependency"],
                invoke: (_, _) =>
                {
                    dependentInvocations++;
                    return ValueTask.CompletedTask;
                }),
        ]);

        var result = await _runner.RunAsync(catalog, TestRunRequest.All, new RecordingEventSink(), Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(
            [TestOutcome.Passed, TestOutcome.UnexpectedFailure, TestOutcome.Skipped],
            result.Cases.Select(static testCase => testCase.Outcome));
        XunitAssert.Equal(0, dependentInvocations);
        XunitAssert.Contains("did not pass", result.Cases[2].Message);
    }

    private sealed class RecordingTestInstance(
        int id,
        Action<int> asyncDispose,
        Action? syncDispose = null) : TestInstance, IAsyncDisposable, IDisposable
    {
        public int Id { get; } = id;

        public ValueTask DisposeAsync()
        {
            asyncDispose(Id);
            return ValueTask.CompletedTask;
        }

        public void Dispose() => syncDispose?.Invoke();
    }

    private sealed class RecordingCaseData : IAsyncDisposable
    {
        public static int CreatedCount { get; private set; }

        public RecordingCaseData() => CreatedCount++;

        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return default;
        }
    }
}
