using System.Text.Json;
using FormAutomation;
using FormAutomation.Reporting;

namespace FormAutomation.Tests.Unit;

public sealed class ReportWriterTests
{
    [Test]
    public void ReportIncludesSummaryChecksAndRelativeScreenshotPaths()
    {
        var directory = Path.Combine(Path.GetTempPath(), "form-report-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var screenshot = Path.Combine(directory, "first-name.png");
            File.WriteAllBytes(screenshot, [1, 2, 3]);
            var result = new FieldResult("First Name", "Passed", "Read-back verified.", "Alex", 100,
                Actions: ["typed", "read-back"], Resolved: new ResolvedField("input", 0.8, ["label=100"], "top", "main", "form:1", "First Name"));
            var reportPath = RunReportWriter.Write(directory, "https://example.test", DateTimeOffset.UtcNow,
                TimeSpan.FromMilliseconds(25), "chrome 1", [result], new Dictionary<string, string> { ["First Name"] = screenshot });
            using var document = JsonDocument.Parse(File.ReadAllText(reportPath));
            var root = document.RootElement;
            Assert.Multiple((Action)(() =>
            {
                Assert.That(root.GetProperty("checks").GetArrayLength(), Is.EqualTo(1));
                Assert.That(root.GetProperty("checks")[0].GetProperty("screenshot").GetString(), Is.EqualTo("first-name.png"));
                Assert.That(root.GetProperty("summary").GetProperty("passed").GetInt32(), Is.EqualTo(1));
                Assert.That(root.GetProperty("summary").GetProperty("errors").GetInt32(), Is.Zero);
            }));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
