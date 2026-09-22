namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed class MethodSourceEmitter : IMethodSourceEmitter
{
    public string Emit(MethodSourceRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        var writer = new CodeWriter(includeHeader: false);
        writer.AppendLine("[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]");
        writer.AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"TUnit\", \"1\")]");
        writer.AppendLine($"internal static class {request.SourceName}");
        writer.AppendLine("{");
        writer.Indent();
        for (var index = 0; index < request.Groups.Length; index++)
        {
            writer.AppendLine($"private static readonly global::TUnit.Core.GeneratedLifecycle __Lifecycle_{index} = {request.Groups[index].LifecycleCode};");
        }

        writer.AppendLine("internal static global::TUnit.Core.GeneratedTestCase[] GetGeneratedCases() => GetGeneratedCasesAsync().AsTask().GetAwaiter().GetResult();");
        writer.AppendLine("internal static async global::System.Threading.Tasks.ValueTask<global::TUnit.Core.GeneratedTestCase[]> GetGeneratedCasesAsync(global::System.Threading.CancellationToken cancellationToken = default)");
        writer.AppendLine("{");
        writer.Indent();
        writer.AppendLine("var cases = new global::System.Collections.Generic.List<global::TUnit.Core.GeneratedTestCase>();");
        foreach (var group in request.Groups)
        {
            foreach (var caseBody in group.CaseBodies)
            {
                writer.AppendRaw(caseBody);
            }
        }

        writer.AppendLine("await global::System.Threading.Tasks.Task.CompletedTask;");
        writer.AppendLine("return cases.ToArray();");
        writer.Unindent();
        writer.AppendLine("}");
        writer.Unindent();
        writer.AppendLine("}");
        return writer.ToString();
    }
}
