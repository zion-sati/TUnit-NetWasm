using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using NetWasm.TUnit.VSTest.Adapter.Protocol;

namespace NetWasm.TUnit.VSTest.Adapter.Mapping;

internal interface ITUnitTestResultMapper
{
    TestResult Create(TestCase testCase, TUnitCompletedCase completed);
}
