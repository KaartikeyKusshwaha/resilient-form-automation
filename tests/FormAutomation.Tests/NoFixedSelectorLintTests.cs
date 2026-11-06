using System.Text.RegularExpressions;

namespace FormAutomation.Tests.Lint;

public sealed class NoFixedSelectorLintTests
{
    [Test]
    public void ProductionSourceContainsNoFixedSelectorCallsOrTargetSpecificNames()
    {
        var root = FindRepositoryRoot();
        var sourceFiles = Directory.GetFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var path in sourceFiles)
        {
            var source = File.ReadAllText(path);
            Assert.That(Regex.IsMatch(source, @"\bBy\.(Id|Name|ClassName|XPath|LinkText)\s*\(", RegexOptions.CultureInvariant),
                Is.False, $"Fixed selector call in {Path.GetRelativePath(root, path)}.");
            Assert.That(source, Does.Not.Contain("AutomationPracticeForm"), $"Page-specific path in {Path.GetRelativePath(root, path)}.");
            if (Path.GetFileName(path).Equals("appsettings.json", StringComparison.OrdinalIgnoreCase)) continue;
            Assert.That(source.Contains("cloudqa", StringComparison.OrdinalIgnoreCase), Is.False,
                $"Site name in production logic: {Path.GetRelativePath(root, path)}.");
        }
    }

    [Test]
    public void ProductionResolverNeverReadsOracleMarkers()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase));
        foreach (var path in files)
        {
            var source = File.ReadAllText(path);
            Assert.That(Regex.IsMatch(source, "data-test-(target|decoy)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                Is.False, $"Test oracle leaked into production source: {Path.GetRelativePath(root, path)}.");
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "PLAN.md")) && Directory.Exists(Path.Combine(directory.FullName, "src")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not find the Task 1 repository root from the test output directory.");
    }
}
