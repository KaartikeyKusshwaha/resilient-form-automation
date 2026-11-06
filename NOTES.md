# Approach and inspection notes

## Environment

- Checked 2026-11-06 (Asia/Kolkata).
- The machine PATH had .NET SDK 9.0.315 only. Installed the required .NET 8.0.427 SDK in the user profile alongside it; `global.json` pins this repository to the .NET 8 feature band.
- Python 3.12.10 and Git 2.51.0 are available.
- Chrome is not on PATH. Selenium Manager is used as the supported first-run browser/driver acquisition path; network access to the target page returned HTTP 200.
- `dotnet build -c Release -warnaserror`: passed after scaffold setup (0 warnings, 0 errors).

## Decisions

- Use the .NET 8 SDK installed under the user profile instead of changing the machine-wide SDK installation.
- Ignore the local `fixtures/live.html` capture because it contains session-specific page markup; commit only the redacted, test-safe baseline fixture.

## Open items

- Implement and verify the full resilient resolver, mutation suite, and live run; the checked-in starter code is not yet submission-ready.
- Record current target-page facts and acceptance evidence after the robust runner is in place.

- Live baseline inspected at `https://app.cloudqa.io/home/AutomationPracticeForm`; the saved response fixture has its anti-forgery tokens redacted. The runner will refresh it with the loaded DOM via `--dump-html`.
- The main First Name is a text input with a visible “First Name” label; Last Name and Country are intentional decoys. Core selection will score human-visible labels first and use DOM order only as a warned tie-break.
- Gender is a native radio group. Its option captions are spans rather than associated labels, so group-caption and option-text heuristics are necessary.
- State is a native select with its own list and a “-- Select Country --” first option. Country is a separate text autocomplete; the State options do not depend on Country.
- The page repeats controls in modal, iframe, open-shadow-root, and nested-shadow-root sections. The resolver scans visible candidates across current-document open roots and same-origin frames, then uses a warned order tie-break.
- Closed shadow roots and cross-origin frames cannot be inspected by page script; frame switching supports browser-accessible frames.
