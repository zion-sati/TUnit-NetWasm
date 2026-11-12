using System.Text;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using NetWasm.TUnit.Runner.Failures;
using NetWasm.TUnit.Runner.Model;
using NetWasm.TUnit.Runner.Reporting;

namespace NetWasm.TUnit.Runner.Hosting;

/// <summary>
/// The NetWasm test program: runs TUnit's engine in-process in place of Microsoft.Testing.Platform's test application,
/// and reports through the guest protocol the VSTest adapter reads. <c>--list</c> writes the catalog,
/// <c>--run</c> (or no arguments) runs every test, and repeated <c>--id &lt;id&gt;</c> runs those tests. Statuses:
/// 0 passed, 1 a test or the session failed, 2 the host could not run, 3 interrupted.
/// </summary>
public static class TestApplication
{
    private const int ProtocolVersion = 2;

    public static async Task<int> RunAsync(
        string[] args,
        Func<IServiceProvider, ITestFramework> createFramework,
        ITestEventSink? eventSink = null,
        TextWriter? diagnostics = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(createFramework);

        // TUnit redirects Console.Out and Console.Error per test, so the protocol and diagnostics use the process's
        // own streams, opened before the engine starts.
        var sink = eventSink ?? new ConsoleTestEventSink(RawStream.Open(Console.OpenStandardOutput()).WriteLine);
        diagnostics ??= RawStream.Open(Console.OpenStandardError());
        try
        {
            sink.Write(new ProtocolVersionEvent(ProtocolVersion));
            var command = TestCommand.Parse(args);
            var session = new EngineSession(createFramework(new PlatformServices(diagnostics)));
            return command.ListOnly
                ? await ListAsync(session, sink, cancellationToken)
                : await RunTestsAsync(session, command.Request, sink, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            sink.Write(new HostResultEvent(3, "interrupted"));
            return 3;
        }
        catch (ArgumentException exception)
        {
            sink.Write(new HostResultEvent(2, exception.Message));
            return 2;
        }
        catch (Exception exception)
        {
            diagnostics.WriteLine(exception.ToString());
            sink.Write(new HostResultEvent(2, "host-setup-failure"));
            return 2;
        }
    }

    private static async Task<int> ListAsync(EngineSession session, ITestEventSink sink, CancellationToken cancellationToken)
    {
        var catalog = new CatalogMessageBus(sink);
        await session.RunAsync(static context => new DiscoverTestExecutionRequest(context), catalog, cancellationToken);
        sink.Write(new HostResultEvent(0, $"listed={catalog.ListedCount}"));
        return 0;
    }

    private static async Task<int> RunTestsAsync(
        EngineSession session,
        TestRunRequest request,
        ITestEventSink sink,
        CancellationToken cancellationToken)
    {
        var selected = request.StableIds;
        sink.Write(new RunStartedEvent(selected?.Count ?? 0));

        var results = new ExecutionMessageBus(sink, new TestFailureClassifier(), cancellationToken);
        var started = Stopwatch.GetTimestamp();
        ITestExecutionFilter? filter = selected is null
            ? null
            : new TestNodeUidListFilter([.. selected.Select(static id => new TestNodeUid(id))]);
        var sessionSucceeded = await session.RunAsync(
            context => filter is null ? new RunTestExecutionRequest(context) : new RunTestExecutionRequest(context, filter),
            results,
            cancellationToken);

        var result = new TestRunResult(results.Results) { Duration = Stopwatch.GetElapsedTime(started) };
        sink.Write(new RunCompletedEvent(result));
        cancellationToken.ThrowIfCancellationRequested();
        if (result.Cases.Any(static testCase => testCase.Outcome == TestOutcome.Cancelled))
        {
            sink.Write(new HostResultEvent(3, "interrupted"));
            return 3;
        }

        if (selected?.FirstOrDefault(id => !results.HasCompleted(id)) is { } unknown)
        {
            throw new ArgumentException($"No test has the ID '{unknown}'.", nameof(request));
        }

        return result.FailedCount == 0 && sessionSucceeded ? 0 : 1;
    }

    private static class RawStream
    {
        public static TextWriter Open(Stream stream) =>
            new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
    }

    private sealed record TestCommand(bool ListOnly, TestRunRequest Request)
    {
        public static TestCommand Parse(string[] args)
        {
            if (args.Length == 0 || (args.Length == 1 && args[0] == "--run"))
            {
                return new TestCommand(false, TestRunRequest.All);
            }

            if (args.Length == 1 && args[0] == "--list")
            {
                return new TestCommand(true, TestRunRequest.All);
            }

            if (args.Length >= 2 && args.Length % 2 == 0)
            {
                var stableIds = new List<string>(args.Length / 2);
                for (var index = 0; index < args.Length; index += 2)
                {
                    if (args[index] != "--id")
                    {
                        throw new ArgumentException("Expected --list, --run, or repeated --id <stable-id> arguments.", nameof(args));
                    }

                    stableIds.Add(args[index + 1]);
                }

                return new TestCommand(false, new TestRunRequest(stableIds));
            }

            throw new ArgumentException("Expected --list, --run, or repeated --id <stable-id> arguments.", nameof(args));
        }
    }
}
