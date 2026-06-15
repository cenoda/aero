# Issues

Track bugs, feature requests, and tasks here. One file per issue.

## Layout

- `open/`   — active issues (status: `open`, `in-progress`, `blocked`)
- `closed/` — resolved issues (status: `closed`)
- `templates/` — issue file template

When closing an issue, move it from `open/` to `closed/` and update `INDEX.md`.

## Naming

`ISSUE-###-short-name.md`

Example: `ISSUE-001-tab-close-crash.md`, `ISSUE-002-dark-theme-flicker.md`

## Labels

Prefix the title or add a label line:

- `[BUG]` — something is broken
- `[FEAT]` — new feature request
- `[CHORE]` — cleanup, refactor, docs, etc.
- `[AGENT]` — agent/orchestration related
- `[DEBUG]` — debugging session, investigation, uncertain fix

## Template

Copy `templates/issue-template.md` and fill it in.

## Debug Rule

If a bug fix is not obvious after 2 attempts, **create an issue file immediately** and record all debug attempts in the `Debug Log` section. Do not keep debug attempts in memory or in chat history only.
