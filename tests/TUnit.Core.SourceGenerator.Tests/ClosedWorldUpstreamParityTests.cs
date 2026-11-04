using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TUnit.Core.SourceGenerator.CodeGenerators;
using TUnit.Core.SourceGenerator.Generators;

namespace TUnit.Core.SourceGenerator.Tests;

/// <summary>
/// Upstream's regression fixtures for generic data rows (tests/TUnit.TestProject/Bugs), compiled into
/// a closed-world catalog and run in process, give the test counts, failure names and messages that
/// upstream's engine tests (tests/TUnit.Engine.Tests/Issue*Tests.cs) assert for source-generated mode.
/// A class the catalog does not support, by a TUNIT1001 diagnostic or the catalog's own #error, is
/// reported as skipped with its message. A class whose file
/// reads TestContext, which TUnit.Core for netwasm0.1 does not have, is checked for its tests and
/// skips but not their outcomes.
/// </summary>
public class ClosedWorldUpstreamParityTests
{
    public sealed record Expectation(
        string Bug,
        string Class,
        int Total,
        int Passed,
        int Failed = 0,
        int Skipped = 0,
        string Method = "*",
        string? Message = null,
        string? FailedName = null,
        string[]? Names = null)
    {
        public override string ToString() => $"{Bug}/{Class}/{Method}";
    }

    private const string Struct = "Type 'String' does not satisfy the 'struct' constraint for generic parameter 'T'";
    private const string NoClassRow = "none of the class-level data rows infer";
    private const string CannotInfer = "Could not infer the type arguments of generic test method 'Generic' from data source [MethodDataSource(nameof(Rows))]";

