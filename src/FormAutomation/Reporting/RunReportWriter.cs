using System.Text.Json;

namespace FormAutomation.Reporting;

/// <summary>Writes the stable Task 1 JSON summary and references to captured screenshots.</summary>
public static class RunReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Writes a complete JSON report and returns its absolute path.</summary>
    public static string Write(string artifactDirectory, string url, DateTimeOffset startedAt,
        TimeSpan duration, string browser, IReadOnlyList<FieldResult> results, IReadOnlyDictionary<string, string> screenshots)
    {
        var directory = Path.GetFullPath(artifactDirectory);
        Directory.CreateDirectory(directory);
        var passed = results.Count(result => result.Status == "Passed");
        var failed = results.Count(result => result.Status == "Failed");
        var errors = results.Count - passed - failed;
        var report = new RunReport(url, startedAt, (long)duration.TotalMilliseconds, browser,
            results.Select(result => new CheckReport(result.Field, result.Status, result.Message, result.Value,
                result.Resolved, result.Actions ?? [], result.Degraded,
                screenshots.TryGetValue(result.Field, out var screenshot) ? Path.GetRelativePath(directory, screenshot) : null)).ToArray(),
            new SummaryReport(passed, failed, errors));
        var reportPath = Path.Combine(directory, "report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions));
        return reportPath;
    }

    private sealed record RunReport(string Url, DateTimeOffset StartedAt, long DurationMs, string Browser,
        IReadOnlyList<CheckReport> Checks, SummaryReport Summary);
    private sealed record CheckReport(string Field, string Status, string Message, string Value,
        ResolvedField? Resolved, IReadOnlyList<string> Actions, bool Degraded, string? Screenshot);
    private sealed record SummaryReport(int Passed, int Failed, int Errors);
}
