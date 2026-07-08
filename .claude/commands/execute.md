---
description: Execute a development plan with task tracking
argument-hint: "[plan-file-path | phase-number] (optional — defaults to next phase in progress.html)"
---

# Execute Development Plan

## Step 1: Determine What to Execute

`$ARGUMENTS` is optional. Resolve the plan to execute in this order:

1. **An explicit plan/phase-doc file path** (e.g. `docs/phases/phase-2-search.html`) — use it directly as the plan.
2. **A bare phase number** (e.g. `2`) — select that phase's `<section class="phase" data-phase="2">` from `docs/progress.html` and resolve its phase doc.
3. **No argument** — consult `docs/progress.html` to pick the phase automatically (below).

### Selecting the next phase from progress.html

Read `docs/progress.html` and walk the `<section class="phase">` elements in ascending `data-phase` order. Pick the **first** phase whose `data-status` is not `complete`:

- **`in-progress`** — resume it (some tasks already `done`; pick up the remaining `todo` tasks).
- **`not-started`** — start it.
- **`not-planned`** — stop and tell the user to run `/plan-phase <N>` first; there is no phase doc to execute. Do not attempt to execute it.
- A task carrying a `<blockquote class="blocker">` is not eligible — skip it within the phase, and if every remaining task in the phase is blocked, stop and surface the blocker text to the user.

If every phase is `complete`, report that there is nothing to execute and stop.

Resolve the chosen phase's **phase doc** from its section: the `<p class="meta"><strong>Phase doc:</strong> <code>docs/phases/phase-<N>-<slug>.html</code></p>` line. Read that phase doc — it is the plan for the rest of this command. Note the phase number `<N>` and slug `<slug>`; you'll need them for the branch name in Step 2.

If `docs/progress.html` does not exist and no explicit plan file was given, stop and ask the user for a plan file path (the project may not use the autonomous-routine flow).

The plan (phase doc or plan file) will contain:
- A list of tasks to implement
- References to existing codebase components and integration points
- Context about where to look in the codebase for implementation

## Step 2: Verify git branch

- Verify you are in an appropriate branch. Either the default branch (main or master) or a branch appropriate to the phase/feature being worked on.

  Detect the repo's default branch instead of assuming `master` or `main`:

  git remote show origin | sed -n '/HEAD branch/s/.*: //p'

  If there is no `origin` remote, fall back to whichever of `main` or `master` exists locally. Refer to the resolved name as `<default-branch>` below.

  1 — Switch to the default branch and sync with remote

  git checkout <default-branch>
  git pull origin <default-branch>

  2 — Verify you're up to date

  git log --oneline -5        # confirm latest commits look right
  git status                  # confirm clean working tree

  3 — Create and switch to the work branch

  - **Executing a phase from `progress.html`:** use the phase branch `phase-<N>-<slug>` (the same name in the section's **Branch** meta line). If it already exists (e.g. resuming an `in-progress` phase), check it out instead of recreating it:

    git checkout phase-<N>-<slug> 2>/dev/null || git checkout -b phase-<N>-<slug>

  - **Executing an ad-hoc plan file** (not tied to a phase): create a descriptive branch following CLAUDE.md naming:
    - feat/ — new features (e.g., feat/langfuse-integration)
    - fix/ — bug fixes (e.g., fix/async-pool-cleanup)

    git checkout -b feat/your-feature-name

  4 — Push the branch to GitHub (first time)

  git push -u origin <branch-name>
  The -u sets the upstream so future git push / git pull on this branch need no extra args.

## Step 3: Create All Tasks

Before writing any code, create a task for each item in the plan using `TaskCreate`:
- Use an imperative subject (e.g., "Add CORS middleware to FastAPI app")
- Include the plan's acceptance criteria and context in the description
- Set `addBlockedBy` on tasks that depend on earlier ones

Create ALL tasks upfront so the full scope is visible before implementation starts.

## Step 4: Record Created Tasks in progress.html

After creating all tasks, reflect them in `docs/progress.html` so it stays the accurate source of truth for the autonomous routine.

- Use the phase `<section class="phase">` resolved in Step 1 (or, for an ad-hoc plan file, match by phase number/slug). If the plan maps to a phase that has no section yet, prefer re-running `/create-progress` instead of hand-authoring one.
- For each task created in Step 3, ensure there is a matching `<li class="task" data-task="<X>" data-status="todo">Task <X>: <description></li>` under that phase's `<ul class="tasks">`, in execution order. Add any that are missing; do not duplicate tasks that already exist.
- Preserve existing state: never clobber tasks already marked `data-status="done"` / `data-status="skipped"` or carrying a `<blockquote class="blocker">`.
- Keep the task numbering contiguous starting at 1 within the phase, matching the order tasks will be implemented.
- If `docs/progress.html` does not exist, note that in the Final Report and skip this step (the project may not use the autonomous-routine flow).

## Step 5: Codebase Analysis

Before implementing anything:
1. Read all files referenced in the plan's context section
2. Use Grep and Glob to understand existing patterns and find similar implementations
3. Verify your understanding of integration points before touching code

## Step 6: Implementation Cycle

Work through tasks in order (lowest ID first). For each task:

**6.1 Start** — Set the task to `in_progress` with `TaskUpdate` before writing any code.

**6.2 Implement** — Make all necessary changes. Follow existing patterns, conventions, and the project's `CLAUDE.md`.

**6.3 Validate** — Run the task's validation command (linter, type check, or test) before moving on. Fix failures before proceeding — do not mark a task complete if its validation fails.

**6.4 Complete** — Set the task to `completed` with `TaskUpdate` only after validation passes. Also flip the matching task in `docs/progress.html` to `data-status="done"` (if the file exists).

Only one task should be `in_progress` at a time.

## Step 7: Final Validation

After all tasks are complete, run the full validation suite specified in the plan (typically lint → tests → build). If anything fails, reopen the relevant task (`in_progress`), fix it, and re-validate.

## Step 8: Update README.md

Before reporting, update the project's `README.md` with any helpful changes that resulted from this plan. Only edit `README.md` if it already exists at the repo root — do not create one.

Look for items worth surfacing to a reader skimming the README:
- New user-facing features, commands, or endpoints
- New configuration keys or environment variables (with defaults)
- New dependencies or required tooling
- Changed setup, build, or run instructions
- Removed or deprecated capabilities

Edit the relevant sections in place; keep additions concise and consistent with the existing tone. If nothing in this plan is README-worthy (pure internal refactor, test-only changes, doc-only changes), state that explicitly in the Final Report and skip the edit.

## Step 9: Final Report

Provide a summary covering:
- Which phase was executed and how it was selected (explicit argument vs. next eligible phase in `progress.html`)
- Tasks completed
- Validation results
- Any deviations from the plan and why
- `docs/progress.html` updates (tasks recorded / marked done, or "none — file not present")
- README.md updates (or "none — change was not user-facing")
- Ready for `/git-compush`
