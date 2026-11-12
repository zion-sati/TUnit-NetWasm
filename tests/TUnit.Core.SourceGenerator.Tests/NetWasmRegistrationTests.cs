using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core.SourceGenerator.CodeGenerators;
using TUnit.Core.SourceGenerator.Generators;

namespace TUnit.Core.SourceGenerator.Tests;

// NetWasm does not yet run the registration shells' initializers for RuntimeHelpers.RunClassConstructor, so a
// NetWasm compilation's module initializer also reads a static field of each shell. Other targets are unchanged.
internal sealed class NetWasmRegistrationTests
{
    private const string Source = """
        using TUnit.Core;

        namespace TestProject;

        public sealed class RegisteredFixture
        {
            [Test]
            public void Runs() { }
        }
        """;

    [Test]
    public async Task NetWasmCompilationsReadEachRegistrationShell()
    {
        var generated = Generate(new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NETWASM"]));

        foreach (var shell in Shells)
        {
            await Assert.That(generated).Contains($"_ = global::TUnit.Generated.{shell}.NetWasmInitialized;");
            await Assert.That(generated).Contains($"internal static partial class {shell} {{ internal static readonly bool NetWasmInitialized = true; }}");
        }
    }

    [Test]
    public async Task OtherCompilationsKeepUpstreamsRegistration()
    {
        var generated = Generate(new CSharpParseOptions(LanguageVersion.Preview));

        await Assert.That(generated).Contains("RunClassConstructor(typeof(global::TUnit.Generated.TUnit_TestRegistration).TypeHandle)");
        await Assert.That(generated).DoesNotContain("NetWasmInitialized");
    }

    private static readonly string[] Shells =
    [
        "TUnit_TestRegistration",
        "TUnit_HookRegistration",
        "TUnit_PropertyRegistration",
        "TUnit_ConverterRegistration",
        "TUnit_StaticPropertyRegistration",
        "TUnit_DynamicTestRegistration",
    ];

    private static string Generate(CSharpParseOptions parseOptions)
    {
        var compilation = CSharpCompilation.Create(
                "NetWasmRegistration",
                [CSharpSyntaxTree.ParseText(Source, parseOptions)],
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithReferences(ReferencesHelper.References);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new InfrastructureGenerator().AsSourceGenerator(), new TestMetadataGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var generatedCompilation, out var diagnostics);

        var errors = diagnostics
            .Concat(generatedCompilation.GetDiagnostics())
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        return string.Join(Environment.NewLine, generatedCompilation.SyntaxTrees.Select(static tree => tree.GetText().ToString()));
    }
}
