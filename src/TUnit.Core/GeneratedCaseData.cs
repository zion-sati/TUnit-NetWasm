namespace TUnit.Core;

/// <summary>
/// Lazily creates one source-generated data value for a logical test case.
/// Retries reuse the value while each attempt still receives a fresh test fixture.
/// </summary>
public sealed class GeneratedCaseData<T>
{
    private readonly Func<T> _create;
    private T? _value;
    private bool _created;
    private bool _disposed;

    public GeneratedCaseData(Func<T> create)
    {
        _create = create ?? throw new ArgumentNullException(nameof(create));
    }

    public T Get()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GeneratedCaseData<T>));
        }

        if (!_created)
        {
            _value = _create();
            _created = true;
        }

        return _value!;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_created)
        {
            return;
        }

        if (_value is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_value is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
