using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

namespace NetWasm.TUnit.Runner.Hosting;

/// <summary>
/// The Microsoft.Testing.Platform services TUnit's test framework reads, supplied in-process: the NetWasm host
/// replaces the platform's test application. TUnit runs its source-generated tests (NetWasm has no reflection mode),
/// its warnings and the platform's display output go to the diagnostic writer, and results go through the protocol.
/// </summary>
public sealed class PlatformServices : IServiceProvider
{
    private const string ExecutionModeOption = "tunit-execution-mode";

    private readonly HostLoggerFactory _loggers;
    private readonly HostOutputDevice _output;
    private readonly HostCommandLineOptions _commandLine = new();
    private readonly HostConfiguration _configuration = new();

    public PlatformServices(TextWriter diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _loggers = new HostLoggerFactory(diagnostics);
        _output = new HostOutputDevice(diagnostics);
    }

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceType == typeof(ILoggerFactory))
        {
            return _loggers;
        }

        if (serviceType == typeof(IOutputDevice))
        {
            return _output;
        }

        if (serviceType == typeof(ICommandLineOptions))
        {
            return _commandLine;
        }

        return serviceType == typeof(IConfiguration) ? _configuration : null;
    }

    private sealed class HostLoggerFactory(TextWriter diagnostics) : ILoggerFactory, ILogger
    {
        public ILogger CreateLogger(string categoryName) => this;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                diagnostics.WriteLine(formatter(state, exception));
            }
        }

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Log(logLevel, state, exception, formatter);
            return Task.CompletedTask;
        }
    }

    private sealed class HostOutputDevice(TextWriter diagnostics) : IOutputDevice
    {
        public Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
        {
            if (data is TextOutputDeviceData text)
            {
                diagnostics.WriteLine(text.Text);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class HostCommandLineOptions : ICommandLineOptions
    {
        public bool IsOptionSet(string optionName) => optionName == ExecutionModeOption;

        public bool TryGetOptionArgumentList(string optionName, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string[]? arguments)
        {
            arguments = optionName == ExecutionModeOption ? ["sourcegeneration"] : null;
            return arguments is not null;
        }
    }

    private sealed class HostConfiguration : IConfiguration
    {
        public string? this[string key] => key switch
        {
            "platformOptions:resultDirectory" => "TestResults",
            "platformOptions:currentWorkingDirectory" or "platformOptions:testHostWorkingDirectory" => ".",
            _ => null,
        };
    }
}
