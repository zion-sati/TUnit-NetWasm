using System.Collections.Immutable;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using NetWasm.TUnit.VSTest.Adapter.Mapping;
using NetWasm.TUnit.VSTest.Adapter.Protocol;

namespace NetWasm.TUnit.Runner.Tests;

public sealed class TUnitVSTestMapperTests
{
    [Fact]
    public void TestCaseMapperPreservesGuestIdentitySourceAndTraits()
    {
        var mapper = new TUnitTestCaseMapper();

        var result = mapper.Create(
            "/artifacts/Tests.dll",
            new TUnitCatalogCase(
                "stable-id",
                "Display name",
                "Tests.Case",
                "Tests.cs",
                42,
                [new TUnitTrait("TestCategory", "fast")]));

        XunitAssert.Equal("stable-id", mapper.GetStableId(result));
        XunitAssert.Equal("Tests.Case", result.FullyQualifiedName);
        XunitAssert.Equal("Display name", result.DisplayName);
        XunitAssert.Equal("/artifacts/Tests.dll", result.Source);
        XunitAssert.Equal("Tests.cs", result.CodeFilePath);
        XunitAssert.Equal(42, result.LineNumber);
        var trait = XunitAssert.Single(result.Traits);
        XunitAssert.Equal("TestCategory", trait.Name);
        XunitAssert.Equal("fast", trait.Value);
    }

    [Theory]
    [InlineData("passed", TestOutcome.Passed)]
    [InlineData("assertion-failed", TestOutcome.Failed)]
    [InlineData("unexpected-failure", TestOutcome.Failed)]
    [InlineData("unsupported", TestOutcome.Skipped)]
    [InlineData("cancelled", TestOutcome.Skipped)]
    public void ResultMapperMapsGuestOutcome(string outcome, TestOutcome expected)
    {
        var testCase = new TestCase("Tests.Case", new Uri("executor://NetWasm/TUnit/v2"), "Tests.dll");

        var result = new TUnitTestResultMapper().Create(
            testCase,
            new TUnitCompletedCase(
                "stable-id",
                outcome,
                TimeSpan.FromMilliseconds(2.5),
                "message"));

        XunitAssert.Equal(expected, result.Outcome);
        XunitAssert.Equal(TimeSpan.FromMilliseconds(2.5), result.Duration);
        XunitAssert.Equal("message", result.ErrorMessage);
    }

    [Fact]
    public void ResultMapperRejectsUnknownGuestOutcome()
    {
        var testCase = new TestCase("Tests.Case", new Uri("executor://NetWasm/TUnit/v2"), "Tests.dll");

        XunitAssert.Throws<InvalidDataException>(() => new TUnitTestResultMapper().Create(
            testCase,
            new TUnitCompletedCase("stable-id", "unknown", TimeSpan.Zero, null)));
    }
}
