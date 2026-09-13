using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TUnit.Core.SourceGenerator.Utilities.CapabilityValidation;

internal sealed class ClosedWorldAssemblyCapabilityValidator : IClosedWorldCapabilityValidator
{
    public IEnumerable<ClosedWorldCapabilityDiagnostic> Validate(CSharpCompilation compilation)
    {
        foreach (var attribute in compilation.Assembly.GetAttributes().Where(static attribute =>
                     attribute.AttributeClass!.Name is "ClassConstructorAttribute" or "ClassConstructorSourceAttribute"))
        {
            yield return new(Location.None,
                "Assembly-level runtime class construction is not supported by the closed-world catalog.");
        }
    }
}
