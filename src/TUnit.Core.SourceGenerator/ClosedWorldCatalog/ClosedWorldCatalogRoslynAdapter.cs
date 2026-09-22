using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        try
        {
            return EmitValidated(request);
        }
        catch (InvalidOperationException exception)
        {
            return "#error TUNIT closed-world catalog: " +
                   exception.Message.Replace("\r", " ").Replace("\n", " ") +
                   "\n";
        }
    }

    private string EmitValidated(CatalogRoslynEmissionRequest request)
    {
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
        var runtimeMethodDataSources = testMethod.MethodAttributes
            .Where(static attribute => IsMethodDataSource(attribute.AttributeClass))
            .ToArray();
        var methodValueParameters = testMethod.MethodSymbol.Parameters
            .Where(static parameter => parameter.Type.GloballyQualified() != "global::System.Threading.CancellationToken")
            .ToImmutableArray();
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
                attribute.AttributeClass?.Name != "ArgumentsAttribute") ||
            testMethod.MethodSymbol.Parameters.Any(static parameter => parameter.GetAttributes().Any(attribute =>
                IsClassDataSource(attribute.AttributeClass))),
            selectedMethodIdentity,
            GetMethodDisplayName(testMethod),
            testMethod.MethodSymbol.Parameters
                .Where(static parameter => parameter.Type.GloballyQualified() != "global::System.Threading.CancellationToken")
                .Select(static parameter => parameter.Name)
                .ToImmutableArray(),
            GetClassDisplayName(testMethod),
            GetConstructorParameterNames(testMethod.TypeSymbol),
            ExtractEffectiveRepeatCount(testMethod)));

        var source = new CodeWriter(includeHeader: false);
        var concreteType = classTypeArguments.Length == 0
            ? testMethod.TypeSymbol
            : testMethod.TypeSymbol.Construct(classTypeArguments);
        var constructor = GetPrimaryConstructor(concreteType);
        var constructorParameters = constructor?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty;
        var methodClassDataSources = ResolveClassDataSources(
            testMethod.MethodAttributes,
            methodValueParameters,
            testMethod,
            classTypeArguments,
            methodTypeArguments,
            "test method").AddRange(ResolveParameterClassDataSource(
                methodValueParameters,
                testMethod,
                classTypeArguments,
                methodTypeArguments,
                "test method"));
        var constructorClassDataSources = ResolveClassDataSources(
            testMethod.TypeSymbol.GetAttributes(),
            constructorParameters,
            testMethod,
            classTypeArguments,
            methodTypeArguments,
            "test class constructor").AddRange(ResolveParameterClassDataSource(
                constructorParameters,
                testMethod,
                classTypeArguments,
                methodTypeArguments,
                "test class constructor"));

        // When constructor data is exclusively runtime-created, the row planner's
        // synthetic empty constructor row would create an invalid default-valued fixture.
        var canEmitStaticConstructorRows = constructorClassDataSources.IsDefaultOrEmpty || !constructorRows.IsDefaultOrEmpty;
        if (canEmitStaticConstructorRows)
        {
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
                    lifecycleName,
                    ExtractEffectiveTimeoutMilliseconds(testMethod),
                    ExtractEffectiveRetryPolicy(testMethod),
                    ExtractEffectiveSkipReason(testMethod),
                    ExtractEffectiveExecutionPriority(testMethod),
                    HasEffectiveAttribute(testMethod, "ExplicitAttribute", includeAssembly: false),
                    HasEffectiveAttribute(testMethod, "NotDiscoverableAttribute", includeAssembly: true))));
            }
        }

        if (runtimeMethodDataSources.Length > 0)
        {
            source.AppendRaw(EmitRuntimeMethodDataSourceCases(
                request,
                concreteType,
                constructorRows,
                runtimeMethodDataSources,
                constructorClassDataSources));
        }

        if (!methodClassDataSources.IsDefaultOrEmpty)
        {
            source.AppendRaw(EmitMethodClassDataSourceCases(
                request,
                concreteType,
                constructorRows,
                methodClassDataSources,
                constructorClassDataSources));
        }

        if (!constructorClassDataSources.IsDefaultOrEmpty)
        {
            source.AppendRaw(EmitRuntimeConstructorCases(
                request,
                concreteType,
                methodRows,
                runtimeMethodDataSources.Length > 0 || !methodClassDataSources.IsDefaultOrEmpty,
                constructorClassDataSources));
        }

        return source.ToString();
    }

    private string EmitRuntimeMethodDataSourceCases(
        CatalogRoslynEmissionRequest request,
        INamedTypeSymbol concreteType,
        ImmutableArray<CatalogArgumentRow> constructorRows,
        IReadOnlyList<AttributeData> attributes,
        ImmutableArray<ClassDataSourceSpec> constructorClassDataSources)
    {
        var testMethod = request.TestMethod;
        var valueParameters = testMethod.MethodSymbol.Parameters
            .Where(static parameter =>
                parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) !=
                "global::System.Threading.CancellationToken")
            .ToImmutableArray();
        var effectiveConstructorRows = constructorRows.IsDefaultOrEmpty
            ? constructorClassDataSources.IsDefaultOrEmpty
                ? ImmutableArray.Create(new CatalogArgumentRow(ImmutableArray<CatalogValue>.Empty))
                : ImmutableArray<CatalogArgumentRow>.Empty
            : constructorRows;
        var writer = new CodeWriter(includeHeader: false);
        var sourceIndex = 0;
        foreach (var attribute in attributes)
        {
            if (GetNamedBoolean(attribute, "DeferEnumeration"))
            {
                throw new InvalidOperationException(
                    "MethodDataSource DeferEnumeration=true is not supported by the closed-world catalog because discovery must materialize stable row identities.");
            }

            var provider = ResolveMethodDataSourceProvider(testMethod, request, attribute);
            var providerResult = UnwrapAwaitable(provider.ResultType, out var awaitProvider);
            var isAsyncEnumerable = TryGetSequenceElement(providerResult, async: true, out var itemType);
            var isEnumerable = !isAsyncEnumerable && TryGetSequenceElement(providerResult, async: false, out itemType);
            if (!isAsyncEnumerable && !isEnumerable)
            {
                itemType = providerResult;
            }

            var runtimeValues = MapRuntimeValues(itemType, valueParameters, testMethod.CompilationContext.Compilation);
            for (var constructorIndex = 0; constructorIndex < effectiveConstructorRows.Length; constructorIndex++)
            {
                var constructorRow = effectiveConstructorRows[constructorIndex];
                var suffix = $"{sourceIndex}_{constructorIndex}";
                var sourceVariable = $"__source_{suffix}";
                var itemVariable = $"__item_{suffix}";
                var rowIndexVariable = $"__rowIndex_{suffix}";
                var repeatIndexVariable = $"__repeatIndex_{suffix}";
                var providerExpression = provider.Expression;
                writer.AppendLine("{");
                writer.Indent();
                writer.AppendLine($"var {sourceVariable} = {(awaitProvider ? "await " : string.Empty)}{providerExpression};");
                writer.AppendLine($"var {rowIndexVariable} = 0;");
                if (isAsyncEnumerable)
                {
                    writer.AppendLine($"await foreach (var {itemVariable} in {sourceVariable})");
                    writer.AppendLine("{");
                    writer.Indent();
                    EmitRuntimeRows(
                        writer,
                        request,
                        concreteType,
                        constructorRow,
                        runtimeValues,
                        itemVariable,
                        rowIndexVariable,
                        repeatIndexVariable,
                        sourceIndex,
                        constructorIndex,
                        null);
                    writer.Unindent();
                    writer.AppendLine("}");
                }
                else if (isEnumerable)
                {
                    writer.AppendLine($"foreach (var {itemVariable} in {sourceVariable})");
                    writer.AppendLine("{");
                    writer.Indent();
                    EmitRuntimeRows(
                        writer,
                        request,
                        concreteType,
                        constructorRow,
                        runtimeValues,
                        itemVariable,
                        rowIndexVariable,
                        repeatIndexVariable,
                        sourceIndex,
                        constructorIndex,
                        null);
                    writer.Unindent();
                    writer.AppendLine("}");
                }
                else
                {
                    writer.AppendLine($"var {itemVariable} = {sourceVariable};");
                    EmitRuntimeRows(
                        writer,
                        request,
                        concreteType,
                        constructorRow,
                        runtimeValues,
                        itemVariable,
                        rowIndexVariable,
                        repeatIndexVariable,
                        sourceIndex,
                        constructorIndex,
                        null);
                }

                if (!GetNamedBoolean(attribute, "SkipIfEmpty"))
                {
                    writer.AppendLine($"if ({rowIndexVariable} == 0)");
                    writer.AppendLine("{");
                    writer.Indent();
                    writer.AppendLine($"throw new global::System.InvalidOperationException(\"Method data source '{EscapeString(provider.MemberName)}' produced no rows for '{EscapeString(testMethod.MethodSymbol.Name)}'.\");");
                    writer.Unindent();
                    writer.AppendLine("}");
                }
                else
                {
                    EmitEmptyRuntimeSourceCase(
                        writer,
                        request,
                        concreteType,
                        constructorRow,
                        valueParameters,
                        rowIndexVariable,
                        sourceIndex,
                        constructorIndex,
                        null,
                        provider.MemberName);
                }

                writer.Unindent();
                writer.AppendLine("}");
            }

            for (var constructorSourceIndex = 0; constructorSourceIndex < constructorClassDataSources.Length; constructorSourceIndex++)
            {
                var constructorSource = constructorClassDataSources[constructorSourceIndex];
                var suffix = $"{sourceIndex}_class_{constructorSourceIndex}";
                var sourceVariable = $"__source_{suffix}";
                var itemVariable = $"__item_{suffix}";
                var rowIndexVariable = $"__rowIndex_{suffix}";
                var repeatIndexVariable = $"__repeatIndex_{suffix}";
                writer.AppendLine("{");
                writer.Indent();
                writer.AppendLine($"var {sourceVariable} = {(awaitProvider ? "await " : string.Empty)}{provider.Expression};");
                writer.AppendLine($"var {rowIndexVariable} = 0;");
                if (isAsyncEnumerable)
                {
                    writer.AppendLine($"await foreach (var {itemVariable} in {sourceVariable})");
                    writer.AppendLine("{");
                    writer.Indent();
                    EmitRuntimeRows(writer, request, concreteType, default, runtimeValues, itemVariable,
                        rowIndexVariable, repeatIndexVariable, sourceIndex, constructorSourceIndex, constructorSource);
                    writer.Unindent();
                    writer.AppendLine("}");
                }
                else if (isEnumerable)
                {
                    writer.AppendLine($"foreach (var {itemVariable} in {sourceVariable})");
                    writer.AppendLine("{");
                    writer.Indent();
                    EmitRuntimeRows(writer, request, concreteType, default, runtimeValues, itemVariable,
                        rowIndexVariable, repeatIndexVariable, sourceIndex, constructorSourceIndex, constructorSource);
                    writer.Unindent();
                    writer.AppendLine("}");
                }
                else
                {
                    writer.AppendLine($"var {itemVariable} = {sourceVariable};");
                    EmitRuntimeRows(writer, request, concreteType, default, runtimeValues, itemVariable,
                        rowIndexVariable, repeatIndexVariable, sourceIndex, constructorSourceIndex, constructorSource);
                }

                if (!GetNamedBoolean(attribute, "SkipIfEmpty"))
                {
                    writer.AppendLine($"if ({rowIndexVariable} == 0)");
                    writer.AppendLine("{");
                    writer.Indent();
                    writer.AppendLine($"throw new global::System.InvalidOperationException(\"Method data source '{EscapeString(provider.MemberName)}' produced no rows for '{EscapeString(testMethod.MethodSymbol.Name)}'.\");");
                    writer.Unindent();
                    writer.AppendLine("}");
                }
                else
                {
                    EmitEmptyRuntimeSourceCase(
                        writer,
                        request,
                        concreteType,
                        null,
                        valueParameters,
                        rowIndexVariable,
                        sourceIndex,
                        constructorSourceIndex,
                        constructorSource,
                        provider.MemberName);
                }

                writer.Unindent();
                writer.AppendLine("}");
            }

            sourceIndex++;
        }

        return writer.ToString();
    }

    private void EmitEmptyRuntimeSourceCase(
        CodeWriter writer,
        CatalogRoslynEmissionRequest request,
        INamedTypeSymbol concreteType,
        CatalogArgumentRow? constructorRow,
        ImmutableArray<IParameterSymbol> valueParameters,
        string rowIndexVariable,
        int sourceIndex,
        int constructorIndex,
        ClassDataSourceSpec? constructorClassDataSource,
        string providerName)
    {
        writer.AppendLine($"if ({rowIndexVariable} == 0)");
        writer.AppendLine("{");
        writer.Indent();
        var constructorCells = EmitClassDataCells(
            writer,
            constructorClassDataSource,
            $"__emptyConstructorData_{sourceIndex}_{constructorIndex}");
        var rowValues = valueParameters.Select(parameter =>
            {
                var type = Substitute(
                    parameter.Type,
                    request.TestMethod,
                    request.ClassTypeArguments,
                    request.MethodTypeArguments);
                return new CatalogValue($"default({type.GloballyQualified()})", "empty", "empty");
            })
            .ToImmutableArray();
        var constructorValues = constructorClassDataSource is null
            ? constructorRow?.Values ?? ImmutableArray<CatalogValue>.Empty
            : ToCellValues(constructorCells);
        var constructorIdentity = constructorClassDataSource is null
            ? $"constructor-row-{constructorIndex}"
            : $"constructor-class-source-{constructorIndex}";
        var skipReason = constructorRow?.SkipReason ??
            $"Method data source '{providerName}' produced no rows.";
        var row = new CatalogRow(
            rowValues,
            constructorValues,
            $"{GetMethodIdentity(request.TestMethod.MethodSymbol, request.FullyQualifiedName)}{request.GeneratedIdentitySuffix}#method-source-{sourceIndex}:{constructorIdentity}:empty",
            $"{request.TestMethod.MethodSymbol.Name} [no data]",
            skipReason,
            constructorRow?.Categories ?? ImmutableArray<string>.Empty,
            0);
        writer.AppendRaw(_caseEmitter.Emit(new CaseRequest(
            request.ConcreteClassName,
            request.TestMethod.MethodSymbol.Name,
            request.FullyQualifiedName,
            request.GroupIdentity,
            request.TestMethod.FilePath ?? string.Empty,
            request.TestMethod.LineNumber,
            GetInvocationKind(request.TestMethod.MethodSymbol),
            CreateInstanceRequest(concreteType, constructorValues),
            CreateInvocationRequest(
                request.TestMethod,
                request.MethodTypeArguments,
                request.ClassTypeArguments,
                rowValues),
            ExtractCategories(request.TestMethod),
            ExtractProperties(request.TestMethod),
            ExtractDependencies(request.TestMethod),
            row,
            request.LifecycleName,
            ExtractEffectiveTimeoutMilliseconds(request.TestMethod),
            ExtractEffectiveRetryPolicy(request.TestMethod),
            ExtractEffectiveSkipReason(request.TestMethod),
            ExtractEffectiveExecutionPriority(request.TestMethod),
            HasEffectiveAttribute(request.TestMethod, "ExplicitAttribute", includeAssembly: false),
            HasEffectiveAttribute(request.TestMethod, "NotDiscoverableAttribute", includeAssembly: true),
            CapturesRuntimeValues: constructorCells.Length > 0,
            DisposeDataExpression: FormatDisposeData(constructorCells),
            LazilyMaterializeConstructorArguments: constructorCells.Length > 0)));
        writer.Unindent();
        writer.AppendLine("}");
    }

    private void EmitRuntimeRows(
        CodeWriter writer,
        CatalogRoslynEmissionRequest request,
        INamedTypeSymbol concreteType,
        CatalogArgumentRow? constructorRow,
        ImmutableArray<RuntimeValueProjection> values,
        string itemVariable,
        string rowIndexVariable,
        string repeatIndexVariable,
        int sourceIndex,
        int constructorIndex,
        ClassDataSourceSpec? constructorClassDataSource)
    {
        var testMethod = request.TestMethod;
        writer.AppendLine("cancellationToken.ThrowIfCancellationRequested();");
        writer.AppendLine($"for (var {repeatIndexVariable} = 0; {repeatIndexVariable} <= {ExtractEffectiveRepeatCount(testMethod)}; {repeatIndexVariable}++)");
        writer.AppendLine("{");
        writer.Indent();
        var constructorCells = EmitClassDataCells(
            writer,
            constructorClassDataSource,
            $"__constructorData_{sourceIndex}_{constructorIndex}_{repeatIndexVariable}");
        var rowValues = values.Select(value => new CatalogValue(
                value.Format(itemVariable),
                "runtime",
                "runtime"))
            .ToImmutableArray();
        var constructorValues = constructorClassDataSource is null
            ? constructorRow?.Values ?? ImmutableArray<CatalogValue>.Empty
            : ToCellValues(constructorCells);
        var row = new CatalogRow(
            rowValues,
            constructorValues,
            "runtime",
            testMethod.MethodSymbol.Name,
            constructorRow?.SkipReason,
            constructorRow?.Categories ?? ImmutableArray<string>.Empty,
            0);
        var instanceRequest = CreateInstanceRequest(concreteType, constructorValues);
        var invocationRequest = CreateInvocationRequest(
            testMethod,
            request.MethodTypeArguments,
            request.ClassTypeArguments,
            rowValues);
        var constructorIdentity = constructorClassDataSource is null
            ? $"constructor-row-{constructorIndex}"
            : $"constructor-class-source-{constructorIndex}";
        var identityPrefix = EscapeString(
            GetMethodIdentity(testMethod.MethodSymbol, request.FullyQualifiedName) +
            request.GeneratedIdentitySuffix +
            $"#method-source-{sourceIndex}:{constructorIdentity}");
        var stableIdExpression = $"$\"{identityPrefix}:row-{{{rowIndexVariable}}}:repeat-{{{repeatIndexVariable}}}\"";
        var displayNameExpression = $"$\"{EscapeString(testMethod.MethodSymbol.Name)} [row {{{rowIndexVariable}}}]\"";
        writer.AppendRaw(_caseEmitter.Emit(new CaseRequest(
            request.ConcreteClassName,
            testMethod.MethodSymbol.Name,
            request.FullyQualifiedName,
            request.GroupIdentity,
            testMethod.FilePath ?? string.Empty,
            testMethod.LineNumber,
            GetInvocationKind(testMethod.MethodSymbol),
            instanceRequest,
            invocationRequest,
            ExtractCategories(testMethod),
            ExtractProperties(testMethod),
            ExtractDependencies(testMethod),
            row,
            request.LifecycleName,
            ExtractEffectiveTimeoutMilliseconds(testMethod),
            ExtractEffectiveRetryPolicy(testMethod),
            ExtractEffectiveSkipReason(testMethod),
            ExtractEffectiveExecutionPriority(testMethod),
            HasEffectiveAttribute(testMethod, "ExplicitAttribute", includeAssembly: false),
            HasEffectiveAttribute(testMethod, "NotDiscoverableAttribute", includeAssembly: true),
            stableIdExpression,
            displayNameExpression,
            CapturesRuntimeValues: true,
            DisposeDataExpression: FormatDisposeData(constructorCells),
            RepeatIndexExpression: repeatIndexVariable,
            LazilyMaterializeRowArguments: true,
            LazilyMaterializeConstructorArguments: constructorCells.Length > 0)));
        writer.Unindent();
        writer.AppendLine("}");
        writer.AppendLine($"{rowIndexVariable}++;");
    }

    private static MethodDataSourceProvider ResolveMethodDataSourceProvider(
        TestMethodMetadata testMethod,
        CatalogRoslynEmissionRequest request,
        AttributeData attribute)
    {
        var providerType = GetMethodDataSourceProviderType(testMethod, attribute);
        var providerTypeExpression = SymbolEqualityComparer.Default.Equals(providerType, testMethod.TypeSymbol)
            ? request.ConcreteClassName
            : providerType.GloballyQualified();
        var memberName = attribute.ConstructorArguments
            .Select(static argument => argument.Value)
            .OfType<string>()
            .LastOrDefault();
        if (string.IsNullOrWhiteSpace(memberName))
        {
            throw new InvalidOperationException("MethodDataSource must name a provider member.");
        }

        var suppliedArguments = GetNamedArray(attribute, "Arguments");
        var members = EnumerateMembers(providerType, memberName!).ToArray();
        var methods = members.OfType<IMethodSymbol>()
            .Where(static candidate => candidate.IsStatic && IsGeneratedAccessible(candidate.DeclaredAccessibility))
            .Where(candidate => CanBindProviderMethod(candidate, suppliedArguments, testMethod.CompilationContext.Compilation))
            .ToArray();
        if (methods.Length > 1)
        {
            throw new InvalidOperationException(
                $"MethodDataSource member '{memberName}' is ambiguous for the supplied arguments; use a uniquely bindable provider name.");
        }

        var method = methods.SingleOrDefault();
        if (method is not null)
        {
            var arguments = FormatProviderArguments(method, suppliedArguments);
            return new MethodDataSourceProvider(
                memberName!,
                $"{providerTypeExpression}.{method.Name}({string.Join(", ", arguments)})",
                method.ReturnType);
        }

        if (suppliedArguments.Length == 0)
        {
            var property = members.OfType<IPropertySymbol>()
                .FirstOrDefault(static candidate =>
                    candidate.IsStatic &&
                    candidate.GetMethod is not null &&
                    IsGeneratedAccessible(candidate.GetMethod.DeclaredAccessibility));
            if (property is not null)
            {
                return new MethodDataSourceProvider(
                    memberName!,
                    $"{providerTypeExpression}.{property.Name}",
                    property.Type);
            }

            var field = members.OfType<IFieldSymbol>()
                .FirstOrDefault(static candidate => candidate.IsStatic && IsGeneratedAccessible(candidate.DeclaredAccessibility));
            if (field is not null)
            {
                return new MethodDataSourceProvider(
                    memberName!,
                    $"{providerTypeExpression}.{field.Name}",
                    field.Type);
            }
        }

        throw new InvalidOperationException(
            $"MethodDataSource member '{memberName}' must resolve to an accessible static method, property, or field.");
    }

    private static INamedTypeSymbol GetMethodDataSourceProviderType(
        TestMethodMetadata testMethod,
        AttributeData attribute)
    {
        if (attribute.AttributeClass is { IsGenericType: true, TypeArguments.Length: 1 } genericAttribute &&
            genericAttribute.TypeArguments[0] is INamedTypeSymbol genericProvider)
        {
            return genericProvider;
        }

        foreach (var argument in attribute.ConstructorArguments)
        {
            if (argument.Value is INamedTypeSymbol provider)
            {
                return provider;
            }
        }

        return testMethod.TypeSymbol;
    }

    private static IEnumerable<ISymbol> EnumerateMembers(INamedTypeSymbol type, string memberName)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(memberName))
            {
                yield return member;
            }
        }
    }

    private static bool IsGeneratedAccessible(Accessibility accessibility) =>
        accessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

    private static bool CanBindProviderMethod(
        IMethodSymbol method,
        ImmutableArray<TypedConstant> suppliedArguments,
        CSharpCompilation compilation)
    {
        var required = method.Parameters.Count(static parameter =>
            !parameter.IsOptional &&
            parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) !=
            "global::System.Threading.CancellationToken");
        if (suppliedArguments.Length < required || suppliedArguments.Length > method.Parameters.Length)
        {
            return false;
        }

        for (var index = 0; index < suppliedArguments.Length; index++)
        {
            var value = suppliedArguments[index];
            var target = method.Parameters[index].Type;
            if (value.IsNull)
            {
                if (!target.IsReferenceType && !IsNullable(target))
                {
                    return false;
                }
            }
            else if (value.Type is null || !compilation.ClassifyConversion(value.Type, target).IsImplicit)
            {
                return false;
            }
        }

        for (var index = suppliedArguments.Length; index < method.Parameters.Length; index++)
        {
            var parameter = method.Parameters[index];
            if (!parameter.IsOptional &&
                parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) !=
                "global::System.Threading.CancellationToken")
            {
                return false;
            }
        }

        return true;
    }

    private static ImmutableArray<string> FormatProviderArguments(
        IMethodSymbol method,
        ImmutableArray<TypedConstant> suppliedArguments)
    {
        var formatter = new TypedConstantFormatter();
        var arguments = ImmutableArray.CreateBuilder<string>();
        for (var index = 0; index < method.Parameters.Length; index++)
        {
            var parameter = method.Parameters[index];
            if (index < suppliedArguments.Length)
            {
                arguments.Add(formatter.FormatValue(suppliedArguments[index].Value, parameter.Type));
            }
            else if (parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                     "global::System.Threading.CancellationToken")
            {
                arguments.Add("cancellationToken");
            }
            else
            {
                arguments.Add(formatter.FormatValue(parameter.ExplicitDefaultValue, parameter.Type));
            }
        }

        return arguments.ToImmutable();
    }

    private static ITypeSymbol UnwrapAwaitable(ITypeSymbol type, out bool awaitValue)
    {
        if (type is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } named &&
            named.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks" &&
            named.Name is "Task" or "ValueTask")
        {
            awaitValue = true;
            return named.TypeArguments[0];
        }

        awaitValue = false;
        return type;
    }

    private static bool TryGetSequenceElement(ITypeSymbol type, bool async, out ITypeSymbol elementType)
    {
        if (!async && type is IArrayTypeSymbol array)
        {
            elementType = array.ElementType;
            return true;
        }

        var expectedName = async ? "IAsyncEnumerable" : "IEnumerable";
        var candidates = type is INamedTypeSymbol named
            ? named.AllInterfaces.Concat(new[] { named })
            : Enumerable.Empty<INamedTypeSymbol>();
        foreach (var candidate in candidates)
        {
            if (candidate.Name == expectedName &&
                candidate.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic" &&
                candidate.TypeArguments.Length == 1)
            {
                elementType = candidate.TypeArguments[0];
                return true;
            }
        }

        elementType = null!;
        return false;
    }

    private string EmitMethodClassDataSourceCases(
        CatalogRoslynEmissionRequest request,
        INamedTypeSymbol concreteType,
        ImmutableArray<CatalogArgumentRow> constructorRows,
        ImmutableArray<ClassDataSourceSpec> methodSources,
        ImmutableArray<ClassDataSourceSpec> constructorSources)
    {
        var writer = new CodeWriter(includeHeader: false);
        var staticConstructorRows = constructorRows.IsDefaultOrEmpty
            ? constructorSources.IsDefaultOrEmpty
                ? ImmutableArray.Create(new CatalogArgumentRow(ImmutableArray<CatalogValue>.Empty))
                : ImmutableArray<CatalogArgumentRow>.Empty
            : constructorRows;
        for (var methodSourceIndex = 0; methodSourceIndex < methodSources.Length; methodSourceIndex++)
        {
            var methodSource = methodSources[methodSourceIndex];
            for (var constructorIndex = 0; constructorIndex < staticConstructorRows.Length; constructorIndex++)
            {
                var constructorRow = staticConstructorRows[constructorIndex];
                for (var repeatIndex = 0; repeatIndex <= ExtractEffectiveRepeatCount(request.TestMethod); repeatIndex++)
                {
                    EmitClassDataBackedCase(writer, request, concreteType, methodSource,
                        constructorRow.Values, null, methodSourceIndex,
                        constructorIndex, repeatIndex, constructorRow.SkipReason, constructorRow.Categories);
                }
            }

            for (var constructorSourceIndex = 0; constructorSourceIndex < constructorSources.Length; constructorSourceIndex++)
            {
                for (var repeatIndex = 0; repeatIndex <= ExtractEffectiveRepeatCount(request.TestMethod); repeatIndex++)
                {
                    EmitClassDataBackedCase(writer, request, concreteType, methodSource,
                        ImmutableArray<CatalogValue>.Empty, constructorSources[constructorSourceIndex],
                        methodSourceIndex, constructorSourceIndex, repeatIndex);
                }
            }
        }

        return writer.ToString();
    }

    private string EmitRuntimeConstructorCases(
        CatalogRoslynEmissionRequest request,
        INamedTypeSymbol concreteType,
        ImmutableArray<CatalogArgumentRow> methodRows,
        bool hasRuntimeMethodSources,
        ImmutableArray<ClassDataSourceSpec> constructorSources)
    {
        if (methodRows.IsDefaultOrEmpty && hasRuntimeMethodSources)
        {
            return string.Empty;
        }

        var effectiveMethodRows = methodRows.IsDefaultOrEmpty
            ? ImmutableArray.Create(new CatalogArgumentRow(ImmutableArray<CatalogValue>.Empty))
            : methodRows;
        var writer = new CodeWriter(includeHeader: false);
        for (var methodRowIndex = 0; methodRowIndex < effectiveMethodRows.Length; methodRowIndex++)
        {
            var methodRow = effectiveMethodRows[methodRowIndex];
            for (var constructorSourceIndex = 0; constructorSourceIndex < constructorSources.Length; constructorSourceIndex++)
            {
                for (var repeatIndex = 0; repeatIndex <= ExtractEffectiveRepeatCount(request.TestMethod); repeatIndex++)
                {
                    EmitClassDataBackedCase(writer, request, concreteType, null,
                        methodRow.Values, constructorSources[constructorSourceIndex], methodRowIndex,
                        constructorSourceIndex, repeatIndex, methodRow.SkipReason, methodRow.Categories);
                }
            }
        }

        return writer.ToString();
    }

    private void EmitClassDataBackedCase(
        CodeWriter writer,
        CatalogRoslynEmissionRequest request,
        INamedTypeSymbol concreteType,
        ClassDataSourceSpec? methodSource,
        ImmutableArray<CatalogValue> staticValues,
        ClassDataSourceSpec? constructorSource,
        int methodSourceIndex,
        int constructorSourceIndex,
        int repeatIndex,
        string? rowSkipReason = null,
        ImmutableArray<string> rowCategories = default)
    {
        var methodIdentity = methodSource is null
            ? $"method-row-{methodSourceIndex}"
            : $"method-class-source-{methodSourceIndex}";
        var constructorIdentity = constructorSource is null
            ? $"constructor-row-{constructorSourceIndex}"
            : $"constructor-class-source-{constructorSourceIndex}";
        var prefix = $"__classData_{(methodSource is null ? "row" : "class")}_{methodSourceIndex}_" +
                     $"{(constructorSource is null ? "row" : "class")}_{constructorSourceIndex}_{repeatIndex}";
        writer.AppendLine("{");
        writer.Indent();
        var methodCells = EmitClassDataCells(writer, methodSource, prefix + "_method");
        var constructorCells = EmitClassDataCells(writer, constructorSource, prefix + "_constructor");
        var methodValues = methodSource is null ? staticValues : ToCellValues(methodCells);
        var constructorValues = constructorSource is null && methodSource is not null
            ? staticValues
            : ToCellValues(constructorCells);
        var row = new CatalogRow(
            methodValues,
            constructorValues,
            $"{GetMethodIdentity(request.TestMethod.MethodSymbol, request.FullyQualifiedName)}{request.GeneratedIdentitySuffix}#{methodIdentity}:{constructorIdentity}:repeat-{repeatIndex}",
            $"{request.TestMethod.MethodSymbol.Name} [class data {methodSourceIndex}]",
            rowSkipReason,
            rowCategories,
            repeatIndex);
        var instanceRequest = CreateInstanceRequest(concreteType, constructorValues);
        var invocationRequest = CreateInvocationRequest(
            request.TestMethod,
            request.MethodTypeArguments,
            request.ClassTypeArguments,
            methodValues);
        var allCells = methodCells.Concat(constructorCells).ToImmutableArray();
        writer.AppendRaw(_caseEmitter.Emit(new CaseRequest(
            request.ConcreteClassName,
            request.TestMethod.MethodSymbol.Name,
            request.FullyQualifiedName,
            request.GroupIdentity,
            request.TestMethod.FilePath ?? string.Empty,
            request.TestMethod.LineNumber,
            GetInvocationKind(request.TestMethod.MethodSymbol),
            instanceRequest,
            invocationRequest,
            ExtractCategories(request.TestMethod),
            ExtractProperties(request.TestMethod),
            ExtractDependencies(request.TestMethod),
            row,
            request.LifecycleName,
            ExtractEffectiveTimeoutMilliseconds(request.TestMethod),
            ExtractEffectiveRetryPolicy(request.TestMethod),
            ExtractEffectiveSkipReason(request.TestMethod),
            ExtractEffectiveExecutionPriority(request.TestMethod),
            HasEffectiveAttribute(request.TestMethod, "ExplicitAttribute", includeAssembly: false),
            HasEffectiveAttribute(request.TestMethod, "NotDiscoverableAttribute", includeAssembly: true),
            CapturesRuntimeValues: true,
            DisposeDataExpression: FormatDisposeData(allCells),
            LazilyMaterializeRowArguments: methodCells.Length > 0,
            LazilyMaterializeConstructorArguments: constructorCells.Length > 0)));
        writer.Unindent();
        writer.AppendLine("}");
    }

    private static ImmutableArray<ClassDataCell> EmitClassDataCells(
        CodeWriter writer,
        ClassDataSourceSpec? source,
        string prefix)
    {
        if (source is null)
        {
            return ImmutableArray<ClassDataCell>.Empty;
        }

        var cells = ImmutableArray.CreateBuilder<ClassDataCell>(source.Types.Length);
        for (var index = 0; index < source.Types.Length; index++)
        {
            var typeName = source.Types[index].GloballyQualified();
            var name = $"{prefix}_{index}";
            writer.AppendLine($"var {name} = new global::TUnit.Core.GeneratedCaseData<{typeName}>(static () => new {typeName}());");
            cells.Add(new ClassDataCell(name, source.Types[index]));
        }

        return cells.ToImmutable();
    }

    private static ImmutableArray<CatalogValue> ToCellValues(ImmutableArray<ClassDataCell> cells) =>
        cells.Select(static cell => new CatalogValue(
                $"{cell.Name}.Get()",
                "class-data",
                cell.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)))
            .ToImmutableArray();

    private static string? FormatDisposeData(ImmutableArray<ClassDataCell> cells)
    {
        if (cells.IsDefaultOrEmpty)
        {
            return null;
        }

        return "() => global::TUnit.Core.GeneratedCaseDataDisposal.DisposeAllAsync(" +
               string.Join(", ", cells.Select(static cell => cell.Name + ".DisposeAsync")) + ")";
    }

    private static ImmutableArray<ClassDataSourceSpec> ResolveClassDataSources(
        IEnumerable<AttributeData> attributes,
        ImmutableArray<IParameterSymbol> parameters,
        TestMethodMetadata testMethod,
        ITypeSymbol[] classTypeArguments,
        ITypeSymbol[] methodTypeArguments,
        string subject)
    {
        var result = ImmutableArray.CreateBuilder<ClassDataSourceSpec>();
        foreach (var attribute in attributes.Where(static attribute => IsClassDataSource(attribute.AttributeClass)))
        {
            if (HasNonDefaultSharing(attribute))
            {
                throw new InvalidOperationException(
                    $"ClassDataSource sharing on {subject} is not yet supported by the closed-world catalog; use SharedType.None.");
            }

            var types = GetClassDataSourceTypes(attribute);
            if (types.IsDefaultOrEmpty)
            {
                types = parameters.Select(static parameter => parameter.Type).ToImmutableArray();
            }

            types = types.Select(type => Substitute(type, testMethod, classTypeArguments, methodTypeArguments)).ToImmutableArray();
            if (types.Length != parameters.Length)
            {
                throw new InvalidOperationException(
                    $"ClassDataSource supplies {types.Length} value(s), but the {subject} requires {parameters.Length}.");
            }

            for (var index = 0; index < types.Length; index++)
            {
                var parameterType = Substitute(parameters[index].Type, testMethod, classTypeArguments, methodTypeArguments);
                if (!CanAssign(types[index], parameterType, testMethod.CompilationContext.Compilation))
                {
                    throw new InvalidOperationException(
                        $"ClassDataSource type '{types[index]}' is not assignable to {subject} parameter '{parameters[index].Name}'.");
                }

                ValidateClassDataType(types[index], subject);
            }

            result.Add(new ClassDataSourceSpec(types));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<ClassDataSourceSpec> ResolveParameterClassDataSource(
        ImmutableArray<IParameterSymbol> parameters,
        TestMethodMetadata testMethod,
        ITypeSymbol[] classTypeArguments,
        ITypeSymbol[] methodTypeArguments,
        string subject)
    {
        if (parameters.IsDefaultOrEmpty)
        {
            return ImmutableArray<ClassDataSourceSpec>.Empty;
        }

        var attributes = parameters
            .Select(parameter => parameter.GetAttributes()
                .Where(static attribute => IsClassDataSource(attribute.AttributeClass))
                .ToArray())
            .ToArray();
        if (attributes.All(static values => values.Length == 0))
        {
            return ImmutableArray<ClassDataSourceSpec>.Empty;
        }

        if (attributes.Any(static values => values.Length != 1))
        {
            throw new InvalidOperationException(
                $"Parameter-level ClassDataSource on the {subject} requires exactly one source on every value parameter.");
        }

        var types = ImmutableArray.CreateBuilder<ITypeSymbol>(parameters.Length);
        for (var index = 0; index < parameters.Length; index++)
        {
            var attribute = attributes[index][0];
            if (HasNonDefaultSharing(attribute))
            {
                throw new InvalidOperationException(
                    $"Parameter-level ClassDataSource sharing on the {subject} is not yet supported; use SharedType.None.");
            }

            var sourceTypes = GetClassDataSourceTypes(attribute);
            if (sourceTypes.IsDefaultOrEmpty)
            {
                sourceTypes = ImmutableArray.Create<ITypeSymbol>(parameters[index].Type);
            }

            if (sourceTypes.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Parameter-level ClassDataSource on '{parameters[index].Name}' must supply exactly one type.");
            }

            var sourceType = Substitute(sourceTypes[0], testMethod, classTypeArguments, methodTypeArguments);
            var parameterType = Substitute(parameters[index].Type, testMethod, classTypeArguments, methodTypeArguments);
            if (!CanAssign(sourceType, parameterType, testMethod.CompilationContext.Compilation))
            {
                throw new InvalidOperationException(
                    $"ClassDataSource type '{sourceType}' is not assignable to {subject} parameter '{parameters[index].Name}'.");
            }

            ValidateClassDataType(sourceType, subject);
            types.Add(sourceType);
        }

        return ImmutableArray.Create(new ClassDataSourceSpec(types.ToImmutable()));
    }

    private static ImmutableArray<ITypeSymbol> GetClassDataSourceTypes(AttributeData attribute)
    {
        if (attribute.AttributeClass is { IsGenericType: true } genericAttribute)
        {
            return genericAttribute.TypeArguments;
        }

        var result = ImmutableArray.CreateBuilder<ITypeSymbol>();
        foreach (var argument in attribute.ConstructorArguments)
        {
            if (argument.Kind == TypedConstantKind.Array)
            {
                result.AddRange(argument.Values
                    .Where(static value => value.Value is ITypeSymbol)
                    .Select(static value => (ITypeSymbol)value.Value!));
            }
            else if (argument.Value is ITypeSymbol type)
            {
                result.Add(type);
            }
        }

        return result.ToImmutable();
    }

    private static bool HasNonDefaultSharing(AttributeData attribute)
    {
        var shared = attribute.NamedArguments.FirstOrDefault(static argument => argument.Key == "Shared").Value;
        if (shared.Kind == TypedConstantKind.Array)
        {
            return shared.Values.Any(static value => Convert.ToInt32(value.Value, CultureInfo.InvariantCulture) != 0);
        }

        return shared.Value is not null && Convert.ToInt32(shared.Value, CultureInfo.InvariantCulture) != 0;
    }

    private static void ValidateClassDataType(ITypeSymbol type, string subject)
    {
        if (type is INamedTypeSymbol named)
        {
            if (named.IsAbstract)
            {
                throw new InvalidOperationException($"ClassDataSource type '{type}' for {subject} must be concrete.");
            }

            if (!named.IsValueType && !named.InstanceConstructors.Any(static constructor =>
                    constructor.Parameters.Length == 0 && IsGeneratedAccessible(constructor.DeclaredAccessibility)))
            {
                throw new InvalidOperationException(
                    $"ClassDataSource type '{type}' for {subject} must have an accessible parameterless constructor.");
            }

            if (named.AllInterfaces.Any(static implemented =>
                    implemented.Name == "IAsyncInitializer" &&
                    implemented.ContainingNamespace?.ToDisplayString() == "TUnit.Core"))
            {
                throw new InvalidOperationException(
                    $"ClassDataSource type '{type}' for {subject} implements IAsyncInitializer, which is not yet supported by the closed-world catalog.");
            }

            return;
        }

        throw new InvalidOperationException($"ClassDataSource type '{type}' for {subject} cannot be constructed directly.");
    }

    private static ImmutableArray<RuntimeValueProjection> MapRuntimeValues(
        ITypeSymbol itemType,
        ImmutableArray<IParameterSymbol> parameters,
        CSharpCompilation compilation)
    {
        if (parameters.Length == 1)
        {
            if (!CanAssign(itemType, parameters[0].Type, compilation))
            {
                throw new InvalidOperationException(
                    $"MethodDataSource item type '{itemType}' is not assignable to parameter '{parameters[0].Name}'.");
            }

            return ImmutableArray.Create(new RuntimeValueProjection(string.Empty));
        }

        if (itemType is INamedTypeSymbol { IsTupleType: true } tuple &&
            tuple.TupleElements.Length == parameters.Length &&
            parameters.Length <= 7)
        {
            var result = ImmutableArray.CreateBuilder<RuntimeValueProjection>(parameters.Length);
            for (var index = 0; index < parameters.Length; index++)
            {
                if (!CanAssign(tuple.TupleElements[index].Type, parameters[index].Type, compilation))
                {
                    throw new InvalidOperationException(
                        $"MethodDataSource tuple item {index + 1} is not assignable to parameter '{parameters[index].Name}'.");
                }

                result.Add(new RuntimeValueProjection($".Item{index + 1}"));
            }

            return result.ToImmutable();
        }

        if (itemType is IArrayTypeSymbol arrayType)
        {
            var result = ImmutableArray.CreateBuilder<RuntimeValueProjection>(parameters.Length);
            for (var index = 0; index < parameters.Length; index++)
            {
                var cast = CanAssign(arrayType.ElementType, parameters[index].Type, compilation)
                    ? string.Empty
                    : $"({parameters[index].Type.GloballyQualified()})";
                result.Add(new RuntimeValueProjection($"[{index}]", cast));
            }

            return result.ToImmutable();
        }

        throw new InvalidOperationException(
            $"MethodDataSource item type '{itemType}' must map to the test method's {parameters.Length} parameters as a tuple or array.");
    }

    private static bool CanAssign(ITypeSymbol source, ITypeSymbol target, CSharpCompilation compilation) =>
        SymbolEqualityComparer.Default.Equals(source, target) ||
        compilation.ClassifyConversion(source, target).IsImplicit;

    private static bool IsNullable(ITypeSymbol type) =>
        type is INamedTypeSymbol { IsGenericType: true, ConstructedFrom.SpecialType: SpecialType.System_Nullable_T };

    private static ImmutableArray<TypedConstant> GetNamedArray(AttributeData attribute, string name)
    {
        var value = attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value;
        return value.Kind == TypedConstantKind.Array && !value.Values.IsDefault
            ? value.Values
            : ImmutableArray<TypedConstant>.Empty;
    }

    private static bool GetNamedBoolean(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value is true;

    private static bool IsMethodDataSource(INamedTypeSymbol? attributeClass) =>
        attributeClass?.Name == "MethodDataSourceAttribute" &&
        attributeClass.ContainingNamespace?.ToDisplayString() == "TUnit.Core";

    private static bool IsClassDataSource(INamedTypeSymbol? attributeClass) =>
        attributeClass?.Name == "ClassDataSourceAttribute" &&
        attributeClass.ContainingNamespace?.ToDisplayString() == "TUnit.Core";

    private static string EscapeString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");

    private sealed record MethodDataSourceProvider(string MemberName, string Expression, ITypeSymbol ResultType);

    private sealed record ClassDataSourceSpec(ImmutableArray<ITypeSymbol> Types);

    private sealed record ClassDataCell(string Name, ITypeSymbol Type);

    private sealed record RuntimeValueProjection(string Suffix, string Prefix = "")
    {
        public string Format(string itemVariable) => Prefix + itemVariable + Suffix;
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
            ArgumentIdentity(attribute),
            GetNamedString(attribute, "Skip"),
            GetNamedStringArray(attribute, "Categories"));
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

    private static string? GetNamedString(AttributeData attribute, string name) => attribute.NamedArguments
        .FirstOrDefault(argument => argument.Key == name)
        .Value.Value as string;

    private static ImmutableArray<string> GetNamedStringArray(AttributeData attribute, string name)
    {
        var value = attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value;
        if (value.Kind != TypedConstantKind.Array || value.Values.IsDefaultOrEmpty)
        {
            return ImmutableArray<string>.Empty;
        }

        return value.Values
            .Where(static item => item.Value is string text && !string.IsNullOrWhiteSpace(text))
            .Select(static item => (string)item.Value!)
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
    }

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
        foreach (var attribute in EnumerateScopedAttributes(testMethod, includeAssembly: true))
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
        foreach (var attributes in new[] { EnumerateScopedAttributes(testMethod, includeAssembly: true) })
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
        foreach (var attribute in EnumerateScopedAttributes(testMethod, includeAssembly: false))
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

    private static int? ExtractEffectiveTimeoutMilliseconds(TestMethodMetadata testMethod)
    {
        if (TryGetTimeoutMilliseconds(testMethod.MethodAttributes, out var milliseconds))
        {
            return milliseconds;
        }

        for (INamedTypeSymbol? type = testMethod.TypeSymbol; type is not null; type = type.BaseType)
        {
            if (TryGetTimeoutMilliseconds(type.GetAttributes(), out milliseconds))
            {
                return milliseconds;
            }
        }

        return TryGetTimeoutMilliseconds(testMethod.TypeSymbol.ContainingAssembly.GetAttributes(), out milliseconds)
            ? milliseconds
            : null;
    }

    private static int ExtractEffectiveRepeatCount(TestMethodMetadata testMethod)
    {
        if (!TryGetScopedAttribute(testMethod, "RepeatAttribute", includeAssembly: true, out var attribute) ||
            attribute.ConstructorArguments.FirstOrDefault().Value is not int value)
        {
            return 0;
        }

        if (value < 0)
        {
            throw new InvalidOperationException("Repeat count cannot be negative.");
        }

        return value;
    }

    private static string? ExtractEffectiveSkipReason(TestMethodMetadata testMethod)
    {
        foreach (var attribute in EnumerateScopedAttributes(testMethod, includeAssembly: true))
        {
            if (IsTUnitAttribute(attribute, "SkipAttribute") &&
                attribute.ConstructorArguments.FirstOrDefault().Value is string reason)
            {
                return reason;
            }
        }

        return null;
    }

    private static int ExtractEffectiveExecutionPriority(TestMethodMetadata testMethod) =>
        TryGetScopedAttribute(testMethod, "ExecutionPriorityAttribute", includeAssembly: true, out var attribute) &&
        attribute.ConstructorArguments.FirstOrDefault().Value is int value
            ? value
            : 2;

    private static RetryPolicyRequest? ExtractEffectiveRetryPolicy(TestMethodMetadata testMethod)
    {
        if (!TryGetScopedAttribute(testMethod, "RetryAttribute", includeAssembly: true, out var attribute) ||
            attribute.ConstructorArguments.FirstOrDefault().Value is not int maxRetries)
        {
            return null;
        }

        var backoffMilliseconds = 0;
        var backoffMultiplier = 2.0;
        var exceptionTypeNames = ImmutableArray<string>.Empty;
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == "BackoffMs" && argument.Value.Value is int configuredBackoff)
            {
                backoffMilliseconds = configuredBackoff;
            }
            else if (argument.Key == "BackoffMultiplier" && argument.Value.Value is double configuredMultiplier)
            {
                backoffMultiplier = configuredMultiplier;
            }
            else if (argument.Key == "RetryOnExceptionTypes" && argument.Value.Kind == TypedConstantKind.Array)
            {
                exceptionTypeNames = argument.Value.Values
                    .Where(static value => value.Value is ITypeSymbol)
                    .Select(static value => ((ITypeSymbol)value.Value!).GloballyQualified())
                    .ToImmutableArray();
            }
        }

        return new RetryPolicyRequest(maxRetries, backoffMilliseconds, backoffMultiplier, exceptionTypeNames);
    }

    private static bool HasEffectiveAttribute(
        TestMethodMetadata testMethod,
        string attributeName,
        bool includeAssembly) =>
        TryGetScopedAttribute(testMethod, attributeName, includeAssembly, out _);

    private static bool TryGetScopedAttribute(
        TestMethodMetadata testMethod,
        string attributeName,
        bool includeAssembly,
        out AttributeData attribute)
    {
        foreach (var candidate in EnumerateScopedAttributes(testMethod, includeAssembly))
        {
            if (IsTUnitAttribute(candidate, attributeName))
            {
                attribute = candidate;
                return true;
            }
        }

        attribute = null!;
        return false;
    }

    private static IEnumerable<AttributeData> EnumerateScopedAttributes(
        TestMethodMetadata testMethod,
        bool includeAssembly)
    {
        foreach (var attribute in testMethod.MethodAttributes)
        {
            yield return attribute;
        }

        for (INamedTypeSymbol? type = testMethod.TypeSymbol; type is not null; type = type.BaseType)
        {
            foreach (var attribute in type.GetAttributes())
            {
                yield return attribute;
            }
        }

        if (includeAssembly)
        {
            foreach (var attribute in testMethod.TypeSymbol.ContainingAssembly.GetAttributes())
            {
                yield return attribute;
            }
        }
    }

    private static bool IsTUnitAttribute(AttributeData attribute, string name) =>
        attribute.AttributeClass?.Name == name &&
        attribute.AttributeClass.ContainingNamespace?.ToDisplayString() == "TUnit.Core";

    private static bool TryGetTimeoutMilliseconds(
        IEnumerable<AttributeData> attributes,
        out int milliseconds)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass?.Name == "TimeoutAttribute" &&
                attribute.AttributeClass.ContainingNamespace?.ToDisplayString() == "TUnit.Core" &&
                attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is int value)
            {
                milliseconds = value;
                return true;
            }
        }

        milliseconds = 0;
        return false;
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
