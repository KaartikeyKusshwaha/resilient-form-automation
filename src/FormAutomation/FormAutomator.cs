using OpenQA.Selenium.Support.UI;

namespace FormAutomation;

/// <summary>Values used by the three requested independent form checks.</summary>
public sealed record TestData(string FirstName, string Gender, string State, string Country);

/// <summary>Outcome of one field check, including the observed value and selected strategy.</summary>
public sealed record FieldResult(string Field, string Status, string Message, string Value, double Score,
    bool Degraded = false, IReadOnlyList<string>? Actions = null, ResolvedField? Resolved = null);

/// <summary>Explainability details for the resolved element.</summary>
public sealed record ResolvedField(string Tag, double Confidence, IReadOnlyList<string> Signals,
    string Frame, string Root, string Scope, string Label);

/// <summary>Executes semantic form checks independently and verifies each action by reading state back.</summary>
public sealed class FormAutomator
{
    private static readonly IReadOnlyList<SemanticField> TargetFields = [Fields.FirstName, Fields.Gender, Fields.State];
    private readonly SemanticResolver _resolver = new();
    private readonly Action<string>? _log;
    private readonly TimeSpan _timeout;
    private readonly int _pollMilliseconds;
    private readonly bool _verbose;
    private bool _hasLoggedResolution;

    /// <summary>Creates the runner's field-check orchestrator.</summary>
    public FormAutomator(Action<string>? log, TimeSpan timeout, int pollMilliseconds = 250, bool verbose = false)
    {
        _log = log;
        _timeout = timeout;
        _pollMilliseconds = Math.Clamp(pollMilliseconds, 10, 5000);
        _verbose = verbose;
    }

