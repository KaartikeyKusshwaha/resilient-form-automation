namespace FormAutomation.Config;

/// <summary>Runtime settings for one resilient form automation run.</summary>
public sealed record RunnerOptions
{
    /// <summary>Absolute HTTP(S) or file URL of the page under test.</summary>
    public required string Url { get; init; }

    /// <summary>Runs Chrome without a visible window when true.</summary>
    public bool Headless { get; init; } = true;

    /// <summary>Maximum time allowed for one field resolution or wait.</summary>
    public int TimeoutSeconds { get; init; } = 15;

    /// <summary>Polling interval used for late-rendered controls and options.</summary>
    public int PollMilliseconds { get; init; } = 250;

    /// <summary>Value entered in the semantically resolved First Name field.</summary>
    public string FirstName { get; init; } = "Alex";

    /// <summary>Option selected in the semantically resolved Gender group.</summary>
    public string Gender { get; init; } = "Male";

    /// <summary>Requested State option, or null to choose the first non-placeholder option.</summary>
    public string? State { get; init; }

    /// <summary>Country precondition used only when a dependent State dropdown is empty.</summary>
    public string? Country { get; init; }

    /// <summary>Whether to repeat available options as extra interaction coverage.</summary>
    public bool Thorough { get; init; }

    /// <summary>Directory for reports and screenshots.</summary>
    public string ArtifactsDir { get; init; } = "artifacts";

    /// <summary>Optional path for a redacted rendered-DOM capture.</summary>
    public string? DumpHtmlPath { get; init; }

    /// <summary>Whether to print candidate scoreboards.</summary>
    public bool Verbose { get; init; }

    /// <summary>Adds Chrome's no-sandbox flag for supported containers.</summary>
    public bool NoSandbox { get; init; }
}
