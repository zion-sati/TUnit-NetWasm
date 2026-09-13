using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TUnit.Core.SourceGenerator.Utilities.CapabilityValidation;

internal sealed class ClosedWorldLifecycleCapabilityValidator : IClosedWorldCapabilityValidator
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
                             attribute.AttributeClass!.Name is "BeforeAttribute" or "AfterAttribute"))
                {
                    if (method.Parameters.Any(static parameter =>
                            parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is not
                            ("global::System.Threading.CancellationToken" or
                             "global::TUnit.Core.TestContext" or
                             "global::TUnit.Core.ClassHookContext")))
                    {
                        yield return new(methodSyntax.GetLocation(),
                            "Generated catalog lifecycle hooks may only inject CancellationToken; other parameters are not supported.");
                    }

                    var hookType = attribute.ConstructorArguments.FirstOrDefault().Value;
                    if (hookType is int scope && scope is not (0 or 1))
                    {
                        yield return new(methodSyntax.GetLocation(),
                            "Assembly, test-session, and discovery hooks are not supported by the closed-world catalog.");
                    }

                    if (hookType is 1 && !method.IsStatic)
                    {
                        yield return new(methodSyntax.GetLocation(),
                            "Generated catalog class lifecycle hooks must be static.");
                    }

                    if (method.Parameters.Any(static parameter =>
                            parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is
                            "global::TUnit.Core.TestContext" or "global::TUnit.Core.ClassHookContext"))
                    {
                        yield return new(methodSyntax.GetLocation(),
                            "Generated catalog lifecycle hooks cannot require TestContext or ClassHookContext.");
                    }

                    if (method.ReturnType.SpecialType != SpecialType.System_Void &&
                        !ClosedWorldMethodCapabilityValidator.IsTaskOrValueTask(method.ReturnType))
                    {
                        yield return new(methodSyntax.GetLocation(),
                            "Generated catalog lifecycle hooks must return void, Task, or ValueTask.");
                    }
                }

                foreach (var _ in attributes.Where(static attribute =>
                             attribute.AttributeClass!.Name is "BeforeEveryAttribute" or "AfterEveryAttribute"))
                {
                    yield return new(methodSyntax.GetLocation(),
                        "Global lifecycle hooks are not supported by the closed-world catalog.");
                }
            }
        }
    }
}
