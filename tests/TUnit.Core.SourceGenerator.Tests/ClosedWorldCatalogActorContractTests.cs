using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core.SourceGenerator.CodeGenerators.Writers;
using TUnit.Core.SourceGenerator.ClosedWorldCatalog;
using TUnit.Core.SourceGenerator.Helpers;
using TUnit.Core.SourceGenerator.Models;

namespace TUnit.Core.SourceGenerator.Tests;

internal sealed class ClosedWorldCatalogActorContractTests
{
    [Test]
    public async Task InstanceCreationCoversDirectAndRequiredShapes()
    {
        IInstanceCreationEmitter emitter = new DirectInstanceCreationEmitter();
        await Assert.That(() => emitter.Emit(null!)).Throws<ArgumentNullException>();
        await Assert.That(emitter.Emit(new InstanceCreationRequest(
            "global::Fixture",
            false,
            ImmutableArray<ConstructorParameter>.Empty,
            ImmutableArray<string>.Empty,
            ImmutableArray<RequiredProperty>.Empty))).IsEqualTo("return new global::Fixture();\n");

        var required = emitter.Emit(new InstanceCreationRequest(
            "global::Fixture",
            false,
            ImmutableArray<ConstructorParameter>.Empty,
            ImmutableArray<string>.Empty,
            ImmutableArray.Create(new RequiredProperty("Value", "default(global::System.Int32)"))));
        await Assert.That(required).Contains("Value = default(global::System.Int32)");

        var constructor = emitter.Emit(new InstanceCreationRequest(
            "global::Fixture",
            false,
            ImmutableArray.Create(
                new ConstructorParameter("global::System.Int32", false, null, null),
                new ConstructorParameter("global::System.String", false, null, "\"default\"")),
            ImmutableArray.Create("7"),
            ImmutableArray<RequiredProperty>.Empty));
        await Assert.That(constructor).Contains("return new global::Fixture(7, \"default\")");

        var missingCollection = emitter.Emit(new InstanceCreationRequest(
            "global::Fixture",
            false,
            ImmutableArray.Create(new ConstructorParameter("global::System.Object", true, null, "default(global::System.Object)")),
            ImmutableArray<string>.Empty,
            ImmutableArray<RequiredProperty>.Empty));
        await Assert.That(missingCollection).Contains("default(global::System.Object)");

        var paramsArray = emitter.Emit(new InstanceCreationRequest(
            "global::Fixture",
            false,
            ImmutableArray.Create(new ConstructorParameter("global::System.Int32[]", true, "global::System.Int32", null)),
            ImmutableArray.Create("1", "2"),
            ImmutableArray<RequiredProperty>.Empty));
        await Assert.That(paramsArray).Contains("new global::System.Int32[] { 1, 2 }");

        var suppliedArray = emitter.Emit(new InstanceCreationRequest(
            "global::Fixture",
            false,
            ImmutableArray.Create(new ConstructorParameter("global::System.Int32[]", true, "global::System.Int32", null)),
            ImmutableArray.Create("new global::System.Int32[] { 3 }"),
            ImmutableArray.Create(new RequiredProperty("Value", "0"))));
        await Assert.That(suppliedArray).Contains("new global::System.Int32[] { 3 }");
        await Assert.That(suppliedArray).Contains("Value = 0");

        var classConstructor = emitter.Emit(new InstanceCreationRequest(
            "global::Fixture",
            true,
            ImmutableArray<ConstructorParameter>.Empty,
            ImmutableArray<string>.Empty,
            ImmutableArray<RequiredProperty>.Empty));
        await Assert.That(classConstructor).Contains("handled at runtime");
    }

