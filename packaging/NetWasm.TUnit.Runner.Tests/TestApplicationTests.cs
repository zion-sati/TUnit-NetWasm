using NetWasm.TUnit.Runner.Hosting;
using NetWasm.TUnit.Runner.Model;
using TUnit.Core;

namespace NetWasm.TUnit.Runner.Tests;

public sealed class TestApplicationTests
{
    [Fact]
    public async Task ListWritesTheGeneratedCatalogInStableOrder()
    {
        var sink = new RecordingEventSink();

        var status = await TestApplication.RunAsync(Catalog("b", "a"), ["--list"], sink, Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(0, status);
        XunitAssert.Collection(
            sink.Events,
            protocol => XunitAssert.Equal(2, XunitAssert.IsType<ProtocolVersionEvent>(protocol).Version),
            first => XunitAssert.Equal("a", XunitAssert.IsType<CatalogEntryEvent>(first).StableId),
            second => XunitAssert.Equal("b", XunitAssert.IsType<CatalogEntryEvent>(second).StableId),
            result => XunitAssert.Equal(0, XunitAssert.IsType<HostResultEvent>(result).Status));
    }

    [Fact]
    public async Task RunExecutesOnlyTheRequestedStableIds()
    {
        var invoked = new List<string>();
        var catalog = new SourceGeneratedTestCatalog(
        [
            TestCaseFactory.Create("a", invoke: (_, _) => Record("a")),
            TestCaseFactory.Create("b", invoke: (_, _) => Record("b")),
        ]);
        var sink = new RecordingEventSink();

        var status = await TestApplication.RunAsync(catalog, ["--id", "b"], sink, Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(0, status);
        XunitAssert.Equal(["b"], invoked);
        XunitAssert.IsType<RunCompletedEvent>(sink.Events[^1]);
        return;

        ValueTask Record(string stableId)
        {
            invoked.Add(stableId);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task RunReturnsFailureWhenASelectedCaseThrows()
    {
        var catalog = new SourceGeneratedTestCatalog(
        [
            TestCaseFactory.Create(
                "failure",
                invoke: static (_, _) => ValueTask.FromException(new InvalidOperationException("expected"))),
        ]);

        var status = await TestApplication.RunAsync(catalog, ["--run"], new RecordingEventSink(), Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(1, status);
    }

    [Fact]
    public async Task InvalidInvocationReturnsSetupFailure()
    {
        var sink = new RecordingEventSink();

        var status = await TestApplication.RunAsync(Catalog("a"), ["--unknown"], sink, Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(2, status);
        AssertSetupFailure(sink);
    }

    [Fact]
    public async Task EmptyCatalogReturnsSetupFailure()
    {
        var sink = new RecordingEventSink();

        var status = await TestApplication.RunAsync(new SourceGeneratedTestCatalog([]), ["--run"], sink, Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(2, status);
        AssertSetupFailure(sink);
    }

    [Fact]
    public async Task UnexpectedCatalogFailureReturnsSetupFailure()
    {
        var sink = new RecordingEventSink();

        var status = await TestApplication.RunAsync(new ThrowingCatalog(), ["--run"], sink, Xunit.TestContext.Current.CancellationToken);

        XunitAssert.Equal(2, status);
        var result = AssertSetupFailure(sink);
        XunitAssert.Equal(2, result.Status);
        XunitAssert.Equal("host-setup-failure", result.Message);
    }

    [Fact]
    public async Task CancellationReturnsHostInterruptionStatus()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var sink = new RecordingEventSink();

        var status = await TestApplication.RunAsync(
            Catalog("a"),
            ["--run"],
            sink,
            source.Token);

        XunitAssert.Equal(3, status);
        XunitAssert.Equal(3, XunitAssert.IsType<HostResultEvent>(sink.Events[^1]).Status);
    }

    private static SourceGeneratedTestCatalog Catalog(params string[] stableIds) =>
        new(stableIds.Select(stableId => TestCaseFactory.Create(stableId)));

    private static HostResultEvent AssertSetupFailure(RecordingEventSink sink)
    {
        XunitAssert.Collection(
            sink.Events,
            protocol => XunitAssert.Equal(2, XunitAssert.IsType<ProtocolVersionEvent>(protocol).Version),
            result => XunitAssert.Equal(2, XunitAssert.IsType<HostResultEvent>(result).Status));
        return XunitAssert.IsType<HostResultEvent>(sink.Events[^1]);
    }

    private sealed class ThrowingCatalog : ITestEntryCatalog
    {
        public IReadOnlyList<GeneratedTestCase> GetGeneratedCases() =>
            throw new InvalidOperationException("catalog is unavailable");
    }
}
