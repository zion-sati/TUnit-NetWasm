using System.Collections.Immutable;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

internal sealed class DirectInstanceCreationEmitter : IInstanceCreationEmitter
{
    public string Emit(InstanceCreationRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        var writer = new CodeWriter(includeHeader: false);

        if (request.HasClassConstructor)
        {
            writer.AppendLine("throw new global::System.NotSupportedException(\"Instance creation for classes with ClassConstructor attribute is handled at runtime\");");
            return writer.ToString();
        }

        if (request.ConstructorParameters.Length > 0)
        {
            writer.Append($"return new {request.TypeName}(");
            var argumentIndex = 0;
            for (var index = 0; index < request.ConstructorParameters.Length; index++)
            {
                if (index > 0) writer.Append(", ");
                var parameter = request.ConstructorParameters[index];
                if (parameter.IsTrailingCollection && parameter.ElementTypeName is not null)
                {
                    if (request.ConstructorArguments.Length - argumentIndex == 1 && request.ConstructorArguments[argumentIndex].StartsWith("new ", StringComparison.Ordinal))
                    {
                        writer.Append(request.ConstructorArguments[argumentIndex]);
                    }
                    else
                    {
                        writer.Append($"new {parameter.ElementTypeName}[] {{ ");
                        writer.Append(string.Join(", ", request.ConstructorArguments.Skip(argumentIndex)));
                        writer.Append(" }");
                    }

                    argumentIndex = request.ConstructorArguments.Length;
                }
                else if (argumentIndex < request.ConstructorArguments.Length)
                {
                    writer.Append(request.ConstructorArguments[argumentIndex++]);
                }
                else
                {
                    writer.Append(parameter.DefaultValue ?? $"default({parameter.TypeName})");
                }
            }

            writer.AppendLine(")");
            AppendRequiredProperties(writer, request.RequiredProperties);
            writer.AppendLine(";");
            return writer.ToString();
        }

        if (request.RequiredProperties.IsDefaultOrEmpty)
        {
            writer.AppendLine($"return new {request.TypeName}();");
            return writer.ToString();
        }

        writer.AppendLine($"return new {request.TypeName}()");
        writer.AppendLine("{");
        writer.Indent();
        foreach (var property in request.RequiredProperties)
        {
            writer.AppendLine($"{property.Name} = {property.DefaultValue},");
        }

        writer.Unindent();
        writer.AppendLine("};");
        return writer.ToString();
    }

    private static void AppendRequiredProperties(CodeWriter writer, ImmutableArray<RequiredProperty> properties)
    {
        if (properties.IsDefaultOrEmpty) return;
        writer.AppendLine();
        writer.AppendLine("{");
        writer.Indent();
        foreach (var property in properties)
        {
            writer.AppendLine($"{property.Name} = {property.DefaultValue},");
        }

        writer.Unindent();
        writer.Append("}");
    }
}
