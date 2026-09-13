namespace TUnit.Core;

/// <summary>
/// Describes how a generated invocation reaches completion.
/// </summary>
public enum GeneratedCompletionPolicy
{
    /// <summary>
    /// The host must await the returned <see cref="System.Threading.Tasks.ValueTask"/>.
    /// </summary>
    Await = 0,
}
