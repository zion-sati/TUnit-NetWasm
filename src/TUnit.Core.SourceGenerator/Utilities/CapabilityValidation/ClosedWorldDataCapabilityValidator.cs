using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TUnit.Core.SourceGenerator.CodeGenerators.Helpers;
using TUnit.Core.SourceGenerator.Extensions;

namespace TUnit.Core.SourceGenerator.Utilities.CapabilityValidation;

internal sealed class ClosedWorldDataCapabilityValidator : IClosedWorldCapabilityValidator
{
    public IEnumerable<ClosedWorldCapabilityDiagnostic> Validate(CSharpCompilation compilation)
    {
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            foreach (var methodSyntax in syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol method)
                {
                    continue;
                }

                var attributes = method.GetAttributes();
                foreach (var attribute in attributes.Where(static attribute =>
                             DataSourceAttributeHelper.IsDataSourceAttribute(attribute.AttributeClass) &&
                             attribute.AttributeClass!.Name != "ArgumentsAttribute"))
                {
                    if (attribute.AttributeClass!.Name is "MethodDataSourceAttribute" or "ClassDataSourceAttribute" &&
                        attribute.AttributeClass.ContainingNamespace?.ToDisplayString() == "TUnit.Core")
                    {
                        continue;
                    }

                    yield return new(methodSyntax.GetLocation(),
                        $"Runtime data source '{attribute.AttributeClass!.Name}' is not supported by the closed-world catalog; use compile-time Arguments data.");
                }

                if (!attributes.Any(static attribute => attribute.IsTestAttribute()))
                {
                    continue;
                }

                var argumentAttributes = attributes.Where(static attribute =>
                             attribute.AttributeClass!.Name == "ArgumentsAttribute")
                    .ToArray();
                var hasRuntimeDataSource = attributes.Any(static attribute =>
                    DataSourceAttributeHelper.IsDataSourceAttribute(attribute.AttributeClass) &&
                    attribute.AttributeClass!.Name != "ArgumentsAttribute") ||
                    method.Parameters.Any(static parameter => parameter.GetAttributes().Any(attribute =>
                        attribute.AttributeClass!.Name == "ClassDataSourceAttribute" &&
                        attribute.AttributeClass.ContainingNamespace?.ToDisplayString() == "TUnit.Core"));
                if (argumentAttributes.Length == 0 && !hasRuntimeDataSource)
                {
                    var methodParameters = method.Parameters;
                    if (methodParameters.Length > 0 &&
                        methodParameters[^1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                        "global::System.Threading.CancellationToken")
                    {
                        methodParameters = methodParameters.RemoveAt(methodParameters.Length - 1);
                    }

                    if (!ValidateArgumentCount(methodParameters, 0, out var countMessage, "test method"))
                    {
                        yield return new(methodSyntax.GetLocation(), countMessage);
                    }
                }

                foreach (var argumentAttribute in argumentAttributes)
                {
                    var values = GetArgumentValues(argumentAttribute);
                    var methodParameters = method.Parameters;
                    if (methodParameters.Length > 0 &&
                        methodParameters[^1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                        "global::System.Threading.CancellationToken")
                    {
                        methodParameters = methodParameters.RemoveAt(methodParameters.Length - 1);
                    }

                    if (!ValidateArgumentCount(methodParameters, values.Length, out var countMessage, "test method"))
                    {
                        yield return new(methodSyntax.GetLocation(), countMessage);
                        continue;
                    }

                    if (!ValidateArgumentValues(methodParameters, values, compilation))
                    {
                        yield return new(methodSyntax.GetLocation(),
                            "Argument conversion requires the desktop AOT converter generator and is not supported by the closed-world catalog.");
                    }
                }
            }

            foreach (var typeSyntax in syntaxTree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(typeSyntax) is not INamedTypeSymbol type)
                {
                    continue;
                }

                var constructor = GetPrimaryConstructor(type);
                if (constructor is null)
                {
                    continue;
                }

                var classArgumentAttributes = type.GetAttributes().Where(static attribute =>
                             attribute.AttributeClass!.Name == "ArgumentsAttribute")
                    .ToArray();
                var hasClassDataSource = type.GetAttributes().Any(static attribute =>
                    attribute.AttributeClass!.Name == "ClassDataSourceAttribute" &&
                    attribute.AttributeClass.ContainingNamespace?.ToDisplayString() == "TUnit.Core") ||
                    constructor.Parameters.Any(static parameter => parameter.GetAttributes().Any(attribute =>
                        attribute.AttributeClass!.Name == "ClassDataSourceAttribute" &&
                        attribute.AttributeClass.ContainingNamespace?.ToDisplayString() == "TUnit.Core"));
                if (classArgumentAttributes.Length == 0 &&
                    !hasClassDataSource &&
                    type.GetMembers().OfType<IMethodSymbol>().Any(static method =>
                        method.GetAttributes().Any(static attribute => attribute.IsTestAttribute())) &&
                    !ValidateArgumentCount(constructor.Parameters, 0, out var missingConstructorMessage, "test class constructor"))
                {
                    yield return new(typeSyntax.GetLocation(), missingConstructorMessage);
                }

                foreach (var argumentAttribute in classArgumentAttributes)
                {
                    var values = GetArgumentValues(argumentAttribute);
                    if (!ValidateArgumentCount(constructor.Parameters, values.Length, out var countMessage, "test class constructor"))
                    {
                        yield return new(typeSyntax.GetLocation(), countMessage);
                        continue;
                    }

                    if (!ValidateArgumentValues(constructor.Parameters, values, compilation))
                    {
                        yield return new(typeSyntax.GetLocation(),
                            "Constructor argument conversion requires the desktop AOT converter generator and is not supported by the closed-world catalog.");
                    }
                }
            }

            foreach (var propertySyntax in syntaxTree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(propertySyntax) is IPropertySymbol property &&
                    property.GetAttributes().Any(static attribute =>
                        DataSourceAttributeHelper.IsDataSourceAttribute(attribute.AttributeClass)))
                {
                    yield return new(propertySyntax.GetLocation(),
                        "Property data injection, including Arguments properties, is not supported by the closed-world catalog.");
                }
            }
        }
    }

    private static ImmutableArray<TypedConstant> GetArgumentValues(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length != 1)
        {
            return ImmutableArray<TypedConstant>.Empty;
        }

        var argument = attribute.ConstructorArguments[0];
        if (argument.Kind == TypedConstantKind.Array)
        {
            if (argument.IsNull)
            {
                return ImmutableArray.Create(argument);
            }

            return argument.Values;
        }

        return ImmutableArray.Create(argument);
    }

    private static bool SupportsConstant(TypedConstant value, ITypeSymbol targetType, CSharpCompilation compilation)
    {
        if (value.IsNull)
        {
            return targetType.IsReferenceType || IsNullable(targetType);
        }

        if (targetType is ITypeParameterSymbol || SymbolEqualityComparer.Default.Equals(value.Type, targetType))
        {
            return true;
        }

        if (IsNullable(targetType, out var nullableUnderlying))
        {
            return SupportsConstant(value, nullableUnderlying, compilation);
        }

        var conversion = compilation.ClassifyConversion(value.Type!, targetType);
        return conversion.IsImplicit;
    }

    private static bool ValidateArgumentCount(
        ImmutableArray<IParameterSymbol> parameters,
        int valueCount,
        out string message,
        string subject,
        bool allowTrailingCollection = true)
    {
        var collectsTrailing = allowTrailingCollection && parameters.Length > 0 && parameters[^1].CollectsTrailingArguments();
        var requiredCount = parameters
            .Where((parameter, index) => !collectsTrailing || index != parameters.Length - 1)
            .Count(parameter => !parameter.HasExplicitDefaultValue && !parameter.IsOptional);

        if (valueCount < requiredCount)
        {
            message = $"Arguments row has {valueCount} value{(valueCount == 1 ? string.Empty : "s")} but {subject} requires at least {requiredCount}.";
            return false;
        }

        if (!collectsTrailing && valueCount > parameters.Length)
        {
            message = $"Arguments row has {valueCount} values but {subject} accepts at most {parameters.Length}.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool ValidateArgumentValues(
        ImmutableArray<IParameterSymbol> parameters,
        ImmutableArray<TypedConstant> values,
        CSharpCompilation compilation,
        bool allowTrailingCollection = true)
    {
        var collectsTrailing = allowTrailingCollection && parameters.Length > 0 && parameters[^1].CollectsTrailingArguments();
        var valueIndex = 0;
        foreach (var value in values)
        {
            var parameterIndex = Math.Min(valueIndex, parameters.Length - 1);

            var parameter = parameters[parameterIndex];
            var targetType = parameter.Type;
            if (collectsTrailing && parameterIndex == parameters.Length - 1)
            {
                var isSingleArrayPayload = values.Length - valueIndex == 1 && value.Kind == TypedConstantKind.Array;
                if (!isSingleArrayPayload)
                {
                    targetType = ((IArrayTypeSymbol) parameter.Type).ElementType;
                }
            }

            if (!SupportsConstant(value, targetType, compilation))
            {
                return false;
            }

            valueIndex++;
        }

        return true;
    }

    private static IMethodSymbol? GetPrimaryConstructor(INamedTypeSymbol type)
    {
        var constructors = type.InstanceConstructors
            .Where(static constructor => constructor.DeclaredAccessibility == Accessibility.Public)
            .ToArray();
        return constructors
            .Where(static constructor => constructor.GetAttributes().Any(attribute =>
                attribute.AttributeClass!.Name == "TestConstructorAttribute"))
            .OrderByDescending(static constructor => constructor.Parameters.Length)
            .FirstOrDefault()
            ?? constructors.OrderByDescending(static constructor => constructor.Parameters.Length).FirstOrDefault();
    }

    private static bool IsNullable(ITypeSymbol type) => IsNullable(type, out _);

    private static bool IsNullable(ITypeSymbol type, out ITypeSymbol underlying)
    {
        if (type is INamedTypeSymbol { IsGenericType: true, ConstructedFrom.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            underlying = nullable.TypeArguments[0];
            return true;
        }

        underlying = type;
        return false;
    }

}
