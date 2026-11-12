using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using NetWasm.TUnit.Runner.Hosting;
using NetWasm.TUnit.Runner.Model;
using TUnit.Assertions.Exceptions;
using static NetWasm.TUnit.Runner.Tests.ScriptedTestFramework;

namespace NetWasm.TUnit.Runner.Tests;

// The host turns the engine's test nodes into the guest protocol and statuses the VSTest adapter reads.
public sealed class TestApplicationTests
{
    private static CancellationToken Cancellation => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListWritesEachDiscoveredTestWithItsTraits()
    {
        var framework = new ScriptedTestFramework(_ =>
        [
            Node(
                "a",
                DiscoveredTestNodeStateProperty.CachedInstance,
                new TestMethodIdentifierProperty("Tests", "Space", "Fixture", "Adds", 0, [], "System.Void"),
                new TestFileLocationProperty("/src/Fixture.cs", new LinePositionSpan(new LinePosition(12, -1), new LinePosition(12, -1))),
                new TestMetadataProperty("fast"),
                new TestMetadataProperty("owner", "core")),
        ]);
        var sink = new RecordingEventSink();

        var status = await TestApplication.RunAsync(["--list"], _ => framework, sink, TextWriter.Null, Cancellation);

        XunitAssert.Equal(0, status);
        XunitAssert.IsType<DiscoverTestExecutionRequest>(XunitAssert.Single(framework.Requests));
        XunitAssert.True(framework.SessionClosed);
        XunitAssert.Equal(2, XunitAssert.IsType<ProtocolVersionEvent>(sink.Events[0]).Version);
        XunitAssert.Equal(new CatalogEntryEvent("a", "a", "Space.Fixture.Adds", "/src/Fixture.cs", 12), sink.Events[1]);
        // A category is metadata with no value; a property has one. Their order is the property bag's.
        XunitAssert.Equivalent(
            new TestRunEvent[] { new CatalogTraitEvent("a", "TestCategory", "fast"), new CatalogTraitEvent("a", "Property", "owner=core") },
            sink.Events.OfType<CatalogTraitEvent>().ToArray());
        XunitAssert.Equal(new HostResultEvent(0, "listed=1"), sink.Events[^1]);
        XunitAssert.Equal(5, sink.Events.Count);
    }

    [Fact]
    public async Task RunFiltersTheEngineByTheSelectedIds()
    {
        var framework = new ScriptedTestFramework(_ =>
        [
            Node("b", InProgressTestNodeStateProperty.CachedInstance),
            Node("b", PassedTestNodeStateProperty.CachedInstance),
        ]);
        var sink = new RecordingEventSink();

        var status = await TestApplication.RunAsync(["--id", "b"], _ => framework, sink, TextWriter.Null, Cancellation);

        XunitAssert.Equal(0, status);
        var request = XunitAssert.IsType<RunTestExecutionRequest>(XunitAssert.Single(framework.Requests));
        var filter = XunitAssert.IsType<TestNodeUidListFilter>(request.Filter);
        XunitAssert.Equal(["b"], filter.TestNodeUids.Select(static uid => uid.Value));
        XunitAssert.Collection(
            sink.Events,
            protocol => XunitAssert.IsType<ProtocolVersionEvent>(protocol),
            started => XunitAssert.Equal(1, XunitAssert.IsType<RunStartedEvent>(started).SelectedCount),
            testStarted => XunitAssert.Equal("b", XunitAssert.IsType<TestStartedEvent>(testStarted).StableId),
            completed => XunitAssert.Equal(TestOutcome.Passed, XunitAssert.IsType<TestCompletedEvent>(completed).Result.Outcome),
            runCompleted => XunitAssert.Equal(1, XunitAssert.IsType<RunCompletedEvent>(runCompleted).Result.PassedCount));
    }

    [Fact]
    public async Task RunAllSelectsNoTests()
    {
        var framework = new ScriptedTestFramework(_ => [Node("a", PassedTestNodeStateProperty.CachedInstance)]);

        var status = await TestApplication.RunAsync([], _ => framework, new RecordingEventSink(), TextWriter.Null, Cancellation);

        XunitAssert.Equal(0, status);
        // Without a filter the platform's request carries its no-op filter.
        XunitAssert.IsNotType<TestNodeUidListFilter>(
            XunitAssert.IsType<RunTestExecutionRequest>(XunitAssert.Single(framework.Requests)).Filter);
    }

