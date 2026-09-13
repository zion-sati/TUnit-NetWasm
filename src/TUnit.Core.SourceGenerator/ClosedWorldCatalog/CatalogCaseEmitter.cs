using System.Text;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed class CatalogCaseEmitter : ICaseEmitter
{
    private readonly IInstanceCreationEmitter _instanceCreationEmitter;
    private readonly IInvocationEmitter _invocationEmitter;

    public CatalogCaseEmitter(IInstanceCreationEmitter instanceCreationEmitter, IInvocationEmitter invocationEmitter)
    {
        _instanceCreationEmitter = instanceCreationEmitter ?? throw new ArgumentNullException(nameof(instanceCreationEmitter));
        _invocationEmitter = invocationEmitter ?? throw new ArgumentNullException(nameof(invocationEmitter));
    }

    public string Emit(CaseRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        var writer = new CodeWriter(includeHeader: false);
        writer.AppendLine($"cases.Add(new global::TUnit.Core.GeneratedTestCase<{request.TypeName}>(");
        writer.Indent();
        writer.AppendLine($"\"{Escape(request.MethodName)}\",");
        writer.AppendLine($"\"{Escape(request.FullyQualifiedName)}\",");
        writer.AppendLine($"\"{Escape(request.GroupIdentity)}\",");
        writer.AppendLine("\"" + Escape(request.FilePath ?? string.Empty) + "\",");
        writer.AppendLine($"{request.LineNumber},");
        writer.AppendLine($"{request.InvocationKind},");
        writer.AppendLine("static () =>");
        writer.AppendLine("{");
        writer.Indent();
        writer.AppendRaw(_instanceCreationEmitter.Emit(request.InstanceCreation));
        writer.Unindent();
        writer.AppendLine("},");
        writer.AppendLine("static (instance, cancellationToken) =>");
        writer.AppendLine("{");
        writer.Indent();
        writer.AppendRaw(_invocationEmitter.Emit(request.Invocation));
        writer.Unindent();
        writer.AppendLine("},");
        writer.AppendLine(FormatStringArray(request.Categories) + ",");
        writer.AppendLine(FormatStringArray(request.Properties) + ",");
        writer.AppendLine(FormatStringArray(request.Dependencies) + ",");
        writer.AppendLine(FormatRow(request.Row) + ",");
        writer.AppendLine($"{request.LifecycleName},");
        writer.AppendLine("global::TUnit.Core.GeneratedCompletionPolicy.Await,");
        writer.AppendLine("\"TUnit.Core.SourceGenerator\"));");
        writer.Unindent();
        return writer.ToString();
    }

    private static string FormatRow(CatalogRow row)
    {
        var values = string.Join(", ", row.MethodValues.Select(static value => value.Code));
        var result = new StringBuilder($"new global::TUnit.Core.GeneratedTestCaseRow(\"{Escape(row.StableId)}\", \"{Escape(row.DisplayName)}\", new object?[] {{ {values} }}");
        if (!row.ConstructorValues.IsDefaultOrEmpty)
        {
            result.Append(", constructorArguments: new object?[] { ");
            result.Append(string.Join(", ", row.ConstructorValues.Select(static value => value.Code)));
            result.Append(" }");
        }

        return result.Append(')').ToString();
    }

    private static string FormatStringArray(System.Collections.Immutable.ImmutableArray<string> values)
    {
        var formatted = values.Select(static value => "\"" + Escape(value) + "\"");
        return $"new string[] {{ {string.Join(", ", formatted)} }}";
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
}
