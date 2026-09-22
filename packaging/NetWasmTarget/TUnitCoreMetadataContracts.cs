using System.Runtime.CompilerServices;

namespace TUnit.Core
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
    public class SkipAttribute(string reason) : Attribute
    {
        public string Reason { get; } = reason;
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    public sealed class RepeatAttribute(int times) : TUnitAttribute
    {
        public int Times { get; } = times;
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class ExplicitAttribute(
        [CallerFilePath] string callerFile = "",
        [CallerMemberName] string callerMemberName = "") : TUnitAttribute
    {
        public string For { get; } = $"{callerFile} {callerMemberName}".Trim();
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    public class ExecutionPriorityAttribute(Enums.Priority priority = Enums.Priority.Normal) : TUnitAttribute
    {
        public Enums.Priority Priority { get; } = priority;
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, Inherited = true)]
    public class NotDiscoverableAttribute : TUnitAttribute
    {
        public NotDiscoverableAttribute()
        {
        }

        public NotDiscoverableAttribute(string reason)
        {
            Reason = reason;
        }

        public string? Reason { get; }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    public class RetryAttribute(int times) : TUnitAttribute
    {
        public int Times { get; } = times;

        public int BackoffMs { get; set; }

        public double BackoffMultiplier { get; set; } = 2.0;

        public Type[]? RetryOnExceptionTypes { get; set; }
    }
}

namespace TUnit.Core.Enums
{
    public enum Priority
    {
        Low = 0,
        BelowNormal = 1,
        Normal = 2,
        AboveNormal = 3,
        High = 4,
        Critical = 5,
    }
}