    [Fact]
    public async Task FinalStatesMapToTheProtocolOutcomes()
    {
        var framework = new ScriptedTestFramework(_ =>
        [
            Node("assertion", new FailedTestNodeStateProperty(new AssertionException("expected 1"), "failed")),
            Node("unsupported", new ErrorTestNodeStateProperty(new PlatformNotSupportedException("no threads"), "error")),
            Node("unexpected", new ErrorTestNodeStateProperty(new InvalidOperationException("boom"), "error")),
            Node("skipped", new SkippedTestNodeStateProperty("not on WASI")),
            Node("timeout", new TimeoutTestNodeStateProperty("took too long")),
            Node("wrapped", new FailedTestNodeStateProperty(
                new global::TUnit.Engine.Exceptions.TestFailedException(new PlatformNotSupportedException("inner")), "failed")),
        ]);
        var sink = new RecordingEventSink();

        var status = await TestApplication.RunAsync(["--run"], _ => framework, sink, TextWriter.Null, Cancellation);

        XunitAssert.Equal(1, status);
        var outcomes = sink.Events.OfType<TestCompletedEvent>().ToDictionary(static e => e.Result.StableId, static e => e.Result);
        XunitAssert.Equal(TestOutcome.AssertionFailed, outcomes["assertion"].Outcome);
        XunitAssert.Equal("expected 1", outcomes["assertion"].Message);
        XunitAssert.Equal(TestOutcome.Unsupported, outcomes["unsupported"].Outcome);
        XunitAssert.Equal(TestOutcome.UnexpectedFailure, outcomes["unexpected"].Outcome);
        XunitAssert.Equal(TestOutcome.Skipped, outcomes["skipped"].Outcome);
        XunitAssert.Equal("not on WASI", outcomes["skipped"].Message);
        XunitAssert.Equal(TestOutcome.TimedOut, outcomes["timeout"].Outcome);
        XunitAssert.Equal(TestOutcome.Unsupported, outcomes["wrapped"].Outcome);
        XunitAssert.Equal("inner", outcomes["wrapped"].Message);
    }

    [Fact]
    public async Task ASelectedIdTheEngineDoesNotRunIsASetupFailure()
    {
        var framework = new ScriptedTestFramework(_ => []);
        var sink = new RecordingEventSink();

        var status = await TestApplication.RunAsync(["--id", "missing"], _ => framework, sink, TextWriter.Null, Cancellation);

        XunitAssert.Equal(2, status);
        XunitAssert.Equal(2, XunitAssert.IsType<HostResultEvent>(sink.Events[^1]).Status);
    }

    [Fact]
    public async Task AFailedSessionFailsTheRun()
    {
        var framework = new ScriptedTestFramework(_ => [Node("a", PassedTestNodeStateProperty.CachedInstance)], sessionSucceeds: false);

        var status = await TestApplication.RunAsync([], _ => framework, new RecordingEventSink(), TextWriter.Null, Cancellation);

        XunitAssert.Equal(1, status);
    }

    [Fact]
    public async Task InvalidInvocationReturnsSetupFailure()
    {
        var sink = new RecordingEventSink();

        var status = await TestApplication.RunAsync(
            ["--unknown"],
            _ => new ScriptedTestFramework(_ => []),
            sink,
            TextWriter.Null,
            Cancellation);

        XunitAssert.Equal(2, status);
        XunitAssert.Equal(2, XunitAssert.IsType<HostResultEvent>(sink.Events[^1]).Status);
    }

    [Fact]
    public async Task AFrameworkThatCannotStartIsASetupFailure()
    {
        var sink = new RecordingEventSink();

        var status = await TestApplication.RunAsync(
            [],
            _ => throw new InvalidOperationException("no engine"),
            sink,
            TextWriter.Null,
            Cancellation);

        XunitAssert.Equal(2, status);
        XunitAssert.Equal(new HostResultEvent(2, "host-setup-failure"), sink.Events[^1]);
    }

    [Fact]
    public async Task CancellationReturnsTheInterruptedStatus()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var sink = new RecordingEventSink();

        var status = await TestApplication.RunAsync(
            [],
            _ => new ScriptedTestFramework(_ => [Node("a", PassedTestNodeStateProperty.CachedInstance)]),
            sink,
            TextWriter.Null,
            cancellation.Token);

        XunitAssert.Equal(3, status);
        XunitAssert.Equal(new HostResultEvent(3, "interrupted"), sink.Events[^1]);
    }
}
