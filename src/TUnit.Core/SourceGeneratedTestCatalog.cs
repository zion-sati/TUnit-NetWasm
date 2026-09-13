using System.Collections.ObjectModel;

namespace TUnit.Core;

/// <summary>
/// Immutable case-only catalog for generated entry points.
/// </summary>
/// <remarks>
/// This type contains no discovery or activation policy. It snapshots the generated
/// cases supplied by the composition root so desktop and alternative hosts can consume
/// the same source-generated contract without a desktop type-name projection.
/// </remarks>
public sealed class SourceGeneratedTestCatalog : ITestEntryCatalog
{
    private readonly IReadOnlyList<GeneratedTestCase> _cases;

    public SourceGeneratedTestCatalog(
        IEnumerable<GeneratedTestCase> cases,
        string? provenance = null)
    {
        if (cases is null)
        {
            throw new ArgumentNullException(nameof(cases));
        }

        var snapshot = new List<GeneratedTestCase>(cases).ToArray();
        var stableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testCase in snapshot)
        {
            if (testCase is null)
            {
                throw new ArgumentException("The catalog cannot contain a null case.", nameof(cases));
            }

            if (!stableIds.Add(testCase.StableId))
            {
                throw new ArgumentException(
                    $"The catalog cannot contain duplicate stable ID '{testCase.StableId}'.",
                    nameof(cases));
            }
        }

        Array.Sort(snapshot, static (left, right) => StringComparer.Ordinal.Compare(left.StableId, right.StableId));
        _cases = new ReadOnlyCollection<GeneratedTestCase>(snapshot);
        Provenance = string.IsNullOrWhiteSpace(provenance)
            ? "TUnit.Core.SourceGenerator"
            : provenance!;
    }

    /// <summary>
    /// Identifies the generator contract that produced this catalog.
    /// </summary>
    public string Provenance { get; }

    /// <inheritdoc />
    public IReadOnlyList<GeneratedTestCase> GetGeneratedCases() => _cases;
}
