namespace TUnit.Core;

/// <summary>
/// Compile-time retry policy carried by a generated closed-world test case.
/// </summary>
public sealed class GeneratedRetryPolicy
{
    private const double MaxSupportedDelayMilliseconds = uint.MaxValue - 1d;

    public static GeneratedRetryPolicy None { get; } = new(0);

    public GeneratedRetryPolicy(
        int maxRetries,
        int backoffMilliseconds = 0,
        double backoffMultiplier = 2.0,
        Func<Exception, bool>? shouldRetry = null)
    {
        if (maxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries));
        }

        if (backoffMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(backoffMilliseconds));
        }

        if (backoffMultiplier < 0 || double.IsNaN(backoffMultiplier) || double.IsInfinity(backoffMultiplier))
        {
            throw new ArgumentOutOfRangeException(nameof(backoffMultiplier));
        }

        if (maxRetries > 0 && backoffMilliseconds > 0)
        {
            var largestDelay = backoffMilliseconds * Math.Pow(backoffMultiplier, maxRetries - 1);
            if (double.IsNaN(largestDelay) ||
                double.IsInfinity(largestDelay) ||
                largestDelay > MaxSupportedDelayMilliseconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(backoffMultiplier),
                    "The retry backoff exceeds the largest supported delay.");
            }
        }

        MaxRetries = maxRetries;
        BackoffMilliseconds = backoffMilliseconds;
        BackoffMultiplier = backoffMultiplier;
        ShouldRetry = shouldRetry;
    }

    public int MaxRetries { get; }

    public int BackoffMilliseconds { get; }

    public double BackoffMultiplier { get; }

    public Func<Exception, bool>? ShouldRetry { get; }

    public bool Accepts(Exception exception) => ShouldRetry?.Invoke(exception) ?? true;

    public TimeSpan GetDelay(int retryNumber)
    {
        if (retryNumber < 1 || retryNumber > MaxRetries)
        {
            throw new ArgumentOutOfRangeException(nameof(retryNumber));
        }

        if (BackoffMilliseconds == 0)
        {
            return TimeSpan.Zero;
        }

        var milliseconds = BackoffMilliseconds * Math.Pow(BackoffMultiplier, retryNumber - 1);
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
