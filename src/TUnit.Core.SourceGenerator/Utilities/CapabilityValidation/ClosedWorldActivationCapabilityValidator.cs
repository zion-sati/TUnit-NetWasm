using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TUnit.Core.SourceGenerator.CodeGenerators.Helpers;

namespace TUnit.Core.SourceGenerator.Utilities.CapabilityValidation;

internal sealed class ClosedWorldActivationCapabilityValidator : IClosedWorldCapabilityValidator
{
    public IEnumerable<ClosedWorldCapabilityDiagnostic> Validate(CSharpCompilation compilation)
    {
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            foreach (var classSyntax in syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol type)
                {
                    continue;
                }

                foreach (var attribute in type.GetAttributes().Where(static attribute =>
                             DataSourceAttributeHelper.IsDataSourceAttribute(attribute.AttributeClass) &&
                             attribute.AttributeClass!.Name != "ArgumentsAttribute"))
                {
                    yield return new(classSyntax.GetLocation(),
                        $"Runtime class data source '{attribute.AttributeClass!.Name}' is not supported by the closed-world catalog.");
                }

                if (type.AllInterfaces.Any(static @interface =>
                        @interface.ToDisplayString() == "TUnit.Core.Interfaces.IAsyncInitializer"))
                {
                    yield return new(classSyntax.GetLocation(),
                        "IAsyncInitializer property lifecycle requires desktop property-injection generation and is not supported by the closed-world catalog.");
                }

                if (type.GetAttributes().Any(static attribute =>
                        attribute.AttributeClass!.Name is "ClassConstructorAttribute" or "ClassConstructorSourceAttribute"))
                {
                    yield return new(classSyntax.GetLocation(),
                        "ClassConstructor runtime activation is not supported by the closed-world catalog.");
                }
            }
        }
    }
}
