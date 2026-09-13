using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace NetWasm.TUnit.VSTest.Adapter.Protocol;

internal sealed class TUnitProtocolReader : ITUnitProtocolReader
{
    private const string Prefix = "NWTUNIT|";
    private const int SupportedVersion = 2;

    public TUnitProtocolSession Read(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var catalog = new List<CatalogBuilder>();
        var catalogById = new Dictionary<string, CatalogBuilder>(StringComparer.Ordinal);
        var started = ImmutableArray.CreateBuilder<string>();
        var completed = ImmutableArray.CreateBuilder<TUnitCompletedCase>();
        var userOutput = new StringBuilder();
        int? protocolVersion = null;
        int? hostStatus = null;
        string? hostMessage = null;

        foreach (var line in EnumerateLines(output))
        {
            if (!line.Content.StartsWith(Prefix, StringComparison.Ordinal))
            {
                userOutput.Append(line.Content);
                userOutput.Append(line.Delimiter);
                continue;
            }

            var fields = DecodeFields(line.Content[Prefix.Length..]);
            if (fields.Count == 0)
            {
                throw new InvalidDataException("The TUnit protocol record kind is missing.");
            }

            if (fields[0] == "protocol-version")
            {
                RequireArity(fields, 2);
                if (protocolVersion is not null)
                {
                    throw new InvalidDataException("The TUnit protocol version was declared more than once.");
                }

                protocolVersion = ParseInt32(fields[1], "protocol version");
                if (protocolVersion != SupportedVersion)
                {
                    throw new InvalidDataException(
                        $"TUnit protocol version {protocolVersion} is unsupported; expected {SupportedVersion}.");
                }
                continue;
            }

            if (protocolVersion is null)
            {
                throw new InvalidDataException("The TUnit protocol version must be the first machine record.");
            }

            switch (fields[0])
            {
                case "catalog":
                    RequireArity(fields, 6);
                    var stableId = RequireValue(fields[1], "catalog stable ID");
                    var entry = new CatalogBuilder(
                        stableId,
                        RequireValue(fields[2], "catalog display name"),
                        RequireValue(fields[3], "catalog fully-qualified name"),
                        RequireValue(fields[4], "catalog source path"),
                        ParseInt32(fields[5], "catalog line number"));
                    if (!catalogById.TryAdd(stableId, entry))
                    {
                        throw new InvalidDataException($"The TUnit catalog contains duplicate stable ID '{stableId}'.");
                    }
                    catalog.Add(entry);
                    break;
                case "trait":
                    RequireArity(fields, 4);
                    if (!catalogById.TryGetValue(fields[1], out var traitEntry))
                    {
                        throw new InvalidDataException(
                            $"The TUnit trait references unknown stable ID '{fields[1]}'.");
                    }
                    traitEntry.Traits.Add(new TUnitTrait(
                        RequireValue(fields[2], "trait name"),
                        fields[3]));
                    break;
                case "test-started":
                    RequireArity(fields, 3);
                    started.Add(RequireValue(fields[1], "started stable ID"));
                    break;
                case "test-completed":
                    RequireArity(fields, 5);
                    completed.Add(new TUnitCompletedCase(
                        RequireValue(fields[1], "completed stable ID"),
                        RequireValue(fields[2], "test outcome"),
                        TimeSpan.FromMilliseconds(ParseDouble(fields[3], "test duration")),
                        fields[4].Length == 0 ? null : fields[4]));
                    break;
                case "host-result":
                    RequireArity(fields, 3);
                    if (hostStatus is not null)
                    {
                        throw new InvalidDataException("The TUnit host result was emitted more than once.");
                    }
                    hostStatus = ParseInt32(fields[1], "host status");
                    hostMessage = fields[2];
                    break;
                case "run-started":
                    RequireArity(fields, 2);
                    _ = ParseInt32(fields[1], "selected test count");
                    break;
                case "run-completed":
                    RequireArity(fields, 4);
                    _ = ParseInt32(fields[1], "passed test count");
                    _ = ParseInt32(fields[2], "failed test count");
                    _ = ParseDouble(fields[3], "run duration");
                    break;
                default:
                    throw new InvalidDataException($"Unknown TUnit protocol record '{fields[0]}'.");
            }
        }

        if (protocolVersion is null)
        {
            throw new InvalidDataException("The TUnit protocol version record is missing.");
        }

        return new TUnitProtocolSession(
            catalog.Select(static entry => entry.Build()).ToImmutableArray(),
            started.ToImmutable(),
            completed.ToImmutable(),
            hostStatus,
            hostMessage,
            userOutput.ToString());
    }

    private static IReadOnlyList<string> DecodeFields(string value)
    {
        var encoded = value.Split('|');
        var decoded = new string[encoded.Length];
        for (var index = 0; index < encoded.Length; index++)
        {
            decoded[index] = Decode(encoded[index]);
        }
        return decoded;
    }

    private static string Decode(string value)
    {
        if (!value.Contains('%', StringComparison.Ordinal))
        {
            return value;
        }

        var decoded = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                decoded.Append(value[index]);
                continue;
            }

            if (index + 2 >= value.Length)
            {
                throw new InvalidDataException("The TUnit protocol contains a truncated escape sequence.");
            }

            var escape = value.AsSpan(index, 3);
            if (escape.Equals("%25", StringComparison.OrdinalIgnoreCase))
            {
                decoded.Append('%');
            }
            else if (escape.Equals("%7C", StringComparison.OrdinalIgnoreCase))
            {
                decoded.Append('|');
            }
            else if (escape.Equals("%0D", StringComparison.OrdinalIgnoreCase))
            {
                decoded.Append('\r');
            }
            else if (escape.Equals("%0A", StringComparison.OrdinalIgnoreCase))
            {
                decoded.Append('\n');
            }
            else
            {
                throw new InvalidDataException($"Unknown TUnit protocol escape '{escape.ToString()}'.");
            }
            index += 2;
        }
        return decoded.ToString();
    }

    private static IEnumerable<ProtocolLine> EnumerateLines(string value)
    {
        var start = 0;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '\n')
            {
                continue;
            }

            var hasCarriageReturn = index > start && value[index - 1] == '\r';
            var contentLength = index - start - (hasCarriageReturn ? 1 : 0);
            yield return new ProtocolLine(
                value.Substring(start, contentLength),
                hasCarriageReturn ? "\r\n" : "\n");
            start = index + 1;
        }

        if (start < value.Length)
        {
            yield return new ProtocolLine(value[start..], string.Empty);
        }
    }

    private static void RequireArity(IReadOnlyList<string> fields, int expected)
    {
        if (fields.Count != expected)
        {
            throw new InvalidDataException(
                $"TUnit protocol record '{fields[0]}' has {fields.Count - 1} fields; expected {expected - 1}.");
        }
    }

    private static string RequireValue(string value, string description) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"The TUnit {description} is empty.")
            : value;

    private static int ParseInt32(string value, string description) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException($"The TUnit {description} is invalid.");

    private static double ParseDouble(string value, string description) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            && double.IsFinite(result)
            && result >= 0
                ? result
                : throw new InvalidDataException($"The TUnit {description} is invalid.");

    private sealed class CatalogBuilder(
        string stableId,
        string displayName,
        string fullyQualifiedName,
        string filePath,
        int lineNumber)
    {
        public List<TUnitTrait> Traits { get; } = [];

        public TUnitCatalogCase Build() => new(
            stableId,
            displayName,
            fullyQualifiedName,
            filePath,
            lineNumber,
            Traits.ToImmutableArray());
    }

    private sealed record ProtocolLine(string Content, string Delimiter);
}
