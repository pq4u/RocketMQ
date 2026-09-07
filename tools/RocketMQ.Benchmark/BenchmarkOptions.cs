namespace RocketMQ.Benchmark;

public enum RoutingMode
{
    Direct,
    Fanout
}

public sealed record BenchmarkOptions(
    Uri Endpoint,
    string DatabasePath,
    TimeSpan Duration,
    TimeSpan Warmup,
    int Workers,
    int PayloadBytes,
    RoutingMode Routing,
    int QueueCount,
    bool DetailedTimings,
    string ResultsDirectory,
    string? ClientCertificatePath,
    string? ClientCertificateKeyPath,
    string? ClientCertificatePasswordEnvironmentVariable)
{
    public bool MutualTlsEnabled => ClientCertificatePath is not null;

    public static BenchmarkOptions Parse(string[] args)
    {
        var values = ParseValues(args);
        var endpoint = RequiredUri(values, "endpoint");
        var databasePath = Required(values, "database-path");
        if (!Path.IsPathFullyQualified(databasePath) || databasePath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            throw new ArgumentException("--database-path must be an absolute path on local storage.");
        }

        var duration = ParseDuration(values, "duration", TimeSpan.FromMinutes(15));
        var warmup = ParseDuration(values, "warmup", TimeSpan.FromSeconds(30));
        var workers = PositiveInt(values, "workers", 32);
        var payloadBytes = PositiveInt(values, "payload-bytes", 1024);
        var queueCount = PositiveInt(values, "queue-count", 1);
        var routing = ParseRouting(values.GetValueOrDefault("routing", "direct"));
        var detailedTimings = ParseBool(values, "detailed-timings", false);
        var resultsDirectory = values.GetValueOrDefault("results-dir", Path.Combine("artifacts", "benchmarks"));
        var clientCertificatePath = Optional(values, "client-certificate-path");
        var clientCertificateKeyPath = Optional(values, "client-certificate-key-path");
        var clientCertificatePasswordEnvironmentVariable = Optional(
            values,
            "client-certificate-password-env");

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentException("--duration must be greater than zero.");
        }

        if (warmup < TimeSpan.Zero)
        {
            throw new ArgumentException("--warmup must not be negative.");
        }

        if (payloadBytes > 16 * 1024 * 1024)
        {
            throw new ArgumentException("--payload-bytes must not exceed 16777216.");
        }

        if (routing == RoutingMode.Direct && queueCount != 1)
        {
            throw new ArgumentException("--queue-count must be 1 when --routing is direct.");
        }

        if (clientCertificatePath is null
            && (clientCertificateKeyPath is not null
                || clientCertificatePasswordEnvironmentVariable is not null))
        {
            throw new ArgumentException(
                "--client-certificate-path is required when a client certificate key or password environment variable is configured.");
        }

        if (clientCertificatePath is not null && endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("A client certificate can be used only with an HTTPS endpoint.");
        }

        return new BenchmarkOptions(
            endpoint,
            databasePath,
            duration,
            warmup,
            workers,
            payloadBytes,
            routing,
            queueCount,
            detailedTimings,
            resultsDirectory,
            clientCertificatePath,
            clientCertificateKeyPath,
            clientCertificatePasswordEnvironmentVariable);
    }

    public static string Usage => """
        Usage: dotnet run --project tools/RocketMQ.Benchmark -- --endpoint https://localhost:50051 --database-path D:\RocketMQData\rocketmq.db [options]

        Options:
          --duration <TimeSpan>       Measurement duration; default 00:15:00.
          --warmup <TimeSpan>         Warm-up duration; default 00:00:30.
          --workers <positive int>    Concurrent closed-loop publishers; default 32.
          --payload-bytes <int>       Payload size (1..16777216); default 1024.
          --routing direct|fanout     Routing scenario; default direct.
          --queue-count <int>         Fanout destination count; default 1.
          --detailed-timings <bool>   Include opt-in server timing breakdown; default false.
          --results-dir <path>        JSON report directory; default artifacts/benchmarks.
          --client-certificate-path <path>       Client PFX/P12 or PEM certificate for mTLS.
          --client-certificate-key-path <path>   Private key path when the certificate is PEM.
          --client-certificate-password-env <name> Environment variable containing the certificate password.
        """;

    private static Dictionary<string, string> ParseValues(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 == args.Length)
            {
                throw new ArgumentException(Usage);
            }

            var key = args[index][2..];
            if (!values.TryAdd(key, args[index + 1]))
            {
                throw new ArgumentException($"Option --{key} was specified more than once.");
            }
        }

        return values;
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"--{key} is required.\n{Usage}");

    private static string? Optional(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value)
            ? !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException($"--{key} must not be empty.")
            : null;

    private static Uri RequiredUri(IReadOnlyDictionary<string, string> values, string key)
    {
        var value = Required(values, key);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"--{key} must be an absolute HTTP(S) URI.");
        }

        return uri;
    }

    private static TimeSpan ParseDuration(IReadOnlyDictionary<string, string> values, string key, TimeSpan defaultValue)
        => values.TryGetValue(key, out var value)
            ? TimeSpan.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : throw new ArgumentException($"--{key} must be a TimeSpan, for example 00:15:00.")
            : defaultValue;

    private static int PositiveInt(IReadOnlyDictionary<string, string> values, string key, int defaultValue)
        => values.TryGetValue(key, out var value)
            ? int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed
                : throw new ArgumentException($"--{key} must be a positive integer.")
            : defaultValue;

    private static bool ParseBool(IReadOnlyDictionary<string, string> values, string key, bool defaultValue)
        => values.TryGetValue(key, out var value)
            ? bool.TryParse(value, out var parsed)
                ? parsed
                : throw new ArgumentException($"--{key} must be true or false.")
            : defaultValue;

    private static RoutingMode ParseRouting(string value)
        => value.ToLowerInvariant() switch
        {
            "direct" => RoutingMode.Direct,
            "fanout" => RoutingMode.Fanout,
            _ => throw new ArgumentException("--routing must be direct or fanout.")
        };
}

