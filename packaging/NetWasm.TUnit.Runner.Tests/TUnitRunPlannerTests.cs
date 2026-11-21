using NetWasm.TUnit.VSTest.Adapter.Execution;

namespace NetWasm.TUnit.Runner.Tests;

public sealed class TUnitRunPlannerTests
{
    [Fact]
    public void TheWholeCatalogRunsOnceWithoutIds()
    {
        ITUnitRunPlanner planner = new TUnitRunPlanner(argumentBudget: 16);

        var plan = XunitAssert.Single(planner.Plan(["a", "b", "c"], catalogCount: 3));

        XunitAssert.Equal(["--run"], plan.Arguments);
        XunitAssert.Equal(["a", "b", "c"], plan.StableIds);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(null)]
    public void APartialOrUnknownSelectionRunsByIdInOrder(int? catalogCount)
    {
        var plan = XunitAssert.Single(new TUnitRunPlanner(1_000).Plan(["b", "a", "c"], catalogCount));

        XunitAssert.Equal(["--id", "b", "--id", "a", "--id", "c"], plan.Arguments);
        XunitAssert.Equal(["b", "a", "c"], plan.StableIds);
    }

    // Each ID costs its length plus 8, so a 20-character budget holds two one-character IDs.
    [Fact]
    public void ASelectionBeyondTheBudgetSplitsIntoRunsThatEachFit()
    {
        var plans = new TUnitRunPlanner(argumentBudget: 20).Plan(["a", "b", "c", "d", "e"], catalogCount: null);

        XunitAssert.Equal([["a", "b"], ["c", "d"], ["e"]], plans.Select(plan => plan.StableIds.ToArray()));
        XunitAssert.All(plans, plan => XunitAssert.Equal(
            plan.StableIds.SelectMany(id => new[] { "--id", id }), plan.Arguments));
    }

    [Fact]
    public void AnIdLongerThanTheBudgetRunsAlone()
    {
        var plans = new TUnitRunPlanner(argumentBudget: 10).Plan(["a", "a-very-long-stable-id", "b"], catalogCount: 5);

        XunitAssert.Equal([["a"], ["a-very-long-stable-id"], ["b"]], plans.Select(plan => plan.StableIds.ToArray()));
    }

    [Fact]
    public void AnEmptySelectionPlansNoRun() =>
        XunitAssert.Empty(new TUnitRunPlanner(100).Plan([], catalogCount: 3));

    [Fact]
    public void InvalidSelectionsAndBudgetsAreRejected()
    {
        var planner = new TUnitRunPlanner(100);

        XunitAssert.Throws<ArgumentNullException>(() => planner.Plan(null!, catalogCount: null));
        XunitAssert.Throws<ArgumentException>(() => planner.Plan(["a", "a"], catalogCount: null));
        XunitAssert.Throws<ArgumentException>(() => planner.Plan([""], catalogCount: null));
        XunitAssert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(["a", "b"], catalogCount: 1));
        XunitAssert.Throws<ArgumentOutOfRangeException>(() => new TUnitRunPlanner(0));
    }

    [Fact]
    public void ThePlatformBudgetFitsTheHostCommandLine() =>
        XunitAssert.InRange(TUnitRunPlanner.PlatformArgumentBudget, 1, OperatingSystem.IsWindows() ? 32_000 : 1_000_000);
}
