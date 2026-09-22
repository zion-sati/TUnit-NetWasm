using System;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace NetWasm.TUnit.Package.Tests;

public sealed class PackageSmokeTests
{
    private static int _retryAttempts;

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

    [Test]
    [Timeout(1_000)]
    [Retry(2)]
    public async Task RetryAndTimeoutRunThroughTheNetWasmRunner(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _retryAttempts++;
        if (_retryAttempts < 3)
        {
            throw new InvalidOperationException("retry canary");
        }

        await Assert.That(_retryAttempts).IsEqualTo(3);
    }

    [Test]
    [MethodDataSource(nameof(Values))]
    public async Task MethodDataSourceRunsThroughTheGeneratedCatalog(int value)
    {
        await Assert.That(value % 21).IsEqualTo(0);
    }

    [Test]
    [Repeat(1)]
    [ClassDataSource<PackageCaseData>]
    public async Task ClassDataSourceRunsThroughTheGeneratedCatalog(PackageCaseData data)
    {
        await Assert.That(data.Value).IsEqualTo(42);
    }

    public static int[] Values => [21, 42];
}

public sealed class PackageCaseData
{
    public int Value => 42;
}
