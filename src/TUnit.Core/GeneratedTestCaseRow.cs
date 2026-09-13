using System.Collections.ObjectModel;

namespace TUnit.Core;

/// <summary>
/// One source-authored generated case row. Identity and display data are emitted
/// together with the payload so they cannot drift through parallel arrays.
/// </summary>
public sealed class GeneratedTestCaseRow
{
    public GeneratedTestCaseRow(
        string stableId,
        string? displayName,
        IEnumerable<object?>? arguments = null,
        IEnumerable<object?>? constructorArguments = null)
    {
        StableId = string.IsNullOrWhiteSpace(stableId)
            ? throw new ArgumentException("Value cannot be empty.", nameof(stableId))
            : stableId;
        DisplayName = displayName;
        Arguments = new ReadOnlyCollection<object?>(new List<object?>(arguments ?? []));
        ConstructorArguments = new ReadOnlyCollection<object?>(new List<object?>(constructorArguments ?? []));
    }

    public string StableId { get; }

    public string? DisplayName { get; }

    public IReadOnlyList<object?> Arguments { get; }

    public IReadOnlyList<object?> ConstructorArguments { get; }

    internal object?[] CreateArguments() => Copy(Arguments);

    internal object?[] CreateConstructorArguments() => Copy(ConstructorArguments);

    private static object?[] Copy(IReadOnlyList<object?> values)
    {
        var result = new object?[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            result[index] = values[index];
        }

        return result;
    }
}
