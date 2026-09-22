using TUnit.Core;

namespace NetWasm.TUnit.Runner.Tests;

public sealed class GeneratedRetryPolicyTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ConstructorRejectsNonFiniteBackoff(double multiplier)
    {
        XunitAssert.Throws<ArgumentOutOfRangeException>(() =>
            new GeneratedRetryPolicy(2, backoffMilliseconds: 1, backoffMultiplier: multiplier));
    }

    [Fact]
    public void ConstructorRejectsBackoffThatCannotBecomeATimeSpan()
    {
        XunitAssert.Throws<ArgumentOutOfRangeException>(() =>
            new GeneratedRetryPolicy(int.MaxValue, int.MaxValue, double.MaxValue));
    }

    [Fact]
    public void ConstructorRejectsBackoffThatTaskDelayCannotSchedule()
    {
        XunitAssert.Throws<ArgumentOutOfRangeException>(() =>
            new GeneratedRetryPolicy(2, backoffMilliseconds: 1, backoffMultiplier: 5_000_000_000));
    }

    [Fact]
    public void ConstructorAcceptsLargestBackoffThatTaskDelayCanSchedule()
    {
        var policy = new GeneratedRetryPolicy(
            2,
            backoffMilliseconds: 1,
            backoffMultiplier: uint.MaxValue - 1d);

        XunitAssert.Equal(TimeSpan.FromMilliseconds(uint.MaxValue - 1d), policy.GetDelay(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void GetDelayRejectsRetryOutsidePolicy(int retryNumber)
    {
        var policy = new GeneratedRetryPolicy(2, backoffMilliseconds: 1);

        XunitAssert.Throws<ArgumentOutOfRangeException>(() => policy.GetDelay(retryNumber));
    }
}
