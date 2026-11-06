# AGENTS.md

## Mission
Implement exactly the Task 1 requirements in `PLAN.md`, Part B. The plan is the source of truth.

## Working agreement
- Work milestones B-M0 through B-M9 in order. After each milestone, run its acceptance commands, fix failures, then commit with the milestone name.
- MUST items are mandatory. Do SHOULD items only after MUST items are green. Do STRETCH items only after everything else is green.
- Do not ask questions. Resolve ordinary ambiguity with the simplest compliant choice and record it in `NOTES.md > Decisions`.

## Hard rules
- No site-specific selectors, URLs, labels, or credentials in core logic.
- The production source must never read test-only `data-test-*` attributes.
- Never commit secrets or session data. Use environment variables and keep local captures ignored.
- Never weaken or delete a failing test to get a green run. Fix the cause or document an explicit skip in `NOTES.md`.
- Consult current SDK documentation and installed package signatures; do not guess API names.
- Use explicit timeouts; do not add bare sleeps except tiny polling intervals.
- Keep code small, readable, warning-free, and free of dead code/debug prints.

## Definition of done
Every Task 1 item in the Strict Compliance Gate in `PLAN.md` has passing evidence. The README works from a clean clone and `NOTES.md` accurately records facts, limitations, and evidence.

## Acceptance commands
```powershell
dotnet build -c Release -warnaserror
dotnet test -c Release --filter "Category!=Live"
dotnet run --project src/FormRunner -- --url <URL>
```
