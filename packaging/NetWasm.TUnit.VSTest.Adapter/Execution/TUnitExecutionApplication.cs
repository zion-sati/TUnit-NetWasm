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
    ITUnitTestResultMapper results,
    ITUnitRunPlanner runs) : ITestExecutionApplication
{
    private readonly ITUnitGuestClient _guest = guest ?? throw new ArgumentNullException(nameof(guest));
    private readonly ITUnitTestCaseMapper _testCases = testCases ?? throw new ArgumentNullException(nameof(testCases));
    private readonly ITUnitTestCaseFilter _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    private readonly ITUnitTestResultMapper _results = results ?? throw new ArgumentNullException(nameof(results));
    private readonly ITUnitRunPlanner _runs = runs ?? throw new ArgumentNullException(nameof(runs));
    private readonly CancellationTokenSource _cancellation = new();

    public void Run(IEnumerable<TestCase> tests, IFrameworkHandle frameworkHandle)
    {
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(frameworkHandle);

        foreach (var sourceGroup in tests.GroupBy(static test => test.Source, StringComparer.Ordinal))
        {
            RunSource(sourceGroup.Key, sourceGroup.ToArray(), catalogCount: null, frameworkHandle);
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
                RunSource(source, _filter.Apply(tests, runContext), tests.Length, frameworkHandle);
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
                    $"TUnit execution failed for '{source}': {Describe(exception)}");
            }
        }
    }

    public void Cancel() => _cancellation.Cancel();

    private void RunSource(
        string source,
        IReadOnlyCollection<TestCase> tests,
        int? catalogCount,
        IFrameworkHandle frameworkHandle)
    {
        if (tests.Count == 0)
        {
            return;
        }

        try
        {
            var selectedById = tests.ToDictionary(_testCases.GetStableId, StringComparer.Ordinal);
            foreach (var plan in _runs.Plan([.. selectedById.Keys], catalogCount))
            {
                RunPlan(source, plan, selectedById, frameworkHandle);
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
                $"TUnit execution failed for '{source}': {Describe(exception)}");
        }
    }

    private void RunPlan(
        string source,
        TUnitGuestRunPlan plan,
        Dictionary<string, TestCase> selectedById,
        IFrameworkHandle frameworkHandle)
    {
        var run = _guest.RunAsync(source, plan.Arguments, _cancellation.Token)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        ReportOutput(run, frameworkHandle);
        var protocol = TUnitGuestRunValidator.ValidateExecution(run, plan.StableIds.Length);
        var planned = plan.StableIds.ToHashSet(StringComparer.Ordinal);

        var started = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stableId in protocol.StartedStableIds)
        {
            if (!planned.Contains(stableId) || !started.Add(stableId))
            {
                throw new InvalidDataException(
                    $"TUnit guest emitted an invalid test-started record for '{stableId}'.");
            }
            frameworkHandle.RecordStart(selectedById[stableId]);
        }

        var completed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var result in protocol.CompletedCases)
        {
            if (!planned.Contains(result.StableId) || !completed.Add(result.StableId))
            {
                throw new InvalidDataException(
                    $"TUnit guest emitted an invalid test-completed record for '{result.StableId}'.");
            }
            var testCase = selectedById[result.StableId];
            if (started.Add(result.StableId))
            {
                frameworkHandle.RecordStart(testCase);
            }
            var mapped = _results.Create(testCase, result);
            frameworkHandle.RecordResult(mapped);
            frameworkHandle.RecordEnd(testCase, mapped.Outcome);
        }

        if (completed.Count != plan.StableIds.Length)
        {
            throw new InvalidDataException("TUnit guest did not complete every selected test.");
        }
    }

    // The failure and its causes, so a host that cannot start says why (an argument list too long, say).
    private static string Describe(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }
        return string.Join(" ", messages);
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
