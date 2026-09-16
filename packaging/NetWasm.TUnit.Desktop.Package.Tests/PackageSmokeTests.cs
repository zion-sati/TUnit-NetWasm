using TUnit.Assertions;
using TUnit.Core;

namespace NetWasm.TUnit.Desktop.Package.Tests;

public sealed class PackageSmokeTests
{
    [Test]
    public async Task PackageOnlyDesktopConsumerRuns()
    {
        await Assert.That(Environment.ProcessId).IsGreaterThan(0);
    }
}
