using System.Collections.Immutable;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed class LifecyclePlanner : ILifecyclePlanner
{
    public ImmutableArray<LifecycleHook> Plan(LifecycleRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        return request.Hooks
            .OrderBy(static hook => StageOrder(hook.Stage))
            .ThenBy(static hook => hook.Stage is "TestTeardown" or "ClassTeardown" ? -hook.Depth : hook.Depth)
            .ThenBy(static hook => hook.Order)
            .ThenBy(static hook => hook.Line)
            .ThenBy(static hook => hook.MethodName, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static int StageOrder(string stage) => stage switch
    {
        "ClassSetup" => 0,
        "TestSetup" => 1,
        "TestTeardown" => 2,
        "ClassTeardown" => 3,
        _ => int.MaxValue,
    };
}
