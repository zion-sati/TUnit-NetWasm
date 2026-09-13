using NetWasm.Hosting.Execution;
using NetWasm.TUnit.VSTest.Adapter.Discovery;
using NetWasm.TUnit.VSTest.Adapter.Execution;
using NetWasm.TUnit.VSTest.Adapter.Mapping;
using NetWasm.TUnit.VSTest.Adapter.Protocol;

namespace NetWasm.TUnit.VSTest.Adapter;

internal static class TUnitNetWasmAdapterComposition
{
    public static ITestDiscoveryApplication CreateDiscoveryApplication()
    {
        var testCases = new TUnitTestCaseMapper();
        return new TUnitDiscoveryApplication(CreateGuestClient(), testCases);
    }

    public static ITestExecutionApplication CreateExecutionApplication()
    {
        var testCases = new TUnitTestCaseMapper();
        return new TUnitExecutionApplication(
            CreateGuestClient(),
            testCases,
            new TUnitTestCaseFilter(),
            new TUnitTestResultMapper());
    }

    private static ITUnitGuestClient CreateGuestClient() =>
        new NetWasmTUnitGuestClient(new NetWasmArtifactClient(), new TUnitProtocolReader());
}
