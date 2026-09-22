namespace TUnit.Core;

/// <summary>Disposes every source-generated case value, preserving all failures.</summary>
public static class GeneratedCaseDataDisposal
{
    public static async ValueTask DisposeAllAsync(params Func<ValueTask>[] disposers)
    {
        if (disposers is null)
        {
            throw new ArgumentNullException(nameof(disposers));
        }
        List<Exception>? failures = null;
        foreach (var dispose in disposers)
        {
            try
            {
                await dispose();
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is { Count: 1 })
        {
            throw failures[0];
        }

        if (failures is { Count: > 1 })
        {
            throw new AggregateException(failures);
        }
    }
}