    public static IEnumerable<Func<Expectation>> Expectations()
    {
        // Issue7106Tests: every class-level and method-level [Arguments] row of a generic test runs.
        foreach (var (name, count) in new[]
                 {
                     ("GenericClassSameTypeRows", 3), ("GenericClassMixedTypeRows", 4), ("GenericClassSameTypeMethodRows", 2),
                     ("GenericClassMixedTypeMethodRows", 3), ("GenericClassRowsTimesGenericMethodRows", 4),
                     ("GenericClassRowsTimesMethodRows", 4), ("GenericClassRowWithMethodRow", 1),
                     ("OverloadedGenericClassMethodRows", 2), ("GenerateGenericTestWithMethodRows", 2),
                     ("GenerateGenericTestWithUninferredMethodRow", 2), ("GenerateGenericTestWithTypeMethodRow", 1),
                     ("ClassRowWithUninferredMethodRow", 1), ("GenerateGenericTestWithClassRowAndUninferredMethodRow", 1),
                     ("ClassRowWithInferredAndUninferredMethodRows", 2), ("GenericMethodUninferredMethodRow", 2),
                     ("GenericMethodSingleNullMethodRow", 2), ("NonGenericClassGenericMethodNullMethodRow", 2),
                     ("TypedNullMethodRowInfersObject", 1), ("NullElementMethodRowInfersObject", 1),
                     ("NullFirstOfSharedTypeParameterMethodRow", 1), ("NullFirstOfSharedTypeParameterWithGenerateGenericTest", 1),
                     ("GenericClassArrayMethodRows", 2), ("GenericClassEnumerableMethodRow", 1),
                     ("GenerateGenericTestWithArrayMethodRow", 2), ("GenericClassSameTypeRowValues", 3),
                 })
        {
            yield return () => new("_7106", name + "*", count, count);
        }

        // Source generation cannot infer type arguments from an untyped runtime class data source.
        yield return () => new("_7106", "PartitionedClassDataSourceReceiver*", 0, 0);
        yield return () => new("_7106", "SingleTypeClassDataSourceReadOnce*", 0, 0);
        yield return () => new("_7106", "DeferredClassDataSourceReceiver*", 0, 0);
        yield return () => new("_7106", "RowSettingsOnGenericClassRows*", 3, 2, Skipped: 1);
        yield return () => new("_7106", "RowSettingsOnNonGenericClassRows*", 3, 2, Skipped: 1);

        // Issue7107Tests: type arguments that break a constraint fail once per row or type.
        yield return () => new("_7107", "ConstraintViolationNeighbourTests", 1, 1);
        foreach (var (name, passed) in new[]
                 {
                     ("ClassRowConstraintTests", 1), ("OnlyInvalidClassRowConstraintTests", 0),
                     ("ClassGenerateGenericConstraintTests", 1), ("MethodGenerateGenericConstraintTests", 1),
                 })
        {
            yield return () => new("_7107", name + "*", passed + 1, passed, Failed: 1, Message: Struct, FailedName: "Run");
        }

        yield return () => new("_7107", "MethodRowInfersClassConstraintTests*", 2, 1, Failed: 1, Message: Struct, FailedName: "Run");
        yield return () => new("_7107", "MatrixInfersTypeConstraintTests", 2, 0, Failed: 2, Method: "Invalid*", Message: Struct, FailedName: "Invalid");
        yield return () => new("_7107", "MatrixInfersTypeConstraintTests", 2, 2, Method: "Valid*");
        yield return () => new("_7107", "SeveralInvalidClassRowsConstraintTests*", 6, 0, Failed: 6);
        yield return () => new("_7107", "SeveralInvalidMethodRowsConstraintTests", 3, 1, Failed: 2, FailedName: "Run");
        yield return () => new("_7107", "ClassAndMethodRowsConstraintTests*", 2, 1, Failed: 1, FailedName: "Run",
            Message: "Type 'String' does not satisfy the 'struct' constraint for generic parameter 'TValue'");
        yield return () => new("_7107", "MethodConstraintNamesClassTypeParameterTests*", 1, 1);
        yield return () => new("_7107", "MethodConstraintNamesClassTypeParameterViolationTests*", 1, 0, Failed: 1,
            Message: "Type 'List`1' does not implement interface 'IEnumerable`1' required for generic parameter 'U'");
        yield return () => new("_7107", "VariantInterfaceConstraintTests*", 1, 1);
        yield return () => new("_7107", "VariantInterfaceClassConstraintTests*", 1, 1);
        yield return () => new("_7107", "ArrayElementConstraintViolationTests", 1, 0, Failed: 1,
            Message: "Type 'List`1' does not implement interface 'IEnumerable`1' required for generic parameter 'T'");
        yield return () => new("_7107", "NestedTypeConstraintTests", 1, 1);
        yield return () => new("_7107", "NestedTypeConstraintViolationTests", 1, 0, Failed: 1,
            Message: "Type 'NestedConstraintPayload' does not implement interface 'IOut`1' required for generic parameter 'T'");
        yield return () => new("_7107", "ArrayElementConstraintTests", 1, 1);
        yield return () => new("_7107", "ArrayTypeArgumentConstraintTests", 2, 1, Failed: 1,
            Message: "Type 'Int32[]' does not satisfy the 'struct' constraint for generic parameter 'T'");
        yield return () => new("_7107", "SuppliedClassTypeMethodRowTests*", 1, 1);
        foreach (var (name, message) in new[]
                 {
                     ("TypeParameterConstraintTests", "Type 'String' does not satisfy the base type constraint 'Stream' for generic parameter 'T'"),
                     ("NewConstraintTests", "Type arguments 'NoParameterlessConstructor' do not satisfy the generic constraints of type 'NewConstraintTests`1'"),
                     ("NullableStructConstraintTests", "Type 'Nullable`1' does not satisfy the 'struct' constraint for generic parameter 'T'"),
                     ("NullableInterfaceConstraintTests", "Type 'Nullable`1' does not implement interface 'IComparable' required for generic parameter 'T'"),
                 })
        {
            yield return () => new("_7107", name + "*", 2, 1, Failed: 1, Message: message, FailedName: "Run");
        }

        yield return () => new("_7107", "NullMethodRowInfersClassConstraintTests*", 1, 0, Failed: 1, FailedName: "Run",
            Message: "Type 'Object' does not satisfy the 'struct' constraint for generic parameter 'T'");
        yield return () => new("_7107", "TypeValueMethodRowInfersClassConstraintTests*", 1, 0, Failed: 1, FailedName: "Run",
            Message: "Type 'Type' does not satisfy the 'struct' constraint for generic parameter 'T'");

        // Issue7092Tests: [GenerateGenericTest] with Arguments and method data sources.
        foreach (var (name, count) in new[]
                 {
                     ("ParameterlessMethodDataSourceTests", 1), ("ValueTaskMethodDataSourceTests", 2),
                     ("GenerateGenericTestWithArgumentsTests", 2), ("GenerateGenericTestWithConvertibleArgumentsTests", 2),
                     ("GenerateGenericTestNotInferredFromArgumentsTests", 4), ("GenerateGenericTestTypedDataSourceTests", 2),
                     ("GenerateGenericTestCoveredByTypedDataSourceTests", 2), ("GenerateGenericTestMatrixInferenceTests", 4),
                     ("GenerateGenericTestMethodDataSourceInferenceTests", 2), ("GenerateGenericTestMultipleMethodDataSourceInferenceTests", 4),
                     ("GenerateGenericTestDuplicateMethodDataSourceInferenceTests", 3), ("GenerateGenericTestGenericMethodDataSourceProviderTests", 2),
                     ("GenerateGenericTestTypedDataSourceDuplicatingArgumentsTests", 1), ("GenerateGenericTestTypedNullArgumentsTests", 2),
                     ("GenerateGenericTestEightElementTupleTests", 1), ("GenericBaseWithClassHookTests", 1),
                     ("EmptyMethodDataSourceTests", 1),
                 })
        {
            yield return () => new("_7092", name, count, count);
        }

        yield return () => new("_7092", "GenerateGenericTestGenericClassArgumentsRowsTests*", 2, 2);
        yield return () => new("_7092", "SkipIfEmptyMethodDataSourceTests", 1, 0, Skipped: 1);

        // Issue7096Tests: generic method rows that infer the same type all run, with their own settings.
        yield return () => new("_7096", "GenericMethodSameTypeRowSettingsTests", 3, 2, Skipped: 1);
        yield return () => new("_7096", "GenericMethodSameTypeArgumentsTests", 3, 3, Method: "SameType*");
        yield return () => new("_7096", "GenericMethodSameTypeArgumentsTests", 4, 4, Method: "MixedTypes*");

        // Issue7105Tests: a generic test keeps a bare path and a display name with its type arguments.
        yield return () => new("_7105", "GenericMethodNames", 2, 2, Method: "Method", Names: ["Method<int>(5)", "Method<string>(s)"]);
        yield return () => new("_7105", "GenericMethodNames", 4, 4, Method: "Generated",
            Names: ["Generated<GenericNamePayload>", "Generated<List<int>>", "Generated<int>", "Generated<int?>"]);
        yield return () => new("_7105", "GenericMethodNames", 2, 2, Method: "SameSimpleName", Names: ["SameSimpleName<SamePayload>", "SameSimpleName<SamePayload>"]);
        yield return () => new("_7105", "GenericClassNames", 1, 1, Method: "Plain", Names: ["Plain"]);

        // Issue7108Tests: method data sources on generic tests resolve the overload desktop resolves.
        foreach (var (name, count) in new[]
                 {
                     ("GenericMethodNonTupleMethodDataSourceTests", 41), ("DataSourceOverloadParityTests", 25),
                     ("GenericClassPrivateMemberDataSourceTests*", 2), ("PublicObjectFieldDataSourceTests", 4),
                     ("KeywordNamedDataSourceTests", 6), ("InheritedProtectedMemberDataSourceTests", 4),
                     ("GenericSingleTupleWithCancellationTokenTests", 2),
                 })
        {
            yield return () => new("_7108", name, count, count);
        }

        yield return () => new("_7108", "PrivateRowTypeDataSourceTests", 2, 0, Failed: 2);
        yield return () => new("_7108", "NoMatchingOverloadDataSourceTests", 1, 0, Failed: 1);

        // Issue7116Tests: a broad declared data source type is inferred as declared, and explained.
        foreach (var (method, message) in new[]
                 {
                     ("ObjectDeclaredType", "Type 'Object' does not implement interface 'IDisposable' required for generic parameter 'T'"),
                     ("NullableDeclaredType", "Type 'Nullable`1' does not implement interface 'IComparable' required for generic parameter 'T'"),
                     ("TypedObjectDeclaredType", "Type 'Object' does not implement interface 'IDisposable' required for generic parameter 'T'"),
                 })
        {
            yield return () => new("_7116", "DeclaredTypeInferenceTests", 1, 0, Failed: 1, Method: method + "*", Message: message);
        }

        foreach (var (method, count) in new[]
                 {
                     ("GenerateGenericTestWithObjectRows", 1), ("GenerateGenericTestWithSingleObjectRows", 1),
                     ("GenerateGenericTestWithExactAndObjectRows", 2), ("GenerateGenericTestWithObjectAndExactRows", 2),
                 })
        {
            yield return () => new("_7116", "DeclaredTypeInferenceTests", count, count, Method: method + "*");
        }

        yield return () => new("_7116", "DeclaredTypeInferenceTests", 1, 1, Method: "MixedArityGenerateGenericTestWithObjectRows*");
        yield return () => new("_7116", "DeclaredTypeInferenceTests", 1, 0, Failed: 1, Method: "GenerateGenericTestWithUnconvertibleRows*",
            Message: "System.IO.StringReader");
        yield return () => new("_7116", "DeclaredTypeInferenceTests", 1, 0, Failed: 1, Method: "EmptyGenerateGenericTestWithObjectRows*");
        yield return () => new("_7116", "DeclaredTypeInferenceTests", 1, 0, Failed: 1, Method: "WrongArityGenerateGenericTestWithObjectRows*");

        // Issue7117Tests: a generic method of a generic class runs each instantiation.
        foreach (var (name, names) in new[]
                 {
                     ("MethodGenerateGenericTestOnClassRow", new[] { "Run<int>(2)" }),
                     ("MethodGenerateGenericTestWithClassRows", new[] { "Run<bool>", "Run<bool>" }),
                     ("ClassGenerateGenericTestWithMethodRow", new[] { "Run<int>(1)" }),
                     ("MethodGenerateGenericTestTypeNotInferredByRow", new[] { "Run<int>(2)", "Run<long>(2)" }),
                     ("MethodDataSourceOnClassRow", new[] { "Run<string>(a, 1)" }),
                     ("MethodDataSourceOnClassGenerateGenericTest", new[] { "Run<string>(a, 1)" }),
                     ("ClassTypedMethodDataSourceOnClassGenerateGenericTest", new[] { "Run<long>(0, 1)" }),
                     ("TypedDataSourceOnClassGenerateGenericTest", new[] { "Run<int>(42)", "Run<long>(42)" }),
                 })
        {
            yield return () => new("_7117", name + "*", names.Length, names.Length, Names: names);
        }

        yield return () => new("_7117", "MethodDataSourceConstraintViolationOnClassRow", 2, 0, Failed: 2, Message: "TMethod");
        yield return () => new("_7117", "MatrixConstraintViolationOnClassRow", 2, 0, Failed: 2, Message: "TMethod");

        // Issue7118Tests: each method row runs on the instantiation it infers, or fails without a class row.
        foreach (var (name, count) in new[]
                 {
                     ("MixedRowsGenericMethod", 4), ("MixedRowsClassTypeMethod", 2), ("MixedRowsSeveralRowsPerType", 5),
                     ("MixedRowsWithUninferredMethodRow", 4), ("ClassRowWithoutMatchingMethodRow", 1),
                     ("MethodRowMatchedByTypedClassSource", 2), ("UninferredMethodRowWithTypedClassSource", 4),
                     ("NullTypedClassRow", 1), ("ArrayParameterNullTypedClassRow", 1), ("ClassRowWithUninferredMethodSource", 3),
                 })
        {
            yield return () => new("_7118", name + "*", count, count);
        }

        foreach (var (name, count) in new[]
                 {
                     ("MethodRowWithoutMatchingClassRow", 2), ("GenerateGenericTestOfClassRowType", 1), ("GenerateGenericTestOfUninferredType", 4),
                 })
        {
            yield return () => new("_7118", name + "*", count, count - 1, Failed: 1, Message: NoClassRow);
        }

        // Issue7120Tests: a null row on a generic method runs as object.
        foreach (var (name, count) in new[]
                 {
                     ("NullArgumentsGenericMethod", 1), ("NullElementGenericMethod", 1), ("NullAndTypedRowsGenericMethod", 2),
                     ("ObjectParameterBeforeNullGenericMethod", 1), ("NullGenericBeforeObjectParameterMethod", 1),
                     ("NullAndValueForSameTypeParameterMethod", 2),
                 })
        {
            yield return () => new("_7120", name, count, count);
        }

        yield return () => new("_7121", "RepeatedTypeParameterTests", 1, 1);
        yield return () => new("_7122", "*", 11, 11);

        // Issue7141Tests: a null row on a generic class runs, or fails a struct constraint.
        yield return () => new("_7141", "NullRowOnGenericClass*", 1, 1);
        yield return () => new("_7141", "NullAndValueRowsOnGenericClass*", 2, 2);
        yield return () => new("_7141", "GenerateGenericTestWithNullRow*", 1, 1);
        yield return () => new("_7141", "NullRowOnConstrainedGenericClass*", 2, 1, Failed: 1, FailedName: "Run",
            Message: "Type 'Object' does not satisfy the 'struct' constraint for generic parameter 'T'");

        yield return () => new("_7146", "*", 9, 9);

        // Issue7153Tests: [Arguments] rows of a [GenerateGenericTest] method that break its constraint fail.
        yield return () => new("_7153", "GenerateGenericTestArgumentsConstraintTests", 2, 1, Failed: 1, Method: "Run*", Message: Struct, FailedName: "Run");
        yield return () => new("_7153", "GenerateGenericTestArgumentsConstraintTests", 3, 1, Failed: 2, Method: "TwoInvalidRows*", Message: Struct);
        yield return () => new("_7153", "GenerateGenericTestArgumentsConstraintTests", 2, 0, Failed: 2, Method: "InvalidRowAndInvalidType*", Message: Struct);
        yield return () => new("_7153", "GenerateGenericTestArgumentsConstraintTests", 2, 1, Failed: 1, Method: "TokenBeforeValue*", Message: Struct,
            FailedName: "TokenBeforeValue");

        // Issue7155Tests: a null and a value-type row infer object.
        foreach (var (name, count) in new[]
                 {
                     ("NullAndValueTypeGenericMethod", 2), ("NullAndValueTypeGenerateGenericTestMethod", 1),
                     ("NullAndValueTypeWithTokenGenerateGenericTestMethod", 1), ("NullableCollectionAndNullGenericMethod", 1),
                 })
        {
            yield return () => new("_7155", name, count, count);
        }

        yield return () => new("_7155", "NullAndValueTypeStructConstraintMethod", 2, 0, Failed: 2);
        yield return () => new("_7158", "GenerateGenericTestTypedClassDataSourceTests", 2, 2);

        // Issue7161Tests: an instantiation only method rows infer is built from the class's data.
        yield return () => new("_7161", "ClassMethodDataSourceWithMethodRows*", 2, 2);
        yield return () => new("_7161", "GenerateGenericTestTypeOnlyMethodRowsInfer*", 2, 2);
        yield return () => new("_7161", "GenericArgumentsArrayClassRow*", 1, 1);
        yield return () => new("_7161", "GenericArgumentsClassRowWithUnmatchedMethodRow*", 2, 1, Failed: 1, Message: NoClassRow);

        // Issue7163Tests: rows whose type arguments cannot be inferred fail instead of vanishing.
        yield return () => new("_7163", "ObjectRowsTests", 2, 0, Failed: 2, Message: CannotInfer, FailedName: "Generic");
        yield return () => new("_7163", "ObjectArrayRowsTests", 1, 0, Failed: 1, Message: CannotInfer, FailedName: "Generic");
        yield return () => new("_7163", "PartlyInferableTests", 2, 1, Failed: 1, Message: CannotInfer);
        foreach (var name in new[] { "GenerateGenericTestWithObjectRowsTests", "NullableGenericReturnTypeTests" })
        {
            yield return () => new("_7163", name, 1, 1);
        }

        foreach (var name in new[] { "ArgumentsAndDataSourceWithSameTypesTests", "TwoDataSourcesWithSameTypesTests" })
        {
            yield return () => new("_7163", name, 2, 2);
        }

        foreach (var name in new[] { "NoDataSourceTests", "NestedGenericReturnTypeTests", "ContainingGenericReturnTypeTests" })
        {
            yield return () => new("_7163", name, 1, 0, Failed: 1, FailedName: "Generic");
        }

        // Issue7164Tests: a typed data source infers the method type arguments on a generic class.
        foreach (var (name, names) in new[]
                 {
                     ("TypedSourceOnClassGenerateGenericTest", new[] { "Run<int>(42)" }),
                     ("TypedSourceAndMethodGenerateGenericTest", new[] { "Run<int>(42)", "Run<long>(42)" }),
                     ("TypedSourceAndSameMethodGenerateGenericTest", new[] { "Run<int>(42)" }),
                     ("TypedSourceOnClassRow", new[] { "Run<int>(42)" }),
                     ("ClassRowTypedSourceAndMethodGenerateGenericTest", new[] { "Run<int>(42)", "Run<long>(42)" }),
                     ("TwoTypeArgumentTypedSourceOnGenericClass", new[] { "Run<int, string>(42, x)" }),
                     ("DuplicateTypedSourcesOnGenericClass", new[] { "Run<int>(42)" }),
                     ("DuplicateArgumentsAndTypedSourceOnClassRow", new[] { "Run<int>(2)" }),
                 })
        {
            yield return () => new("_7164", name + "*", names.Length, names.Length, Names: names);
        }

        yield return () => new("_7173", "*", 23, 23);
    }

