---
description: Stage all changes, commit with a comprehensive message (no signature), and push
---

Commit everything in the working tree and push it:

1. Run `git status` and `git diff` (and `git diff --staged`) to see everything that
   changed since the last commit.
2. Stage all changes with `git add -A`.
3. Write ONE comprehensive commit message that summarizes the actual work — a concise
   subject line plus a body describing what changed and why, grouped logically if
   there are several distinct changes. Do NOT include any Claude/Co-Authored-By
   signature or trailer.
4. Commit, then `git push`.

If `$ARGUMENTS` is non-empty, use it as extra guidance for the commit message.

If there is nothing to commit, say so and stop. If the push fails (e.g. no upstream
or rejected), report the error plainly instead of force-pushing or working around it.
