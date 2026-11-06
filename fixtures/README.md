# Mutation fixtures

Run `python scripts/generate_fixtures.py` to recreate these deterministic HTML pages. `data-test-target` and `data-test-decoy` are test-only oracle markers; the production collector and scorer never inspect them. `MutationTests` reads these markers after the selected actions and verifies decoys remain unchanged.

| Fixture | Mutation | Intended coverage |
|---|---|---|
| `m00_baseline.html` | Ordinary labelled form | Sanity case |
| `m01_reordered.html` | State and Gender before First Name; Last Name first | Order independence and look-alikes |
| `m02_attr_scramble.html` | Random IDs, names, and classes with labels kept associated | Weak attribute evidence |
| `m03_structure.html` | Deep divs, no form/fieldset, labels rendered as sibling spans | Layout and structural changes |
| `m04_decoys.html` | Similar text fields, hidden/off-screen honeypots, newsletter radios, multiple State-like selects, hidden modal | Semantic negatives and visibility checks |
| `m05_no_labels.html` | Placeholders, ARIA group name, bare radio captions | Label-less forms and whole-option matching |
| `m06_custom_controls.html` | Styled zero-opacity radio inputs and a custom listbox | Label-driven radio interaction and custom dropdown |
| `m07_late_render.html` | Form appears after a bounded delay | Retry behavior |
| `m08_iframe.html` | Complete form in an iframe | Frame discovery and re-entry |
| `m09_shadow.html` | Complete form in an open shadow root | Shadow-root discovery |
| `m10_shadow_iframe.html` | Iframe nested inside an open shadow root | Combined context traversal |
| `m11_two_forms.html` | Compact look-alike form precedes a larger complete form | Scope-first selection |
| `m12_randomized.html` | Reordered form, scrambled attributes, and semantic synonyms | Extra combined mutation coverage |

The live page capture is stored separately at `../baseline.html`; it is not used as a test oracle.
