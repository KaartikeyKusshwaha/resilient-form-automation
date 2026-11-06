using FormAutomation;
using FormAutomation.Discovery;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace FormAutomation.Tests.Integration;

[NonParallelizable]
public sealed class MutationTests
{
    private static readonly string[] Fixtures =
    [
        "m00_baseline.html", "m01_reordered.html", "m02_attr_scramble.html", "m03_structure.html",
        "m04_decoys.html", "m05_no_labels.html", "m06_custom_controls.html", "m07_late_render.html",
        "m08_iframe.html", "m09_shadow.html", "m10_shadow_iframe.html", "m11_two_forms.html"
    ];

    [TestCaseSource(nameof(FixtureNames))]
    [Category("Mutation")]
    public void CorrectTargetsAreUsedAndDecoysRemainUntouched(string file)
    {
        var path = Path.Combine(FindRepositoryRoot(), "fixtures", "mutations", file);
        using var driver = CreateDriver();
        driver.Navigate().GoToUrl(new Uri(path).AbsoluteUri);
        var observed = new Dictionary<string, string?>(StringComparer.Ordinal);
        var runner = new FormAutomator(null, TimeSpan.FromSeconds(6), pollMilliseconds: 100);
        var results = runner.Run(driver, new TestData("TestFirst", "Male", "Gujarat", "India"),
            onResolvedForTest: (key, element) => observed[key] = element.GetAttribute("data-test-target"));

        Assert.That(results, Has.Count.EqualTo(3), file);
        Assert.That(results.All(result => result.Status == "Passed"), Is.True,
            $"{file}: {string.Join(" | ", results.Select(result => $"{result.Field}: {result.Message}"))}");
        Assert.That(observed["first_name"], Is.EqualTo("first_name"), file);
        Assert.That(observed["gender_male"], Is.EqualTo("gender_male"), file);
        Assert.That(observed["state"], Is.EqualTo("state"), file);

        var collector = new CandidateCollector();
        var all = collector.Collect(driver).Candidates;
        foreach (var decoy in all)
        {
            collector.EnterFramePath(driver, decoy.FramePath);
            try
            {
                if (decoy.Element.GetAttribute("data-test-decoy") is null) continue;
                var kind = decoy.Descriptor.Kind;
                if (kind == "text")
                    Assert.That(decoy.Element.GetDomProperty("value"), Is.Not.EqualTo("TestFirst"), $"{file}: text decoy changed");
                else if (kind == "radio")
                    Assert.That(decoy.Element.Selected, Is.False, $"{file}: radio decoy changed");
                else if (kind == "select" && decoy.Element.TagName.Equals("select", StringComparison.OrdinalIgnoreCase))
                {
                    var options = decoy.Element.FindElements(By.TagName("option"));
                    if (options.Count > 0)
                        Assert.That(new OpenQA.Selenium.Support.UI.SelectElement(decoy.Element).SelectedOption.Text,
                            Does.Not.Contain("Gujarat"), $"{file}: select decoy changed");
                }
            }
            finally { driver.SwitchTo().DefaultContent(); }
        }
    }

    [Test]
    [Category("Mutation")]
    public void ThoroughModeCyclesOptionsAndRestoresConfiguredChoices()
    {
        var path = Path.Combine(FindRepositoryRoot(), "fixtures", "mutations", "m00_baseline.html");
        using var driver = CreateDriver();
        driver.Navigate().GoToUrl(new Uri(path).AbsoluteUri);
        var runner = new FormAutomator(null, TimeSpan.FromSeconds(6), pollMilliseconds: 100, thorough: true);
        var results = runner.Run(driver, new TestData("TestFirst", "Male", "Gujarat", "India"));

        Assert.That(results.All(result => result.Status == "Passed"), Is.True,
            string.Join(" | ", results.Select(result => $"{result.Field}: {result.Message}")));
        Assert.That(results.Single(result => result.Field == "Gender").Value, Is.EqualTo("Male"));
        Assert.That(results.Single(result => result.Field == "State").Value, Is.EqualTo("Gujarat"));
        Assert.That(results.Single(result => result.Field == "Gender").Actions,
            Does.Contain("thoroughly verified 'Female'"));
        Assert.That(results.Single(result => result.Field == "State").Actions,
            Does.Contain("thoroughly verified 'California'"));
    }

    private static IEnumerable<string> FixtureNames() => Fixtures;

    private static IWebDriver CreateDriver()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--window-size=1280,900");
        return new ChromeDriver(options);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "PLAN.md")) && Directory.Exists(Path.Combine(directory.FullName, "fixtures")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not find the Task 1 repository root from the test output directory.");
    }
}
