using Microsoft.CodeAnalysis;

namespace TUnit.Core.SourceGenerator.Utilities.CapabilityValidation;

internal readonly record struct ClosedWorldCapabilityDiagnostic(Location Location, string Message);
