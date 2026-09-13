using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace NetWasm.TUnit.VSTest.Adapter.Discovery;

internal interface ITestDiscoveryApplication
{
    void Discover(
        IEnumerable<string> sources,
        IMessageLogger logger,
        ITestCaseDiscoverySink discoverySink);
}
