namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed class DirectInvocationEmitter : IInvocationEmitter
{
    public string Emit(InvocationRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        return request.ReturnKind switch
        {
            InvocationReturnKind.Sync => $"{request.MethodCall};\nreturn default(global::System.Threading.Tasks.ValueTask);",
            InvocationReturnKind.ValueTask => $"return {request.MethodCall};",
            InvocationReturnKind.ValueTaskOfT => $"return new global::System.Threading.Tasks.ValueTask({request.MethodCall}.AsTask());",
            InvocationReturnKind.Task => $"return new global::System.Threading.Tasks.ValueTask({request.MethodCall});",
            InvocationReturnKind.Unsupported => "throw new global::System.NotSupportedException(\"Generated catalog supports only void, Task, and ValueTask test methods.\");",
            _ => throw new ArgumentOutOfRangeException(nameof(request.ReturnKind)),
        };
    }
}
