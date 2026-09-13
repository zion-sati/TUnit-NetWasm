using NetWasm.TUnit.Runner.Composition;
using NetWasm.TUnit.Runner.Model;
using NetWasm.TUnit.Runner.Reporting;
using NetWasm.TUnit.Runner.Selection;
using TUnit.Core;

namespace NetWasm.TUnit.Runner.Hosting;

public static class TestApplication
{
    private const int ProtocolVersion = 2;

    public static async Task<int> RunAsync(
        ITestEntryCatalog catalog,
        string[] args,
        ITestEventSink? eventSink = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(args);

        var sink = eventSink ?? new ConsoleTestEventSink();
        try
        {
            sink.Write(new ProtocolVersionEvent(ProtocolVersion));
            var command = TestCommand.Parse(args);
            if (command.ListOnly)
            {
                return List(catalog, sink);
            }

            var result = await TestRunnerComposition.Create().RunAsync(
                catalog,
                command.Request,
                sink,
                cancellationToken);
            if (ContainsCancellation(result))
            {
                sink.Write(new HostResultEvent(3, "interrupted"));
                return 3;
            }

            return result.FailedCount == 0 ? 0 : 1;
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
        catch (Exception)
        {
            sink.Write(new HostResultEvent(2, "host-setup-failure"));
            return 2;
        }
    }

    private static int List(ITestEntryCatalog catalog, ITestEventSink sink)
    {
        var cases = new TestCaseResolver().Resolve(catalog, TestRunRequest.All);

        foreach (var testCase in cases)
        {
            sink.Write(new CatalogEntryEvent(
                testCase.StableId,
                testCase.DisplayName,
                testCase.FullyQualifiedName,
                testCase.FilePath,
                testCase.LineNumber));
            foreach (var category in testCase.Categories)
            {
                sink.Write(new CatalogTraitEvent(testCase.StableId, "TestCategory", category));
            }
            foreach (var property in testCase.Properties)
            {
                sink.Write(new CatalogTraitEvent(testCase.StableId, "Property", property));
            }
        }

        sink.Write(new HostResultEvent(0, $"listed={cases.Count}"));
        return 0;
    }

    private static bool ContainsCancellation(TestRunResult result)
    {
        foreach (var testCase in result.Cases)
        {
            if (testCase.Outcome == TestOutcome.Cancelled)
            {
                return true;
            }
        }

        return false;
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
