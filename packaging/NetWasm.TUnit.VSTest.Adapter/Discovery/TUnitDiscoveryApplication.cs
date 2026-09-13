using System.Collections.Immutable;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using NetWasm.TUnit.VSTest.Adapter.Execution;
using NetWasm.TUnit.VSTest.Adapter.Mapping;

namespace NetWasm.TUnit.VSTest.Adapter.Discovery;

internal sealed class TUnitDiscoveryApplication(
    ITUnitGuestClient guest,
    ITUnitTestCaseMapper testCases) : ITestDiscoveryApplication
{
    private readonly ITUnitGuestClient _guest = guest ?? throw new ArgumentNullException(nameof(guest));
    private readonly ITUnitTestCaseMapper _testCases = testCases ?? throw new ArgumentNullException(nameof(testCases));

    public void Discover(
        IEnumerable<string> sources,
        IMessageLogger logger,
        ITestCaseDiscoverySink discoverySink)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(discoverySink);

        foreach (var source in sources.Distinct(StringComparer.Ordinal))
        {
            try
            {
                var run = _guest.RunAsync(source, ["--list"], CancellationToken.None)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                ReportOutput(run, logger);
                var protocol = TUnitGuestRunValidator.ValidateDiscovery(run);
                foreach (var catalogCase in protocol.Catalog)
                {
                    discoverySink.SendTestCase(_testCases.Create(source, catalogCase));
                }
            }
            catch (Exception exception)
            {
                logger.SendMessage(
                    TestMessageLevel.Error,
                    $"TUnit discovery failed for '{source}': {exception.Message}");
            }
        }
    }

    internal static void ReportOutput(TUnitGuestRun run, IMessageLogger logger)
    {
        var output = run.Protocol?.UserOutput ?? run.StandardOutput;
        if (output.Length != 0)
        {
            logger.SendMessage(TestMessageLevel.Informational, output);
        }
        if (run.StandardError.Length != 0)
        {
            logger.SendMessage(TestMessageLevel.Warning, run.StandardError);
        }
    }
}
