using System.Collections.Immutable;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed class LifecycleFormatter : ILifecycleFormatter
{
    public string Format(ImmutableArray<LifecycleHook> hooks)
    {
        if (hooks.IsDefaultOrEmpty) return "global::TUnit.Core.GeneratedLifecycle.Empty";
        var writer = new CodeWriter(includeHeader: false);
        writer.AppendLine("new global::TUnit.Core.GeneratedLifecycle(new global::TUnit.Core.GeneratedLifecycleAction[]");
        writer.AppendLine("{");
        writer.Indent();
        foreach (var hook in hooks)
        {
            writer.AppendLine($"new global::TUnit.Core.GeneratedLifecycleAction(global::TUnit.Core.GeneratedLifecycleStage.{hook.Stage}, {hook.Order}, {FormatDelegate(hook.Invocation, hook.ReturnType)}),");
        }

        writer.Unindent();
        writer.AppendLine("})");
        return writer.ToString();
    }

    private static string FormatDelegate(string invocation, string returnType)
    {
        if (returnType == "global::System.Threading.Tasks.ValueTask") return $"static (instance, cancellationToken) => {invocation}";
        if (returnType.StartsWith("global::System.Threading.Tasks.ValueTask<", StringComparison.Ordinal)) return $"static (instance, cancellationToken) => new global::System.Threading.Tasks.ValueTask({invocation}.AsTask())";
        if (returnType.StartsWith("global::System.Threading.Tasks.Task", StringComparison.Ordinal)) return $"static (instance, cancellationToken) => new global::System.Threading.Tasks.ValueTask({invocation})";
        return $"static (instance, cancellationToken) => {{ {invocation}; return default(global::System.Threading.Tasks.ValueTask); }}";
    }
}
