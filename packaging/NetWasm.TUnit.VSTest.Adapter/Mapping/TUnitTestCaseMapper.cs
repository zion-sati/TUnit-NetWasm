using System.Security.Cryptography;
using System.Text;
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
            Id = CreateId(source, testCase.StableId),
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

    // VSTest derives a test case's Id from its fully qualified name, which every case of a parameterized test shares,
    // so a TRX report or an IDE would file the cases' results under one test. TUnit's stable ID tells the cases apart;
    // the Id is derived from it and the source, the same in discovery and execution.
    private static Guid CreateId(string source, string stableId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{source}\n{stableId}")).AsSpan(0, 16));

    public string GetStableId(TestCase testCase)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        return testCase.GetPropertyValue(StableIdProperty) as string
            ?? throw new InvalidDataException("The selected TUnit test case has no stable ID.");
    }
}
