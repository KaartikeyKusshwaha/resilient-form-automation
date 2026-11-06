using System.Collections;
using System.Reflection;

namespace FormAutomation.Discovery;

/// <summary>Collects generic form controls through open shadow roots and nested frames.</summary>
public sealed class CandidateCollector
{
    private const int MaximumFrameDepth = 4;
    private const int MaximumContexts = 25;
    private static readonly string CollectorScript = LoadScript();

    /// <summary>Returns visible and hidden candidates with frame paths and discovery counts.</summary>
    public CandidateCollection Collect(IWebDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);
        var candidates = new List<DomCandidate>();
        var visitedContexts = 0;
        var framesSeen = 0;
        var rootKeys = new HashSet<string>(StringComparer.Ordinal);
        driver.SwitchTo().DefaultContent();
        Walk([] , 0);
        driver.SwitchTo().DefaultContent();
        return new CandidateCollection(candidates, visitedContexts, framesSeen, rootKeys.Count);

        void Walk(IReadOnlyList<int> path, int depth)
        {
            if (++visitedContexts > MaximumContexts) return;
            var payload = SemanticResolver.AsList(((IJavaScriptExecutor)driver).ExecuteScript(CollectorScript));
            if (payload.Count < 3) return;
            var elements = SemanticResolver.AsList(payload[0]);
            var descriptors = SemanticResolver.AsList(payload[1]);
            var frames = SemanticResolver.AsList(payload[2]);
            framesSeen += frames.Count;

            for (var i = 0; i < Math.Min(elements.Count, descriptors.Count); i++)
            {
                if (elements[i] is not IWebElement element || descriptors[i] is not IDictionary descriptor) continue;
                var parsed = Parse(descriptor);
                rootKeys.Add(parsed.RootKey);
                candidates.Add(new DomCandidate(element, parsed, path.ToArray()));
            }

            if (depth >= MaximumFrameDepth) return;
            for (var i = 0; i < frames.Count && visitedContexts < MaximumContexts; i++)
            {
                if (frames[i] is not IWebElement frame) continue;
                var entered = false;
                try
                {
                    driver.SwitchTo().Frame(frame);
                    entered = true;
                    Walk(path.Concat([i]).ToArray(), depth + 1);
                }
                catch (WebDriverException) { /* Unavailable frames are reflected by missing candidates. */ }
                finally
                {
                    if (entered)
                    {
                        try { driver.SwitchTo().ParentFrame(); }
                        catch (WebDriverException) { driver.SwitchTo().DefaultContent(); }
                    }
                }
            }
        }
    }

    /// <summary>Re-enters a previously collected browsing-context path before interacting.</summary>
    public void EnterFramePath(IWebDriver driver, IReadOnlyList<int> framePath)
    {
        driver.SwitchTo().DefaultContent();
        foreach (var frameIndex in framePath)
        {
            var payload = SemanticResolver.AsList(((IJavaScriptExecutor)driver).ExecuteScript(CollectorScript));
            if (payload.Count < 3) throw new InvalidOperationException("Could not enumerate frames while restoring the candidate context.");
            var frames = SemanticResolver.AsList(payload[2]);
            if (frameIndex < 0 || frameIndex >= frames.Count || frames[frameIndex] is not IWebElement frame)
                throw new InvalidOperationException("The page's frame structure changed while resolving a field.");
            driver.SwitchTo().Frame(frame);
        }
    }

    private static CandidateDescriptor Parse(IDictionary data)
    {
        var labels = Strings(Get(data, "labelTexts"));
        var labelledBy = Strings(Get(data, "labelledByTexts"));
        var options = SemanticResolver.AsList(Get(data, "options"))
            .OfType<IDictionary>()
            .Select(x => new OptionDescriptor(Text(x, "text"), Text(x, "value"), Flag(x, "disabled"), Flag(x, "selected")))
            .ToArray();
        var rect = Get(data, "rect") as IDictionary;
        var label = Text(data, "label");
        var accessible = Text(data, "accessible");
        var near = Text(data, "nearby");
        var attrs = Text(data, "attributes");
        var kind = Text(data, "kind");
        var descriptor = new CandidateDescriptor(label, accessible, near, attrs, kind, Text(data, "type"),
            Text(data, "optionText"), Text(data, "formKey"), Flag(data, "inModal"), Number(data, "order"))
        {
            Tag = Text(data, "tag"), Role = Text(data, "role"), Id = Text(data, "id"), Name = Text(data, "name"),
            Classes = Text(data, "classes"), Value = Text(data, "value"), Checked = Flag(data, "checked"),
            Disabled = Flag(data, "disabled"), ReadOnly = Flag(data, "readOnly"), Required = Flag(data, "required"),
            SelfVisible = Flag(data, "selfVisible"), LabelVisible = Flag(data, "labelVisible"), Offscreen = Flag(data, "offscreen"),
            OpacityZero = Flag(data, "opacityZero"), AriaHidden = Flag(data, "ariaHidden"), TinyBox = Flag(data, "tinyBox"),
            LabelTexts = labels, LabelledByTexts = labelledBy, AriaLabel = Text(data, "ariaLabel"),
            Placeholder = Text(data, "placeholder"), Title = Text(data, "title"), LegendText = Text(data, "legendText"),
            GroupAriaLabel = Text(data, "groupAriaLabel"), NearLeft = Text(data, "nearLeft"), NearAbove = Text(data, "nearAbove"),
            PrecedingSiblingText = Text(data, "precedingSiblingText"), NextText = Text(data, "nextText"), Options = options,
            RootKey = Text(data, "rootKey"), ScopeKey = Text(data, "scopeKey"), ScopeControlCount = Number(data, "scopeControlCount"),
            GroupKey = Text(data, "groupKey")
        };
        _ = rect;
        return descriptor;
    }

    private static string LoadScript()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("Discovery.collector.js", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static object? Get(IDictionary data, string key) => data.Contains(key) ? data[key] : null;
    private static string Text(IDictionary data, string key) => Convert.ToString(Get(data, key), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    private static bool Flag(IDictionary data, string key) => bool.TryParse(Text(data, key), out var parsed) && parsed;
    private static int Number(IDictionary data, string key) => int.TryParse(Text(data, key), out var parsed) ? parsed : 0;
    private static string[] Strings(object? value) => SemanticResolver.AsList(value)
        .Select(item => Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture) ?? "")
        .Where(item => item.Length > 0).ToArray();
}

/// <summary>Collected candidates and tree/frame discovery statistics.</summary>
public sealed record CandidateCollection(IReadOnlyList<DomCandidate> Candidates, int ContextsVisited, int FramesSeen, int RootsSeen);
