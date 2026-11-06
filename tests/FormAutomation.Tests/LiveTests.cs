using FormAutomation;
using OpenQA.Selenium.Chrome;

namespace FormAutomation.Tests.Integration;

public sealed class LiveTests
{
    [Test]
    [Category("Live")]
    public void AssignmentPageAcceptsAndVerifiesTheThreeRequestedFields()
    {
        var url = Environment.GetEnvironmentVariable("FORM_URL") ?? "https://app.cloudqa.io/home/AutomationPracticeForm";
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--window-size=1280,900");
        using var driver = new ChromeDriver(options);
        driver.Navigate().GoToUrl(url);
        var results = new FormAutomator(null, TimeSpan.FromSeconds(30), pollMilliseconds: 250)
            .Run(driver, new TestData("Alex", "Male", "", ""));

        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results.All(result => result.Status == "Passed"), Is.True,
            string.Join(Environment.NewLine, results.Select(result => $"{result.Field}: {result.Message}")));
    }
}