    /// <summary>Runs First Name, Gender, and State checks; a failed field does not stop later checks.</summary>
    public IReadOnlyList<FieldResult> Run(IWebDriver driver, TestData data, Action<FieldResult>? onVerified = null,
        Action<string, IWebElement>? onResolvedForTest = null)
    {
        WaitUntil(() => string.Equals(((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState")?.ToString(),
            "complete", StringComparison.OrdinalIgnoreCase), "document.readyState=complete");
        var results = new List<FieldResult>(3);
        results.Add(RunIndependent(driver, Fields.FirstName, () => CheckFirstName(driver, data.FirstName, onResolvedForTest)));
        onVerified?.Invoke(results[^1]);
        results.Add(RunIndependent(driver, Fields.Gender, () => CheckGender(driver, data.Gender, onResolvedForTest)));
        onVerified?.Invoke(results[^1]);
        results.Add(RunIndependent(driver, Fields.State, () => CheckState(driver, data.State, data.Country, onResolvedForTest)));
        onVerified?.Invoke(results[^1]);
        return results;
    }

    private FieldResult RunIndependent(IWebDriver driver, SemanticField field, Func<FieldResult> action)
    {
        try { return action(); }
        catch (Exception exception)
        {
            try { driver.SwitchTo().DefaultContent(); } catch (WebDriverException) { }
            return new FieldResult(field.Name, "Error", exception.Message, "", 0, Actions: []);
        }
    }

    private FieldResult CheckFirstName(IWebDriver driver, string value, Action<string, IWebElement>? onResolvedForTest)
    {
        var resolved = ResolveWithRetry(driver, Fields.FirstName);
        _resolver.SwitchToCandidateContext(driver, resolved.Candidate);
        var element = resolved.Candidate.Element;
        onResolvedForTest?.Invoke("first_name", element);
        var actions = new List<string>();
        var degraded = false;
        try
        {
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", element);
            try
            {
                element.Clear();
                element.SendKeys(value);
                actions.Add("clear + type");
            }
            catch (WebDriverException)
            {
                ReplaceByKeyboard(element, value);
                degraded = true;
                actions.Add("keyboard replacement fallback");
            }
            var actual = ReadTextValue(driver, element);
            if (!string.Equals(actual, value, StringComparison.Ordinal))
            {
                ReplaceByKeyboard(element, value);
                actual = ReadTextValue(driver, element);
                actions.Add("keyboard retry");
            }
            if (!string.Equals(actual, value, StringComparison.Ordinal))
            {
                SetNativeValue(driver, element, value);
                actual = ReadTextValue(driver, element);
                degraded = true;
                actions.Add("native setter fallback");
            }
            if (!string.Equals(actual, value, StringComparison.Ordinal))
                throw new InvalidOperationException($"First Name read-back mismatch: expected '{Mask(value)}', got '{Mask(actual)}'.");
            actions.Add("read-back verified");
        }
        finally { driver.SwitchTo().DefaultContent(); }

        VerifyNoTextDecoyReceivedValue(driver, resolved.Candidate, value);
        return Passed(Fields.FirstName, value, resolved, actions, degraded);
    }

    private FieldResult CheckGender(IWebDriver driver, string expected, Action<string, IWebElement>? onResolvedForTest)
    {
        var anchor = ResolveWithRetry(driver, Fields.Gender);
        var groupKey = anchor.Candidate.Descriptor.GroupKey;
        var group = _resolver.Gather(driver).Where(candidate => candidate.Descriptor.Kind == "radio" &&
            candidate.Descriptor.GroupKey == groupKey && candidate.Descriptor.RootKey == anchor.Candidate.Descriptor.RootKey &&
            candidate.FramePath.SequenceEqual(anchor.Candidate.FramePath)).OrderBy(candidate => candidate.Descriptor.Order).ToList();
        var selected = group.FirstOrDefault(candidate => CandidateScorer.IsGenderOptionMatch(candidate.Descriptor.OptionText, expected));
        if (selected is null)
            throw new InvalidOperationException($"Gender option '{expected}' was not found as a whole option in the resolved radio group.");
        _resolver.SwitchToCandidateContext(driver, selected);
        onResolvedForTest?.Invoke($"gender_{TextNormalizer.Normalize(expected).Replace(' ', '_')}", selected.Element);
        try
        {
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", selected.Element);
            try { selected.Element.Click(); }
            catch (WebDriverException)
            {
                var clicked = (bool?)((IJavaScriptExecutor)driver).ExecuteScript("const e=arguments[0]; if(e.labels?.length){e.labels[0].click();return true;} if(e.nextElementSibling){e.nextElementSibling.click();return true;} e.click();return true;", selected.Element) ?? false;
                if (!clicked) throw;
            }
            if (!selected.Element.Selected && selected.Element.GetAttribute("aria-checked") != "true")
                throw new InvalidOperationException("Gender option did not become selected.");
            var otherSelected = group.Where(candidate => candidate.Element != selected.Element)
                .Any(candidate => candidate.Element.Selected || candidate.Element.GetAttribute("aria-checked") == "true");
            if (otherSelected) throw new InvalidOperationException("More than one option in the Gender group is selected.");
        }
        finally { driver.SwitchTo().DefaultContent(); }
        var selectedScore = anchor with { Candidate = selected };
        return Passed(Fields.Gender, selected.Descriptor.OptionText, selectedScore, ["selected exact option", "verified selected state and unselected peers"], false);
    }

    private FieldResult CheckState(IWebDriver driver, string requestedState, string requestedCountry, Action<string, IWebElement>? onResolvedForTest)
    {
        var resolved = ResolveWithRetry(driver, Fields.State);
        var element = resolved.Candidate.Element;
        var actions = new List<string>();
        var value = requestedState;
        _resolver.SwitchToCandidateContext(driver, resolved.Candidate);
        onResolvedForTest?.Invoke("state", element);
        try
        {
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", element);
            if (element.TagName.Equals("select", StringComparison.OrdinalIgnoreCase))
            {
                var select = new SelectElement(element);
                var selectable = select.Options.Where(option => !IsDisabled(option) && !IsPlaceholder(option.Text, option.GetAttribute("value"))).ToList();
                if (selectable.Count == 0)
                {
                    driver.SwitchTo().DefaultContent();
                    ChooseCountryPrecondition(driver, requestedCountry, actions);
                    _resolver.SwitchToCandidateContext(driver, resolved.Candidate);
                    select = new SelectElement(element);
                    WaitUntil(() => select.Options.Any(option => !IsDisabled(option) && !IsPlaceholder(option.Text, option.GetAttribute("value"))), "State options");
                    selectable = select.Options.Where(option => !IsDisabled(option) && !IsPlaceholder(option.Text, option.GetAttribute("value"))).ToList();
                }
                var desired = string.IsNullOrWhiteSpace(requestedState)
                    ? selectable[0]
                    : selectable.FirstOrDefault(option => MatchStrength.IsWholeOptionMatch(option.Text, requestedState));
                if (desired is null)
                {
                    if (string.IsNullOrWhiteSpace(requestedCountry))
                        throw new InvalidOperationException($"State option '{requestedState}' is unavailable. Configure --country if the page requires a Country precondition.");
                    driver.SwitchTo().DefaultContent();
                    ChooseCountryPrecondition(driver, requestedCountry, actions);
                    _resolver.SwitchToCandidateContext(driver, resolved.Candidate);
                    WaitUntil(() => new SelectElement(element).Options.Any(option => MatchStrength.IsWholeOptionMatch(option.Text, requestedState)), $"State '{requestedState}' after Country selection");
                    select = new SelectElement(element);
                    desired = select.Options.FirstOrDefault(option => MatchStrength.IsWholeOptionMatch(option.Text, requestedState));
                }
                if (desired is null) throw new InvalidOperationException($"State option '{requestedState}' was not found after waiting for the dependent dropdown.");
                select.SelectByText(desired.Text);
                value = select.SelectedOption.Text.Trim();
            }
            else
            {
                element.Click();
                var desired = FindVisibleOption(driver, requestedState);
                if (desired is null) desired = FindVisibleOption(driver, "");
                if (desired is null) throw new InvalidOperationException("No visible option was found in the custom State dropdown.");
                value = SemanticResolver.Str(((IJavaScriptExecutor)driver).ExecuteScript("return arguments[0].innerText || arguments[0].textContent || '';", desired)).Trim();
                desired.Click();
            }
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("State dropdown did not expose a selected value after interaction.");
            if (!string.IsNullOrWhiteSpace(requestedState) && !MatchStrength.IsWholeOptionMatch(value, requestedState))
                throw new InvalidOperationException($"State read-back mismatch: expected '{requestedState}', got '{value}'.");
            actions.Add($"selected '{value}'");
            actions.Add("read-back verified");
        }
        finally { driver.SwitchTo().DefaultContent(); }
        return Passed(Fields.State, value, resolved, actions, false);
    }

    private void ChooseCountryPrecondition(IWebDriver driver, string requestedCountry, ICollection<string> actions)
    {
        if (string.IsNullOrWhiteSpace(requestedCountry))
            throw new InvalidOperationException("State dropdown is dependent and empty. Supply --country to select its precondition.");
        var countrySelect = Fields.Country with { Kind = FieldKind.Select };
        ScoredCandidate country;
        try { country = ResolveWithRetry(driver, countrySelect); }
        catch (TimeoutException) { country = ResolveWithRetry(driver, Fields.Country); }
        _resolver.SwitchToCandidateContext(driver, country.Candidate);
        try
        {
            if (country.Candidate.Element.TagName.Equals("select", StringComparison.OrdinalIgnoreCase))
            {
                var select = new SelectElement(country.Candidate.Element);
                var option = select.Options.FirstOrDefault(item => MatchStrength.IsWholeOptionMatch(item.Text, requestedCountry));
                if (option is null) throw new InvalidOperationException($"Country option '{requestedCountry}' was not found.");
                select.SelectByText(option.Text);
            }
            else
            {
                country.Candidate.Element.Clear();
                country.Candidate.Element.SendKeys(requestedCountry);
                var option = FindVisibleOption(driver, requestedCountry);
                option?.Click();
                var actual = ReadTextValue(driver, country.Candidate.Element);
                if (!MatchStrength.IsWholeOptionMatch(actual, requestedCountry) && option is null)
                    throw new InvalidOperationException($"Country precondition did not read back as '{requestedCountry}'.");
            }
            actions.Add($"selected Country precondition '{requestedCountry}'");
        }
        finally { driver.SwitchTo().DefaultContent(); }
    }

    private ScoredCandidate ResolveWithRetry(IWebDriver driver, SemanticField field)
    {
        var deadline = DateTime.UtcNow + _timeout;
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var requestedFields = TargetFields.Any(target => target.Name == field.Name)
                    ? TargetFields
                    : TargetFields.Append(field).ToArray();
                var set = _resolver.ResolveAll(driver, requestedFields, _verbose && !_hasLoggedResolution ? _log : null);
                _hasLoggedResolution = true;
                if (set.Candidates.TryGetValue(field.Name, out var candidate)) return candidate;
                last = new InvalidOperationException(set.Errors.GetValueOrDefault(field.Name, $"Could not resolve {field.Name}."));
            }
            catch (WebDriverException exception) { last = exception; }
            Thread.Sleep(_pollMilliseconds);
        }
        throw new TimeoutException($"Timed out resolving '{field.Name}'. {last?.Message ?? "No matching candidate appeared."}");
    }

