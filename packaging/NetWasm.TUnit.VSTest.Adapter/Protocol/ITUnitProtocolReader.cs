namespace NetWasm.TUnit.VSTest.Adapter.Protocol;

internal interface ITUnitProtocolReader
{
    TUnitProtocolSession Read(string output);
}
