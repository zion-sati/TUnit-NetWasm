using System.Collections.Immutable;
using System.Text;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed class CatalogRowPlanner : ICatalogRowPlanner
{
    public ImmutableArray<CatalogRow> Plan(CatalogRowRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        var methodRows = request.MethodRows;
        if (methodRows.IsDefaultOrEmpty)
        {
            if (request.HasRuntimeDataSource) return ImmutableArray<CatalogRow>.Empty;
            methodRows = ImmutableArray.Create(new CatalogArgumentRow(ImmutableArray<CatalogValue>.Empty));
        }

        if (request.SelectedMethodRowIdentity is not null)
        {
            methodRows = methodRows
                .Where(row => string.Equals(row.Identity, request.SelectedMethodRowIdentity, StringComparison.Ordinal))
                .ToImmutableArray();
        }

        if (methodRows.IsDefaultOrEmpty) return ImmutableArray<CatalogRow>.Empty;
        var constructorRows = request.ConstructorRows.IsDefaultOrEmpty
            ? ImmutableArray.Create(new CatalogArgumentRow(ImmutableArray<CatalogValue>.Empty))
            : request.ConstructorRows;
        var rows = ImmutableArray.CreateBuilder<CatalogRow>();
        var rowIndex = 0;
        foreach (var constructorRow in constructorRows)
        {
            foreach (var methodRow in methodRows)
            {
                var methodCanonical = string.Join("|", methodRow.Values.Select(static value => value.Canonical));
                var constructorCanonical = string.Join("|", constructorRow.Values.Select(static value => value.Canonical));
                var canonical = $"method:[{methodCanonical}];constructor:[{constructorCanonical}]";
                var customDisplayNameTemplate = request.MethodDisplayName ?? request.ClassDisplayName;
                var customDisplayName = customDisplayNameTemplate is null
                    ? null
                    : FormatCustomDisplayName(
                        customDisplayNameTemplate,
                        request.MethodParameterNames,
                        methodRow.Values,
                        request.ConstructorParameterNames,
                        constructorRow.Values);
                var rowDisplayName = FormatRowDisplayName(methodRow.DisplayName, request.MethodParameterNames, methodRow.Values) ??
                    FormatRowDisplayName(constructorRow.DisplayName, request.MethodParameterNames, methodRow.Values) ??
                    $"{request.MethodName}({string.Join(", ", methodRow.Values.Select(static value => value.Display))})";
                var displayName = !string.IsNullOrEmpty(customDisplayName)
                    ? customDisplayName!
                    : rowDisplayName ?? request.MethodName;
                var methodCategories = methodRow.Categories.IsDefault ? ImmutableArray<string>.Empty : methodRow.Categories;
                var constructorCategories = constructorRow.Categories.IsDefault ? ImmutableArray<string>.Empty : constructorRow.Categories;
                var categories = methodCategories
                    .Concat(constructorCategories)
                    .Where(static category => !string.IsNullOrWhiteSpace(category))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static category => category, StringComparer.Ordinal)
                    .ToImmutableArray();
                var skipReason = methodRow.SkipReason is not null
                    ? methodRow.SkipReason
                    : constructorRow.SkipReason;
                for (var repeatIndex = 0; repeatIndex <= request.RepeatCount; repeatIndex++)
                {
                    var stableId = $"{request.MethodIdentity}#row-{rowIndex}:repeat-{repeatIndex}:{canonical}";
                    rows.Add(new CatalogRow(
                        methodRow.Values,
                        constructorRow.Values,
                        stableId,
                        displayName,
                        skipReason,
                        categories,
                        repeatIndex));
                }
                rowIndex++;
            }
        }

        return rows.ToImmutable();
    }

    private static string? FormatRowDisplayName(
        string? template,
        ImmutableArray<string> parameterNames,
        ImmutableArray<CatalogValue> values)
    {
        if (string.IsNullOrEmpty(template))
        {
            return null;
        }

        if (parameterNames.IsDefault)
        {
            parameterNames = ImmutableArray<string>.Empty;
        }

        var displayName = template!;

        for (var index = 0; index < values.Length; index++)
        {
            displayName = ReplacePlaceholder(displayName, $"$arg{index + 1}", values[index].Display);
        }

        for (var index = 0; index < values.Length && index < parameterNames.Length; index++)
        {
            displayName = ReplacePlaceholder(displayName, $"${parameterNames[index]}", values[index].Display);
        }

        return displayName;
    }

    private static string FormatCustomDisplayName(
        string template,
        ImmutableArray<string> methodParameterNames,
        ImmutableArray<CatalogValue> methodValues,
        ImmutableArray<string> constructorParameterNames,
        ImmutableArray<CatalogValue> constructorValues)
    {
        var displayName = template;
        ReplaceNamedParameters(ref displayName, methodParameterNames, methodValues);

        if (constructorValues.IsDefaultOrEmpty)
        {
            return displayName;
        }

        ReplaceNamedParameters(ref displayName, constructorParameterNames, constructorValues);
        return displayName;
    }

    private static void ReplaceNamedParameters(
        ref string displayName,
        ImmutableArray<string> parameterNames,
        ImmutableArray<CatalogValue> values)
    {
        if (parameterNames.IsDefault)
        {
            parameterNames = ImmutableArray<string>.Empty;
        }

        foreach (var index in Enumerable.Range(0, Math.Min(parameterNames.Length, values.Length))
                     .OrderByDescending(index => parameterNames[index].Length))
        {
            displayName = displayName.Replace(
                $"${parameterNames[index]}",
                values[index].Display);
        }
    }

    private static string ReplacePlaceholder(string input, string placeholder, string value)
    {
        var index = input.IndexOf(placeholder, StringComparison.Ordinal);
        if (index < 0)
        {
            return input;
        }

        var builder = new StringBuilder(input.Length);
        var searchStart = 0;
        while (index >= 0)
        {
            var afterIndex = index + placeholder.Length;
            var isBoundary = afterIndex >= input.Length || !IsIdentifierCharacter(input[afterIndex]);
            builder.Append(input, searchStart, index - searchStart);
            builder.Append(isBoundary ? value : placeholder);
            searchStart = afterIndex;
            index = input.IndexOf(placeholder, searchStart, StringComparison.Ordinal);
        }

        builder.Append(input, searchStart, input.Length - searchStart);
        return builder.ToString();
    }

    private static bool IsIdentifierCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';
}