    private void VerifyNoTextDecoyReceivedValue(IWebDriver driver, DomCandidate intended, string value)
    {
        var sameScope = _resolver.Gather(driver).Where(candidate => candidate.Descriptor.Kind == "text" &&
            candidate.Descriptor.ScopeKey == intended.Descriptor.ScopeKey && candidate.Descriptor.RootKey == intended.Descriptor.RootKey &&
            candidate.FramePath.SequenceEqual(intended.FramePath) && !candidate.Element.Equals(intended.Element));
        foreach (var candidate in sameScope)
        {
            _resolver.SwitchToCandidateContext(driver, candidate);
            try
            {
                if (string.Equals(ReadTextValue(driver, candidate.Element), value, StringComparison.Ordinal))
                    throw new InvalidOperationException("Decoy check failed: another text control in the selected form received the First Name value.");
            }
            finally { driver.SwitchTo().DefaultContent(); }
        }
    }

    private static void ReplaceByKeyboard(IWebElement element, string value)
    {
        element.Click();
        element.SendKeys(Keys.Control + "a");
        element.SendKeys(Keys.Delete);
        element.SendKeys(value);
    }

    private static void SetNativeValue(IWebDriver driver, IWebElement element, string value) =>
        ((IJavaScriptExecutor)driver).ExecuteScript("const e=arguments[0],v=arguments[1],p=e.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:HTMLInputElement.prototype,d=Object.getOwnPropertyDescriptor(p,'value');if(d?.set)d.set.call(e,v);else e.value=v;e.dispatchEvent(new Event('input',{bubbles:true}));e.dispatchEvent(new Event('change',{bubbles:true}));", element, value);