    [Test]
    [MethodDataSource(nameof(Expectations))]
    public async Task CatalogReportsWhatUpstreamReports(Expectation expected)
    {
        var run = await RunBug(expected.Bug);
        var unsupported = run.Unsupported.Where(pair => Matches(pair.Key, expected.Class)).ToArray();
        if (unsupported.Length > 0)
        {
            Skip.Test(string.Join("; ", unsupported.Select(static pair => $"{pair.Key}: {pair.Value}")));
        }

        var results = run.Results
            .Where(result => Matches(Bare(result.Class), expected.Class) && Matches(Bare(result.Test), expected.Method))
            .ToArray();
        var description = string.Join(Environment.NewLine, results.Select(static result => result.ToString()));

        await Assert.That(results.Length).IsEqualTo(expected.Total).Because(description);
        await Assert.That(results.Count(static result => result.Outcome == "Skipped")).IsEqualTo(expected.Skipped).Because(description);
        await Assert.That(results.Select(static result => result.StableId).Distinct().Count()).IsEqualTo(results.Length).Because(description);
        if (expected.Names is not null)
        {
            await Assert.That(results.Select(static result => result.DisplayName).OrderBy(static name => name, StringComparer.Ordinal))
                .IsEquivalentTo(expected.Names.OrderBy(static name => name, StringComparer.Ordinal)).Because(description);
        }

        if (results.Any(result => run.ContextDependent.Contains(Bare(result.Class))))
        {
            return;
        }

        await Assert.That(results.Count(static result => result.Outcome == "Passed")).IsEqualTo(expected.Passed).Because(description);
        await Assert.That(results.Count(static result => result.Outcome == "Failed")).IsEqualTo(expected.Failed).Because(description);
        foreach (var failed in results.Where(static result => result.Outcome == "Failed"))
        {
            if (expected.Message is not null)
            {
                await Assert.That(failed.Message).Contains(expected.Message).Because(description);
            }

            if (expected.FailedName is not null)
            {
                await Assert.That(failed.DisplayName).IsEqualTo(expected.FailedName).Because(description);
            }
        }
    }

