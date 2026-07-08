---
description: Aggregate authored phase docs into progress.html (the autonomous-routine source of truth)
argument-hint: "[phases-dir] [output-path]"
---

# Create progress.html

## Mission

Read every phase doc under `docs/phases/` and emit `docs/progress.html` — the index the autonomous routine reads on every run to find the next task.

This command is **purely mechanical**. It makes no design decisions. The phase docs (authored by `/plan-phase`) are the source of truth for slugs, branch names, dependencies, and tasks for *planned* phases; the PRD is the source of truth for the full set of phases that exist (planned or not). This command just parses and aggregates.

Both the phase docs and the PRD are **standalone HTML documents** with a fixed, parseable shape (see `/plan-phase` and `/create-prd` for the contracts). This command parses those shapes — it does not interpret prose.

Phases listed in the PRD but without a phase doc are still emitted to `progress.html` with `data-status="not-planned"` and no Tasks list. The autonomous routine treats `not-planned` as a stop condition: it writes a blocker (`<blockquote class="blocker">phase needs a plan — run /plan-phase &lt;N&gt;</blockquote>`) and stops. This keeps `progress.html` a complete index of the project's phase pipeline rather than a partial view of just-the-planned-ones.

## Arguments

Parse `$ARGUMENTS` positionally:

1. **phases-dir** (optional, default `docs/phases/`) — directory containing `phase-<N>-<slug>.html` files.
2. **output-path** (optional, default `docs/progress.html`) — where to write the index.

## Step 0: Discover phase docs and PRD phases

**Phase docs:** Glob `<phases-dir>/phase-*.html`. The expected filename pattern is `phase-<N>-<slug>.html` where:

- `<N>` is an integer or short alphanumeric tag (zero-indexed allowed: `phase-0-skeleton.html` is valid; sub-phases like `phase-1b-topics.html` are valid).
- `<slug>` is kebab-case.

**PRD phases:** Read `docs/prd.html` (or wherever the PRD lives — fall back to `prd.html` at the repo root if `docs/prd.html` is absent). Find the Implementation Phases section and extract every phase heading, which has the shape:

```html
<h3 class="phase" data-phase="<N>">Phase <N>: <name></h3>
```

Read the phase number from `data-phase` and the phase name from the heading text after the colon. Preserve the name verbatim. (Per the PRD contract, time estimates are never inside the heading text — they live in a following `<p class="meta">` — so the heading text after the colon is already the clean name.) Produce an ordered list of `(number, name)` tuples.

If `docs/prd.html` cannot be found or parsed, fall back to "phase docs only" mode and warn the user that `progress.html` may not be a complete index. Do not stop.

If neither phase docs nor PRD phases are found, stop and tell the user to author at least one PRD or run `/plan-phase` first.

**Merge:** for each PRD phase, look for a matching phase doc by phase number. The result is a unified ordered list where every entry is either:
- **planned** (phase doc exists) — full metadata available;
- **not planned** (PRD lists it, no phase doc) — only `(number, name)` available.

Sort by phase number ascending. Phase 0 comes before Phase 1; sub-phases like `1b` sort immediately after `1` and before `2`.

## Step 1: Parse each phase doc

For each *planned* phase (phase doc exists), extract by parsing the HTML:

| Field | Source in phase doc |
|---|---|
| **Phase number** | filename (`phase-<N>-...`) |
| **Slug** | filename (`phase-<N>-<slug>.html`) |
| **Phase name** | text after the colon in `<h1 data-phase="<N>">Phase <N>: <name></h1>` |
| **Summary** | first sentence of the first `<p>` after the `<h2>Phase Description</h2>` heading |
| **Dependencies** | text content of `<dd data-field="dependencies">…</dd>` inside `<dl class="phase-meta">` |
| **Tasks** | every `<h4 class="task" data-task="<X>">Task <X>: <name></h4>` heading, in document order. Ignore `<h3 class="milestone">` wrappers — milestones are organizational only and not tracked in `progress.html`. |

**Task description**: use the heading text after `Task <X>:` verbatim as the one-line description. Example: `<h4 class="task" data-task="3">Task 3: Add EF Core migration for FTS5</h4>` → `Task 3: Add EF Core migration for FTS5`.

**Task numbering check**: tasks must be numbered contiguously starting at 1 within each phase (read from `data-task`). If you find a gap or a duplicate, stop and report the inconsistency — don't paper over it. The user needs to fix the phase doc.

**Slug consistency check**: the slug in the filename should not be contradicted elsewhere in the doc. Trust the filename — it's what the autonomous routine resolves against.

## Step 2: Check overwrite safety

Read the existing `output-path` if it exists. Compare phase metadata (numbers + slugs) to what you parsed:

- **No existing file**: proceed.
- **Existing file with the same phases and slugs**: regenerate (the user is expected to re-run this whenever phase docs change).
- **Existing file with different phases or slugs**, or with tasks whose status is not `todo` that wouldn't be preserved: stop and ask the user. Specifically warn if regenerating would clobber `data-status="done"` / `data-status="skipped"` marks or `<blockquote class="blocker">` lines.

