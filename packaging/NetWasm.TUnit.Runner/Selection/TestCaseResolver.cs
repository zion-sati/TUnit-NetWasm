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
            var roots = new List<GeneratedTestCase>(cases.Count);
            foreach (var testCase in cases)
            {
                if (request.IncludeExplicit || !testCase.IsExplicit)
                {
                    roots.Add(testCase);
                }
            }

            return OrderWithDependencies(roots, cases);
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

        var containsOrdinary = false;
        foreach (var testCase in selected)
        {
            if (!testCase.IsExplicit)
            {
                containsOrdinary = true;
                break;
            }
        }

        if (containsOrdinary)
        {
            selected.RemoveAll(static testCase => testCase.IsExplicit);
        }

        return OrderWithDependencies(selected, cases);
    }

    private static IReadOnlyList<GeneratedTestCase> OrderWithDependencies(
        IEnumerable<GeneratedTestCase> roots,
        IReadOnlyList<GeneratedTestCase> allCases)
    {
        var selected = new Dictionary<string, GeneratedTestCase>(StringComparer.Ordinal);
        var pending = new Queue<GeneratedTestCase>(roots);
        while (pending.Count > 0)
        {
            var testCase = pending.Dequeue();
            if (!selected.TryAdd(testCase.StableId, testCase))
            {
                continue;
            }

            foreach (var dependency in ResolveDependencies(testCase, allCases))
            {
                pending.Enqueue(dependency);
            }
        }

        var incoming = new Dictionary<string, int>(StringComparer.Ordinal);
        var dependents = new Dictionary<string, List<GeneratedTestCase>>(StringComparer.Ordinal);
        foreach (var testCase in selected.Values)
        {
            incoming.Add(testCase.StableId, 0);
            dependents.Add(testCase.StableId, []);
        }
        foreach (var testCase in selected.Values)
        {
            foreach (var dependency in ResolveDependencies(testCase, allCases))
            {
                if (!selected.ContainsKey(dependency.StableId))
                {
                    continue;
                }

                incoming[testCase.StableId]++;
                dependents[dependency.StableId].Add(testCase);
            }
        }

        var available = new List<GeneratedTestCase>();
        foreach (var testCase in selected.Values)
        {
            if (incoming[testCase.StableId] == 0)
            {
                available.Add(testCase);
            }
        }
        var ordered = new List<GeneratedTestCase>(selected.Count);
        while (available.Count > 0)
        {
            available.Sort(CompareReadyCases);
            var next = available[0];
            available.RemoveAt(0);
            ordered.Add(next);
            foreach (var dependent in dependents[next.StableId])
            {
                incoming[dependent.StableId]--;
                if (incoming[dependent.StableId] == 0)
                {
                    available.Add(dependent);
                }
            }
        }

        if (ordered.Count != selected.Count)
        {
            throw new ArgumentException("The generated test dependency graph contains a cycle.", nameof(allCases));
        }

        return new ReadOnlyCollection<GeneratedTestCase>(ordered);
    }

    private static int CompareReadyCases(GeneratedTestCase left, GeneratedTestCase right)
    {
        var priority = right.ExecutionPriority.CompareTo(left.ExecutionPriority);
        return priority != 0
            ? priority
            : StringComparer.Ordinal.Compare(left.StableId, right.StableId);
    }

    private static IReadOnlyList<GeneratedTestCase> ResolveDependencies(
        GeneratedTestCase testCase,
        IReadOnlyList<GeneratedTestCase> allCases)
    {
        if (testCase.Dependencies.Count == 0)
        {
            return [];
        }

        var resolved = new List<GeneratedTestCase>();
        foreach (var dependency in testCase.Dependencies)
        {
            var separator = dependency.IndexOf(':');
            var className = separator < 0 ? string.Empty : dependency[..separator];
            var methodName = separator < 0 ? dependency : dependency[(separator + 1)..];
            var before = resolved.Count;
            foreach (var candidate in allCases)
            {
                if (string.Equals(candidate.MethodName, methodName, StringComparison.Ordinal) &&
                    (className.Length == 0
                        ? string.Equals(candidate.GroupIdentity, testCase.GroupIdentity, StringComparison.Ordinal)
                        : candidate.GroupIdentity.EndsWith(className, StringComparison.Ordinal)))
                {
                    resolved.Add(candidate);
                }
            }

            if (resolved.Count == before)
            {
                throw new ArgumentException(
                    $"Dependency '{dependency}' for '{testCase.StableId}' is not present in the generated catalog.",
                    nameof(allCases));
            }
        }

        return resolved;
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
