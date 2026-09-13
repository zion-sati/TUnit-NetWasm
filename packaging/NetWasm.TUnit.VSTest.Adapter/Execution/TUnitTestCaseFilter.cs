using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace NetWasm.TUnit.VSTest.Adapter.Execution;

internal sealed class TUnitTestCaseFilter : ITUnitTestCaseFilter
{
    private const string CategoryProperty = "Category";
    private const string DisplayNameProperty = nameof(TestCase.DisplayName);
    private const string FullyQualifiedNameProperty = nameof(TestCase.FullyQualifiedName);
    private const string TestCategoryProperty = "TestCategory";

    public IReadOnlyCollection<TestCase> Apply(
        IEnumerable<TestCase> testCases,
        IRunContext runContext)
    {
        ArgumentNullException.ThrowIfNull(testCases);
        ArgumentNullException.ThrowIfNull(runContext);

        var snapshot = testCases.ToArray();
        var supportedProperties = snapshot
            .SelectMany(static testCase => testCase.Traits.Select(static trait => trait.Name))
            .Append(CategoryProperty)
            .Append(DisplayNameProperty)
            .Append(FullyQualifiedNameProperty)
            .Append(TestCategoryProperty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var expression = runContext.GetTestCaseFilter(supportedProperties, static _ => null);
        if (expression is null)
        {
            return snapshot;
        }

        return snapshot
            .Where(testCase => expression.MatchTestCase(
                testCase,
                propertyName => GetPropertyValue(testCase, propertyName)))
            .ToArray();
    }

    private static object? GetPropertyValue(TestCase testCase, string propertyName)
    {
        if (string.Equals(propertyName, FullyQualifiedNameProperty, StringComparison.OrdinalIgnoreCase))
        {
            return testCase.FullyQualifiedName;
        }
        if (string.Equals(propertyName, DisplayNameProperty, StringComparison.OrdinalIgnoreCase))
        {
            return testCase.DisplayName;
        }

        var isCategory = string.Equals(propertyName, CategoryProperty, StringComparison.OrdinalIgnoreCase)
            || string.Equals(propertyName, TestCategoryProperty, StringComparison.OrdinalIgnoreCase);
        var values = testCase.Traits
            .Where(trait => string.Equals(trait.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                || isCategory && (string.Equals(trait.Name, CategoryProperty, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trait.Name, TestCategoryProperty, StringComparison.OrdinalIgnoreCase)))
            .Select(static trait => trait.Value)
            .ToArray();
        return values.Length == 0 ? null : values;
    }
}
