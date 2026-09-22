using System.Collections.Immutable;
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
        writer.AppendLine(request.CapturesRuntimeValues ? "() =>" : "static () =>");
        writer.AppendLine("{");
        writer.Indent();
        writer.AppendRaw(_instanceCreationEmitter.Emit(request.InstanceCreation));
        writer.Unindent();
        writer.AppendLine("},");
        writer.AppendLine(request.CapturesRuntimeValues ? "(instance, cancellationToken) =>" : "static (instance, cancellationToken) =>");
        writer.AppendLine("{");
        writer.Indent();
        writer.AppendRaw(_invocationEmitter.Emit(request.Invocation));
        writer.Unindent();
        writer.AppendLine("},");
        var rowCategories = request.Row.Categories.IsDefault ? ImmutableArray<string>.Empty : request.Row.Categories;
        writer.AppendLine(FormatStringArray(request.Categories.Concat(rowCategories).Distinct(StringComparer.Ordinal).ToImmutableArray()) + ",");
        writer.AppendLine(FormatStringArray(request.Properties) + ",");
        writer.AppendLine(FormatStringArray(request.Dependencies) + ",");
        writer.AppendLine(FormatRow(request) + ",");
        writer.AppendLine($"{request.LifecycleName},");
        writer.AppendLine("global::TUnit.Core.GeneratedCompletionPolicy.Await,");
        writer.AppendLine("\"TUnit.Core.SourceGenerator\",");
        writer.AppendLine($"timeout: {FormatTimeout(request.TimeoutMilliseconds)},");
        writer.AppendLine($"retryPolicy: {FormatRetryPolicy(request.RetryPolicy)},");
        writer.AppendLine($"skipReason: {FormatNullableString(request.Row.SkipReason ?? request.SkipReason)},");
        writer.AppendLine($"executionPriority: {request.ExecutionPriority.ToString(global::System.Globalization.CultureInfo.InvariantCulture)},");
        writer.AppendLine($"isExplicit: {FormatBoolean(request.IsExplicit)},");
        writer.AppendLine($"isNotDiscoverable: {FormatBoolean(request.IsNotDiscoverable)},");
        writer.AppendLine($"repeatIndex: {request.RepeatIndexExpression ?? request.Row.RepeatIndex.ToString(global::System.Globalization.CultureInfo.InvariantCulture)},");
        writer.AppendLine($"disposeData: {request.DisposeDataExpression ?? "null"}));");
        writer.Unindent();
        return writer.ToString();
    }

    private static string FormatRow(CaseRequest request)
    {
        var row = request.Row;
        var values = string.Join(", ", row.MethodValues.Select(static value => value.Code));
        var stableId = request.StableIdExpression ?? $"\"{Escape(row.StableId)}\"";
        var displayName = request.DisplayNameExpression ?? $"\"{Escape(row.DisplayName)}\"";
        var result = new StringBuilder($"new global::TUnit.Core.GeneratedTestCaseRow({stableId}, {displayName}");
        if (request.LazilyMaterializeRowArguments)
        {
            result.Append($", argumentsFactory: () => new object?[] {{ {values} }}");
        }
        else
        {
            result.Append($", new object?[] {{ {values} }}");
        }

        if (!row.ConstructorValues.IsDefaultOrEmpty)
        {
            result.Append(request.LazilyMaterializeConstructorArguments
                ? ", constructorArgumentsFactory: () => new object?[] { "
                : ", constructorArguments: new object?[] { ");
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

    private static string FormatTimeout(int? milliseconds) => milliseconds is int value
        ? $"global::System.TimeSpan.FromMilliseconds({value.ToString(global::System.Globalization.CultureInfo.InvariantCulture)})"
        : "null";

    private static string FormatRetryPolicy(RetryPolicyRequest? policy)
    {
        if (policy is null || policy.MaxRetries == 0)
        {
            return "global::TUnit.Core.GeneratedRetryPolicy.None";
        }

        var predicate = policy.ExceptionTypeNames.IsDefaultOrEmpty
            ? "null"
            : "static exception => " + string.Join(" || ", policy.ExceptionTypeNames.Select(static type => $"exception is {type}"));
        return $"new global::TUnit.Core.GeneratedRetryPolicy({policy.MaxRetries.ToString(global::System.Globalization.CultureInfo.InvariantCulture)}, {policy.BackoffMilliseconds.ToString(global::System.Globalization.CultureInfo.InvariantCulture)}, {policy.BackoffMultiplier.ToString("R", global::System.Globalization.CultureInfo.InvariantCulture)}, {predicate})";
    }

    private static string FormatNullableString(string? value) => value is null
        ? "null"
        : $"\"{Escape(value)}\"";

    private static string FormatBoolean(bool value) => value ? "true" : "false";

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
}