    public sealed record CaseResult(string Class, string Test, string StableId, string DisplayName, string Outcome, string? Message)
    {
        public override string ToString() => $"{Class}/{Test} [{DisplayName}] {Outcome} {Message}";
    }

    // The cases of a fixture directory, and the classes left out because they use what the
    // closed-world catalog does not support, with its diagnostic.
    public sealed record BugRun(
        CaseResult[] Results,
        IReadOnlyDictionary<string, string> Unsupported,
        IReadOnlyCollection<string> ContextDependent);

    private static readonly ConcurrentDictionary<string, Lazy<Task<BugRun>>> Runs = new();

    private static Task<BugRun> RunBug(string bug) =>
        Runs.GetOrAdd(bug, static bug => new Lazy<Task<BugRun>>(() => CompileAndRun(bug))).Value;

    private static async Task<BugRun> CompileAndRun(string bug)
    {
        var directory = Path.Combine(Git.TestsDirectory.FullName, "TUnit.TestProject", "Bugs", bug);
        var attributes = Path.Combine(Git.TestsDirectory.FullName, "TUnit.TestProject", "Attributes", "ExpectedPassEngineTest.cs");
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        // [EngineTest] only tags fixtures for upstream's engine test runs.
        var fixtures = Directory.GetFiles(directory, "*.cs")
            .Select(path => (SyntaxTree) CSharpSyntaxTree.ParseText(File.ReadAllText(path), parse, path))
            .Select(tree => tree.WithRootAndOptions(new EngineTestRemover().Visit(tree.GetRoot()), parse))
            .ToList();
        var support = new[]
        {
            CSharpSyntaxTree.ParseText(File.ReadAllText(attributes), parse, attributes),
            CSharpSyntaxTree.ParseText(GlobalUsings, parse, "GlobalUsings.cs"),
        };
        var unsupported = new Dictionary<string, string>(StringComparer.Ordinal);
        var contextDependent = fixtures
            .Where(static tree => tree.GetText().ToString().Contains("TestContext", StringComparison.Ordinal))
            .SelectMany(static tree => tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            .Select(static type => type.Identifier.Text)
            .ToHashSet(StringComparer.Ordinal);
        while (true)
        {
            var (generated, errors) = Generate(bug, fixtures.Concat(support), parse);
            var types = fixtures
                .SelectMany(static tree => tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                .Where(static type => type.Parent is not BaseTypeDeclarationSyntax)
                .ToArray();
            var rejected = errors
                .Select(error => (Error: error, Type: Rejected(error, types, bug)))
                .Where(static rejection => rejection.Type is not null)
                .ToArray();
            if (rejected.Length == 0)
            {
                if (errors.Length > 0)
                {
                    throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(static error =>
                        error.Location.SourceTree is { } tree
                            ? $"{error}{Environment.NewLine}    {tree.GetText().Lines.GetLineFromPosition(error.Location.SourceSpan.Start).ToString().Trim()}"
                            : error.ToString())));
                }

                return new BugRun(await Execute(bug, generated), unsupported, contextDependent);
            }

            foreach (var (error, type) in rejected)
            {
                unsupported.TryAdd(type!.Identifier.Text, error.GetMessage());
            }

            var removed = rejected.Select(static rejection => rejection.Type!).ToHashSet();
            fixtures = fixtures
                .Select(tree => tree.WithRootAndOptions(
                    tree.GetRoot().RemoveNodes(tree.GetRoot().DescendantNodes().Where(node => removed.Contains(node)), SyntaxRemoveOptions.KeepNoTrivia)!,
                    parse))
                .ToList();
        }
    }

