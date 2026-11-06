using System.Text.RegularExpressions;
using FormAutomation;
using FormAutomation.Config;
using FormAutomation.Reporting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

if (args.Any(x => x.Equals("--help", StringComparison.OrdinalIgnoreCase) || x.Equals("-h", StringComparison.OrdinalIgnoreCase)))
{
    PrintHelp();
    return 0;
}

RunnerOptions options;
try
{
    options = OptionsLoader.Load(args, AppContext.BaseDirectory);
}
catch (OptionsException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

ChromeDriver? driver = null;
var stage = "browser startup";
try
{
    var chromeOptions = new ChromeOptions();
    if (options.Headless) chromeOptions.AddArgument("--headless=new");
    chromeOptions.AddArgument("--window-size=1920,1080");
    chromeOptions.AddArgument("--disable-dev-shm-usage");
    if (options.NoSandbox) chromeOptions.AddArgument("--no-sandbox");

    driver = new ChromeDriver(chromeOptions);
    driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(options.TimeoutSeconds * 2);
    stage = "page navigation";
    driver.Navigate().GoToUrl(options.Url);
    WaitForPage(driver, options);

    if (options.DumpHtmlPath is not null)
    {
        var fullPath = Path.GetFullPath(options.DumpHtmlPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, HtmlDumpRedactor.Redact(driver.PageSource));
        Console.WriteLine($"Saved rendered DOM to {fullPath}; request-verification tokens were redacted.");
        return 0;
    }

    stage = "field checks";
    var automator = new FormAutomator(options.Verbose ? Console.WriteLine : null,
        TimeSpan.FromSeconds(options.TimeoutSeconds), options.PollMilliseconds, options.Verbose);
    var data = new TestData(options.FirstName, options.Gender, options.State ?? "", options.Country ?? "");
    var startedAt = DateTimeOffset.UtcNow;
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var artifactDirectory = Path.GetFullPath(options.ArtifactsDir);
    Directory.CreateDirectory(artifactDirectory);
    var screenshots = new Dictionary<string, string>(StringComparer.Ordinal);
    var results = automator.Run(driver, data, result => CaptureScreenshot(driver, artifactDirectory, result, screenshots));
    stopwatch.Stop();
    var passed = 0;
    foreach (var result in results)
    {
        var status = result.Status.Equals("Passed", StringComparison.OrdinalIgnoreCase) ? "PASS" : result.Status.ToUpperInvariant();
        if (status == "PASS") passed++;
        var value = result.Value.Length == 0 ? "<none>" : $"'{result.Value}'";
        Console.WriteLine($"[{status}] {result.Field}: {value} — {result.Message} (score {result.Score:0.0}).");
        if (result.Degraded) Console.WriteLine($"  Degraded interaction strategy used: {string.Join(", ", result.Actions ?? [])}.");
    }
    Console.WriteLine($"Summary: {passed} passed, {results.Count - passed} failed or errored.");
    var browser = $"{driver.Capabilities.GetCapability("browserName")} {driver.Capabilities.GetCapability("browserVersion")}".Trim();
    var reportPath = RunReportWriter.Write(artifactDirectory, options.Url, startedAt, stopwatch.Elapsed, browser, results, screenshots);
    Console.WriteLine($"Report: {reportPath}");
    return results.Count == 3 && passed == 3 ? 0 : 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{stage} failed: {ex.Message}");
    return stage == "browser startup" ? 2 : 1;
}
finally
{
    driver?.Quit();
}

static void WaitForPage(IWebDriver driver, RunnerOptions options)
{
    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(options.TimeoutSeconds))
    {
        PollingInterval = TimeSpan.FromMilliseconds(options.PollMilliseconds)
    };
    wait.Until(current =>
    {
        var javascript = (IJavaScriptExecutor)current;
        var ready = javascript.ExecuteScript("return document.readyState")?.ToString();
        var collectable = javascript.ExecuteScript("const roots=[document];while(roots.length){const root=roots.shift();if(root.querySelector('input,select,textarea,[role=textbox],[role=radio],[role=combobox],[role=listbox],[aria-haspopup=listbox],[contenteditable=true]'))return true;if(root.querySelector('iframe,frame'))return true;for(const host of root.querySelectorAll('*'))if(host.shadowRoot?.mode==='open')roots.push(host.shadowRoot);}return false;");
        return string.Equals(ready, "complete", StringComparison.OrdinalIgnoreCase) &&
               bool.TryParse(collectable?.ToString(), out var hasCandidate) && hasCandidate;
    });
}

static void PrintHelp()
{
    Console.WriteLine("Usage: FormRunner [--url URL] [--headed|--headless] [--timeout SECONDS] [--poll-ms MS]");
    Console.WriteLine("       [--first-name VALUE] [--gender VALUE] [--state VALUE] [--country VALUE]");
    Console.WriteLine("       [--thorough] [--artifacts DIR] [--dump-html PATH] [--verbose] [--no-sandbox]");
    Console.WriteLine("URL precedence: command line > FORM_* environment variables > appsettings.json.");
}

static void CaptureScreenshot(ChromeDriver driver, string directory, FieldResult result, IDictionary<string, string> screenshots)
{
    try
    {
        var name = Regex.Replace(result.Field.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        var path = Path.Combine(directory, $"{name}.png");
        driver.GetScreenshot().SaveAsFile(path);
        screenshots[result.Field] = path;
    }
    catch (WebDriverException exception)
    {
        Console.Error.WriteLine($"Could not capture screenshot for {result.Field}: {exception.Message}");
    }
}

internal static partial class HtmlDumpRedactor
{
    private static readonly Regex InputTag = InputTagRegex();
    private static readonly Regex VerificationName = VerificationNameRegex();
    private static readonly Regex ValueAttribute = ValueAttributeRegex();

    public static string Redact(string html) => InputTag.Replace(html, match =>
        VerificationName.IsMatch(match.Value)
            ? ValueAttribute.Replace(match.Value, "value=\"REDACTED\"", 1)
            : match.Value);

    [GeneratedRegex("<input\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InputTagRegex();

    [GeneratedRegex("\\bname\\s*=\\s*(['\"])__RequestVerificationToken\\1", RegexOptions.IgnoreCase)]
    private static partial Regex VerificationNameRegex();

    [GeneratedRegex("\\bvalue\\s*=\\s*(['\"])[^'\"]*\\1", RegexOptions.IgnoreCase)]
    private static partial Regex ValueAttributeRegex();
}
