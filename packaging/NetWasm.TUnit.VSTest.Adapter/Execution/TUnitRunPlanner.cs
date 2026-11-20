using System.Collections.Immutable;

namespace NetWasm.TUnit.VSTest.Adapter.Execution;

// Plans the guest runs for a selection. The guest takes its tests on the host's command line, which every platform
// bounds (Windows at 32,767 characters, macOS at 1 MiB with the environment), so a selection of the whole catalog
// runs as one --run, and any other selection is split into --id runs whose arguments fit the budget.
internal sealed class TUnitRunPlanner : ITUnitRunPlanner
{
    // Each --id argument pair's share of the command line beyond the ID: the option, two separators and quotes.
    private const int IdArgumentOverhead = 8;

    private readonly int _argumentBudget;

    public TUnitRunPlanner(int argumentBudget)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(argumentBudget, 1);
        _argumentBudget = argumentBudget;
    }

    // The budget for this platform's command line, below its limit so the host's own arguments fit too.
    public static int PlatformArgumentBudget => OperatingSystem.IsWindows() ? 24_000 : 512_000;

    public ImmutableArray<TUnitGuestRunPlan> Plan(IReadOnlyList<string> selectedStableIds, int? catalogCount)
    {
        ArgumentNullException.ThrowIfNull(selectedStableIds);
        if (selectedStableIds.Any(string.IsNullOrEmpty) ||
            selectedStableIds.Distinct(StringComparer.Ordinal).Count() != selectedStableIds.Count)
        {
            throw new ArgumentException("The selected stable IDs must be distinct and non-empty.", nameof(selectedStableIds));
        }
        if (catalogCount is { } count && (count < selectedStableIds.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(catalogCount), "The catalog is smaller than the selection.");
        }
        if (selectedStableIds.Count == 0)
        {
            return [];
        }
        if (catalogCount == selectedStableIds.Count)
        {
            return [new TUnitGuestRunPlan(["--run"], [.. selectedStableIds])];
        }

        var runs = ImmutableArray.CreateBuilder<TUnitGuestRunPlan>();
        var arguments = ImmutableArray.CreateBuilder<string>();
        var stableIds = ImmutableArray.CreateBuilder<string>();
        var used = 0;
        foreach (var stableId in selectedStableIds)
        {
            var cost = stableId.Length + IdArgumentOverhead;
            // A run always takes at least one test, so an ID beyond the budget runs alone.
            if (stableIds.Count != 0 && used + cost > _argumentBudget)
            {
                runs.Add(new TUnitGuestRunPlan(arguments.ToImmutable(), stableIds.ToImmutable()));
                arguments.Clear();
                stableIds.Clear();
                used = 0;
            }
            arguments.Add("--id");
            arguments.Add(stableId);
            stableIds.Add(stableId);
            used += cost;
        }
        runs.Add(new TUnitGuestRunPlan(arguments.ToImmutable(), stableIds.ToImmutable()));
        return runs.ToImmutable();
    }
}
