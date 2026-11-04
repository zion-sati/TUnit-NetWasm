using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using TUnit.Core.SourceGenerator.CodeGenerators.Helpers;
using TUnit.Core.SourceGenerator.Extensions;
using TUnit.Core.SourceGenerator.Helpers;
using TUnit.Core.SourceGenerator.Models;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed class CatalogOnlyMethodSourceEmitter : ICatalogOnlyMethodSourceEmitter
{
    private readonly IClosedWorldCatalogRoslynAdapter _catalogAdapter;
    private readonly IClosedWorldLifecycleRoslynAdapter _lifecycleAdapter;
    private readonly IMethodSourceEmitter _methodSourceEmitter;

    public CatalogOnlyMethodSourceEmitter(
        IClosedWorldCatalogRoslynAdapter catalogAdapter,
        IClosedWorldLifecycleRoslynAdapter lifecycleAdapter,
        IMethodSourceEmitter methodSourceEmitter)
    {
        _catalogAdapter = catalogAdapter ?? throw new ArgumentNullException(nameof(catalogAdapter));
        _lifecycleAdapter = lifecycleAdapter ?? throw new ArgumentNullException(nameof(lifecycleAdapter));
        _methodSourceEmitter = methodSourceEmitter ?? throw new ArgumentNullException(nameof(methodSourceEmitter));
    }

    public string Emit(CatalogOnlyMethodSourceRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (request.TestMethod is null) throw new ArgumentNullException(nameof(request.TestMethod));

        var testMethod = request.TestMethod;
        var sourceName = FileNameHelper.GetDeterministicFileNameForMethod(testMethod.TypeSymbol, testMethod.MethodSymbol)
            .Replace(".g.cs", "_TestSource");
        var groups = request.Instantiations
            .Select((entry, index) =>
            {
                // A violation of an open generic class has no type to instantiate: its failed tests
                // carry no instance and run no hooks.
                var open = testMethod.TypeSymbol.IsGenericType && entry.ClassTypeArgs.Length == 0;
                var caseTypeName = open ? "global::System.Object" : entry.ConcreteClassName;
                return new MethodSourceGroup(
                open
                    ? "global::TUnit.Core.GeneratedLifecycle.Empty"
                    : _lifecycleAdapter.Format(new LifecycleRoslynRequest(testMethod.TypeSymbol, entry.ConcreteClassName)),
                ImmutableArray.Create(_catalogAdapter.Emit(new CatalogRoslynEmissionRequest(
                    testMethod,
                    caseTypeName,
                    caseTypeName,
                    ClosedWorldCatalogIdentity.FormatFullyQualifiedName(
                        testMethod.TypeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                        testMethod.TypeSymbol.GetNestedClassName(),
                        entry.TestName,
                        entry.ClassTypeArgs),
                    ClosedWorldCatalogIdentity.Format(entry.ClassTypeArgs, entry.MethodTypeArgs),
                    entry.ClassTypeArgs,
                    entry.MethodTypeArgs,
                    entry,
                    $"__Lifecycle_{index.ToString(CultureInfo.InvariantCulture)}"))));
            })
            .ToImmutableArray();
        return _methodSourceEmitter.Emit(new MethodSourceRequest(sourceName, groups));
    }
}
