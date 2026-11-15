using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace NetWasm.TUnit.Conformance;

// Compiled into the desktop and the netwasm0.1 package consumers.
// [MatrixDataSource] runs every combination of its parameters' [Matrix] values except the excluded ones: on NetWasm the
// values and exclusions come from the generated metadata rather than reflection.
public sealed class MatrixConformanceTests
{
    [Test]
    [MatrixDataSource]
    [MatrixExclusion(2, "y")]
    public async Task MatrixRunsEachIncludedCombination([Matrix(1, 2)] int number, [Matrix("x", "y")] string letter)
    {
        await Assert.That(number is 1 or 2).IsTrue();
        await Assert.That(letter is "x" or "y").IsTrue();
        await Assert.That(number == 2 && letter == "y").IsFalse();
    }
}
