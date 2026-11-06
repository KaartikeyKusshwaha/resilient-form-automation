using FormAutomation.Config;

namespace FormAutomation.Tests;

[NonParallelizable]
public sealed class ConfigTests
{
    private readonly string? _oldUrl = Environment.GetEnvironmentVariable("FORM_URL");
    private readonly string? _oldTimeout = Environment.GetEnvironmentVariable("FORM_TIMEOUT_SECONDS");
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        Environment.SetEnvironmentVariable("FORM_URL", null);
        Environment.SetEnvironmentVariable("FORM_TIMEOUT_SECONDS", null);
        _root = Path.Combine(Path.GetTempPath(), "form-options-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "appsettings.json"), "{\"Url\":\"https://config.example/form\",\"TimeoutSeconds\":11}");
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("FORM_URL", _oldUrl);
        Environment.SetEnvironmentVariable("FORM_TIMEOUT_SECONDS", _oldTimeout);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void CommandLineOverridesEnvironmentAndJson()
    {
        Environment.SetEnvironmentVariable("FORM_URL", "https://env.example/form");
        var options = OptionsLoader.Load(["--url", "https://cli.example/form"], _root);
        Assert.That(options.Url, Is.EqualTo("https://cli.example/form"));
    }

    [Test]
    public void EnvironmentOverridesJson()
    {
        Environment.SetEnvironmentVariable("FORM_URL", "https://env.example/form");
        var options = OptionsLoader.Load([], _root);
        Assert.That(options.Url, Is.EqualTo("https://env.example/form"));
    }

    [Test]
    public void JsonOverridesBuiltInDefaults()
    {
        var options = OptionsLoader.Load([], _root);
        Assert.Multiple((Action)(() =>
        {
            Assert.That(options.Url, Is.EqualTo("https://config.example/form"));
            Assert.That(options.TimeoutSeconds, Is.EqualTo(11));
        }));
    }

    [Test]
    public void MissingUrlFailsWithAllSupportedSources()
    {
        File.WriteAllText(Path.Combine(_root, "appsettings.json"), "{}");
        var error = Assert.Throws<OptionsException>((Action)(() => OptionsLoader.Load([], _root)));
        Assert.That(error!.Message, Does.Contain("--url").And.Contain("FORM_URL").And.Contain("appsettings.json"));
    }

    [TestCase("javascript:alert(1)")]
    [TestCase("data:text/html,hello")]
    public void RejectsUnsafeSchemes(string url) =>
        Assert.Throws<OptionsException>((Action)(() => OptionsLoader.Load(["--url", url], _root)));

    [Test]
    public void HeadedSwitchSetsHeadlessFalse()
    {
        var options = OptionsLoader.Load(["--url", "https://cli.example", "--headed"], _root);
        Assert.That(options.Headless, Is.False);
    }

    [Test]
    public void RejectsOutOfRangeTimeout()
    {
        var error = Assert.Throws<OptionsException>((Action)(() => OptionsLoader.Load(["--url", "https://cli.example", "--timeout", "0"], _root)));
        Assert.That(error!.Message, Does.Contain("whole number"));
    }
}
