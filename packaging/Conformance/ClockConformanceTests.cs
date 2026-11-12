using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace NetWasm.TUnit.Conformance;

// Compiled into the desktop and the netwasm0.1 package consumers.
// A test project reads both clocks, as an application does: the runner's own need for the monotonic clock must not
// take the wall clock away.
public sealed class ClockConformanceTests
{
    [Test]
    public async Task TestsReadTheWallAndMonotonicClocks()
    {
        var now = DateTime.UtcNow;
        var started = Stopwatch.GetTimestamp();

        await Assert.That(now.Year).IsGreaterThanOrEqualTo(2024);
        await Assert.That(Stopwatch.GetTimestamp()).IsGreaterThanOrEqualTo(started);
    }
}
