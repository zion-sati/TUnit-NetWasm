namespace TUnit.Core;

/// <summary>
/// Compile-time parallel constraint carried by a generated closed-world test
/// case: its [NotInParallel] and [ParallelGroup] settings. A sequential runner
/// satisfies every constraint and uses them only to keep upstream TUnit's
/// execution phases and Order.
/// </summary>
public sealed class GeneratedParallelConstraint
{
    public GeneratedParallelConstraint(
        bool notInParallel,
        IEnumerable<string> constraintKeys,
        int notInParallelOrder,
        string? parallelGroup,
        int parallelGroupOrder)
    {
        if (constraintKeys is null)
        {
            throw new ArgumentNullException(nameof(constraintKeys));
        }

        var keys = new List<string>(constraintKeys);
        if (!notInParallel && keys.Count != 0)
        {
            throw new ArgumentException("Constraint keys require NotInParallel.", nameof(constraintKeys));
        }

        if (parallelGroup is { Length: 0 })
        {
            throw new ArgumentException("A parallel group needs a name.", nameof(parallelGroup));
        }

        NotInParallel = notInParallel;
        ConstraintKeys = keys.AsReadOnly();
        NotInParallelOrder = notInParallelOrder;
        ParallelGroup = parallelGroup;
        ParallelGroupOrder = parallelGroupOrder;
    }

    public bool NotInParallel { get; }

    public IReadOnlyList<string> ConstraintKeys { get; }

    public int NotInParallelOrder { get; }

    public string? ParallelGroup { get; }

    public int ParallelGroupOrder { get; }
}
