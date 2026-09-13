using Microsoft.CodeAnalysis.CSharp;

namespace TUnit.Core.SourceGenerator.Utilities.CapabilityValidation;

/// <summary>
/// Composes independent closed-world capability checks. Each validator owns
/// one rejection policy and exposes only the single validation action.
/// </summary>
internal sealed class CompositeClosedWorldCapabilityValidator : IClosedWorldCapabilityValidator
{
    private readonly IReadOnlyList<IClosedWorldCapabilityValidator> _validators;

    public CompositeClosedWorldCapabilityValidator(IEnumerable<IClosedWorldCapabilityValidator> validators)
    {
        _validators = validators?.ToArray() ?? throw new ArgumentNullException(nameof(validators));
    }

    public IEnumerable<ClosedWorldCapabilityDiagnostic> Validate(CSharpCompilation compilation)
    {
        foreach (var validator in _validators)
        {
            foreach (var diagnostic in validator.Validate(compilation))
            {
                yield return diagnostic;
            }
        }
    }
}
