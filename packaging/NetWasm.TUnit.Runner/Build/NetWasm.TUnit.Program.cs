// The NetWasm test program's entry point, which the package compiles into each netwasm0.1 test project. Upstream
// generates its entry point through Microsoft.Testing.Platform and its analyzer reports any other Main (TUnit0034);
// this one is the package's own.
#pragma warning disable TUnit0034
namespace NetWasm.TUnit.Generated;

internal static class NetWasmTestProgram
{
    public static async global::System.Threading.Tasks.Task<int> Main(string[] args) =>
        await global::NetWasm.TUnit.Runner.Hosting.TestApplication.RunAsync(
            args,
            global::TUnit.Engine.NetWasm.NetWasmTestFramework.Create);
}
