using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core.SourceGenerator.Utilities.CapabilityValidation;

namespace TUnit.Core.SourceGenerator.Tests;

internal sealed class ClosedWorldCapabilityValidatorTests
{
    [Test]
    public async Task SemanticValidatorRejectsUnsupportedDiscoveryAndExecutionAttributes()
    {
        var validator = new ClosedWorldSemanticCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using TUnit.Core;

            [assembly: Retry(1)]
            [assembly: ParallelGroup("assembly")]

            namespace TestProject;

            public sealed class Fixture
            {
                [Test]
                [Skip("not in the catalog")]
                [Explicit]
                [Timeout(100)]
                [NotInParallel]
                [ParallelGroup("method")]
                public void Case() { }
            }
            """)).ToArray();

        await Assert.That(diagnostics.Count(static diagnostic =>
                diagnostic.Message.Contains("is not supported by the closed-world catalog", StringComparison.Ordinal)))
            .IsEqualTo(7);
        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Message.Contains("SkipAttribute", StringComparison.Ordinal))).IsTrue();
        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Message.Contains("ParallelGroupAttribute", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task SemanticValidatorRejectsFutureTUnitSemanticAttributes()
    {
        var validator = new ClosedWorldSemanticCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using TUnit.Core;

            namespace TestProject;

            public sealed class Fixture
            {
                [Test]
                [FutureSemantic]
                public void Case() { }
            }

            [global::System.AttributeUsage(global::System.AttributeTargets.Method)]
            public sealed class FutureSemanticAttribute : global::TUnit.Core.TUnitAttribute;
            """)).ToArray();

        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(diagnostics[0].Message).Contains("FutureSemanticAttribute");
    }

    [Test]
    public async Task SemanticValidatorAcceptsRepresentedCatalogSemantics()
    {
        IClosedWorldCapabilityValidator validator = new ClosedWorldSemanticCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using TUnit.Core;

            [assembly: Category("assembly")]
            [assembly: Property("key", "value")]

            namespace TestProject;

            [Category("class")]
            public sealed class Fixture
            {
                [Test]
                [Arguments(7, DisplayName = "row")]
                [DisplayName("method $value")]
                [Category("method")]
                [Property("key", "value")]
                [DependsOn("Other")]
                public void Case(int value) { }

                [Before(HookType.Test)]
                public void Setup() { }

                [Test]
                public void Other() { }
            }
            """)).ToArray();

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task SemanticValidatorRejectsUnsupportedArgumentAndDependencyShapes()
    {
        IClosedWorldCapabilityValidator validator = new ClosedWorldSemanticCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using TUnit.Core;

            namespace TestProject;

            public sealed class Fixture
            {
                [Test]
                [Arguments(1, Skip = "unsupported", Categories = new[] { "row" }, SkipIfEmpty = true)]
                public void ArgumentMetadata(int value) { }

                [Test]
                [Arguments(2)]
                public void ParameterData([Arguments(3)] int value) { }

                [Test]
                [DependsOn("Other", new[] { typeof(string) })]
                public void OverloadDependency() { }

                [Test]
                [DependsOn(typeof(Fixture), "Other")]
                public void ClassDependency() { }

                [Test]
                [DependsOn("Other", ProceedOnFailure = true)]
                public void ProceedDependency() { }

                [Test]
                [DependsOn<Fixture>("Other")]
                public void GenericDependency() { }

                [Test]
                [DependsOn("Other")]
                public void SupportedDependency() { }

                [Test]
                public void Other() { }
            }
            """)).ToArray();

        await Assert.That(diagnostics.Count(static diagnostic =>
                diagnostic.Message.Contains("not supported by the closed-world catalog", StringComparison.Ordinal)))
            .IsEqualTo(8)
            .Because(string.Join(Environment.NewLine, diagnostics.Select(static diagnostic => diagnostic.Message)));
        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Message.Contains("Arguments row Skip", StringComparison.Ordinal))).IsTrue();
        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Message.Contains("Arguments row Categories", StringComparison.Ordinal))).IsTrue();
        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Message.Contains("Arguments row SkipIfEmpty", StringComparison.Ordinal))).IsTrue();
        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Message.Contains("Parameter data source", StringComparison.Ordinal))).IsTrue();
        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Message.Contains("DependsOn class identities", StringComparison.Ordinal))).IsTrue();
        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Message.Contains("ProceedOnFailure=true", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task MethodValidatorRejectsUnsupportedTestReturn()
    {
        var validator = new ClosedWorldMethodCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using TUnit.Core;
            namespace TestProject;
            public sealed class Fixture
            {
                [Test]
                public CustomAwaitable Case() => new();
            }
            public sealed class CustomAwaitable { }
            """)).ToArray();

        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Message.Contains("must return void, Task, or ValueTask", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task MethodValidatorAcceptsEachSupportedAwaitableShape()
    {
        var validator = new ClosedWorldMethodCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using System.Threading.Tasks;
            using TUnit.Core;
            namespace TestProject;
            public sealed class Fixture
            {
                [Test] public Task PlainTask() => Task.CompletedTask;
                [Test] public Task<int> GenericTask() => Task.FromResult(1);
                [Test] public ValueTask PlainValueTask() => ValueTask.CompletedTask;
                [Test] public ValueTask<int> GenericValueTask() => ValueTask.FromResult(1);
                [Test] public void PlainVoid() { }
            }
            """)).ToArray();

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task LifecycleValidatorRejectsUnsupportedScope()
    {
        var validator = new ClosedWorldLifecycleCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using TUnit.Core;
            namespace TestProject;
            public sealed class Fixture
            {
                [Before(HookType.Assembly)]
                public static void BeforeAssembly() { }

                [Before(HookType.Class)]
                public void InstanceClassHook() { }
            }
            """)).ToArray();

        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Message.Contains("Assembly, test-session, and discovery hooks", StringComparison.Ordinal))).IsTrue();
        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Message.Contains("class lifecycle hooks must be static", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task LifecycleValidatorChecksEverySupportedHookShape()
    {
        var validator = new ClosedWorldLifecycleCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using System.Threading.Tasks;
            using TUnit.Core;
            namespace TestProject;
            public sealed class Fixture
            {
                [Before(HookType.TestSession)] public static void SessionHook() { }
                [After(HookType.TestDiscovery)] public static void DiscoveryHook() { }
                [Before(HookType.Class)] public static void StaticClassHook() { }
                [After(HookType.Class)] public static Task ClassTaskHook() => Task.CompletedTask;
                [Before(HookType.Class)] public static ValueTask ClassValueTaskHook() => ValueTask.CompletedTask;
                [Before(HookType.Test)] public void TestContextHook(TestContext context) { }
                [After(HookType.Test)] public void ClassContextHook(ClassHookContext context) { }
                [Before(HookType.Test)] public int UnsupportedReturn() => 1;
                [BeforeEvery(HookType.Test)] public static void GlobalHook() { }
            }
            """)).ToArray();

        await Assert.That(diagnostics.Count(static diagnostic =>
                diagnostic.Message.Contains("Assembly, test-session, and discovery hooks", StringComparison.Ordinal)))
            .IsEqualTo(2);
        await Assert.That(diagnostics.Count(static diagnostic =>
                diagnostic.Message.Contains("TestContext or ClassHookContext", StringComparison.Ordinal)))
            .IsEqualTo(2);
        await Assert.That(diagnostics.Count(static diagnostic =>
                diagnostic.Message.Contains("must return void, Task, or ValueTask", StringComparison.Ordinal)))
            .IsEqualTo(1);
        await Assert.That(diagnostics.Count(static diagnostic =>
                diagnostic.Message.Contains("Global lifecycle hooks", StringComparison.Ordinal)))
            .IsEqualTo(1);
    }

    [Test]
    public async Task LifecycleValidatorRejectsUnsupportedHookParameters()
    {
        IClosedWorldCapabilityValidator validator = new ClosedWorldLifecycleCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using TUnit.Core;
            namespace TestProject;
            public sealed class Fixture
            {
                [Before(HookType.Test)]
                public void Dependency(string value) { }
            }
            """)).ToArray();

        await Assert.That(diagnostics.Count(static diagnostic =>
                diagnostic.Message.Contains("may only inject CancellationToken", StringComparison.Ordinal)))
            .IsEqualTo(1);
    }

    [Test]
    public async Task LifecycleValidatorAllowsUnknownNamedHookScopes()
    {
        IClosedWorldCapabilityValidator validator = new ClosedWorldLifecycleCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            namespace TestProject;
            public sealed class Fixture
            {
                [Before("custom")]
                public void Hook() { }
            }

            [global::System.AttributeUsage(global::System.AttributeTargets.Method)]
            public sealed class BeforeAttribute(string scope) : global::System.Attribute;
            """)).ToArray();

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task DataValidatorRejectsPropertyInjection()
    {
        var validator = new ClosedWorldDataCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using TUnit.Core;
            namespace TestProject;
            public sealed class Fixture
            {
                [Arguments(1)]
                public int Value { get; set; }

                [Arguments(2)]
                public static int StaticValue { get; set; }
            }
            """)).ToArray();

        await Assert.That(diagnostics.Count(static diagnostic => diagnostic.Message.Contains("Property data injection", StringComparison.Ordinal))).IsEqualTo(2);
    }

    [Test]
    public async Task DataValidatorRejectsInvalidArgumentArityAndAllowsOptionalAndParamsRows()
    {
        var validator = new ClosedWorldDataCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using TUnit.Core;
            namespace TestProject;
            public sealed class Fixture
            {
                [Test]
                [Arguments()]
                public void Missing(int value) { }

                [Test]
                public void MissingWithoutRow(int value) { }

                [Test]
                [global::TUnit.Core.Arguments()]
                public void MissingCancellation(global::System.Threading.CancellationToken cancellationToken, int value) { }

                [Test]
                [Arguments(1, 2)]
                public void Extra(int value) { }

                [Test]
                [Arguments()]
                public void Optional(int value = 0) { }

                [Test]
                [Arguments(1, 2)]
                public void Params(params int[] values) { }

                [Test]
                [Arguments(1, "bad")]
                public void InvalidTrailing(params int[] values) { }

                [Test]
                [global::TUnit.Core.Arguments(new int[] { 1, 2 })]
                public void ArrayPayload(int[] values) { }
            }
            """)).ToArray();

        await Assert.That(diagnostics.Count(static diagnostic =>
                diagnostic.Message.Contains("Arguments row has", StringComparison.Ordinal)))
            .IsEqualTo(4);
        await Assert.That(diagnostics.Any(static diagnostic =>
                diagnostic.Message.Contains("requires at least 1", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(diagnostics.Any(static diagnostic =>
                diagnostic.Message.Contains("accepts at most 1", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(diagnostics.Count(static diagnostic =>
                diagnostic.Message.Contains("Argument conversion", StringComparison.Ordinal)))
            .IsEqualTo(1);
    }

    [Test]
    public async Task DataValidatorChecksConstructorArgumentArity()
    {
        var validator = new ClosedWorldDataCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using TUnit.Core;
            namespace TestProject;

            [Arguments()]
            public sealed class MissingConstructorValue(int value)
            {
                [Test]
                public void Case() { }
            }

            public sealed class MissingConstructorWithoutRow(int value)
            {
                [Test]
                public void Case() { }
            }

            [Arguments("ready")]
            public sealed class ValidConstructorValue(string value)
            {
                [Test]
                public void Case() { }
            }
            """)).ToArray();

        await Assert.That(diagnostics.Any(static diagnostic =>
                diagnostic.Message.Contains("test class constructor requires at least 1", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(diagnostics.Count(static diagnostic =>
                diagnostic.Message.Contains("test class constructor", StringComparison.Ordinal)))
            .IsEqualTo(2);
    }

    [Test]
    public async Task DataValidatorChecksNullNullableScalarAndConstructorSelectionCases()
    {
        var validator = new ClosedWorldDataCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using TUnit.Core;
            namespace TestProject;
            public sealed class Fixture
            {
                [Test] [global::TUnit.Core.Arguments(null)] public void NullableNull(int? value) { }
                [Test] [global::TUnit.Core.Arguments(null)] public void ReferenceNull(string value) { }
                [Test] [global::TUnit.Core.Arguments(null)] public void InvalidNull(int value) { }
                [Test] [global::TUnit.Core.Arguments(1)] public void NullableValue(int? value) { }
                [Test] [global::TUnit.Core.Arguments(1)] public void GenericValue<T>(T value) { }
                [Test] [global::TUnit.Core.Arguments()] public void EmptyValues() { }
                [Test] [global::TUnit.Core.Arguments(1)] public void MissingSecond(int first, int second) { }
                [Test] [global::TUnit.Core.Arguments(1)] public void MissingCancellation(global::System.Threading.CancellationToken cancellationToken, int value) { }
                [Test] [Arguments(1, 2)] public void MultipleConstructorArguments(int first, int second) { }
            }

            [global::TUnit.Core.Arguments(1)]
            public sealed class SelectedConstructor(int value)
            {
                [Test] public void Case() { }

                [TestConstructor]
                public SelectedConstructor(string value) : this(value.Length) { }
            }

            [global::System.AttributeUsage(global::System.AttributeTargets.Method)]
            public sealed class ArgumentsAttribute(int first, int second) : global::System.Attribute;
            """)).ToArray();

        await Assert.That(diagnostics.Any(static diagnostic =>
                diagnostic.Message.Contains("Argument conversion", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(diagnostics.Any(static diagnostic =>
                diagnostic.Message.Contains("requires at least 2", StringComparison.Ordinal)))
            .IsTrue();
    }

    [Test]
    public async Task DataValidatorAcceptsTrailingCancellationTokensAndGenericScalarArguments()
    {
        IClosedWorldCapabilityValidator validator = new ClosedWorldDataCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using System.Threading;
            using TUnit.Core;
            namespace TestProject;
            public sealed class Fixture
            {
                [Test]
                public void CancellationOnly(CancellationToken cancellationToken) { }

                [Test]
                [Arguments<int>(1)]
                public void ScalarWithCancellation(int value, CancellationToken cancellationToken) { }
            }
            """)).ToArray();

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task ActivationValidatorRejectsRuntimeClassData()
    {
        var validator = new ClosedWorldActivationCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using TUnit.Core;
            namespace TestProject;
            [ClassDataSource(typeof(Data))]
            public sealed class Fixture { }
            [Arguments()]
            public sealed class ArgumentsFixture { }
            [ClassConstructor(typeof(object))]
            public sealed class ConstructorFixture { }
            [ClassConstructorSource]
            public sealed class SourceConstructorFixture { }
            [global::System.AttributeUsage(global::System.AttributeTargets.Class)]
            public sealed class ClassConstructorSourceAttribute : global::System.Attribute { }
            public sealed class Data { }
            """)).ToArray();

        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Message.Contains("Runtime class data source", StringComparison.Ordinal))).IsTrue();
        await Assert.That(diagnostics.Count(static diagnostic => diagnostic.Message.Contains("ClassConstructor runtime activation", StringComparison.Ordinal))).IsEqualTo(2);
    }

    [Test]
    public async Task AssemblyValidatorRejectsRuntimeConstruction()
    {
        var validator = new ClosedWorldAssemblyCapabilityValidator();
        var diagnostics = validator.Validate(CreateCompilation("""
            using TUnit.Core;
            [assembly: ClassConstructor(typeof(object))]
            [assembly: ClassConstructorSource]
            namespace TestProject;
            public sealed class Fixture { }
            [global::System.AttributeUsage(global::System.AttributeTargets.Assembly)]
            public sealed class ClassConstructorSourceAttribute : global::System.Attribute { }
            """)).ToArray();

        await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Message.Contains("Assembly-level runtime class construction", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task CompositeValidatorRejectsNullValidatorList()
    {
        await Assert.That(() => new CompositeClosedWorldCapabilityValidator(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task CompositeValidatorAcceptsAValidCompilation()
    {
        var validator = new CompositeClosedWorldCapabilityValidator(
        [
            new ClosedWorldMethodCapabilityValidator(),
            new ClosedWorldLifecycleCapabilityValidator(),
            new ClosedWorldDataCapabilityValidator(),
            new ClosedWorldActivationCapabilityValidator(),
            new ClosedWorldAssemblyCapabilityValidator(),
        ]);
        var diagnostics = validator.Validate(CreateCompilation("""
            using TUnit.Core;
            namespace TestProject;
            public sealed class Fixture
            {
                [Test]
                public void Case() { }
            }
            """)).ToArray();

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task CompositeValidatorPreservesEachCapabilityContract()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using TUnit.Core;
            using TUnit.Core.Interfaces;

            [assembly: ClassConstructor(typeof(object))]

            namespace TestProject;

            public sealed class UnsupportedFixture : IAsyncInitializer
            {
                [Test]
                [MethodDataSource(nameof(Data))]
                public string RuntimeData(int value) => value.ToString();

                [Before(HookType.Test)]
                public void ContextHook(TestContext context) { }

                [BeforeEvery(HookType.Test)]
                public static void GlobalHook() { }

                [DynamicTestBuilder]
                public void DynamicBuilder() { }

                [MethodDataSource(nameof(Data))]
                public int Injected { get; set; }

                public static IEnumerable<int> Data() => new[] { 1 };

                public Task InitializeAsync() => Task.CompletedTask;
            }

            [ClassDataSource(typeof(RuntimeClassData))]
            public sealed class RuntimeClassDataFixture { }

            public sealed class RuntimeClassData { }
            """;

        var compilation = CSharpCompilation.Create(
                "ClosedWorldCapabilityValidatorContract",
                [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithReferences(ReferencesHelper.References);

        IClosedWorldCapabilityValidator[] validators =
        [
            new ClosedWorldMethodCapabilityValidator(),
            new ClosedWorldLifecycleCapabilityValidator(),
            new ClosedWorldDataCapabilityValidator(),
            new ClosedWorldActivationCapabilityValidator(),
            new ClosedWorldAssemblyCapabilityValidator(),
        ];
        var composite = new CompositeClosedWorldCapabilityValidator(validators);
        var expected = validators.SelectMany(validator => validator.Validate(compilation)).ToArray();
        var actual = composite.Validate(compilation).ToArray();

        await Assert.That(actual.Select(static diagnostic => diagnostic.Message)
                .SequenceEqual(expected.Select(static diagnostic => diagnostic.Message)))
            .IsTrue();
        await Assert.That(actual.Select(static diagnostic => diagnostic.Message)
                .Distinct(StringComparer.Ordinal).Count())
            .IsGreaterThan(4);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        return CSharpCompilation.Create(
                "ClosedWorldCapabilityValidatorLeafContract",
                [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithReferences(ReferencesHelper.References);
    }
}
