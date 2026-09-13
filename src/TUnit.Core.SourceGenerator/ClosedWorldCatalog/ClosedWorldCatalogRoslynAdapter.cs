using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using TUnit.Core.SourceGenerator.CodeGenerators.Formatting;
using TUnit.Core.SourceGenerator.CodeGenerators.Helpers;
using TUnit.Core.SourceGenerator.Extensions;
using TUnit.Core.SourceGenerator.Models;

namespace TUnit.Core.SourceGenerator.ClosedWorldCatalog;

/// <summary>
/// Adapts Roslyn symbols and attributes into the immutable contracts consumed by
/// the closed-world actors. Policy and source emission remain in those actors.
/// </summary>
internal sealed class ClosedWorldCatalogRoslynAdapter : IClosedWorldCatalogRoslynAdapter
{
    private readonly ICatalogRowPlanner _rowPlanner;
    private readonly ICaseEmitter _caseEmitter;

    public ClosedWorldCatalogRoslynAdapter(
        ICatalogRowPlanner rowPlanner,
        ICaseEmitter caseEmitter)
    {
        _rowPlanner = rowPlanner ?? throw new ArgumentNullException(nameof(rowPlanner));
        _caseEmitter = caseEmitter ?? throw new ArgumentNullException(nameof(caseEmitter));
    }

    public string Emit(CatalogRoslynEmissionRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        var testMethod = request.TestMethod;
        var concreteClassName = request.ConcreteClassName;
        var groupIdentity = request.GroupIdentity;
        var fullyQualifiedName = request.FullyQualifiedName;
        var generatedIdentitySuffix = request.GeneratedIdentitySuffix;
        var classTypeArguments = request.ClassTypeArguments;
        var methodTypeArguments = request.MethodTypeArguments;
        var selectedArgumentsAttribute = request.SelectedArgumentsAttribute;
        var lifecycleName = request.LifecycleName;
        if (testMethod is null) throw new ArgumentNullException(nameof(request.TestMethod));
        var methodAttributes = testMethod.MethodSymbol.GetAttributes()
            .Where(static attribute => attribute.AttributeClass?.Name == "ArgumentsAttribute")
            .ToArray();
        var selectedClassArguments = selectedArgumentsAttribute is not null &&
            testMethod.TypeSymbol.GetAttributes().Any(attribute => SameAttribute(attribute, selectedArgumentsAttribute));
        var methodRows = methodAttributes
            .Select(ToArgumentRow)
            .ToImmutableArray();
        var selectedMethodIdentity = selectedArgumentsAttribute is not null && !selectedClassArguments
            ? ArgumentIdentity(selectedArgumentsAttribute)
            : null;
        var constructorRows = selectedClassArguments
            ? ImmutableArray.Create(ToArgumentRow(selectedArgumentsAttribute!))
            : testMethod.TypeSymbol.GetAttributes()
                .Where(static attribute => attribute.AttributeClass?.Name == "ArgumentsAttribute")
                .Select(ToArgumentRow)
                .ToImmutableArray();
        var rows = _rowPlanner.Plan(new CatalogRowRequest(
            GetMethodIdentity(testMethod.MethodSymbol, fullyQualifiedName) + generatedIdentitySuffix,
            testMethod.MethodSymbol.Name,
            methodRows,
            constructorRows,
            testMethod.MethodAttributes.Any(static attribute =>
                DataSourceAttributeHelper.IsDataSourceAttribute(attribute.AttributeClass) &&
                attribute.AttributeClass?.Name != "ArgumentsAttribute"),
            selectedMethodIdentity,
            GetMethodDisplayName(testMethod),
            testMethod.MethodSymbol.Parameters
                .Where(static parameter => parameter.Type.GloballyQualified() != "global::System.Threading.CancellationToken")
                .Select(static parameter => parameter.Name)
                .ToImmutableArray(),
            GetClassDisplayName(testMethod),
            GetConstructorParameterNames(testMethod.TypeSymbol)));

        var source = new CodeWriter(includeHeader: false);
        var concreteType = classTypeArguments.Length == 0
            ? testMethod.TypeSymbol
            : testMethod.TypeSymbol.Construct(classTypeArguments);
        foreach (var row in rows)
        {
            var instanceRequest = CreateInstanceRequest(concreteType, row.ConstructorValues);
            var invocationRequest = CreateInvocationRequest(testMethod, methodTypeArguments, classTypeArguments, row.MethodValues);
            source.AppendRaw(_caseEmitter.Emit(new CaseRequest(
                concreteClassName,
                testMethod.MethodSymbol.Name,
                fullyQualifiedName,
                groupIdentity,
                testMethod.FilePath ?? string.Empty,
                testMethod.LineNumber,
                GetInvocationKind(testMethod.MethodSymbol),
                instanceRequest,
                invocationRequest,
                ExtractCategories(testMethod),
                ExtractProperties(testMethod),
                ExtractDependencies(testMethod),
                row,
                lifecycleName)));
        }

        return source.ToString();
    }

