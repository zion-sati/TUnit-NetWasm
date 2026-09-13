using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using NetWasm.TUnit.VSTest.Adapter.Protocol;

namespace NetWasm.TUnit.VSTest.Adapter.Mapping;

internal interface ITUnitTestCaseMapper
{
    TestCase Create(string source, TUnitCatalogCase testCase);

    string GetStableId(TestCase testCase);
}
