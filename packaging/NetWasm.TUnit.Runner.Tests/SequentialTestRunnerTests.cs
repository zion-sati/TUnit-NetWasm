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
}
