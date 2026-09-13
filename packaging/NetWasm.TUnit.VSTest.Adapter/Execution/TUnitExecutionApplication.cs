using System.Collections.Immutable;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using NetWasm.TUnit.VSTest.Adapter.Mapping;

namespace NetWasm.TUnit.VSTest.Adapter.Execution;

internal sealed class TUnitExecutionApplication(
    ITUnitGuestClient guest,
    ITUnitTestCaseMapper testCases,
    ITUnitTestCaseFilter filter,
    ITUnitTestResultMapper results) : ITestExecutionApplication
{
    private readonly ITUnitGuestClient _guest = guest ?? throw new ArgumentNullException(nameof(guest));
    private readonly ITUnitTestCaseMapper _testCases = testCases ?? throw new ArgumentNullException(nameof(testCases));
    private readonly ITUnitTestCaseFilter _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    private readonly ITUnitTestResultMapper _results = results ?? throw new ArgumentNullException(nameof(results));
    private readonly CancellationTokenSource _cancellation = new();

    public void Run(IEnumerable<TestCase> tests, IFrameworkHandle frameworkHandle)
    {
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(frameworkHandle);

        foreach (var sourceGroup in tests.GroupBy(static test => test.Source, StringComparer.Ordinal))
        {
            RunSource(sourceGroup.Key, sourceGroup.ToArray(), frameworkHandle);
        }
    }

    public void Run(
        IEnumerable<string> sources,
        IRunContext runContext,
        IFrameworkHandle frameworkHandle)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(runContext);
        ArgumentNullException.ThrowIfNull(frameworkHandle);

        foreach (var source in sources.Distinct(StringComparer.Ordinal))
        {
            try
            {
                var discovery = _guest.RunAsync(source, ["--list"], _cancellation.Token)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                ReportOutput(discovery, frameworkHandle);
                var discoveryProtocol = TUnitGuestRunValidator.ValidateDiscovery(discovery);
                var tests = discoveryProtocol.Catalog
                    .Select(testCase => _testCases.Create(source, testCase))
                    .ToArray();
                RunSource(source, _filter.Apply(tests, runContext), frameworkHandle);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                frameworkHandle.SendMessage(TestMessageLevel.Informational, "TUnit execution was cancelled.");
                return;
            }
            catch (Exception exception)
            {
                frameworkHandle.SendMessage(
                    TestMessageLevel.Error,
                    $"TUnit execution failed for '{source}': {exception.Message}");
            }
        }
    }

    public void Cancel() => _cancellation.Cancel();

    private void RunSource(
        string source,
        IReadOnlyCollection<TestCase> tests,
        IFrameworkHandle frameworkHandle)
    {
        if (tests.Count == 0)
        {
            return;
        }

        try
        {
            var selectedById = tests.ToDictionary(_testCases.GetStableId, StringComparer.Ordinal);
            var arguments = ImmutableArray.CreateBuilder<string>(selectedById.Count * 2);
            foreach (var stableId in selectedById.Keys)
            {
                arguments.Add("--id");
                arguments.Add(stableId);
            }

            var run = _guest.RunAsync(source, arguments.ToImmutable(), _cancellation.Token)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            ReportOutput(run, frameworkHandle);
            var protocol = TUnitGuestRunValidator.ValidateExecution(run, selectedById.Count);

            var started = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stableId in protocol.StartedStableIds)
            {
                if (!selectedById.TryGetValue(stableId, out var testCase) || !started.Add(stableId))
                {
                    throw new InvalidDataException(
                        $"TUnit guest emitted an invalid test-started record for '{stableId}'.");
                }
                frameworkHandle.RecordStart(testCase);
            }

            var completed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var result in protocol.CompletedCases)
            {
                if (!selectedById.TryGetValue(result.StableId, out var testCase)
                    || !completed.Add(result.StableId))
                {
                    throw new InvalidDataException(
                        $"TUnit guest emitted an invalid test-completed record for '{result.StableId}'.");
                }
                if (started.Add(result.StableId))
                {
                    frameworkHandle.RecordStart(testCase);
                }
                var mapped = _results.Create(testCase, result);
                frameworkHandle.RecordResult(mapped);
                frameworkHandle.RecordEnd(testCase, mapped.Outcome);
            }

            if (completed.Count != selectedById.Count)
            {
                throw new InvalidDataException("TUnit guest did not complete every selected test.");
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            frameworkHandle.SendMessage(TestMessageLevel.Informational, "TUnit execution was cancelled.");
        }
        catch (Exception exception)
        {
            frameworkHandle.SendMessage(
                TestMessageLevel.Error,
                $"TUnit execution failed for '{source}': {exception.Message}");
        }
    }

    private static void ReportOutput(TUnitGuestRun run, IFrameworkHandle frameworkHandle)
    {
        var output = run.Protocol?.UserOutput ?? run.StandardOutput;
        if (output.Length != 0)
        {
            frameworkHandle.SendMessage(TestMessageLevel.Informational, output);
        }
        if (run.StandardError.Length != 0)
        {
            frameworkHandle.SendMessage(TestMessageLevel.Warning, run.StandardError);
        }
    }
}
