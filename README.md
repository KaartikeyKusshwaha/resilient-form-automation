# Resilient Form Automation

C# / Selenium runner for testing First Name, Gender, and State without depending on fixed DOM attributes or field order.

## Prerequisites

- .NET 8 SDK
- Google Chrome or Chromium
- Internet access for the live test and Selenium Manager's browser driver, or use the local fixtures

## Configure and run

URL precedence is `--url`, `FORM_URL`, then `appsettings.json`. Default test data can be changed with CLI options or `FORM_FIRST_NAME`, `FORM_GENDER`, `FORM_STATE`, and `FORM_COUNTRY`.

```powershell
dotnet run --project src/FormRunner -- --url https://app.cloudqa.io/home/AutomationPracticeForm
dotnet run --project src/FormRunner -- --url fixtures/baseline.html --headed
$env:FORM_URL='https://app.cloudqa.io/home/AutomationPracticeForm'
dotnet run --project src/FormRunner
```

Pass `--dump-html <path>` to save the loaded DOM with request-token values redacted. Chrome is headless by default; use `--headed` to watch it.

## Tests

```powershell
dotnet test
dotnet test --filter TestCategory=Live
```

The default suite uses local fixtures; the live test runs only when `FORM_URL` is set.

## Exit codes

`0` means all three values were selected and verified; `1` means an automation assertion/resolution failed; `2` means configuration or browser startup prevented execution.
