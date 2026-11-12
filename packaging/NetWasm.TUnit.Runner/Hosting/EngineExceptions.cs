namespace NetWasm.TUnit.Runner.Hosting;

/// <summary>
/// The exception a test threw, beneath the reporting exceptions TUnit's engine wraps it in
/// (TUnit.Engine.Exceptions), so the failure classifier sees what the test raised.
/// </summary>
public static class EngineExceptions
{
    private const string EngineExceptionNamespace = "TUnit.Engine.Exceptions";

    public static Exception Unwrap(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        while (exception.InnerException is { } inner && exception.GetType().Namespace == EngineExceptionNamespace)
        {
            exception = inner;
        }

        return exception;
    }
}
