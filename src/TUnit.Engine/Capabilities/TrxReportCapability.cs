#if !NETWASM // NetWasm builds no TRX report, so the engine names no TRX types there.
using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace TUnit.Engine.Capabilities;

internal class TrxReportCapability : ITrxReportCapability
{
    public void Enable()
    {
        IsTrxEnabled = true;
    }

    public bool IsTrxEnabled { get; private set; }

    public bool IsSupported => true;
}
#endif
