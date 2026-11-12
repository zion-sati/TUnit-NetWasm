using Microsoft.Testing.Platform.Extensions;

namespace TUnit.Engine.Framework;

internal class TUnitExtension : IExtension
{
    public string Uid => nameof(TUnitExtension);

    public string DisplayName => "TUnit";

#if NETWASM
    public string Version => NetWasmBuildInfo.Version;
#else
    public string Version => typeof(TUnitExtension).Assembly.GetName().Version?.ToString() ?? "1.0.0";
#endif

    public string Description => "TUnit Framework for Microsoft Testing Platform";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
