using System.Runtime.CompilerServices;
using TUnit.Assertions.Exceptions;

namespace TUnit.Assertions;

/// <summary>
/// Starts assertions that are supported by the closed-world NetWasm runner.
/// </summary>
public static class Assert
{
    public static ValueAssertion<TValue> That<TValue>(
        TValue? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
    {
        return new ValueAssertion<TValue>(value, expression);
    }

    public static void Fail(string reason)
    {
        throw new AssertionException(reason);
    }

    public static TException Throws<TException>(Action action)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new AssertionException("The action threw a different exception type.", exception);
        }

        throw new AssertionException("The action did not throw the expected exception type.");
    }
}