    private static InstanceCreationRequest CreateInstanceRequest(
        ITypeSymbol type,
        ImmutableArray<CatalogValue> arguments)
    {
        var parameters = GetPrimaryConstructor(type);
        var constructorParameters = parameters is null
            ? ImmutableArray<ConstructorParameter>.Empty
            : parameters.Parameters.Select(parameter => new ConstructorParameter(
                    parameter.Type.GloballyQualified(),
                    parameter.CollectsTrailingArguments() && parameter.Type is IArrayTypeSymbol,
                    parameter.Type is IArrayTypeSymbol array ? array.ElementType.GloballyQualified() : null,
                    parameter.HasExplicitDefaultValue
                        ? new TypedConstantFormatter().FormatValue(parameter.ExplicitDefaultValue, parameter.Type)
                        : null))
                .ToImmutableArray();
        var requiredProperties = RequiredPropertyHelper.GetAllRequiredProperties(type)
            .Select(property => new RequiredProperty(
                property.Name,
                RequiredPropertyHelper.GetDefaultValueForType(property.Type)))
            .ToImmutableArray();
        return new InstanceCreationRequest(
            type.GloballyQualified(),
            type is INamedTypeSymbol named && InstanceFactoryGenerator.HasClassConstructorAttribute(named),
            constructorParameters,
            arguments.Select(static argument => argument.Code).ToImmutableArray(),
            requiredProperties);
    }

    private static InvocationRequest CreateInvocationRequest(
        TestMethodMetadata testMethod,
        ITypeSymbol[] methodTypeArguments,
        ITypeSymbol[] classTypeArguments,
        ImmutableArray<CatalogValue> values)
    {
        var parameters = testMethod.MethodSymbol.Parameters;
        var valueParameters = parameters
            .Where(static parameter => parameter.Type.GloballyQualified() != "global::System.Threading.CancellationToken")
            .ToArray();
        var lastValueOrdinal = valueParameters.Length == 0 ? -1 : valueParameters[^1].Ordinal;
        var expressions = new List<string>();
        var valueIndex = 0;
        foreach (var parameter in parameters)
        {
            if (parameter.Type.GloballyQualified() == "global::System.Threading.CancellationToken" &&
                parameter.Ordinal == parameters.Length - 1)
            {
                expressions.Add("cancellationToken");
                continue;
            }

            var parameterType = Substitute(parameter.Type, testMethod, classTypeArguments, methodTypeArguments);
            if (parameter.Ordinal == lastValueOrdinal && parameter.CollectsTrailingArguments() && parameterType is IArrayTypeSymbol array)
            {
                if (values.Length - valueIndex == 1 && values[valueIndex].Code.StartsWith("new ", StringComparison.Ordinal))
                {
                    expressions.Add(values[valueIndex++].Code);
                }
                else
                {
                    expressions.Add($"new {array.ElementType.GloballyQualified()}[] {{ {string.Join(", ", values.Skip(valueIndex).Select(static value => value.Code))} }}");
                    valueIndex = values.Length;
                }
                continue;
            }

            expressions.Add(valueIndex < values.Length
                ? values[valueIndex++].Code
                : parameter.HasExplicitDefaultValue
                    ? new TypedConstantFormatter().FormatValue(parameter.ExplicitDefaultValue, parameterType)
                    : $"default({parameterType.GloballyQualified()})");
        }

        var methodName = testMethod.MethodSymbol.Name;
        if (methodTypeArguments.Length > 0)
        {
            methodName += "<" + string.Join(", ", methodTypeArguments.Select(static argument => argument.GloballyQualified())) + ">";
        }

        var receiver = $"instance.{methodName}({string.Join(", ", expressions)})";
        return new InvocationRequest(receiver, GetReturnKind(testMethod.MethodSymbol));
    }

    private static IMethodSymbol? GetPrimaryConstructor(ITypeSymbol type)
    {
        var constructors = type.GetMembers().OfType<IMethodSymbol>()
            .Where(static member => member.MethodKind == MethodKind.Constructor && !member.IsStatic)
            .ToArray();
        var marked = constructors.FirstOrDefault(constructor => constructor.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == WellKnownFullyQualifiedClassNames.TestConstructorAttribute.WithoutGlobalPrefix));
        if (marked is not null)
        {
            return marked;
        }

