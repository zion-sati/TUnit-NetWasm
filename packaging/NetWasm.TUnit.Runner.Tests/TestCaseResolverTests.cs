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

    private static SourceGeneratedTestCatalog Catalog(params string[] stableIds) =>
        new(stableIds.Select(stableId => TestCaseFactory.Create(stableId)));

    private sealed class RawCatalog(IReadOnlyList<GeneratedTestCase> cases) : ITestEntryCatalog
    {
        public IReadOnlyList<GeneratedTestCase> GetGeneratedCases() => cases;
    }
}
