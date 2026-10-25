using System.Collections.Generic;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;
using TUnit.Core.Interfaces;

namespace NetWasm.TUnit.Conformance;

// Compiled into the desktop and the netwasm0.1 package consumers. Method names
// sort opposite to Order, so running in name order fails these tests.
public sealed class ParallelConstraintConformanceTests
{
    private static readonly List<int> s_orders = [];

    [Test]
    [NotInParallel(Order = 3)]
    public Task AThird() => Record(3);

    [Test]
    [NotInParallel(Order = 2)]
    public Task BSecond() => Record(2);

    [Test]
    [NotInParallel(Order = 1)]
    public Task CFirst() => Record(1);

    [Test]
    [NotInParallel("conformance-key")]
    [ParallelGroup("conformance-group")]
    [ParallelLimiter<SingleTestLimit>]
    public async Task ParallelConstraintAttributesRun()
    {
        await Assert.That(new SingleTestLimit().Limit).IsEqualTo(1);
    }

    private static async Task Record(int order)
    {
        foreach (var earlier in s_orders)
        {
            await Assert.That(earlier < order).IsTrue();
        }

        s_orders.Add(order);
    }
}

public sealed class SingleTestLimit : IParallelLimit
{
    public int Limit => 1;
}
