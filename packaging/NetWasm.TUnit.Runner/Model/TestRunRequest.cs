using System.Collections.ObjectModel;

namespace NetWasm.TUnit.Runner.Model;

public sealed class TestRunRequest
{
    public static TestRunRequest All { get; } = new(null);

    public TestRunRequest(IEnumerable<string>? stableIds)
    {
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
}