        var ordered = constructors.OrderByDescending(static constructor => constructor.Parameters.Length).ToArray();
        if (ordered.Length == 1)
        {
            return ordered[0];
        }

        return ordered.FirstOrDefault(static constructor => constructor.DeclaredAccessibility == Accessibility.Public)
            ?? ordered.FirstOrDefault();
    }

    private static CatalogArgumentRow ToArgumentRow(AttributeData attribute)
    {
        var values = GetArgumentValues(attribute);
        return new CatalogArgumentRow(
            values.Select(value => new CatalogValue(
                TypedConstantParser.GetRawTypedConstantValue(value),
                Canonicalize(value),
                Display(value))).ToImmutableArray(),
            GetDisplayName(attribute),
            ArgumentIdentity(attribute));
    }

    private static ImmutableArray<TypedConstant> GetArgumentValues(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length != 1)
        {
            return ImmutableArray<TypedConstant>.Empty;
        }

        var argument = attribute.ConstructorArguments[0];
        if (argument.Kind == TypedConstantKind.Array)
        {
            return argument.IsNull
                ? ImmutableArray.Create(argument)
                : argument.Values.IsDefault ? ImmutableArray<TypedConstant>.Empty : argument.Values;
        }

        return ImmutableArray.Create(argument);
    }

    private static string ArgumentIdentity(AttributeData attribute) =>
        string.Join("|", GetArgumentValues(attribute).Select(Canonicalize));

    private static string Canonicalize(TypedConstant value)
    {
        var typeName = value.Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "unknown";
        if (value.IsNull) return $"null:{typeName}";
        if (value.Kind == TypedConstantKind.Array)
        {
            return $"array:{typeName}:{value.Values.Length}[{string.Join(",", value.Values.Select(Canonicalize))}]";
        }

        if (value.Kind == TypedConstantKind.Type && value.Value is ITypeSymbol type)
        {
            return "type:" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        var text = value.Value switch
        {
            string stringValue => "string:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(stringValue)),
            char character => "char:" + ((int)character).ToString("X4", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "bool:1" : "bool:0",
            _ => "value:" + (Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty),
        };
        return $"scalar:{typeName.Length}:{typeName}:{text.Length}:{text}";
    }

    private static string Display(TypedConstant value)
    {
        if (value.IsNull) return "null";
        if (value.Kind == TypedConstantKind.Array) return string.Join(", ", value.Values.Select(Display));
        if (value.Kind == TypedConstantKind.Type && value.Value is ITypeSymbol type)
        {
            return type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        return value.Value switch
        {
            string text => text.Replace(".", "·"),
            char character => character.ToString(),
            bool boolean => boolean.ToString(),
            _ => Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }

    private static string? GetDisplayName(AttributeData attribute) => attribute.NamedArguments
        .FirstOrDefault(static argument => argument.Key == "DisplayName")
        .Value.Value as string;

    private static string? GetMethodDisplayName(TestMethodMetadata testMethod) => testMethod.MethodAttributes
        .FirstOrDefault(static attribute => attribute.AttributeClass?.Name == "DisplayNameAttribute")
        ?.ConstructorArguments.FirstOrDefault().Value as string;

    private static string? GetClassDisplayName(TestMethodMetadata testMethod) => testMethod.TypeSymbol.GetAttributes()
        .FirstOrDefault(static attribute => attribute.AttributeClass?.Name == "DisplayNameAttribute")
        ?.ConstructorArguments.FirstOrDefault().Value as string;

    private static ImmutableArray<string> GetConstructorParameterNames(INamedTypeSymbol type)
    {
        var constructor = GetPrimaryConstructor(type);
        return constructor is null
            ? ImmutableArray<string>.Empty
            : constructor.Parameters.Select(static parameter => parameter.Name).ToImmutableArray();
    }

    private static bool SameAttribute(AttributeData left, AttributeData right) =>
        SymbolEqualityComparer.Default.Equals(left.AttributeClass, right.AttributeClass) &&
        left.ConstructorArguments.SequenceEqual(right.ConstructorArguments, new TypedConstantComparer());

    private sealed class TypedConstantComparer : IEqualityComparer<TypedConstant>
    {
        public bool Equals(TypedConstant left, TypedConstant right) => Canonicalize(left) == Canonicalize(right);
        public int GetHashCode(TypedConstant value) => Canonicalize(value).GetHashCode();
    }

    private static string GetMethodIdentity(IMethodSymbol method, string fullyQualifiedName)
    {
        var arity = method.TypeParameters.Length == 0 ? string.Empty : $"`{method.TypeParameters.Length}";
        var parameters = string.Join(",", method.Parameters.Select(static parameter =>
            parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        return $"{fullyQualifiedName}{arity}({parameters})";
    }

    private static InvocationReturnKind GetReturnKind(IMethodSymbol method)
    {
        var returnType = method.ReturnType.ToDisplayString();
        if (method.ReturnsVoid) return InvocationReturnKind.Sync;
        if (returnType == "System.Threading.Tasks.ValueTask") return InvocationReturnKind.ValueTask;
        if (returnType.StartsWith("System.Threading.Tasks.ValueTask<", StringComparison.Ordinal)) return InvocationReturnKind.ValueTaskOfT;
        if (returnType.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal) || returnType.StartsWith("Task<", StringComparison.Ordinal)) return InvocationReturnKind.Task;
        return InvocationReturnKind.Unsupported;
    }

    private static string GetInvocationKind(IMethodSymbol method) => GetReturnKind(method) switch
    {
        InvocationReturnKind.Task => "global::TUnit.Core.GeneratedInvocationKind.Task",
        InvocationReturnKind.ValueTask or InvocationReturnKind.ValueTaskOfT => "global::TUnit.Core.GeneratedInvocationKind.ValueTask",
        InvocationReturnKind.Unsupported => "global::TUnit.Core.GeneratedInvocationKind.Unsupported",
        _ => "global::TUnit.Core.GeneratedInvocationKind.Sync",
    };

    private static ImmutableArray<string> ExtractCategories(TestMethodMetadata testMethod)
    {
        var result = new List<string>();
        foreach (var attribute in testMethod.MethodAttributes.Concat(testMethod.TypeSymbol.GetAttributes()).Concat(testMethod.TypeSymbol.ContainingAssembly.GetAttributes()))
        {
            if (attribute.AttributeClass?.Name == "CategoryAttribute" && attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string value)
            {
                result.Add(value);
            }
        }

        return result.Distinct().OrderBy(static value => value, StringComparer.Ordinal).ToImmutableArray();
    }

    private static ImmutableArray<string> ExtractProperties(TestMethodMetadata testMethod)
    {
        var result = new List<string>();
        foreach (var attributes in new[] { testMethod.MethodAttributes, testMethod.TypeSymbol.GetAttributes(), testMethod.TypeSymbol.ContainingAssembly.GetAttributes() })
        {
            foreach (var attribute in attributes)
            {
                if (attribute.AttributeClass?.Name == "PropertyAttribute" && attribute.ConstructorArguments.Length >= 2 && attribute.ConstructorArguments[0].Value is string key && attribute.ConstructorArguments[1].Value is string value)
                {
                    result.Add($"{key}={value}");
                }
            }
        }

        return result.Distinct().OrderBy(static value => value, StringComparer.Ordinal).ToImmutableArray();
    }

    private static ImmutableArray<string> ExtractDependencies(TestMethodMetadata testMethod)
    {
        var result = new List<string>();
        foreach (var attribute in testMethod.MethodAttributes.Concat(testMethod.TypeSymbol.GetAttributes()))
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass?.Name != "DependsOnAttribute" && !(attributeClass?.IsGenericType == true && attributeClass.ConstructedFrom?.Name == "DependsOnAttribute")) continue;
            string? className = attributeClass.IsGenericType && attributeClass.TypeArguments.Length > 0 ? attributeClass.TypeArguments[0].Name : null;
            string? methodName = null;
            foreach (var argument in attribute.ConstructorArguments)
            {
                if (argument.Kind == TypedConstantKind.Array) continue;
                if (argument.Value is INamedTypeSymbol type) className = type.Name;
                else if (argument.Value is string name) methodName = name;
            }

            var dependency = $"{className ?? string.Empty}:{methodName ?? string.Empty}";
            if (dependency != ":") result.Add(dependency);
        }

        return result.Distinct().OrderBy(static value => value, StringComparer.Ordinal).ToImmutableArray();
    }

    private static ITypeSymbol Substitute(ITypeSymbol type, TestMethodMetadata testMethod, ITypeSymbol[] classArguments, ITypeSymbol[] methodArguments)
    {
        if (type is ITypeParameterSymbol parameter)
        {
            var classIndex = testMethod.TypeSymbol.TypeParameters.IndexOf(parameter);
            if (classIndex >= 0 && classIndex < classArguments.Length) return classArguments[classIndex];
            var methodIndex = testMethod.MethodSymbol.TypeParameters.IndexOf(parameter);
            if (methodIndex >= 0 && methodIndex < methodArguments.Length) return methodArguments[methodIndex];
            return type;
        }

        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            return named.OriginalDefinition.Construct(named.TypeArguments.Select(argument => Substitute(argument, testMethod, classArguments, methodArguments)).ToArray());
        }

        return type;
    }
}
