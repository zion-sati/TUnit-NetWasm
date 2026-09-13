using System.Runtime.CompilerServices;
using TUnit.Assertions.Exceptions;

namespace TUnit.Assertions;

public sealed class ValueAssertionResult<TValue>
{
    private readonly TValue? _value;
    private readonly string? _expression;
    private readonly string _expectation;
    private readonly bool _passed;
    private string? _reason;

    internal ValueAssertionResult(
        TValue? value,
        string? expression,
        string expectation,
        bool passed)
    {
        _value = value;
        _expression = expression;
        _expectation = expectation;
        _passed = passed;
    }

    public ValueAssertionResult<TValue> Because(string reason)
    {
        _reason = reason;
        return this;
    }

    public Task<TValue?> AssertAsync()
    {
        if (!_passed)
        {
            var expression = string.IsNullOrEmpty(_expression) ? "the value" : _expression;
            var reason = string.IsNullOrWhiteSpace(_reason) ? string.Empty : $" because {_reason}";
            throw new AssertionException($"Expected {expression} {_expectation}{reason}.");
        }

        return Task.FromResult(_value);
    }

    public TaskAwaiter<TValue?> GetAwaiter()
    {
        return AssertAsync().GetAwaiter();
    }
}
