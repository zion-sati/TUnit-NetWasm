// The parallel constraints, with upstream TUnit's public members. netwasm0.1
// runs tests one at a time, which satisfies every constraint; the catalog
// generator records them so the runner keeps upstream's execution phases and
// orders NotInParallel and ParallelGroup tests by Order (issue #117).

namespace TUnit.Core
{
    public class SingleTUnitAttribute : TUnitAttribute
    {
        internal SingleTUnitAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    public class NotInParallelAttribute : SingleTUnitAttribute
    {
        public string[] ConstraintKeys { get; } = [];

        public int Order { get; init; } = int.MaxValue / 2;

        public NotInParallelAttribute()
        {
        }

        public NotInParallelAttribute(string constraintKey) : this([constraintKey])
        {
            if (constraintKey is null or { Length: < 1 })
            {
                throw new ArgumentException("No constraint key was provided");
            }
        }

        public NotInParallelAttribute(string[] constraintKeys)
        {
            if (constraintKeys.Length != new HashSet<string>(constraintKeys).Count)
            {
                throw new ArgumentException("Duplicate constraint keys are not allowed.");
            }

            ConstraintKeys = constraintKeys;
        }

        public Type ScopeType => typeof(NotInParallelAttribute);
    }

    public class ParallelGroupAttribute(string group) : TUnitAttribute
    {
        public int Order { get; set; }

        public string Group { get; } = group;
    }

    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class ParallelLimiterAttribute<TParallelLimit> : TUnitAttribute
        where TParallelLimit : Interfaces.IParallelLimit, new()
    {
        public int Order => 0;
    }
}

namespace TUnit.Core.Interfaces
{
    public interface IParallelLimit
    {
        int Limit { get; }
    }
}
