---
description: Log what we did this session into CLAUDE.md
---

Summarize the concrete work completed in this session (code/assets/docs changed,
decisions made, gotchas resolved) and record it in `CLAUDE.md`.

How to write it:
- Append a dated entry under a "## Change Log" section in `CLAUDE.md`. Create that
  section (just below the Standing TODOs section) if it doesn't exist yet.
- Use today's date as an `###` heading, then a short bullet list — what changed and
  why it mattered, not a play-by-play. Keep each bullet to one line.
- If something we did completes or supersedes a Standing TODO, update/remove that
  TODO in the same edit.
- Don't duplicate detail that already lives in dedicated docs (e.g. tutorials,
  BoxSystem.md) — link to them instead.

If `$ARGUMENTS` is non-empty, use it as the focus/scope of the log entry rather than
the whole session.

Before saving, show me the entry you're about to add and apply it.
