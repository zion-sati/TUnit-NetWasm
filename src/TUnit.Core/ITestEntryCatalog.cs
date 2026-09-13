namespace TUnit.Core;

/// <summary>
/// Provides the immutable source-generated test catalog for an assembly.
/// </summary>
public interface ITestEntryCatalog
{
    IReadOnlyList<GeneratedTestCase> GetGeneratedCases();
}
