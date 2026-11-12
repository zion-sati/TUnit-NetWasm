#if NETWASM
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using TUnit.Engine.Framework;

namespace TUnit.Engine.NetWasm;

/// <summary>
/// Creates TUnit's test framework for a NetWasm host. The host replaces Microsoft.Testing.Platform's test application
/// and supplies the platform services the framework reads: logger factory, output device, command-line options and
/// configuration.
/// </summary>
public static class NetWasmTestFramework
{
    public static ITestFramework Create(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new TUnitTestFramework(new TUnitExtension(), services, new TestFrameworkCapabilities());
    }
}
#endif