    // The fixture type a catalog rejection belongs to: a TUNIT1001 diagnostic in its source, or the
    // catalog's #error in the file generated for one of its methods.
    private static BaseTypeDeclarationSyntax? Rejected(Diagnostic error, BaseTypeDeclarationSyntax[] types, string bug)
    {
        if (error.Id == "TUNIT1001" && error.Location.SourceTree is { } tree && types.Any(type => type.SyntaxTree == tree))
        {
            return tree.GetRoot().FindNode(error.Location.SourceSpan).AncestorsAndSelf()
                .OfType<BaseTypeDeclarationSyntax>().LastOrDefault();
        }

        if (error.Id != "CS1029" || !error.GetMessage().Contains("TUNIT closed-world catalog", StringComparison.Ordinal))
        {
            return null;
        }

        // A long name is cut short and given a hash suffix.
        var file = Path.GetFileName(error.Location.SourceTree?.FilePath ?? string.Empty);
        var prefix = $"TUnit_TestProject_Bugs_{bug}_";
        if (!file.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var stem = System.Text.RegularExpressions.Regex.Replace(file.Substring(prefix.Length), @"(_[0-9A-F]{8})?\.g\.cs$", string.Empty);
        return types
            .Where(type => stem.StartsWith(type.Identifier.Text + "_", StringComparison.Ordinal) ||
                           type.Identifier.Text.StartsWith(stem, StringComparison.Ordinal))
            .OrderByDescending(static type => type.Identifier.Text.Length)
            .FirstOrDefault();
    }

    private static (Compilation Generated, Diagnostic[] Errors) Generate(string bug, IEnumerable<SyntaxTree> trees, CSharpParseOptions parse)
    {
        var compilation = CSharpCompilation.Create(
                "ClosedWorldUpstreamParity" + bug,
                trees,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithReferences(ReferencesHelper.References);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [
                    new TestMetadataGenerator().AsSourceGenerator(),
                    new HookMetadataGenerator().AsSourceGenerator(),
                    new PropertyInjectionSourceGenerator().AsSourceGenerator(),
                    new AotConverterGenerator().AsSourceGenerator(),
                    new InfrastructureGenerator().AsSourceGenerator(),
                    new DynamicTestsGenerator().AsSourceGenerator(),
                    new StaticPropertyInitializationGenerator().AsSourceGenerator(),
                ],
                parseOptions: parse)
            .WithUpdatedAnalyzerConfigOptions(new TestAnalyzerConfigOptionsProvider(
                ImmutableDictionary<string, string>.Empty.Add("build_property.TUnitSourceGenerationMode", "ClosedWorldCatalog")));
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var diagnostics);
        return (generated, diagnostics.Concat(generated.GetDiagnostics())
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray());
    }

