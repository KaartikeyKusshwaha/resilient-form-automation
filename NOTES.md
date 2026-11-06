# Approach and implementation notes

## Approach

The runner locates First Name, Gender, and State by the same evidence a user sees: associated labels, ARIA names, placeholders, visible text near the control, radio option captions, and only weakly id/name/class tokens. It scans generic HTML controls and ARIA roles in the main document, open shadow roots, and nested frames. A scorer rejects incompatible, hidden, disabled, or read-only elements and penalizes negative signals such as Last Name or Country. A scope-first resolver prefers one form context containing all three fields. Every interaction is checked by reading the typed value, selected radio state, or chosen dropdown value back from the page.

## Why not fixed selectors

The assignment says page order and id/name/class values may change. The collector therefore uses generic element types and roles to discover candidates, not fixed page selectors. Human-facing labels and nearby text carry most of the score. Attributes can add only a small capped amount, so randomizing them does not break the lookup.

## Scoring model

| Evidence family | Weight / behavior |
|---|---|
| Visible label, ARIA group name, legend | Full match strength; exact canonical phrase scores 100 |
| ARIA label, placeholder, title | 0.8 multiplier |
| Nearby left/above text and sibling text | 0.65 multiplier |
| id/name/class tokens | 8 points per distinct hint, capped at 15 |
| Negative phrase in visible semantic text | -120, except exact positive phrase |
| Negative phrase only in attributes | -15 |
| Radio group with at least two gender-like options | +60 content signal when the group label is absent |
| Scope selection | Sum of best field scores + 50 per covered field + `5 * log2(1 + controls)` + 3 for top-document tie-break |

Acceptance threshold is 50. Gender options use normalized whole-option equality first, with limited documented synonyms after exact equality fails. The dropdown's option text is never treated as the State field's label.

## Decisions

- Use .NET 8 and Selenium WebDriver 4 with Selenium Manager for driver management.
- Use the user-profile .NET 8 SDK without changing the machine-wide SDK installation.
- Keep the captured live baseline separate from generated mutation pages so test-only oracle markers never enter a captured page.
- Use `file://` local fixtures so the offline mutation suite does not need a local HTTP server.
- Accept a zero-opacity native radio only when its associated visible label is usable; this supports common styled-radio markup while still rejecting invisible honeypots.

## Facts from the live page preflight

Checked 2026-11-06. The assignment URL returned HTTP 200 and was opened with Selenium. The main First Name field has a visible label; Last Name is adjacent. Gender is a native radio group whose visible option captions are bare adjacent text (`Male`, `Female`, `Transgender`) rather than associated labels. State is a native select with a `-- Select Country --` placeholder; the captured page currently offers country names under the State label. The page also contains a separate Country autocomplete, three iframe elements, repeated field sections, a modal, and rich-text controls. This is treated as observed page behavior, not a reason to assume all modified pages use the same structure.

## Mutation coverage

| Fixture | Evidence |
|---|---|
| m00–m01 | baseline and reordered controls |
| m02–m03 | scrambled attributes and restructured layout |
| m04 | look-alike labels, hidden/off-screen honeypots, modal, and decoy selects/radios |
| m05–m06 | no-label ARIA fields and custom/visually hidden controls |
| m07 | delayed form rendering |
| m08–m10 | iframe, open shadow root, iframe inside shadow root |
| m11 | two competing complete forms; larger scope selected |
| m12 | combined randomized wording/order/attributes (extra coverage) |

The integration tests use `data-test-target` and `data-test-decoy` only in fixtures and test code as an oracle. The lint suite rejects these markers in production source and rejects fixed-selector calls.

## Evidence

- `.NET 8.0.427`; `dotnet build -c Release -warnaserror`: passed with 0 warnings and 0 errors.
- `dotnet test -c Release --filter "Category!=Live"`: 35 tests passed, 0 failed, 0 skipped on 2026-11-06, after the `--thorough` integration test and radio-group collector fix.
- `dotnet test -c Release --filter "Category=Live"`: 1 live CloudQA end-to-end test passed on 2026-11-06; First Name, Gender, and State each passed read-back verification. A separate manual headed-independent `FormRunner` live run also reported 3/3 PASS.
- Manual runner checks on m00–m12: all 13 fixture pages produced three PASS results. The m06 custom radio issue and m11 duplicate-id fixture issue were found and fixed before the oracle-backed suite.
- `MutationTests`: 12 required fixtures passed with the correct target markers and decoys unchanged.
- `--verbose` printed candidate rankings and the selected form scope.
- Manual `report.json` check showed three checks, confidence/signals, actions, screenshots, duration, browser, and summary counts.
- `--dump-html` saved the rendered DOM while redacting request-verification token values.
- `scripts/verify-clean-clone.ps1`: passed on Windows; clean clone built warning-free and all 35 offline tests passed.
- GitHub Actions run `42206785547`: passed on 2026-11-06; warning-free build and non-live/mutation tests green.

## Limitations

- Heuristic English matching may miss unusual or non-English labels, images, canvas-based controls, or equally plausible duplicate forms.
- Closed shadow roots cannot be inspected. Cross-origin frames are supported when Selenium can enter them; inaccessible or sandboxed frames may be skipped.
- Custom widgets are best effort. Virtualized menus, canvas controls, and unusual option state are not guaranteed.
- The live assignment page currently presents country names under the State label; a requested State value that is absent fails rather than selecting a different option.
- Gender option synonyms are intentionally conservative; extend the catalog for additional vocabularies.

## Open items

- CI status is recorded above; the run includes informational runner/action deprecation notices only.
- Keep the limitations section aligned with the actual behavior and any test failures.
