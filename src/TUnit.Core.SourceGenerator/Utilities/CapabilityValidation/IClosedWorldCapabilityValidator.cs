using Microsoft.CodeAnalysis.CSharp;

namespace TUnit.Core.SourceGenerator.Utilities.CapabilityValidation;

internal interface IClosedWorldCapabilityValidator
{
    IEnumerable<ClosedWorldCapabilityDiagnostic> Validate(CSharpCompilation compilation);
}
