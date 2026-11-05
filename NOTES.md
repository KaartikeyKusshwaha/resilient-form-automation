# Approach and inspection notes

- Live baseline inspected at `https://app.cloudqa.io/home/AutomationPracticeForm`; the saved response fixture has its anti-forgery tokens redacted. The runner will refresh it with the loaded DOM via `--dump-html`.
- The main First Name is a text input with a visible “First Name” label; Last Name and Country are intentional decoys. Core selection will score human-visible labels first and use DOM order only as a warned tie-break.
- Gender is a native radio group. Its option captions are spans rather than associated labels, so group-caption and option-text heuristics are necessary.
- State is a native select with its own list and a “-- Select Country --” first option. Country is a separate text autocomplete; the State options do not depend on Country.
- The page repeats controls in modal, iframe, open-shadow-root, and nested-shadow-root sections. The resolver scans visible candidates across current-document open roots and same-origin frames, then uses a warned order tie-break.
- Closed shadow roots and cross-origin frames cannot be inspected by page script; frame switching supports browser-accessible frames.
