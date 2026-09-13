using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using NetWasm.TUnit.VSTest.Adapter;
using NetWasm.TUnit.VSTest.Adapter.Execution;
using NSubstitute;

namespace NetWasm.TUnit.Runner.Tests;

public sealed class TUnitTestCaseFilterTests
{
    [Fact]
    public void ApplyReturnsEveryDiscoveredTestWhenVSTestHasNoFilter()
    {
        var tests = CreateTests();
        var runContext = Substitute.For<IRunContext>();
        runContext
            .GetTestCaseFilter(
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<Func<string, TestProperty?>>())
            .Returns((ITestCaseFilterExpression?) null);

        var result = new TUnitTestCaseFilter().Apply(tests, runContext);

        XunitAssert.Equal(tests, result);
    }

    [Fact]
    public void ApplyExposesIdentityAndTraitPropertiesAndReturnsOnlyMatches()
    {
        var tests = CreateTests();
        var runContext = Substitute.For<IRunContext>();
        var expression = Substitute.For<ITestCaseFilterExpression>();
        string[]? supportedProperties = null;
        runContext
            .GetTestCaseFilter(
                Arg.Do<IEnumerable<string>>(properties => supportedProperties = properties.ToArray()),
                Arg.Any<Func<string, TestProperty?>>())
            .Returns(expression);
        expression
            .MatchTestCase(
                Arg.Any<TestCase>(),
                Arg.Any<Func<string, object?>>())
            .Returns(call =>
            {
                var property = call.Arg<Func<string, object?>>();
                return string.Equals(
                        property("FullyQualifiedName") as string,
                        "Tests.FilterCanary",
                        StringComparison.Ordinal)
                    && (property("TestCategory") as string[])?.Contains("filter-canary") is true;
            });

        var result = new TUnitTestCaseFilter().Apply(tests, runContext);

        var selected = XunitAssert.Single(result);
        XunitAssert.Equal("Tests.FilterCanary", selected.FullyQualifiedName);
        XunitAssert.NotNull(supportedProperties);
        XunitAssert.Contains("FullyQualifiedName", supportedProperties);
        XunitAssert.Contains("DisplayName", supportedProperties);
        XunitAssert.Contains("Category", supportedProperties);
        XunitAssert.Contains("TestCategory", supportedProperties);
        XunitAssert.Contains("lane", supportedProperties);
    }

    private static TestCase[] CreateTests()
    {
        var selected = new TestCase(
            "Tests.FilterCanary",
            new Uri(TUnitNetWasmAdapterConstants.ExecutorUri),
            "Tests.dll")
        {
            DisplayName = "Filter canary",
        };
        selected.Traits.Add(new Trait("Category", "filter-canary"));

        var other = new TestCase(
            "Tests.PackageSmoke",
            new Uri(TUnitNetWasmAdapterConstants.ExecutorUri),
            "Tests.dll")
        {
            DisplayName = "Package smoke",
        };
        other.Traits.Add(new Trait("lane", "package-only"));
        return [selected, other];
    }
}
