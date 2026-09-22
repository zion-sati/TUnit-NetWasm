using NetWasm.TUnit.Runner.Model;
using NetWasm.TUnit.Runner.Selection;
using TUnit.Core;

namespace NetWasm.TUnit.Runner.Tests;

public sealed class TestCaseResolverTests
{
    private readonly ITestCaseResolver _resolver = new TestCaseResolver();

    [Fact]
    public void SelectReturnsEveryCatalogCaseForAllRequest()
    {
        var catalog = Catalog("b", "a");

        var selected = _resolver.Resolve(catalog, TestRunRequest.All);

        XunitAssert.Equal(["a", "b"], selected.Select(testCase => testCase.StableId));
    }

    [Fact]
    public void SelectPreservesCatalogOrderForStableIdSubset()
    {
        var catalog = Catalog("c", "a", "b");

        var selected = _resolver.Resolve(catalog, new TestRunRequest(["c", "a"]));

        XunitAssert.Equal(["a", "c"], selected.Select(testCase => testCase.StableId));
    }

    [Fact]
    public void SelectRejectsAnExplicitEmptySelection()
    {
        var exception = XunitAssert.Throws<ArgumentException>(() =>
            _resolver.Resolve(Catalog("a"), new TestRunRequest([])));

        XunitAssert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void SelectRejectsAnEmptyCatalog()
    {
        var exception = XunitAssert.Throws<ArgumentException>(() =>
            _resolver.Resolve(new SourceGeneratedTestCatalog([]), TestRunRequest.All));

        XunitAssert.Equal("catalog", exception.ParamName);
    }

    [Fact]
    public void SelectRejectsDuplicateStableIds()
    {
        var exception = XunitAssert.Throws<ArgumentException>(() =>
            _resolver.Resolve(Catalog("a"), new TestRunRequest(["a", "a"])));

        XunitAssert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void SelectRejectsUnknownStableIds()
    {
        var exception = XunitAssert.Throws<ArgumentException>(() =>
            _resolver.Resolve(Catalog("a"), new TestRunRequest(["missing"])));

        XunitAssert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void SelectRejectsDuplicateIdsFromAnUnvalidatedCatalog()
    {
        var testCase = TestCaseFactory.Create("a");
        var exception = XunitAssert.Throws<ArgumentException>(() =>
            _resolver.Resolve(
                new RawCatalog([testCase, testCase]),
                TestRunRequest.All));

        XunitAssert.Equal("cases", exception.ParamName);
    }

    [Fact]
    public void SelectRejectsUnknownInvocationKindsFromAnUnvalidatedCatalog()
    {
        var testCase = TestCaseFactory.Create("invalid", invocationKind: (GeneratedInvocationKind) 999);
        var exception = XunitAssert.Throws<ArgumentException>(() =>
            _resolver.Resolve(new RawCatalog([testCase]), TestRunRequest.All));

        XunitAssert.Equal("cases", exception.ParamName);
    }

    [Fact]
    public void RequestRejectsEmptyStableIds()
    {
        var exception = XunitAssert.Throws<ArgumentException>(() => new TestRunRequest([""]));

        XunitAssert.Equal("stableIds", exception.ParamName);
    }

    [Fact]
    public void AllExcludesExplicitCasesWhileDiscoveryIncludesThem()
    {
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create("ordinary"),
            TestCaseFactory.Create("explicit", isExplicit: true),
        ]);

        var runCases = _resolver.Resolve(catalog, TestRunRequest.All);
        var discoveryCases = _resolver.Resolve(catalog, TestRunRequest.Discovery);

        XunitAssert.Equal(["ordinary"], runCases.Select(static testCase => testCase.StableId));
        XunitAssert.Equal(["explicit", "ordinary"], discoveryCases.Select(static testCase => testCase.StableId));
    }

    [Fact]
    public void ExplicitSelectionRunsOnlyWhenNoOrdinaryCaseIsSelected()
    {
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create("ordinary"),
            TestCaseFactory.Create("explicit", isExplicit: true),
        ]);

        var explicitOnly = _resolver.Resolve(catalog, new TestRunRequest(["explicit"]));
        var mixed = _resolver.Resolve(catalog, new TestRunRequest(["ordinary", "explicit"]));

        XunitAssert.Equal(["explicit"], explicitOnly.Select(static testCase => testCase.StableId));
        XunitAssert.Equal(["ordinary"], mixed.Select(static testCase => testCase.StableId));
    }

    [Fact]
    public void SelectionAddsTransitiveDependenciesAndOrdersReadyCasesByPriority()
    {
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create("low", methodName: "Low", executionPriority: 0),
            TestCaseFactory.Create("high", methodName: "High", executionPriority: 5),
            TestCaseFactory.Create(
                "middle",
                methodName: "Middle",
                dependencies: ["High"],
                executionPriority: 2),
            TestCaseFactory.Create(
                "root",
                methodName: "Root",
                dependencies: ["Middle"],
                executionPriority: 5),
        ]);

        var selected = _resolver.Resolve(catalog, new TestRunRequest(["low", "root"]));

        XunitAssert.Equal(
            ["high", "middle", "root", "low"],
            selected.Select(static testCase => testCase.StableId));
    }

    [Fact]
    public void SelectionRejectsDependencyCycles()
    {
        var catalog = new SourceGeneratedTestCatalog([
            TestCaseFactory.Create("a", methodName: "A", dependencies: ["B"]),
            TestCaseFactory.Create("b", methodName: "B", dependencies: ["A"]),
        ]);

        var exception = XunitAssert.Throws<ArgumentException>(() =>
            _resolver.Resolve(catalog, TestRunRequest.All));

        XunitAssert.Contains("cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SourceGeneratedTestCatalog Catalog(params string[] stableIds) =>
        new(stableIds.Select(stableId => TestCaseFactory.Create(stableId)));

    private sealed class RawCatalog(IReadOnlyList<GeneratedTestCase> cases) : ITestEntryCatalog
    {
        public IReadOnlyList<GeneratedTestCase> GetGeneratedCases() => cases;
    }
}
