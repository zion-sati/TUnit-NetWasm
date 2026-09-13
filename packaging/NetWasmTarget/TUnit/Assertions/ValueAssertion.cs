namespace TUnit.Assertions;

public sealed class ValueAssertion<TValue>
{
    private readonly string? _expression;

    internal TValue? Value { get; }

    internal ValueAssertion(TValue? value, string? expression)
    {
        Value = value;
        _expression = expression;
    }

    public ValueAssertionResult<TValue> IsEqualTo(TValue? expected)
    {
        return Check(EqualityComparer<TValue>.Default.Equals(Value!, expected!), "to be equal to the expected value");
    }

    public ValueAssertionResult<TValue> IsNotEqualTo(TValue? unexpected)
    {
        return Check(!EqualityComparer<TValue>.Default.Equals(Value!, unexpected!), "not to be equal to the unexpected value");
    }

    public ValueAssertionResult<TValue> IsNull()
    {
        return Check(Value is null, "to be null");
    }

    public ValueAssertionResult<TValue> IsNotNull()
    {
        return Check(Value is not null, "not to be null");
    }

    internal ValueAssertionResult<TValue> Check(bool passed, string expectation)
    {
        return new ValueAssertionResult<TValue>(Value, _expression, expectation, passed);
    }
}
