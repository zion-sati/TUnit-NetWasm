using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using NetWasm.TUnit.VSTest.Adapter.Discovery;

namespace NetWasm.TUnit.VSTest.Adapter;

[FileExtension(".dll")]
[DefaultExecutorUri(TUnitNetWasmAdapterConstants.ExecutorUri)]
public sealed class TUnitNetWasmTestDiscoverer : ITestDiscoverer
{
    private readonly ITestDiscoveryApplication _application;

    public TUnitNetWasmTestDiscoverer()
        : this(TUnitNetWasmAdapterComposition.CreateDiscoveryApplication())
    {
    }

    internal TUnitNetWasmTestDiscoverer(ITestDiscoveryApplication application)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
    }

    public void DiscoverTests(
        IEnumerable<string> sources,
        IDiscoveryContext discoveryContext,
        IMessageLogger logger,
        ITestCaseDiscoverySink discoverySink) =>
        _application.Discover(sources, logger, discoverySink);
}
