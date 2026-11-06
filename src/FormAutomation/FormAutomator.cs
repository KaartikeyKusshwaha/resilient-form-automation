using OpenQA.Selenium.Support.UI;

namespace FormAutomation;

public sealed record TestData(string FirstName, string Gender, string State, string Country);
public sealed record FieldResult(string Field, string Value, double Score);

public sealed class FormAutomator
{
    private readonly SemanticResolver _resolver = new();
    private readonly Action<string> _log;
    private readonly TimeSpan _timeout;

    public FormAutomator(Action<string> log, TimeSpan timeout) { _log = log; _timeout = timeout; }

    public IReadOnlyList<FieldResult> Run(IWebDriver driver, TestData data, Action<FieldResult>? onVerified = null)
    {
        WaitForDocument(driver);
        var results = new List<FieldResult>();

        var first = ResolveWithRetry(driver, Fields.FirstName);
        _resolver.SwitchToCandidateContext(driver, first.Candidate);
        var firstValue = data.FirstName;
        try
        {
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", first.Candidate.Element);
            try
            {
                first.Candidate.Element.Clear();
                first.Candidate.Element.SendKeys(firstValue);
            }
            catch (WebDriverException)
            {
                ((IJavaScriptExecutor)driver).ExecuteScript("const e=arguments[0], v=arguments[1], p=e.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:HTMLInputElement.prototype; Object.getOwnPropertyDescriptor(p,'value').set.call(e,v); e.dispatchEvent(new Event('input',{bubbles:true})); e.dispatchEvent(new Event('change',{bubbles:true}));", first.Candidate.Element, firstValue);
            }
            var actual = first.Candidate.Element.GetAttribute("value") ?? "";
            if (actual != firstValue) throw new InvalidOperationException($"First Name read-back mismatch: expected '{firstValue}', got '{actual}'.");
            results.Add(new("First Name", actual, first.Score));
            onVerified?.Invoke(results[^1]);
        }
        finally { driver.SwitchTo().DefaultContent(); }

        var genderAnchor = ResolveWithRetry(driver, Fields.Gender);
        var radioGroup = _resolver.Gather(driver).Where(c => c.Descriptor.Kind == "radio" &&
            c.Descriptor.FormKey == genderAnchor.Candidate.Descriptor.FormKey &&
            c.FramePath.SequenceEqual(genderAnchor.Candidate.FramePath)).OrderBy(c => c.Descriptor.Order).ToList();
        var selected = radioGroup.FirstOrDefault(c => CandidateScorer.IsWholeOptionMatch(c.Descriptor.OptionText, data.Gender));
        if (selected is null)
            throw new InvalidOperationException($"Gender option '{data.Gender}' was not found as a whole-word option in the resolved group.");
        _resolver.SwitchToCandidateContext(driver, selected);
        try
        {
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", selected.Element);
            try { selected.Element.Click(); }
            catch (WebDriverException)
            {
                var clicked = (bool?)((IJavaScriptExecutor)driver).ExecuteScript("const e=arguments[0]; if(e.labels?.length){e.labels[0].click();return true;} if(e.nextElementSibling){e.nextElementSibling.click();return true;} e.click();return true;", selected.Element) ?? false;
                if (!clicked) throw;
            }
            var selectedRadios = radioGroup.Select(c => (Candidate: c, IsSelected: c.Element.Selected)).ToList();
            if (!selected.Element.Selected || selectedRadios.Any(x => x.Candidate != selected && x.IsSelected))
                throw new InvalidOperationException("Gender radio selection did not verify exclusively.");
            results.Add(new("Gender", selected.Descriptor.OptionText, genderAnchor.Score));
            onVerified?.Invoke(results[^1]);
        }
        finally { driver.SwitchTo().DefaultContent(); }

        var state = ResolveWithRetry(driver, Fields.State);
        _resolver.SwitchToCandidateContext(driver, state.Candidate);
        try
        {
            var tag = state.Candidate.Element.TagName.ToLowerInvariant();
            string selectedText;
            if (tag == "select")
            {
                var select = new SelectElement(state.Candidate.Element);
                var selectable = select.Options.Where(o => !IsPlaceholder(o.Text) && !IsPlaceholder(o.GetAttribute("value"))).ToList();
                if (selectable.Count == 0)
                {
                    driver.SwitchTo().DefaultContent();
                    var country = ResolveWithRetry(driver, Fields.Country);
                    _resolver.SwitchToCandidateContext(driver, country.Candidate);
                    country.Candidate.Element.Clear();
                    country.Candidate.Element.SendKeys(data.Country);
                    var countryOption = FindVisibleTextOption(driver, data.Country);
                    countryOption?.Click();
                    driver.SwitchTo().DefaultContent();
                    _resolver.SwitchToCandidateContext(driver, state.Candidate);
                    select = new SelectElement(state.Candidate.Element);
                    WaitUntil(() => select.Options.Any(o => !IsPlaceholder(o.Text)), $"State options after Country '{data.Country}'");
                    selectable = select.Options.Where(o => !IsPlaceholder(o.Text)).ToList();
                }
                var desired = string.IsNullOrWhiteSpace(data.State) ? selectable[0] : selectable.FirstOrDefault(o => CandidateScorer.IsWholeOptionMatch(o.Text, data.State));
                if (desired is null) throw new InvalidOperationException($"State option '{data.State}' is not present. Available: {string.Join(", ", selectable.Take(8).Select(o => o.Text))}");
                select.SelectByText(desired.Text);
                selectedText = select.SelectedOption.Text.Trim();
            }
            else
            {
                state.Candidate.Element.Click();
                var option = FindVisibleTextOption(driver, data.State);
                if (option is null) throw new InvalidOperationException($"Visible custom option '{data.State}' was not found.");
                option.Click();
                selectedText = (state.Candidate.Element.GetAttribute("aria-label") ?? state.Candidate.Element.Text).Trim();
            }
            if (!string.IsNullOrWhiteSpace(data.State) && !CandidateScorer.IsWholeOptionMatch(selectedText, data.State))
                throw new InvalidOperationException($"State read-back mismatch: expected '{data.State}', got '{selectedText}'.");
            results.Add(new("State", selectedText, state.Score));
            onVerified?.Invoke(results[^1]);
        }
        finally { driver.SwitchTo().DefaultContent(); }
        return results;
    }