    private static string ReadTextValue(IWebDriver driver, IWebElement element) => element.GetAttribute("contenteditable") == "true"
        ? SemanticResolver.Str(((IJavaScriptExecutor)driver).ExecuteScript("return arguments[0].textContent || '';", element))
        : element.GetDomProperty("value") ?? element.GetAttribute("value") ?? "";

    private IWebElement? FindVisibleOption(IWebDriver driver, string text)
    {
        const string script = "const out=[],roots=[document];while(roots.length){const r=roots.shift();for(const e of r.querySelectorAll('*')){if(e.shadowRoot?.mode==='open')roots.push(e.shadowRoot);if(!e.matches('option,[role=option],[role=menuitem],li'))continue;const b=e.getBoundingClientRect(),s=getComputedStyle(e);if(b.width>0&&b.height>0&&s.display!=='none'&&s.visibility!=='hidden')out.push([e,(e.innerText||e.textContent||'').trim()]);}}return out;";
        foreach (var rowObject in SemanticResolver.AsList(((IJavaScriptExecutor)driver).ExecuteScript(script)))
        {
            var row = SemanticResolver.AsList(rowObject);
            if (row.Count >= 2 && row[0] is IWebElement element && (string.IsNullOrWhiteSpace(text) || MatchStrength.IsWholeOptionMatch(SemanticResolver.Str(row[1]), text)))
                return element;
        }
        return null;
    }

    private void WaitUntil(Func<bool> predicate, string description)
    {
        var deadline = DateTime.UtcNow + _timeout;
        while (DateTime.UtcNow < deadline)
        {
            try { if (predicate()) return; } catch (WebDriverException) { }
            Thread.Sleep(_pollMilliseconds);
        }
        throw new TimeoutException($"Timed out waiting for {description}.");
    }

    private static bool IsPlaceholder(string? text, string? value)
    {
        var raw = $"{text} {value}".Trim().ToLowerInvariant();
        return raw.Length == 0 || raw.StartsWith("--", StringComparison.Ordinal) || raw.StartsWith("select ", StringComparison.Ordinal) ||
            raw.StartsWith("choose ", StringComparison.Ordinal) || raw.StartsWith("please select", StringComparison.Ordinal) ||
            TextNormalizer.Normalize(text) is "select" or "choose";
    }

    private static bool IsDisabled(IWebElement option) =>
        string.Equals(option.GetDomProperty("disabled"), "true", StringComparison.OrdinalIgnoreCase);

    private static string Mask(string value) => value.Length <= 2 ? "**" : value[..1] + new string('*', Math.Min(8, value.Length - 2)) + value[^1..];

    private static FieldResult Passed(SemanticField field, string value, ScoredCandidate resolved, IReadOnlyList<string> actions, bool degraded)
    {
        var candidate = resolved.Candidate;
        var descriptor = candidate.Descriptor;
        var details = new ResolvedField(descriptor.Tag, Math.Clamp(resolved.Score / 150d, 0, 1), resolved.Signals,
            candidate.FramePath.Count == 0 ? "top" : string.Join("/", candidate.FramePath), descriptor.RootKey, descriptor.ScopeKey, descriptor.Label);
        return new FieldResult(field.Name, "Passed", "Read-back verified.", value, resolved.Score, degraded, actions, details);
    }
}
