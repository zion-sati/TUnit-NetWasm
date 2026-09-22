using System.Collections.ObjectModel;

namespace NetWasm.TUnit.Runner.Model;

public sealed class TestRunRequest
{
    public static TestRunRequest All { get; } = new(null, includeExplicit: false);

    public static TestRunRequest Discovery { get; } = new(null, includeExplicit: true);

    public TestRunRequest(IEnumerable<string>? stableIds)
        : this(stableIds, includeExplicit: false)
    {
    }

    private TestRunRequest(IEnumerable<string>? stableIds, bool includeExplicit)
    {
        IncludeExplicit = includeExplicit;
        if (stableIds is null)
        {
            StableIds = null;
            return;
        }

        var snapshot = new List<string>();
        foreach (var stableId in stableIds)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("A selected stable ID cannot be empty.", nameof(stableIds));
            }

            snapshot.Add(stableId);
        }

        StableIds = new ReadOnlyCollection<string>(snapshot);
    }

    public IReadOnlyList<string>? StableIds { get; }

    public bool IncludeExplicit { get; }
}
