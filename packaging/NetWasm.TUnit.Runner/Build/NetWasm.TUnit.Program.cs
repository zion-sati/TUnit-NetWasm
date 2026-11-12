namespace NetWasm.TUnit.Generated;

internal static class NetWasmTestProgram
{
    public static async global::System.Threading.Tasks.Task<int> Main(string[] args) =>
        await global::NetWasm.TUnit.Runner.Hosting.TestApplication.RunAsync(
            args,
            global::TUnit.Engine.NetWasm.NetWasmTestFramework.Create);
}
