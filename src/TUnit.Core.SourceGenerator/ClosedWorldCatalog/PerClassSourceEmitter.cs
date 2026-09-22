namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed class PerClassSourceEmitter : IPerClassSourceEmitter
{
    public string Emit(PerClassSourceRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        var writer = new CodeWriter();
        writer.AppendLine("#nullable enable");
        writer.AppendLine();
        writer.AppendLine();
        writer.AppendLine("namespace TUnit.Generated;");
        writer.AppendLine();
        writer.AppendLine("[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]");
        writer.AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"TUnit\", \"1\")]");
        writer.AppendLine($"internal static class {request.SourceName}");
        writer.AppendLine("{");
        writer.Indent();
        writer.AppendLine($"private static readonly global::TUnit.Core.GeneratedLifecycle __Lifecycle = {request.LifecycleCode};");
        writer.AppendLine("internal static global::TUnit.Core.GeneratedTestCase[] GetGeneratedCases() => GetGeneratedCasesAsync().AsTask().GetAwaiter().GetResult();");
        writer.AppendLine("internal static async global::System.Threading.Tasks.ValueTask<global::TUnit.Core.GeneratedTestCase[]> GetGeneratedCasesAsync(global::System.Threading.CancellationToken cancellationToken = default)");
        writer.AppendLine("{");
        writer.Indent();
        writer.AppendLine("var cases = new global::System.Collections.Generic.List<global::TUnit.Core.GeneratedTestCase>();");
        foreach (var caseBody in request.CaseBodies)
        {
            writer.AppendRaw(caseBody);
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
