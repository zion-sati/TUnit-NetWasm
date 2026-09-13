using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TUnit.Core.SourceGenerator.Extensions;

namespace TUnit.Core.SourceGenerator.Utilities.CapabilityValidation;

internal sealed class ClosedWorldMethodCapabilityValidator : IClosedWorldCapabilityValidator
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
                if (attributes.Any(static attribute => attribute.AttributeClass!.Name == "DynamicTestBuilderAttribute"))
                {
                    yield return new(methodSyntax.GetLocation(),
                        "Dynamic test builders require desktop runtime discovery and are not supported by the closed-world catalog.");
                }

                if (attributes.Any(static attribute => attribute.IsTestAttribute()) &&
                    IsUnsupportedReturn(method))
                {
                    yield return new(methodSyntax.GetLocation(),
                        "Generated catalog test methods must return void, Task, or ValueTask.");
                }
            }
        }
    }

    private static bool IsUnsupportedReturn(IMethodSymbol method) =>
        method.ReturnType.SpecialType != SpecialType.System_Void &&
        !IsTaskOrValueTask(method.ReturnType);

    internal static bool IsTaskOrValueTask(ITypeSymbol returnType)
    {
        var displayName = returnType.ToDisplayString();
        return displayName == "System.Threading.Tasks.Task" ||
               displayName.StartsWith("System.Threading.Tasks.Task<", StringComparison.Ordinal) ||
               displayName == "System.Threading.Tasks.ValueTask" ||
               displayName.StartsWith("System.Threading.Tasks.ValueTask<", StringComparison.Ordinal);
    }
}
