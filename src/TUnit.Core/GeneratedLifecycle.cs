using System.Collections.ObjectModel;

namespace TUnit.Core;

/// <summary>
/// A lifecycle action emitted alongside a generated test entry.
/// </summary>
public sealed class GeneratedLifecycleAction
{
    public GeneratedLifecycleAction(
        GeneratedLifecycleStage stage,
        int order,
        Func<object?, CancellationToken, ValueTask> invoke)
    {
        Invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
        Stage = stage;
        Order = order;
    }

    public GeneratedLifecycleStage Stage { get; }

    public int Order { get; }

    public Func<object?, CancellationToken, ValueTask> Invoke { get; }
}

/// <summary>
/// The ordered setup and teardown actions for a generated test case.
/// </summary>
public sealed class GeneratedLifecycle
{
    public static GeneratedLifecycle Empty { get; } = new([]);

    public GeneratedLifecycle(IEnumerable<GeneratedLifecycleAction> actions)
    {
        if (actions is null)
        {
            throw new ArgumentNullException(nameof(actions));
        }

        // The source generator emits the complete hierarchy order (base-to-derived
        // setup and derived-to-base teardown). Preserve that order here: sorting only
        // by stage/order would lose the inheritance boundary needed by the runner.
        Actions = new ReadOnlyCollection<GeneratedLifecycleAction>(new List<GeneratedLifecycleAction>(actions));
    }

    public IReadOnlyList<GeneratedLifecycleAction> Actions { get; }

    public bool IsEmpty => Actions.Count == 0;
}

/// <summary>
/// The lifecycle scope represented by a generated action.
/// </summary>
public enum GeneratedLifecycleStage
{
    ClassSetup,
    TestSetup,
    TestTeardown,
    ClassTeardown,
}
