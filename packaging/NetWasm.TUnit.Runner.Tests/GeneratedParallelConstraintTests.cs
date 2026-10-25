using TUnit.Core;

namespace NetWasm.TUnit.Runner.Tests;

public sealed class GeneratedParallelConstraintTests
{
    [Fact]
    public void ConstructorCopiesTheConstraintKeys()
    {
        string[] keys = ["files", "network"];

        var constraint = new GeneratedParallelConstraint(true, keys, 3, "database", 4);
        keys[0] = "changed";

        XunitAssert.Equal(["files", "network"], constraint.ConstraintKeys);
        XunitAssert.True(constraint.NotInParallel);
        XunitAssert.Equal(3, constraint.NotInParallelOrder);
        XunitAssert.Equal("database", constraint.ParallelGroup);
        XunitAssert.Equal(4, constraint.ParallelGroupOrder);
    }

    [Fact]
    public void ConstructorRejectsNullKeys()
    {
        XunitAssert.Throws<ArgumentNullException>(() =>
            new GeneratedParallelConstraint(true, null!, 0, null, 0));
    }

    [Fact]
    public void ConstructorRejectsKeysWithoutNotInParallel()
    {
        XunitAssert.Throws<ArgumentException>(() =>
            new GeneratedParallelConstraint(false, ["files"], 0, "database", 0));
    }

    [Fact]
    public void ConstructorRejectsAnEmptyGroupName()
    {
        XunitAssert.Throws<ArgumentException>(() =>
            new GeneratedParallelConstraint(false, [], 0, string.Empty, 0));
    }
}