    private static async Task<CaseResult[]> Execute(string bug, Compilation generated)
    {
        using var stream = new MemoryStream();
        var emitted = generated.Emit(stream);
        if (!emitted.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, emitted.Diagnostics
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.ToString())));
        }

        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        var cases = assembly.GetTypes()
            .Where(static type => type.Namespace == "TUnit.Generated")
            .SelectMany(static type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(static method => method.Name == "GetGeneratedCases" && method.ReturnType == typeof(GeneratedTestCase[])))
            .SelectMany(static method =>
            {
                try
                {
                    return (GeneratedTestCase[]) method.Invoke(null, null)!;
                }
                catch (TargetInvocationException exception)
                {
                    throw new InvalidOperationException($"{method.DeclaringType!.Name} failed discovery: {exception.InnerException}");
                }
            })
            .ToArray();
        var results = new List<CaseResult>();
        foreach (var testCase in cases)
        {
            var (@class, test) = Split(testCase.FullyQualifiedName, $"TUnit.TestProject.Bugs.{bug}.");
            if (testCase.SkipReason is not null)
            {
                results.Add(new(@class, test, testCase.StableId, testCase.DisplayName, "Skipped", testCase.SkipReason));
                continue;
            }

            try
            {
                // The runner passes each case a token it can cancel.
                using var cancellation = new CancellationTokenSource();
                await testCase.ExecuteAsync(cancellation.Token);
                results.Add(new(@class, test, testCase.StableId, testCase.DisplayName, "Passed", null));
            }
            catch (Exception exception)
            {
                results.Add(new(@class, test, testCase.StableId, testCase.DisplayName, "Failed", exception.Message));
            }
            finally
            {
                await testCase.DisposeDataAsync();
            }
        }

        return results.ToArray();
    }

    // "Namespace.Class<int>.Test" splits into the class and the test, outside type argument lists.
    private static (string Class, string Test) Split(string fullyQualifiedName, string namespacePrefix)
    {
        var name = fullyQualifiedName.StartsWith(namespacePrefix, StringComparison.Ordinal)
            ? fullyQualifiedName.Substring(namespacePrefix.Length)
            : fullyQualifiedName;
        var depth = 0;
        for (var index = name.Length - 1; index >= 0; index--)
        {
            depth += name[index] switch { '>' => 1, '<' => -1, _ => 0 };
            if (depth == 0 && name[index] == '.')
            {
                return (name.Substring(0, index), name.Substring(index + 1));
            }
        }

        return (string.Empty, name);
    }

    // A path segment without its type arguments, as upstream's tree filters name generic tests.
    private static string Bare(string segment) =>
        segment.IndexOf('<') is var index and >= 0 ? segment.Substring(0, index) : segment;

    // Tree filter segments: a trailing '*' matches any suffix, as upstream's engine tests use them.
    private static bool Matches(string value, string pattern) =>
        pattern == "*" ||
        (pattern.EndsWith("*", StringComparison.Ordinal)
            ? value.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.Ordinal)
            : value == pattern);

    private sealed class EngineTestRemover : Microsoft.CodeAnalysis.CSharp.CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitAttributeList(Microsoft.CodeAnalysis.CSharp.Syntax.AttributeListSyntax node)
        {
            var kept = node.Attributes.Where(static attribute => attribute.Name.ToString() != "EngineTest").ToArray();
            return kept.Length == 0 ? null : node.WithAttributes(Microsoft.CodeAnalysis.CSharp.SyntaxFactory.SeparatedList(kept));
        }
    }

    private const string GlobalUsings = """
        global using global::System;
        global using global::System.Collections.Generic;
        global using global::System.IO;
        global using global::System.Linq;
        global using global::System.Threading;
        global using global::System.Threading.Tasks;
        global using global::TUnit.Core;
        global using global::TUnit.Assertions;
        global using global::TUnit.Assertions.Extensions;
        global using static global::TUnit.Core.HookType;
        """;
}
