namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed class CatalogCollectionEmitter : ICatalogCollectionEmitter
{
    public string Emit(CatalogCollectionRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        var writer = new CodeWriter(includeHeader: false);
        foreach (var group in request.Groups)
        {
            if (request.IncludeGroupMethods)
            {
                writer.AppendLine($"internal static global::TUnit.Core.GeneratedTestCase[] GetGeneratedCases_{group.Index}()");
                writer.AppendLine("{");
                writer.Indent();
                writer.AppendLine("var cases = new global::System.Collections.Generic.List<global::TUnit.Core.GeneratedTestCase>();");
                foreach (var caseBody in group.CaseBodies) writer.AppendRaw(caseBody);
                writer.AppendLine("return cases.ToArray();");
                writer.Unindent();
                writer.AppendLine("}");
            }
        }

        writer.AppendLine("internal static global::TUnit.Core.GeneratedTestCase[] GetGeneratedCases()");
        writer.AppendLine("{");
        writer.Indent();
        writer.AppendLine("var cases = new global::System.Collections.Generic.List<global::TUnit.Core.GeneratedTestCase>();");
        foreach (var group in request.Groups)
        {
            if (request.IncludeGroupMethods)
            {
                writer.AppendLine($"cases.AddRange(GetGeneratedCases_{group.Index}());");
            }
            else
            {
                foreach (var caseBody in group.CaseBodies) writer.AppendRaw(caseBody);
            }
        }
        writer.AppendLine("return cases.ToArray();");
        writer.Unindent();
        writer.AppendLine("}");
        return writer.ToString();
    }
}
