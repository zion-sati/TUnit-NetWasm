using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using NetWasm.TUnit.VSTest.Adapter.Protocol;

namespace NetWasm.TUnit.VSTest.Adapter.Mapping;

internal sealed class TUnitTestCaseMapper : ITUnitTestCaseMapper
{
    private static readonly Uri ExecutorUri = new(TUnitNetWasmAdapterConstants.ExecutorUri);
    private static readonly TestProperty StableIdProperty = TestProperty.Register(
        "NetWasm.TUnit.StableId",
        "TUnit stable ID",
        typeof(string),
        TestPropertyAttributes.Hidden,
        typeof(TUnitTestCaseMapper));

    public TestCase Create(string source, TUnitCatalogCase testCase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(testCase);

        var result = new TestCase(testCase.FullyQualifiedName, ExecutorUri, source)
        {
            DisplayName = testCase.DisplayName,
            CodeFilePath = testCase.FilePath,
            LineNumber = testCase.LineNumber,
        };
        result.SetPropertyValue(StableIdProperty, testCase.StableId);
        foreach (var trait in testCase.Traits)
        {
            result.Traits.Add(new Trait(trait.Name, trait.Value));
        }
        return result;
    }

    public string GetStableId(TestCase testCase)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        return testCase.GetPropertyValue(StableIdProperty) as string
            ?? throw new InvalidDataException("The selected TUnit test case has no stable ID.");
    }
}
