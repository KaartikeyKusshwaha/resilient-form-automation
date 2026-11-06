# Resilient Form Automation

[![Task 1 CI](https://github.com/KaartikeyKusshwaha/resilient-form-automation/actions/workflows/ci.yml/badge.svg)](https://github.com/KaartikeyKusshwaha/resilient-form-automation/actions/workflows/ci.yml)

A .NET 8 console tool that uses Selenium to find and exercise First Name, Gender, and State by visible meaning rather than fixed page selectors. Its URL is configurable, and every action must be verified by reading the page state back.

## Prerequisites

- .NET 8 SDK
- Recent Google Chrome or Chromium
- Network access on first run so Selenium Manager can obtain a compatible driver if needed

## Quick start

```bash
dotnet run --project src/FormRunner -- --url https://app.cloudqa.io/home/AutomationPracticeForm
```

The default test values are `Alex`, `Male`, and the first available non-placeholder State option. Choose explicit values when the modified page expects them:

```bash
dotnet run --project src/FormRunner -- --url "https://example.test/form" --first-name "Alex" --gender "Female" --state "Gujarat"
```

The browser is headless by default. Add `--headed` to watch it interact with the page.

## Configure the page URL

The first configured value wins:

1. Command line: `--url <absolute-url>`
2. Environment: `FORM_URL`
3. `src/FormRunner/appsettings.json`: `Url`

Accepted schemes are `http`, `https`, and `file`. A local fixture can be passed as an absolute file URL or path:

```bash
dotnet run --project src/FormRunner -- --url "file:///absolute/path/to/form.html"
```

## Options

| Option | Environment variable | Default | Meaning |
|---|---|---:|---|
| `--url <URL>` | `FORM_URL` | assignment page | Page under test |
| `--headed` / `--headless` | `FORM_HEADLESS` | headless | Show or hide the browser |
| `--timeout <seconds>` | `FORM_TIMEOUT_SECONDS` | `15` | Resolution and wait timeout (1–120 seconds) |
| `--poll-ms <milliseconds>` | `FORM_POLL_MILLISECONDS` | `250` | Retry interval (10–5000 ms) |
| `--first-name <text>` | `FORM_FIRST_NAME` | `Alex` | First Name value to type |
| `--gender <option>` | `FORM_GENDER` | `Male` | Exact Gender option, with documented synonyms |
| `--state <option>` | `FORM_STATE` | first available | State option to select; empty chooses the first non-placeholder |
| `--country <option>` | `FORM_COUNTRY` | unset | Country precondition for a dependent State dropdown |
| `--artifacts <directory>` | `FORM_ARTIFACTS_DIR` | `artifacts` | Report and screenshots directory |
| `--dump-html <path>` | — | unset | Save rendered DOM and exit; request verification values are redacted |
| `--verbose` | `FORM_VERBOSE` | off | Print candidate scoreboards and scope choice |
| `--no-sandbox` | `FORM_NO_SANDBOX` | off | Add Chrome's no-sandbox flag in a container |
| `--thorough` | `FORM_THOROUGH` | off | Cycle all Gender radios and up to three native State options, then restore the configured choices |

## Results and exit codes

Each check prints `PASS`, `FAILED`, or `ERROR`. A check passes only after the selected control has the requested value/state on read-back. The run writes:

- `artifacts/report.json` — URL, browser, timing, field outcomes, resolver evidence, actions, and summary.
- `artifacts/first-name.png`, `artifacts/gender.png`, `artifacts/state.png` — screenshots captured after each check.

Exit codes are `0` when all three checks pass, `1` when a field check fails or errors, and `2` for invalid configuration or browser startup errors.

## How it works

The collector scans generic form-control tags and ARIA roles, including open shadow roots and nested iframes. It gathers accessible labels, ARIA names, nearby text, option captions, render state, and weak id/name/class tokens. The scorer normalizes text, compares it with an extendable synonym list, applies negative evidence to look-alikes, and ignores hidden or unusable controls. A scope-first step prefers one form that contains all three target fields. The runner then types, selects, or clicks and reads the changed state back. A mutation suite checks the actual intended target and confirms marked decoys remain untouched.

```text
URL configuration
      |
      v
FormRunner -> three independent field checks
      |               |
      |       CandidateCollector (frames + open shadow roots)
      |               |
      |       normalize -> score -> choose complete form scope
      |               |
      +------> interact -> read back -> report + screenshots
```

## Tests

```bash
dotnet test
dotnet test --filter "Category!=Live"
dotnet test --filter "Category=Live"
```

The non-live suite uses local HTML fixtures and a test-only oracle marker. The production resolver never reads those markers. To regenerate the deterministic pages, run `python scripts/generate_fixtures.py`; see [fixtures/README.md](fixtures/README.md) for the mutation list.

## Repository map

```text
src/FormAutomation/       collector, normalization, scoring, resolution, interactions, reports
src/FormRunner/           console entry point and runtime configuration
tests/                    unit, lint, discovery, mutation, and optional live checks
fixtures/                 redacted live baseline plus local mutation pages
scripts/                  fixture generator and clean-clone verification
```

## Troubleshooting

- **Driver download fails:** allow Selenium Manager network access or install a ChromeDriver matching the local Chrome version.
- **Chrome is not found:** install a recent Chrome/Chromium browser, then rerun. Selenium Manager can only manage the driver when a supported browser is available.
- **Container startup fails:** try `--no-sandbox` where the container requires it.
- **A field cannot be resolved:** rerun with `--verbose`; use `--dump-html <path>` to inspect the rendered page. The failure report includes the top candidates and searched contexts.
- **The State list is dependent:** set `--country <option>` if the dropdown is empty or the requested State appears only after choosing a Country.

See [NOTES.md](NOTES.md) for the approach, observed live-page behavior, evidence, and limitations.
