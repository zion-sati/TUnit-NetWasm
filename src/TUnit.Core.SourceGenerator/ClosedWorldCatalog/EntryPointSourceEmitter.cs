namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed class EntryPointSourceEmitter : IEntryPointSourceEmitter
{
    public string Emit(EntryPointSourceRequest request)
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
        writer.AppendLine("public static class GeneratedTestEntryPoint");
        writer.AppendLine("{");
        writer.Indent();
        writer.AppendLine("public static global::TUnit.Core.ITestEntryCatalog GetCatalog() => GetCatalogAsync().AsTask().GetAwaiter().GetResult();");
        writer.AppendLine("public static async global::System.Threading.Tasks.ValueTask<global::TUnit.Core.ITestEntryCatalog> GetCatalogAsync(global::System.Threading.CancellationToken cancellationToken = default)");
        writer.AppendLine("{");
        writer.Indent();
        writer.AppendLine("var cases = new global::System.Collections.Generic.List<global::TUnit.Core.GeneratedTestCase>();");
        foreach (var sourceName in request.SourceNames)
        {
            writer.AppendLine($"cases.AddRange(await {sourceName}.GetGeneratedCasesAsync(cancellationToken));");
        }

        writer.AppendLine("return new global::TUnit.Core.SourceGeneratedTestCatalog(cases, \"TUnit.Core.SourceGenerator\");");
        writer.Unindent();
        writer.AppendLine("}");
        writer.Unindent();
        writer.AppendLine("}");
        return writer.ToString();
    }
}