    [Test]
    public async Task RowPlannerPlansEmptyRuntimeSelectedAndCartesianRows()
    {
        ICatalogRowPlanner planner = new CatalogRowPlanner();
        await Assert.That(() => planner.Plan(null!)).Throws<ArgumentNullException>();
        var noRows = planner.Plan(new CatalogRowRequest(
            "M()", "M", ImmutableArray<CatalogArgumentRow>.Empty,
            ImmutableArray<CatalogArgumentRow>.Empty, true));
        await Assert.That(noRows).IsEmpty();

        var methodRow = new CatalogArgumentRow(
            ImmutableArray.Create(new CatalogValue("7", "int:7", "7")),
            Identity: "selected");
        var constructorRow = new CatalogArgumentRow(
            ImmutableArray.Create(new CatalogValue("\"x\"", "string:x", "x")));
        var rows = planner.Plan(new CatalogRowRequest(
            "M()", "M", ImmutableArray.Create(methodRow), ImmutableArray.Create(constructorRow), false, "selected"));
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].StableId).Contains("method:[int:7];constructor:[string:x]");
        await Assert.That(rows[0].DisplayName).IsEqualTo("M(7)");

        var explicitName = planner.Plan(new CatalogRowRequest(
            "M()", "M", ImmutableArray.Create(methodRow with { DisplayName = "named" }),
            ImmutableArray<CatalogArgumentRow>.Empty, false));
        await Assert.That(explicitName[0].DisplayName).IsEqualTo("named");
        await Assert.That(planner.Plan(new CatalogRowRequest(
            "M()", "M", ImmutableArray.Create(methodRow), ImmutableArray<CatalogArgumentRow>.Empty, false, "missing"))).IsEmpty();

        var unknownStage = new LifecyclePlanner().Plan(new LifecycleRequest(ImmutableArray.Create(
            new LifecycleHook("Unknown", 0, 0, 0, "unknown", "instance.unknown()", "global::System.Void"))));
        await Assert.That(unknownStage[0].Stage).IsEqualTo("Unknown");
    }

    [Test]
    public async Task LifecyclePlanningAndFormattingPreserveHookOrder()
    {
        ILifecyclePlanner planner = new LifecyclePlanner();
        await Assert.That(() => planner.Plan(null!)).Throws<ArgumentNullException>();
        var planned = planner.Plan(new LifecycleRequest(ImmutableArray.Create(
            new LifecycleHook("ClassTeardown", 0, 0, 0, "classDown", "instance.classDown()", "global::System.Void"),
            new LifecycleHook("ClassSetup", 0, 0, 0, "classUp", "Fixture.classUp()", "global::System.Void"),
            new LifecycleHook("TestTeardown", 1, 0, 0, "derivedDown", "instance.derivedDown()", "global::System.Threading.Tasks.ValueTask"),
            new LifecycleHook("TestTeardown", 0, 0, 0, "baseDown", "instance.baseDown()", "global::System.Threading.Tasks.Task"))));
        await Assert.That(planned[0].Stage).IsEqualTo("ClassSetup");
        await Assert.That(planned[1].MethodName).IsEqualTo("derivedDown");
        await Assert.That(planned[2].MethodName).IsEqualTo("baseDown");
        var allStages = planner.Plan(new LifecycleRequest(ImmutableArray.Create(
            new LifecycleHook("ClassSetup", 0, 0, 0, "classUp", "instance.classUp()", "global::System.Void"),
            new LifecycleHook("TestSetup", 0, 0, 0, "testUp", "instance.testUp()", "global::System.Void"),
            new LifecycleHook("TestTeardown", 0, 0, 0, "testDown", "instance.testDown()", "global::System.Void"),
            new LifecycleHook("ClassTeardown", 0, 0, 0, "classDown", "instance.classDown()", "global::System.Void"),
            new LifecycleHook("Unknown", 0, 0, 0, "unknown", "instance.unknown()", "global::System.Void"))));
        await Assert.That(allStages.Select(static hook => hook.Stage)).IsEquivalentTo(
            ["ClassSetup", "TestSetup", "TestTeardown", "ClassTeardown", "Unknown"]);

        ILifecycleFormatter formatter = new LifecycleFormatter();
        await Assert.That(formatter.Format(ImmutableArray<LifecycleHook>.Empty)).IsEqualTo("global::TUnit.Core.GeneratedLifecycle.Empty");
        var source = formatter.Format(planned);
        await Assert.That(source).Contains("GeneratedLifecycleStage.ClassSetup");
        await Assert.That(source).Contains("new global::System.Threading.Tasks.ValueTask(instance.baseDown())");
        await Assert.That(source).Contains("instance.classDown();");
        await Assert.That(formatter.Format(ImmutableArray.Create(new LifecycleHook(
            "TestSetup", 0, 0, 0, "generic", "instance.generic()", "global::System.Threading.Tasks.ValueTask<global::System.Int32>"))))
            .Contains("AsTask()");
        await Assert.That(formatter.Format(ImmutableArray.Create(new LifecycleHook(
            "TestSetup", 0, 0, 0, "task", "instance.task()", "global::System.Threading.Tasks.Task<global::System.Int32>"))))
            .Contains("new global::System.Threading.Tasks.ValueTask(instance.task())");
    }

    [Test]
    public async Task InvocationAndCaseEmittersPreserveTypedSemantics()
    {
        IInvocationEmitter invocation = new DirectInvocationEmitter();
        await Assert.That(() => invocation.Emit(null!)).Throws<ArgumentNullException>();
        await Assert.That(invocation.Emit(new InvocationRequest("instance.Case()", InvocationReturnKind.Sync))).Contains("return default");
        await Assert.That(invocation.Emit(new InvocationRequest("instance.Case()", InvocationReturnKind.ValueTask))).IsEqualTo("return instance.Case();");
        await Assert.That(invocation.Emit(new InvocationRequest("instance.Case()", InvocationReturnKind.ValueTaskOfT))).Contains("AsTask()");
        await Assert.That(invocation.Emit(new InvocationRequest("instance.Case()", InvocationReturnKind.Task))).Contains("ValueTask(instance.Case())");
        await Assert.That(invocation.Emit(new InvocationRequest("instance.Case()", InvocationReturnKind.Unsupported))).Contains("only void, Task, and ValueTask");
        await Assert.That(() => invocation.Emit(new InvocationRequest("instance.Case()", (InvocationReturnKind) 99))).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new CatalogCaseEmitter(null!, invocation)).Throws<ArgumentNullException>();
        await Assert.That(() => new CatalogCaseEmitter(new DirectInstanceCreationEmitter(), null!)).Throws<ArgumentNullException>();

        ICaseEmitter cases = new CatalogCaseEmitter(new DirectInstanceCreationEmitter(), new DirectInvocationEmitter());
        await Assert.That(() => cases.Emit(null!)).Throws<ArgumentNullException>();
        var source = cases.Emit(new CaseRequest(
            "global::Fixture", "Case", "Fixture.Case", "Fixture", "fixture.cs", 4,
            "global::TUnit.Core.GeneratedInvocationKind.Sync",
            new InstanceCreationRequest("global::Fixture", false, ImmutableArray<ConstructorParameter>.Empty, ImmutableArray<string>.Empty, ImmutableArray<RequiredProperty>.Empty),
            new InvocationRequest("instance.Case()", InvocationReturnKind.Sync),
            ImmutableArray.Create("fast"), ImmutableArray.Create("key=value"), ImmutableArray.Create("Dependency:Case"),
            new CatalogRow(ImmutableArray<CatalogValue>.Empty,
                ImmutableArray.Create(new CatalogValue("\"ctor\"", "string:ctor", "ctor")), "stable", "Case()"), "__Lifecycle"));
        await Assert.That(source).Contains("GeneratedTestCase<global::Fixture>");
        await Assert.That(source).Contains("\"stable\"");
        await Assert.That(source).Contains("constructorArguments");
        await Assert.That(source).Contains("new string[] { \"fast\" }");
        var emptyRowSource = cases.Emit(new CaseRequest(
            "global::Fixture", "Case", "Fixture.Case", "Fixture", "fixture.cs", 4,
            "global::TUnit.Core.GeneratedInvocationKind.Sync",
            new InstanceCreationRequest("global::Fixture", false, ImmutableArray<ConstructorParameter>.Empty, ImmutableArray<string>.Empty, ImmutableArray<RequiredProperty>.Empty),
            new InvocationRequest("instance.Case()", InvocationReturnKind.Sync),
            ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, ImmutableArray<string>.Empty,
            new CatalogRow(ImmutableArray<CatalogValue>.Empty, ImmutableArray<CatalogValue>.Empty, "empty", "Case()"), "__Lifecycle"));
        await Assert.That(emptyRowSource).Contains("new global::TUnit.Core.GeneratedTestCaseRow(\"empty\"");
        var escapedSource = cases.Emit(new CaseRequest(
            "global::Fixture", "Case\"", "Fixture.Case", "Fixture", "fixture\\file.cs", 4,
            "global::TUnit.Core.GeneratedInvocationKind.Sync",
            new InstanceCreationRequest("global::Fixture", false, ImmutableArray<ConstructorParameter>.Empty, ImmutableArray<string>.Empty, ImmutableArray<RequiredProperty>.Empty),
            new InvocationRequest("instance.Case()", InvocationReturnKind.Sync),
            ImmutableArray.Create("line\nvalue"), ImmutableArray<string>.Empty, ImmutableArray<string>.Empty,
            new CatalogRow(ImmutableArray.Create(new CatalogValue("7", "int:7", "7")), ImmutableArray<CatalogValue>.Empty, "escaped", "Case()"), "__Lifecycle"));
        await Assert.That(escapedSource).Contains("Case" + "\\\"");
    }

    [Test]
    public async Task RoslynCatalogAdapterEmitsSemanticCaseFromSymbolsThroughInterface()
    {
        var metadata = CreateMetadata("""
            using System.Threading.Tasks;
            using TUnit.Core;
            namespace TestProject;
            public sealed class Fixture
            {
                [Test]
                [Arguments(7)]
                public ValueTask Case(int value) => ValueTask.CompletedTask;
            }
            """, "Case");

        IClosedWorldCatalogRoslynAdapter adapter = new ClosedWorldCatalogRoslynAdapter(
            new CatalogRowPlanner(),
            new CatalogCaseEmitter(new DirectInstanceCreationEmitter(), new DirectInvocationEmitter()));
        var source = adapter.Emit(new CatalogRoslynEmissionRequest(
            metadata,
            "global::TestProject.Fixture",
            "Fixture",
            "TestProject.Fixture.Case",
            string.Empty,
            Array.Empty<ITypeSymbol>(),
            Array.Empty<ITypeSymbol>(),
            null,
            "__Lifecycle"));

        await Assert.That(source).Contains("GeneratedTestCase<global::TestProject.Fixture>");
        await Assert.That(source).Contains("instance.Case(7)");
        await Assert.That(source).Contains("TestProject.Fixture.Case");

        ICatalogOnlyMethodSourceEmitter methodEmitter = new CatalogOnlyMethodSourceEmitter(
            adapter,
            new ClosedWorldLifecycleRoslynAdapter(new LifecyclePlanner(), new LifecycleFormatter()),
            new MethodSourceEmitter());
        await Assert.That(() => methodEmitter.Emit(null!)).Throws<ArgumentNullException>();
        var methodSource = methodEmitter.Emit(new CatalogOnlyMethodSourceRequest(
            metadata,
            ImmutableArray.Create(new ConcreteInstantiation
            {
                ConcreteClassName = "global::TestProject.Fixture",
                TypeArguments = Array.Empty<ITypeSymbol>(),
                ClassTypeArgs = Array.Empty<ITypeSymbol>(),
                MethodTypeArgs = Array.Empty<ITypeSymbol>(),
                TestName = "Case",
                MethodName = "Case",
            })));
        await Assert.That(methodSource).Contains("class");
        await Assert.That(methodSource).Contains("GetGeneratedCases");
    }

    [Test]
    public async Task RoslynLifecycleAdapterMapsSupportedHooksThroughInterface()
    {
        var metadata = CreateMetadata("""
            using System.Threading;
            using System.Threading.Tasks;
            using TUnit.Core;
            namespace TestProject;
            public sealed class Fixture
            {
                [Test] public void Case() { }
                [Before(HookType.Class)] public static void ClassSetup() { }
                [After(HookType.Class)] public static Task ClassTeardown() => Task.CompletedTask;
                [Before(HookType.Test)] public void TestSetup(CancellationToken cancellationToken) { }
                [After(HookType.Test)] public void TestTeardown(TestContext context) { }
                [Before(HookType.Assembly)] public static void UnsupportedScope() { }
                [Before(HookType.Test)] public void UnsupportedParameter(string value) { }
                [After(HookType.Test)] public int UnsupportedReturn() => 0;
            }
            """, "Case");

        IClosedWorldLifecycleRoslynAdapter adapter = new ClosedWorldLifecycleRoslynAdapter(
            new LifecyclePlanner(),
            new LifecycleFormatter());
        var source = adapter.Format(new LifecycleRoslynRequest(
            metadata.TypeSymbol,
            "global::TestProject.Fixture"));

        await Assert.That(source).Contains("GeneratedLifecycleStage.ClassSetup");
        await Assert.That(source).Contains("GeneratedLifecycleStage.ClassTeardown");
        await Assert.That(source).Contains("cancellationToken");
        await Assert.That(source).Contains("TestContext.Current");
        await Assert.That(source).DoesNotContain("UnsupportedScope");
        await Assert.That(source).DoesNotContain("UnsupportedParameter");
        await Assert.That(source).DoesNotContain("UnsupportedReturn");
    }

    [Test]
    public async Task SourceEmittersProduceRootAndPerClassContracts()
    {
        IPerClassSourceEmitter perClass = new PerClassSourceEmitter();
        var perClassSource = perClass.Emit(new PerClassSourceRequest("FixtureSource", "global::TUnit.Core.GeneratedLifecycle.Empty", ImmutableArray.Create("cases.Add(1);")));
        await Assert.That(perClassSource).Contains("class FixtureSource");
        await Assert.That(perClassSource).Contains("cases.Add(1);");

        IMethodSourceEmitter method = new MethodSourceEmitter();
        var methodSource = method.Emit(new MethodSourceRequest("MethodSource", ImmutableArray.Create(new MethodSourceGroup("global::TUnit.Core.GeneratedLifecycle.Empty", ImmutableArray.Create("cases.Add(2);")))));
        await Assert.That(methodSource).Contains("__Lifecycle_0");
        await Assert.That(methodSource).Contains("cases.Add(2);");

        ICatalogCollectionEmitter collection = new CatalogCollectionEmitter();
        await Assert.That(() => collection.Emit(null!)).Throws<ArgumentNullException>();
        var grouped = collection.Emit(new CatalogCollectionRequest(
            ImmutableArray.Create(new CatalogCollectionGroup(0, ImmutableArray.Create("cases.Add(3);")))));
        await Assert.That(grouped).Contains("GetGeneratedCases_0");
        await Assert.That(grouped).Contains("cases.AddRange(GetGeneratedCases_0())");
        var flat = collection.Emit(new CatalogCollectionRequest(
            ImmutableArray.Create(new CatalogCollectionGroup(0, ImmutableArray.Create("cases.Add(4);"))),
            IncludeGroupMethods: false));
        await Assert.That(flat).DoesNotContain("GetGeneratedCases_0");
        await Assert.That(flat).Contains("cases.Add(4);");

        IEntryPointSourceEmitter root = new EntryPointSourceEmitter();
        await Assert.That(() => root.Emit(null!)).Throws<ArgumentNullException>();
        var rootSource = root.Emit(new EntryPointSourceRequest(ImmutableArray.Create("FixtureSource", "MethodSource")));
        await Assert.That(rootSource).Contains("cases.AddRange(FixtureSource.GetGeneratedCases())");
        await Assert.That(rootSource).Contains("GetCatalog");

        await Assert.That(() => perClass.Emit(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => method.Emit(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task CompositionProvidesIndependentActorAbstractions()
    {
        await Assert.That(ClosedWorldCatalogComposition.CreateInstanceCreationEmitter()).IsTypeOf<IInstanceCreationEmitter>();
        await Assert.That(ClosedWorldCatalogComposition.CreateRowPlanner()).IsTypeOf<ICatalogRowPlanner>();
        await Assert.That(ClosedWorldCatalogComposition.CreateLifecyclePlanner()).IsTypeOf<ILifecyclePlanner>();
        await Assert.That(ClosedWorldCatalogComposition.CreateLifecycleFormatter()).IsTypeOf<ILifecycleFormatter>();
        await Assert.That(ClosedWorldCatalogComposition.CreateInvocationEmitter()).IsTypeOf<IInvocationEmitter>();
        await Assert.That(ClosedWorldCatalogComposition.CreateCaseEmitter()).IsTypeOf<ICaseEmitter>();
        await Assert.That(ClosedWorldCatalogComposition.CreatePerClassSourceEmitter()).IsTypeOf<IPerClassSourceEmitter>();
        await Assert.That(ClosedWorldCatalogComposition.CreateCatalogCollectionEmitter()).IsTypeOf<ICatalogCollectionEmitter>();
        await Assert.That(ClosedWorldCatalogComposition.CreateMethodSourceEmitter()).IsTypeOf<IMethodSourceEmitter>();
        await Assert.That(ClosedWorldCatalogComposition.CreateEntryPointSourceEmitter()).IsTypeOf<IEntryPointSourceEmitter>();
        await Assert.That(ClosedWorldCatalogComposition.CreateCatalogRoslynAdapter()).IsTypeOf<IClosedWorldCatalogRoslynAdapter>();
        await Assert.That(ClosedWorldCatalogComposition.CreateLifecycleRoslynAdapter()).IsTypeOf<IClosedWorldLifecycleRoslynAdapter>();
        await Assert.That(ClosedWorldCatalogComposition.CreateCatalogOnlyMethodSourceEmitter()).IsTypeOf<ICatalogOnlyMethodSourceEmitter>();
        await Assert.That(() => ClosedWorldCatalogComposition.CreateCatalogRoslynAdapter().Emit(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => ClosedWorldCatalogComposition.CreateLifecycleRoslynAdapter().Format(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => new ClosedWorldCatalogRoslynAdapter(null!, ClosedWorldCatalogComposition.CreateCaseEmitter())).Throws<ArgumentNullException>();
        await Assert.That(() => new ClosedWorldCatalogRoslynAdapter(ClosedWorldCatalogComposition.CreateRowPlanner(), null!)).Throws<ArgumentNullException>();
        await Assert.That(() => new ClosedWorldLifecycleRoslynAdapter(null!, ClosedWorldCatalogComposition.CreateLifecycleFormatter())).Throws<ArgumentNullException>();
        await Assert.That(() => new ClosedWorldLifecycleRoslynAdapter(ClosedWorldCatalogComposition.CreateLifecyclePlanner(), null!)).Throws<ArgumentNullException>();
        await Assert.That(() => new CatalogOnlyMethodSourceEmitter(null!, ClosedWorldCatalogComposition.CreateLifecycleRoslynAdapter(), ClosedWorldCatalogComposition.CreateMethodSourceEmitter())).Throws<ArgumentNullException>();
        await Assert.That(() => new CatalogOnlyMethodSourceEmitter(ClosedWorldCatalogComposition.CreateCatalogRoslynAdapter(), null!, ClosedWorldCatalogComposition.CreateMethodSourceEmitter())).Throws<ArgumentNullException>();
        await Assert.That(() => new CatalogOnlyMethodSourceEmitter(ClosedWorldCatalogComposition.CreateCatalogRoslynAdapter(), ClosedWorldCatalogComposition.CreateLifecycleRoslynAdapter(), null!)).Throws<ArgumentNullException>();
    }

    private static TestMethodMetadata CreateMetadata(string source, string methodName)
    {
        var compilation = CSharpCompilation.Create(
                "ClosedWorldCatalogActorContract",
                [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithReferences(ReferencesHelper.References);
        var type = compilation.GetTypeByMetadataName("TestProject.Fixture")!;
        var method = type.GetMembers(methodName).OfType<IMethodSymbol>().Single();
        var testAttribute = method.GetAttributes().Single(attribute => attribute.AttributeClass?.Name == "TestAttribute");
        return new TestMethodMetadata
        {
            MethodSymbol = method,
            TypeSymbol = type,
            FilePath = "fixture.cs",
            LineNumber = 1,
            StartColumnNumber = 1,
            EndLineNumber = 1,
            EndColumnNumber = 1,
            TestAttribute = testAttribute,
            CompilationContext = new CompilationContext(
                compilation,
                new AttributeWriter(compilation),
                new WellKnownTypes(compilation)),
            MethodSyntax = null,
            MethodAttributes = method.GetAttributes(),
        };
    }
}
