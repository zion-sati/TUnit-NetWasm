using System.Collections.ObjectModel;
using NetWasm.TUnit.Runner.Model;
using TUnit.Core;

namespace NetWasm.TUnit.Runner.Selection;

public sealed class TestCaseResolver : ITestCaseResolver
{
    public IReadOnlyList<GeneratedTestCase> Resolve(ITestEntryCatalog catalog, TestRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);

        var cases = catalog.GetGeneratedCases();
        ValidateCatalog(cases);
        if (cases.Count == 0)
        {
            throw new ArgumentException("The generated test catalog is empty.", nameof(catalog));
        }

        if (request.StableIds is null)
        {
            return cases;
        }

        var requested = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stableId in request.StableIds)
        {
            if (!requested.Add(stableId))
            {
                throw new ArgumentException($"Stable ID '{stableId}' was selected more than once.", nameof(request));
            }
        }

        if (requested.Count == 0)
        {
            throw new ArgumentException("At least one stable ID must be selected.", nameof(request));
        }

        var selected = new List<GeneratedTestCase>(requested.Count);
        foreach (var testCase in cases)
        {
            if (requested.Contains(testCase.StableId))
            {
                selected.Add(testCase);
            }
        }

        if (selected.Count != requested.Count)
        {
            throw new ArgumentException("At least one selected stable ID is not present in the catalog.", nameof(request));
        }

        return new ReadOnlyCollection<GeneratedTestCase>(selected);
    }

    private static void ValidateCatalog(IReadOnlyList<GeneratedTestCase> cases)
    {
        if (cases is null)
        {
            throw new ArgumentException("The generated test catalog returned no case collection.", nameof(cases));
        }

        var stableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testCase in cases)
        {
            if (testCase is null)
            {
                throw new ArgumentException("The generated test catalog cannot contain a null case.", nameof(cases));
            }

            if (!stableIds.Add(testCase.StableId))
            {
                throw new ArgumentException(
                    $"The generated test catalog contains duplicate stable ID '{testCase.StableId}'.",
                    nameof(cases));
            }

            if (testCase.InvocationKind is not
                (GeneratedInvocationKind.Sync
                or GeneratedInvocationKind.Task
                or GeneratedInvocationKind.ValueTask
                or GeneratedInvocationKind.Unsupported))
            {
                throw new ArgumentException(
                    $"The generated test catalog contains unsupported invocation kind for '{testCase.StableId}'.",
                    nameof(cases));
            }
        }
    }
}
