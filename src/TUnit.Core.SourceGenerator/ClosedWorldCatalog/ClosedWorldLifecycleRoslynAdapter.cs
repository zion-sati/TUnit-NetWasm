using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using TUnit.Core.SourceGenerator.Extensions;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed class ClosedWorldLifecycleRoslynAdapter : IClosedWorldLifecycleRoslynAdapter
{
    private readonly ILifecyclePlanner _planner;
    private readonly ILifecycleFormatter _formatter;

    public ClosedWorldLifecycleRoslynAdapter(ILifecyclePlanner planner, ILifecycleFormatter formatter)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
    }

    public string Format(LifecycleRoslynRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        var typeSymbol = request.TypeSymbol;
        var concreteClassName = request.ConcreteClassName;
        if (typeSymbol is null) throw new ArgumentNullException(nameof(typeSymbol));
        var hooks = ImmutableArray.CreateBuilder<LifecycleHook>();
        var hierarchy = typeSymbol.GetSelfAndBaseTypes().Reverse().ToArray();
        for (var depth = 0; depth < hierarchy.Length; depth++)
        {
            foreach (var member in hierarchy[depth].GetMembers())
            {
                if (member is not IMethodSymbol method ||
                    method.MethodKind != MethodKind.Ordinary ||
                    method.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                foreach (var attribute in method.GetAttributes())
                {
                    var attributeName = attribute.AttributeClass?.Name;
                    if (attributeName is not ("BeforeAttribute" or "AfterAttribute") ||
                        attribute.ConstructorArguments.Length == 0 ||
                        !TryGetStage(attribute.ConstructorArguments[0], attributeName, out var stage))
                    {
                        continue;
                    }

                    var arguments = new List<string>();
                    var supported = true;
                    foreach (var parameter in method.Parameters)
                    {
                        if (parameter.Type.Name == "CancellationToken" && parameter.Type.ContainingNamespace?.ToString() == "System.Threading")
                        {
                            arguments.Add("cancellationToken");
                        }
                        else if (parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::TUnit.Core.TestContext")
                        {
                            arguments.Add("global::TUnit.Core.TestContext.Current!");
                        }
                        else if (parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::TUnit.Core.ClassHookContext")
                        {
                            arguments.Add("global::TUnit.Core.ClassHookContext.Current!");
                        }
                        else
                        {
                            supported = false;
                            break;
                        }
                    }

                    if (!supported || !IsSupportedReturnType(method.ReturnType))
                    {
                        continue;
                    }

                    var receiver = method.IsStatic ? concreteClassName + "." : $"(({concreteClassName})instance).";
                    var invocation = receiver + method.Name + "(" + string.Join(", ", arguments) + ")";
                    var line = method.Locations.FirstOrDefault()?.GetLineSpan().StartLinePosition.Line ?? 0;
                    var order = attribute.NamedArguments.FirstOrDefault(static pair => pair.Key == "Order").Value.Value is int configuredOrder
                        ? configuredOrder
                        : 0;
                    hooks.Add(new LifecycleHook(
                        stage,
                        depth,
                        order,
                        line,
                        method.Name,
                        invocation,
                        method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        GetTimeoutMilliseconds(method)));
                }
            }
        }

        return _formatter.Format(_planner.Plan(new LifecycleRequest(hooks.ToImmutable())));
    }

    private static bool IsSupportedReturnType(ITypeSymbol returnType) =>
        returnType.SpecialType == SpecialType.System_Void ||
        returnType.Name is "Task" or "ValueTask";

    private static bool TryGetStage(TypedConstant value, string attributeName, out string stage)
    {
        var hookValue = value.Value is null ? -1 : Convert.ToInt32(value.Value, CultureInfo.InvariantCulture);
        stage = (attributeName, hookValue) switch
        {
            ("BeforeAttribute", 0) => "TestSetup",
            ("AfterAttribute", 0) => "TestTeardown",
            ("BeforeAttribute", 1) => "ClassSetup",
            ("AfterAttribute", 1) => "ClassTeardown",
            _ => string.Empty,
        };
        return stage.Length > 0;
    }

    private static int? GetTimeoutMilliseconds(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.Name == "TimeoutAttribute" &&
                attribute.AttributeClass.ContainingNamespace?.ToDisplayString() == "TUnit.Core" &&
                attribute.ConstructorArguments.FirstOrDefault().Value is int milliseconds)
            {
                return milliseconds;
            }
        }

        return null;
    }
}
