using System.Collections.ObjectModel;

namespace TUnit.Core;

/// <summary>
/// One source-authored generated case row. Identity and display data are emitted
/// together with the payload so they cannot drift through parallel arrays.
/// </summary>
public sealed class GeneratedTestCaseRow
{
    private readonly Func<object?[]>? _argumentsFactory;
    private readonly Func<object?[]>? _constructorArgumentsFactory;
    private IReadOnlyList<object?>? _arguments;
    private IReadOnlyList<object?>? _constructorArguments;

    public GeneratedTestCaseRow(
        string stableId,
        string? displayName,
        IEnumerable<object?>? arguments = null,
        IEnumerable<object?>? constructorArguments = null,
        Func<object?[]>? argumentsFactory = null,
        Func<object?[]>? constructorArgumentsFactory = null)
    {
        StableId = string.IsNullOrWhiteSpace(stableId)
            ? throw new ArgumentException("Value cannot be empty.", nameof(stableId))
            : stableId;
        DisplayName = displayName;
        _arguments = arguments is null ? null : CopyToReadOnly(arguments);
        _constructorArguments = constructorArguments is null ? null : CopyToReadOnly(constructorArguments);
        _argumentsFactory = argumentsFactory;
        _constructorArgumentsFactory = constructorArgumentsFactory;
    }

    public string StableId { get; }

    public string? DisplayName { get; }

    public IReadOnlyList<object?> Arguments =>
        _arguments ??= CopyToReadOnly(_argumentsFactory?.Invoke() ?? []);

    public IReadOnlyList<object?> ConstructorArguments =>
        _constructorArguments ??= CopyToReadOnly(_constructorArgumentsFactory?.Invoke() ?? []);

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

    private static IReadOnlyList<object?> CopyToReadOnly(IEnumerable<object?> values) =>
        new ReadOnlyCollection<object?>(new List<object?>(values));
}