When regenerating to preserve state, lift each task's `data-status` value (`done` / `skipped`) and any `<blockquote class="blocker">` onto matching tasks (matched by phase number + task number) in the freshly rendered output. If a previously-tracked task no longer exists in the phase doc, leave a one-line note in the user-facing report so they can verify the deletion was intentional.

## Step 3: Render `progress.html`

Emit a standalone HTML document. The status attributes are the machine-readable contract the autonomous routine reads and updates in place — keep them exact.

**Status conventions (what the routine reads/writes):**

- Phase `<section>` carries `data-status`: `not-started` | `in-progress` | `complete` | `not-planned`.
- Task `<li>` carries `data-status`: `todo` (eligible to pick up) | `done` (validation passed, committed) | `skipped` (a human set it; agent moves past it).
- A blocker is a `<blockquote class="blocker">…</blockquote>` nested inside the task `<li>`; the agent will not retry the task until it is removed.

The routine's edits are in-place attribute/blockquote changes (`data-status="todo"` → `data-status="done"`, add/remove a blocker). `/create-progress` preserves those edits across regeneration.

**Document to emit:**

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>PRD Progress Tracker</title>
  <style>
    :root { --fg:#1a1a1a; --muted:#666; --accent:#2563eb; --done:#16a34a; --warn:#dc2626; --rule:#e5e7eb; }
    body { font:16px/1.6 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif; color:var(--fg); max-width:60rem; margin:2rem auto; padding:0 1.25rem; }
    h1 { font-size:2rem; border-bottom:2px solid var(--rule); padding-bottom:.3rem; }
    h2 { font-size:1.4rem; margin-top:2.5rem; }
    section.phase { border:1px solid var(--rule); border-radius:8px; padding:1rem 1.25rem; margin:1.25rem 0; }
    section.phase > h2 { margin-top:0; }
    .badge { display:inline-block; font-size:.75rem; font-weight:600; padding:.1rem .5rem; border-radius:999px; vertical-align:middle; }
    section.phase[data-status="not-started"] .badge { background:#eef; color:var(--accent); }
    section.phase[data-status="in-progress"] .badge { background:#fff7ed; color:#c2410c; }
    section.phase[data-status="complete"] .badge { background:#ecfdf5; color:var(--done); }
    section.phase[data-status="not-planned"] .badge { background:#fef2f2; color:var(--warn); }
    .meta { color:var(--muted); font-size:.9rem; margin:.2rem 0; }
    ul.tasks { list-style:none; padding-left:0; }
    ul.tasks > li.task { padding:.15rem 0; }
    ul.tasks > li.task::before { content:"☐ "; color:var(--muted); }
    ul.tasks > li.task[data-status="done"]::before { content:"☑ "; color:var(--done); }
    ul.tasks > li.task[data-status="done"] { color:var(--muted); text-decoration:line-through; }
    ul.tasks > li.task[data-status="skipped"]::before { content:"⊘ "; color:var(--muted); }
    blockquote.blocker { border-left:3px solid var(--warn); background:#fef2f2; color:#7f1d1d; margin:.4rem 0 .4rem 1rem; padding:.4rem .75rem; }
    code { font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; background:#f6f8fa; padding:.1rem .3rem; border-radius:4px; }
  </style>
</head>
<body>

<h1>PRD Progress Tracker</h1>

<p>This file is the source of truth for the autonomous routine. The routine reads it on every run, finds the next task whose <code>data-status="todo"</code>, executes it, and updates this file (flipping the task to <code>data-status="done"</code> or adding a <code>blockquote.blocker</code>) in the same commit. Keep it accurate — if it drifts from reality, the agent drifts.</p>

<p>This file is <strong>generated</strong> from the phase docs in <code>docs/phases/</code> by <code>/create-progress</code>. The phase docs are the source of truth for slugs, branch names, dependencies, and tasks. To change any of those, edit the phase doc and re-run <code>/create-progress</code>. The routine itself updates task statuses (<code>todo</code> → <code>done</code>, blocker blockquotes) directly in this file — those edits are preserved across regeneration.</p>

<h2>Conventions</h2>
<ul>
  <li><strong>Phase docs location:</strong> <code>docs/phases/phase-&lt;N&gt;-&lt;slug&gt;.html</code></li>
  <li><strong>Phase branch naming:</strong> <code>phase-&lt;N&gt;-&lt;slug&gt;</code></li>
  <li><strong>Commit message format:</strong> <code>phase &lt;N&gt; task &lt;X&gt;: &lt;short description&gt;</code></li>
  <li><strong>PR title format:</strong> <code>Phase &lt;N&gt;: &lt;phase name&gt;</code></li>
  <li><strong>PR opens when:</strong> the last task in a phase is <code>done</code></li>
  <li><strong>Task numbering:</strong> contiguous within a phase (Task 1, Task 2, …), reset at each new phase; matches the <code>data-task</code> values in the phase doc.</li>
</ul>

<h2>Phase statuses (<code>section.phase[data-status]</code>)</h2>
<ul>
  <li><code>not-started</code> — phase doc exists, no tasks done yet</li>
  <li><code>in-progress</code> — at least one task done, others outstanding</li>
  <li><code>complete</code> — every task in the phase is <code>done</code></li>
  <li><code>not-planned</code> — phase listed in PRD but has no phase doc; the routine writes a blocker and stops on this phase until <code>/plan-phase &lt;N&gt;</code> is run</li>
</ul>

<h2>Task statuses (<code>li.task[data-status]</code>)</h2>
<ul>
  <li><code>todo</code> — not started, eligible to be picked up</li>
  <li><code>done</code> — complete (validation passed, committed)</li>
  <li><code>skipped</code> — skipped by a human; agent moves past it</li>
  <li><code>blockquote.blocker</code> inside a task — blocked; agent will not retry until removed</li>
</ul>

<h2>How to resolve a blocker</h2>
<p>The agent leaves a <code>&lt;blockquote class="blocker"&gt;</code> inside any task it couldn't finish. To unblock:</p>
<ol>
  <li>Read the blocker text on the phase branch.</li>
  <li>Either fix the underlying issue (edit code, add an env var, clarify the phase doc), or change the task description to be more specific.</li>
  <li>Delete the <code>blockquote.blocker</code> element.</li>
  <li>Commit and push to the phase branch.</li>
  <li>The next routine run will pick the task up again.</li>
</ol>
<p>To skip a task entirely, set its <code>data-status</code> to <code>skipped</code>.</p>

<h2>Phases</h2>

<!-- One <section> per planned phase, in ascending phase-number order: -->
<section class="phase" data-phase="<N>" data-status="not-started">
  <h2>Phase <N>: <phase name> <span class="badge">not started</span></h2>
  <p class="meta"><strong>Branch:</strong> <code>phase-<N>-<slug></code></p>
  <p class="meta"><strong>Phase doc:</strong> <code>docs/phases/phase-<N>-<slug>.html</code></p>
  <p class="meta"><strong>Depends on:</strong> <dependency text from phase doc></p>
  <p class="meta"><strong>Summary:</strong> <one-sentence summary parsed from phase doc></p>
  <ul class="tasks">
    <li class="task" data-task="1" data-status="todo">Task 1: <task heading from phase doc></li>
    <li class="task" data-task="2" data-status="todo">Task 2: <task heading from phase doc></li>
    <li class="task" data-task="N" data-status="todo">Task N: <task heading from phase doc></li>
  </ul>
</section>

<!-- One <section> per unplanned phase (PRD lists it, no phase doc), interleaved in ascending order: -->
<section class="phase" data-phase="<N>" data-status="not-planned">
  <h2>Phase <N>: <phase name from PRD> <span class="badge">not planned</span></h2>
  <p class="meta"><strong>Branch:</strong> <em>to be determined by <code>/plan-phase &lt;N&gt;</code></em></p>
  <p class="meta"><strong>Phase doc:</strong> <em>none yet — run <code>/plan-phase &lt;N&gt;</code></em></p>
</section>

</body>
</html>
```

Notes on rendering:

- The `data-status` on each `<section class="phase">` and the badge text: `not-started` if no tasks are done, `in-progress` if some but not all are done, `complete` if all are done. Only relevant when regenerating an existing `progress.html` — fresh files are always `not-started`.
- Keep the badge text in sync with `data-status` (`not-started` → "not started", etc.). The `data-status` attribute is authoritative; the badge is cosmetic.
- Don't emit an estimated-diff/LOC field — phase docs don't track LOC, and an inaccurate estimate is worse than no estimate.
- Preserve the order of tasks as they appear in the phase doc (which is execution order — tasks run top to bottom).

## Step 4: Write and report

1. Write the rendered file to `output-path`.
2. Print a short summary to the user:
   - Number of phase docs found and aggregated.
   - For each phase: `Phase <N>: <name> (slug: <slug>) — <task-count> tasks`.
   - Any phases listed in the PRD (if you can determine that — by reading `docs/prd.html` if it exists) but missing a phase doc, so the user knows what still needs `/plan-phase`.
   - Any preserved `done` / `skipped` / blocker state (count is enough).
   - Any tasks dropped during regeneration (so the user can confirm intentional deletion).

## Quality criteria

- [ ] Output is a valid standalone HTML document (doctype, `<head>` with embedded `<style>`, `<body>`)
- [ ] Phase numbers and slugs come from filenames, never invented
- [ ] Each phase is a `<section class="phase" data-phase="<N>" data-status="…">`
- [ ] Each task is a `<li class="task" data-task="<X>" data-status="todo|done|skipped">`
- [ ] Branch names match `phase-<N>-<slug>` exactly
- [ ] Task numbers are contiguous starting at 1 per phase, and match `data-task` in the phase doc
- [ ] Existing completion state (`done`, `skipped`, `blockquote.blocker`) is preserved across regeneration when phase + task numbers still match
- [ ] No phase metadata is inferred or invented — if a field is missing from the phase doc, report it and stop rather than guess
