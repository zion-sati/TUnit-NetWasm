using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TUnit.Core.SourceGenerator.CodeGenerators.Helpers;
using TUnit.Core.SourceGenerator.Extensions;
using TUnit.Core.SourceGenerator.Helpers;

namespace TUnit.Core.SourceGenerator.Utilities.CapabilityValidation;

/// <summary>
/// Rejects TUnit semantics which the closed-world case model does not carry.
/// Specialized validators own the detailed diagnostics for the supported
/// families (data, lifecycle, activation, and method shape).
/// </summary>
internal sealed class ClosedWorldSemanticCapabilityValidator : IClosedWorldCapabilityValidator
{
    private static readonly HashSet<string> SupportedAttributes = new(StringComparer.Ordinal)
    {
        "ArgumentsAttribute",
        "CategoryAttribute",
        "DependsOnAttribute",
        "DisplayNameAttribute",
        "GenerateGenericTestAttribute",
        "InheritsTestsAttribute",
        "PropertyAttribute",
        "TestAttribute",
        "TestConstructorAttribute",
        "BeforeAttribute",
        "AfterAttribute",
        "BeforeEveryAttribute",
        "AfterEveryAttribute",
        "DynamicTestBuilderAttribute",
        "ClassConstructorAttribute",
        "ClassConstructorSourceAttribute",
    };

    public IEnumerable<ClosedWorldCapabilityDiagnostic> Validate(CSharpCompilation compilation)
    {
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            foreach (var declaration in syntaxTree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(declaration) is not ISymbol symbol)
                {
                    continue;
                }

                foreach (var diagnostic in ValidateAttributes(symbol.GetAttributes(), declaration.GetLocation()))
                {
                    yield return diagnostic;
                }

            }

            foreach (var parameter in syntaxTree.GetRoot().DescendantNodes().OfType<ParameterSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(parameter) is IParameterSymbol parameterSymbol)
                {
                    foreach (var diagnostic in ValidateAttributes(parameterSymbol.GetAttributes(), parameter.GetLocation(), isParameter: true))
                    {
                        yield return diagnostic;
                    }
                }
            }
        }

        foreach (var diagnostic in ValidateAttributes(compilation.Assembly.GetAttributes(), Location.None))
        {
            yield return diagnostic;
        }
    }

    private static IEnumerable<ClosedWorldCapabilityDiagnostic> ValidateAttributes(
        IEnumerable<AttributeData> attributes,
        Location location,
        bool isParameter = false)
    {
        foreach (var attribute in attributes)
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass is null)
            {
                continue;
            }

            if (isParameter && IsParameterDataSource(attributeClass))
            {
                yield return new(
                    location,
                    $"Parameter data source '{attributeClass.Name}' is not supported by the closed-world catalog; use method or class Arguments data.");
                continue;
            }

            if (IsDependsOn(attributeClass))
            {
                if (TryGetDependsOnDiagnostic(attribute, out var dependencyMessage))
                {
                    yield return new(location, dependencyMessage);
                }

                continue;
            }

            if (IsArguments(attributeClass))
            {
                foreach (var message in GetArgumentsDiagnostics(attribute))
                {
                    yield return new(location, message);
                }

                continue;
            }

            if (IsSupported(attributeClass) || !IsClosedWorldSemantic(attributeClass))
            {
                continue;
            }

            yield return new(
                location,
                $"TUnit semantic attribute '{attributeClass.Name}' is not supported by the closed-world catalog.");
        }
    }

    private static bool IsParameterDataSource(INamedTypeSymbol attributeClass) =>
        DataSourceAttributeHelper.IsDataSourceAttribute(attributeClass) ||
        InterfaceHelper.ImplementsInterface(attributeClass, "global::TUnit.Core.IDataSourceMemberAttribute");

    private static bool IsArguments(INamedTypeSymbol attributeClass) =>
        attributeClass.Name == "ArgumentsAttribute" &&
        attributeClass.ContainingNamespace?.ToDisplayString() == "TUnit.Core";

    private static bool IsDependsOn(INamedTypeSymbol attributeClass) =>
        attributeClass.Name == "DependsOnAttribute" &&
        attributeClass.ContainingNamespace?.ToDisplayString() == "TUnit.Core";

    private static IEnumerable<string> GetArgumentsDiagnostics(AttributeData attribute)
    {
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (namedArgument.Key == "Skip")
            {
                yield return "Arguments row Skip is not supported by the closed-world catalog.";
            }
            else if (namedArgument.Key == "Categories")
            {
                yield return "Arguments row Categories are not supported by the closed-world catalog.";
            }
            else if (namedArgument.Key == "SkipIfEmpty" && namedArgument.Value.Value is true)
            {
                yield return "Arguments row SkipIfEmpty is not supported by the closed-world catalog.";
            }
        }
    }

    private static bool TryGetDependsOnDiagnostic(AttributeData attribute, out string message)
    {
        var attributeClass = attribute.AttributeClass!;
        if (attributeClass.IsGenericType ||
            attribute.ConstructorArguments.Any(static argument =>
                argument.Kind is TypedConstantKind.Type or TypedConstantKind.Array))
        {
            message = "DependsOn class identities and overload parameter types are not supported by the closed-world catalog; use a same-class method name.";
            return true;
        }

        if (attribute.NamedArguments.Any(static argument =>
                argument.Key == "ProceedOnFailure" && argument.Value.Value is true))
        {
            message = "DependsOn ProceedOnFailure=true is not supported by the closed-world catalog.";
            return true;
        }

        if (attribute.ConstructorArguments.Length != 1 ||
            attribute.ConstructorArguments[0].Value is not string methodName ||
            string.IsNullOrWhiteSpace(methodName))
        {
            message = "DependsOn must specify a non-empty same-class method name for the closed-world catalog.";
            return true;
        }

        message = string.Empty;
        return false;
    }

    private static bool IsSupported(INamedTypeSymbol attributeClass)
    {
        if (SupportedAttributes.Contains(attributeClass.Name))
        {
            return true;
        }

        if (DataSourceAttributeHelper.IsDataSourceAttribute(attributeClass))
        {
            // The data validator reports whether a particular data source can
            // be represented; this rule must not produce a duplicate message.
            return true;
        }

        return false;
    }

    private static bool IsClosedWorldSemantic(INamedTypeSymbol attributeClass)
    {
        if (attributeClass.IsOrInherits("global::TUnit.Core.TUnitAttribute") ||
            attributeClass.IsOrInherits("global::TUnit.Core.SkipAttribute"))
        {
            return true;
        }

        var namespaceName = attributeClass.ContainingNamespace?.ToDisplayString();
        return namespaceName is not null &&
               (namespaceName.Equals("TUnit.Core", StringComparison.Ordinal) ||
                namespaceName.StartsWith("TUnit.Core.", StringComparison.Ordinal)) &&
               attributeClass.Name.EndsWith("Attribute", StringComparison.Ordinal);
    }
}
