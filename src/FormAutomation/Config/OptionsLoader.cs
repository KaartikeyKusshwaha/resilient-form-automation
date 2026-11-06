using Microsoft.Extensions.Configuration;

namespace FormAutomation.Config;

/// <summary>Loads options using command line, environment, JSON, then built-in defaults.</summary>
public static class OptionsLoader
{
    private static readonly Dictionary<string, string> SwitchMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["--url"] = "Url",
        ["--timeout"] = "TimeoutSeconds",
        ["--poll-ms"] = "PollMilliseconds",
        ["--first-name"] = "FirstName",
        ["--gender"] = "Gender",
        ["--state"] = "State",
        ["--country"] = "Country",
        ["--thorough"] = "Thorough",
        ["--artifacts"] = "ArtifactsDir",
        ["--dump-html"] = "DumpHtmlPath",
        ["--verbose"] = "Verbose",
        ["--no-sandbox"] = "NoSandbox",
        ["--headless"] = "Headless",
    };

    private static readonly HashSet<string> BooleanSwitches = new(StringComparer.OrdinalIgnoreCase)
    {
        "--headless", "--headed", "--thorough", "--verbose", "--no-sandbox"
    };

    private static readonly HashSet<string> ValueSwitches = new(StringComparer.OrdinalIgnoreCase)
    {
        "--url", "--timeout", "--poll-ms", "--first-name", "--gender", "--state",
        "--country", "--artifacts", "--dump-html"
    };

    /// <summary>Loads options from appsettings.json, FORM_* environment variables, and CLI flags.</summary>
    public static RunnerOptions Load(string[] args, string? contentRoot = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        var normalizedArgs = NormalizeArgs(args);
        var root = contentRoot ?? Directory.GetCurrentDirectory();
        var config = new ConfigurationBuilder()
            .SetBasePath(root)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("FORM_")
            .AddCommandLine(normalizedArgs, SwitchMappings)
            .Build();

        var rawUrl = First(config, "Url", "URL");
        if (string.IsNullOrWhiteSpace(rawUrl))
            throw new OptionsException("No form URL configured. Pass --url, set FORM_URL, or set Url in appsettings.json.");

        var url = NormalizeUrl(rawUrl);
        var timeout = ParseInt(First(config, "TimeoutSeconds", "TIMEOUT_SECONDS"), 15, "timeout", 1, 120);
        var poll = ParseInt(First(config, "PollMilliseconds", "POLL_MILLISECONDS"), 250, "poll interval", 10, 5000);

        return new RunnerOptions
        {
            Url = url,
            Headless = ParseBool(First(config, "Headless"), true, "headless"),
            TimeoutSeconds = timeout,
            PollMilliseconds = poll,
            FirstName = First(config, "FirstName", "FIRST_NAME") ?? "Alex",
            Gender = First(config, "Gender") ?? "Male",
            State = EmptyToNull(First(config, "State")),
            Country = EmptyToNull(First(config, "Country")),
            Thorough = ParseBool(First(config, "Thorough"), false, "thorough"),
            ArtifactsDir = First(config, "ArtifactsDir", "ARTIFACTS_DIR") ?? "artifacts",
            DumpHtmlPath = EmptyToNull(First(config, "DumpHtmlPath")),
            Verbose = ParseBool(First(config, "Verbose"), false, "verbose"),
            NoSandbox = ParseBool(First(config, "NoSandbox", "NO_SANDBOX"), false, "no-sandbox"),
        };
    }

    private static string[] NormalizeArgs(string[] args)
    {
        var result = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (token.Equals("--headed", StringComparison.OrdinalIgnoreCase))
            {
                result.Add("--headless");
                result.Add("false");
                continue;
            }
            if (BooleanSwitches.Contains(token))
            {
                result.Add(token);
                result.Add("true");
                continue;
            }
            if (!ValueSwitches.Contains(token))
                throw new OptionsException($"Unknown option '{token}'. Use --help to see available options.");
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new OptionsException($"Missing value for {token}.");
            result.Add(token);
            result.Add(args[++i]);
        }
        return result.ToArray();
    }

    private static string NormalizeUrl(string value)
    {
        var candidate = value.Trim();
        if (File.Exists(candidate)) candidate = new Uri(Path.GetFullPath(candidate)).AbsoluteUri;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            !(uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
              uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
              uri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)))
            throw new OptionsException("Invalid URL. Use an absolute http(s):// URL or an existing file path/file:// URL.");
        return uri.AbsoluteUri;
    }

    private static string? First(IConfiguration config, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = config[key];
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return null;
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool ParseBool(string? value, bool fallback, string name) => value is null
        ? fallback
        : bool.TryParse(value, out var parsed)
            ? parsed
            : throw new OptionsException($"Invalid {name} value '{value}'. Use true or false.");

    private static int ParseInt(string? value, int fallback, string name, int minimum, int maximum)
    {
        if (value is null) return fallback;
        if (!int.TryParse(value, out var parsed) || parsed < minimum || parsed > maximum)
            throw new OptionsException($"Invalid {name}. Use a whole number from {minimum} to {maximum}.");
        return parsed;
    }
}

/// <summary>Represents a user-correctable command-line or configuration error.</summary>
public sealed class OptionsException(string message) : Exception(message);
