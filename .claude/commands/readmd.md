---
description: Read the CLAUDE.md file (optionally from a given directory)
---

Read the `CLAUDE.md` file to load the project context.

- If `$ARGUMENTS` is non-empty, treat it as the path to the directory containing
  `CLAUDE.md` and read `$ARGUMENTS/CLAUDE.md`.
- If `$ARGUMENTS` is empty, look for `CLAUDE.md` in the current working directory.

If no `CLAUDE.md` is found at the resolved location, say so plainly instead of
guessing. After reading, give me a brief one or two line confirmation of what you
loaded.