    private ScoredCandidate ResolveWithRetry(IWebDriver driver, SemanticField field)
    {
        var stop = DateTime.UtcNow + _timeout;
        Exception? last = null;
        while (DateTime.UtcNow < stop)
        {
            try { return _resolver.Resolve(driver, field, _log); }
            catch (InvalidOperationException ex) { last = ex; Thread.Sleep(250); }
        }
        throw new TimeoutException($"Timed out resolving '{field.Name}'. {last?.Message}");
    }

    private void WaitForDocument(IWebDriver driver)
    {
        WaitUntil(() => string.Equals(((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState")?.ToString(), "complete", StringComparison.OrdinalIgnoreCase), "document.readyState=complete");
    }

    private void WaitUntil(Func<bool> condition, string description)
    {
        var stop = DateTime.UtcNow + _timeout;
        while (DateTime.UtcNow < stop)
        {
            try { if (condition()) return; } catch (WebDriverException) { }
            Thread.Sleep(200);
        }
        throw new TimeoutException($"Timed out waiting for {description}.");
    }

    private IWebElement? FindVisibleTextOption(IWebDriver driver, string text)
    {
        const string script = """
            const out=[]; const roots=[document];
            while(roots.length){const r=roots.shift(); for(const e of r.querySelectorAll('*')){
              if(e.shadowRoot?.mode==='open') roots.push(e.shadowRoot);
              if(!['OPTION','LI'].includes(e.tagName) && !['option','menuitem'].includes((e.getAttribute('role')||'').toLowerCase())) continue;
              const b=e.getBoundingClientRect(),s=getComputedStyle(e);
              if(b.width>0&&b.height>0&&s.display!=='none'&&s.visibility!=='hidden') out.push([e,(e.innerText||e.textContent||'').trim()]);
            }} return out;
            """;
        var rows = SemanticResolver.AsList(((IJavaScriptExecutor)driver).ExecuteScript(script));
        foreach (var rowObj in rows)
        {
            var row = SemanticResolver.AsList(rowObj);
            if (row.Count >= 2 && row[0] is IWebElement el && CandidateScorer.IsWholeOptionMatch(SemanticResolver.Str(row[1]), text))
                return el;
        }
        return null;
    }

    private static bool IsPlaceholder(string? value)
    {
        var text = CandidateScorer.Normalize(value);
        return text.Length == 0 || text.StartsWith("select ") || text.StartsWith("choose ") || text.StartsWith(" ") || text.StartsWith("country ");
    }
}
