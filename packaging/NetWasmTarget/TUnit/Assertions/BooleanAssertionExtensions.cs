namespace TUnit.Assertions.Extensions;

public static class BooleanAssertionExtensions
{
    public static ValueAssertionResult<bool> IsTrue(this ValueAssertion<bool> assertion)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return assertion.Check(assertion.Value, "to be true");
    }

    public static ValueAssertionResult<bool> IsFalse(this ValueAssertion<bool> assertion)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        return assertion.Check(!assertion.Value, "to be false");
    }
}
