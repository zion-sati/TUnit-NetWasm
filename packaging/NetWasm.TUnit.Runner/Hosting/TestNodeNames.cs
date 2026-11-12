using Microsoft.Testing.Platform.Extensions.Messages;

namespace NetWasm.TUnit.Runner.Hosting;

/// <summary>A test node's fully qualified name, namespace.type.method, as VSTest filters match it.</summary>
public static class TestNodeNames
{
    public static string GetFullyQualifiedName(TestNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Properties.SingleOrDefault<TestMethodIdentifierProperty>() is not { } method)
        {
            return node.DisplayName;
        }

        return string.IsNullOrEmpty(method.Namespace)
            ? $"{method.TypeName}.{method.MethodName}"
            : $"{method.Namespace}.{method.TypeName}.{method.MethodName}";
    }
}
