# Approach and inspection notes

## Environment

- Checked 2026-11-06 (Asia/Kolkata).
- The machine PATH had .NET SDK 9.0.315 only. Installed the required .NET 8.0.427 SDK in the user profile alongside it; `global.json` pins this repository to the .NET 8 feature band.
- Python 3.12.10 and Git 2.51.0 are available.
- Chrome is not on PATH. Selenium Manager is used as the supported first-run browser/driver acquisition path; network access to the target page returned HTTP 200.
- `dotnet build -c Release -warnaserror`: passed after scaffold setup (0 warnings, 0 errors).
- `dotnet run --project src/FormRunner -- --url https://app.cloudqa.io/home/AutomationPracticeForm --timeout 30 --dump-html fixtures/live.html`: exited 0; anti-forgery input values were redacted. The live capture remains ignored by Git.
- `dotnet run --project src/FormRunner -- --url fixtures/baseline.html --timeout 30 --dump-html artifacts/baseline_rendered.html`: exited 0; Selenium opened the local baseline and found rendered controls.

## Decisions

- Use the .NET 8 SDK installed under the user profile instead of changing the machine-wide SDK installation.
- Ignore the local `fixtures/live.html` capture because it contains session-specific page markup; commit only the redacted, test-safe baseline fixture.

## Facts

- The target page returned HTTP 200 and a rendered Selenium capture was saved locally. Both the prior `fixtures/baseline.html` and the temporary live capture contain seven anti-forgery fields whose values are redacted. The live capture is ignored by Git.
- The primary First Name control is a text input with a visible “First Name” label; a Last Name input is adjacent and is a deliberate look-alike.
- Gender is a native radio group. The visible option captions (“Male”, “Female”, “Transgender”) are bare adjacent text, not associated `<label>` elements. `Male` must be matched as a whole option; it must never match `Female`.
- State is a native `<select>` with a “-- Select Country --” placeholder. Its current options are country names even though the field is labelled State. A separate Country text/autocomplete control exists. The current static source shows three State selects, so order and duplicated controls cannot identify the target by themselves.
- The rendered source contains three iframe tags, repeated fields, a modal, and unrelated rich-text editor controls. The source serialization does not include open shadow-root contents; runtime discovery still needs to check those contexts.

## Evidence

- Preflight command: `curl.exe -sI --max-time 15 https://app.cloudqa.io/home/AutomationPracticeForm` returned `HTTP/1.1 200 OK`.
- Capture command: `dotnet run --project src/FormRunner -- --url https://app.cloudqa.io/home/AutomationPracticeForm --timeout 30 --dump-html fixtures/live.html`; result: exit 0, rendered page saved, request-verification values redacted.
- Local baseline command: `dotnet run --project src/FormRunner -- --url fixtures/baseline.html --timeout 30 --dump-html artifacts/baseline_rendered.html`; result: exit 0, page rendered and controls were present.

## Open items

- Implement the full semantic resolver, mutation suite, reports, and CI; the checked-in starter code is not submission-ready.
- Verify runtime discovery across the live page's frames and open shadow roots after the collector is implemented.
- Run the three checks against the live page and record the honest result.
