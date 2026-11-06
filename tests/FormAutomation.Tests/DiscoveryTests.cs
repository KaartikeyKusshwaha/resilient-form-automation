using FormAutomation.Discovery;
using OpenQA.Selenium.Chrome;

namespace FormAutomation.Tests;

public sealed class DiscoveryTests
{
    [TestCase("m08_iframe.html", "frame")]
    [TestCase("m09_shadow.html", "shadow")]
    public void CollectorFindsCandidatesInFramesAndOpenShadowRoots(string file, string context)
    {
        var fixture = Path.Combine(FindRepositoryRoot(), "fixtures", "mutations", file);
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--window-size=1280,900");
        using var driver = new ChromeDriver(options);
        driver.Navigate().GoToUrl(new Uri(fixture).AbsoluteUri);
        var pageInfo = ((OpenQA.Selenium.IJavaScriptExecutor)driver).ExecuteScript("return JSON.stringify([location.href, document.title, document.body.innerText, document.querySelectorAll('iframe,input,select').length])");

        var collection = new CandidateCollector().Collect(driver);
        var debug = $"page={pageInfo}; candidate count={collection.Candidates.Count}; contexts={collection.ContextsVisited}; frames={collection.FramesSeen}; roots={collection.RootsSeen}; labels={string.Join(" | ", collection.Candidates.Select(item => item.Descriptor.Label))}";
        Assert.That(collection.Candidates, Is.Not.Empty, debug);
        var firstName = collection.Candidates.FirstOrDefault(candidate => candidate.Descriptor.Label == "First Name");
        Assert.That(firstName, Is.Not.Null, debug);
        var candidate = firstName ?? throw new AssertionException(debug);
        Assert.That(candidate.Descriptor.Kind, Is.EqualTo("text"));
        if (context == "frame")
        {
            Assert.That(candidate.FramePath, Is.EqualTo(new[] { 0 }));
            Assert.That(collection.ContextsVisited, Is.GreaterThanOrEqualTo(2));
        }
        else
        {
            Assert.That(candidate.Descriptor.RootKey, Does.StartWith("shadow:"));
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "PLAN.md")) && Directory.Exists(Path.Combine(directory.FullName, "fixtures")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not find the Task 1 repository root from the test output directory.");
    }
}
