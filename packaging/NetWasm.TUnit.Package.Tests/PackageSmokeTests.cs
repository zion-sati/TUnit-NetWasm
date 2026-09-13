using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace NetWasm.TUnit.Package.Tests;

public sealed class PackageSmokeTests
{
    [Test]
    [Property("lane", "package-only")]
    public async Task PackageOnlyConsumerRunsThroughStandardDotNetTest()
    {
        var answer = 42;

        await Assert.That(answer).IsEqualTo(42);
    }

    [Test]
    [Category("filter-canary")]
    public async Task FilterCanaryPreservesTraits()
    {
        var product = string.Concat("Net", "Wasm");

        await Assert.That(product).IsEqualTo("NetWasm");
    }
}
