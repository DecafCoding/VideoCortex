---
description: "Create a comprehensive phase plan (the doc the autonomous routine will execute) with deep codebase analysis and research"
---

# Plan a phase

## Phase: $ARGUMENTS

## Mission

Transform a phase definition (drawn from `docs/prd.html`'s Implementation Phases section) into a **comprehensive phase doc** at `docs/phases/phase-<N>-<slug>.html`. A phase may bundle multiple features/tasks — this command names and plans the *phase*, not any single feature inside it. The output is the same file the autonomous routine reads on every run, written as a **standalone HTML document** (see "HTML Output Contract" below).

This command is the **slug and task authority** for the phase. It decides the kebab-case slug, picks the branch name (`phase-<N>-<slug>`), and authors the canonical task list. Downstream, `/create-progress` reads every phase doc under `docs/phases/` and aggregates the metadata + tasks into `docs/progress.html` — `progress.html` is a derived index, not an input. Run `/plan-phase` for each phase before running `/create-progress`.

Review `docs/prd.html` to identify the phase's scope and dependencies before planning.

**Core Principle**: We do NOT write code in this command. The goal is to produce a context-rich phase doc that enables one-pass implementation success for the execution agent (whether `/execute` or the autonomous routine).

**Key Philosophy**: Context is King. The plan must contain ALL information needed for implementation - patterns, mandatory reading, documentation, validation commands - so the execution agent succeeds on the first attempt.

## Planning Process

### Step 1: Phase Understanding

**Deep Phase Analysis:**

- Extract the core problem(s) the phase solves
- Identify user value and business impact
- Determine phase type: New Capability / Enhancement / Refactor / Bug Fix / Mixed (a phase can bundle multiple feature types)
- Assess complexity: Low/Medium/High
- Map affected systems and components
- List the discrete features/work items the phase bundles, if more than one

**Create or refine User Stories — one per feature in the phase:**

```
As a <type of user>
I want to <action/goal>
So that <benefit/value>
```

### Step 2: Codebase Intelligence Gathering

**Use specialized agents and parallel analysis:**

**1. Project Structure Analysis**

- Detect primary language(s), frameworks, and runtime versions
- Map directory structure and architectural patterns
- Identify service/component boundaries and integration points
- Locate configuration files (pyproject.toml, package.json, etc.)
- Find environment setup and build processes

**2. Pattern Recognition** (Use specialized subagents when beneficial)

- Search for similar implementations in codebase
- Identify coding conventions:
  - Naming patterns (CamelCase, snake_case, kebab-case)
  - File organization and module structure
  - Error handling approaches
  - Logging patterns and standards
- Extract common patterns for the phase's domain
- Document anti-patterns to avoid
- Check CLAUDE.md for project-specific rules and conventions

**3. Dependency Analysis**

- Catalog external libraries relevant to the phase
- Understand how libraries are integrated (check imports, configs)
- Find relevant documentation in `docs/` (or wherever the project keeps its docs)
- Note library versions and compatibility requirements

**4. Choose the slug and verify git branch**

Choose a kebab-case slug for this phase. This is *the* authoritative slug — `/create-progress` will read it back from the filename when it builds the index. Rules:

- Lowercase ASCII, words separated by `-`, no trailing punctuation.
- Aim for ≤ 20 chars, ideally 1–2 words. Drop parenthetical qualifiers from the PRD's phase name.
- Examples: "Skeleton" → `skeleton`, "Discovery round" → `discovery`, "Transcript worker (Q1: Apify)" → `transcript-worker`, "Polish and packaging" → `packaging`.
- Verify the slug doesn't collide with another phase doc already in `docs/phases/`.

Then verify you are on an appropriate branch — either the default branch (main or master) or the phase branch for the phase being planned. The branch name must match `phase-<N>-<slug>` using the slug you just chose. This is the same branch the autonomous routine will look for on origin — if the slug here diverges from the phase doc filename, the routine will create a separate branch and the manual planning work will be stranded.

The phase branch does not need to exist for planning — the autonomous routine creates it from the default branch on first execution. If you want to create it now (e.g., because you'll iterate on the phase doc alongside early implementation), detect the default branch (`git remote show origin | sed -n '/HEAD branch/s/.*: //p'`, falling back to whichever of `main` or `master` exists), then use `git checkout -b phase-<N>-<slug> <default-branch>` after pulling it.


**5. Testing Patterns**

- Identify test framework and structure (pytest, jest, etc.)
- Find similar test examples for reference
- Understand test organization (unit vs integration)
- Note coverage requirements and testing standards

**6. Integration Points**

- Identify existing files that need updates
- Determine new files that need creation and their locations
- Map router/API registration patterns
- Understand database/model patterns if applicable
- Identify authentication/authorization patterns if relevant

**Clarify Ambiguities — resolve before writing the plan:**

- If requirements are unclear at this point, ask the user to clarify **before you continue planning**
- Get specific implementation preferences (libraries, approaches, patterns)
- Resolve architectural decisions before proceeding
- **Critical for unattended execution:** the generated plan must contain **zero** "ask the user" or "confirm with user" steps inside tasks. An autonomous execution agent has no user to ask. Every ambiguity must be either (a) resolved with the user *now*, before the plan is written, or (b) decided by you with a clear default and documented in the NOTES section as an assumption the implementation will follow. If you cannot resolve an ambiguity either way, surface it to the user before generating the plan rather than embedding it in a task.

### Step 3: External Research & Documentation

**Use specialized subagents when beneficial for external research:**

**Documentation Gathering:**

- Research latest library versions and best practices
- Find official documentation with specific section anchors
- Locate implementation examples and tutorials
- Identify common gotchas and known issues
- Check for breaking changes and migration guides

**Technology Trends:**

- Research current best practices for the technology stack
- Find relevant blog posts, guides, or case studies
- Identify performance optimization patterns
- Document security considerations

**Compile Research References** (these become `<a href>` links in the "Relevant Documentation" section of the output):

- Library official docs, with the specific feature/section anchor and *why* it's needed
- Framework integration guides, with the specific section and *why* it matters

### Step 4: Deep Strategic Thinking

**Think Harder About:**

- How does this phase fit into the existing architecture?
- What are the critical dependencies and order of operations?
- What could go wrong? (Edge cases, race conditions, errors)
- How will this be tested comprehensively?
- What performance implications exist?
- Are there security considerations?
- How maintainable is this approach?

**Design Decisions:**

- Choose between alternative approaches with clear rationale
- Design for extensibility and future modifications
- Plan for backward compatibility if needed
- Consider scalability implications

**PRD Validation (if PRD exists):**
- Read PRD at `docs/prd.html`
- Verify plan preserves architectural patterns defined in PRD
- Validate against any architectural principles or design constraints in PRD

### Step 5: Plan Structure Generation

**Create a comprehensive plan as a standalone HTML document.** See the "HTML Output Contract" below for the document skeleton and the required parseable shapes, then the "Phase Doc Content" section for what goes in each part.

## HTML Output Contract

The phase doc is a **standalone HTML document** and a machine-readable contract. `/create-progress` parses it to build `docs/progress.html`, and the autonomous routine reads both. The following shapes are **required** — if they drift, the routine cannot find the phase or its tasks.

**Required parseable shapes:**

- **Phase title (exactly one):** `<h1 data-phase="<N>">Phase <N>: <phase name></h1>` — `<N>` is the phase number (preserve `0`; sub-phases like `1b` allowed). `/create-progress` reads the phase number from `data-phase` and the name from the text after the colon.
- **Phase metadata** lives in a `<dl class="phase-meta">`. The **Dependencies** entry is required and parsed:
  ```html
  <dl class="phase-meta">
    <dt>Phase Type</dt><dd data-field="type">New Capability</dd>
    <dt>Estimated Complexity</dt><dd data-field="complexity">Medium</dd>
    <dt>Primary Systems Affected</dt><dd data-field="systems">API, Data layer</dd>
    <dt>Dependencies</dt><dd data-field="dependencies">Phase 0 (skeleton); httpx</dd>
  </dl>
  ```
- **Tasks (parsed, numbered contiguously from 1):** `<h4 class="task" data-task="<N>">Task <N>: <short name></h4>`. The text after the colon is the one-line task description copied verbatim into `progress.html`.
- **Milestones (planning-time only, NOT tracked):** `<h3 class="milestone" data-milestone="<M>">Milestone <M>: <name></h3>`. `/create-progress` strips these — they are organizational scaffolding for human readers. Task numbering must stay contiguous *across* milestones.
- **Sections** are `<h2>` (Phase Description, Context References, Implementation Plan, Testing Strategy, Validation Commands, Acceptance Criteria, Completion Checklist, Notes).
- **Checklists** (acceptance criteria, completion checklist) use:
  ```html
  <ul class="checklist"><li data-checked="false">criterion text</li></ul>
  ```
- **Code, commands, and VALIDATE snippets** go in `<pre><code>…</code></pre>` (or inline `<code>` for short commands).

**Document skeleton** — emit this scaffold, filling `<body>` per "Phase Doc Content":

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Phase <N>: <phase name></title>
  <style>
    :root { --fg:#1a1a1a; --muted:#666; --accent:#2563eb; --done:#16a34a; --warn:#dc2626; --rule:#e5e7eb; }
    body { font:16px/1.6 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif; color:var(--fg); max-width:62rem; margin:2rem auto; padding:0 1.25rem; }
    h1 { font-size:2rem; border-bottom:2px solid var(--rule); padding-bottom:.3rem; }
    h2 { font-size:1.5rem; margin-top:2.5rem; border-bottom:1px solid var(--rule); padding-bottom:.2rem; }
    h3.milestone { font-size:1.25rem; margin-top:2rem; color:var(--accent); }
    h4.task { font-size:1.1rem; margin-top:1.5rem; }
    dl.phase-meta { display:grid; grid-template-columns:max-content 1fr; gap:.25rem 1rem; }
    dl.phase-meta dt { font-weight:600; color:var(--muted); }
    code, pre { font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace; }
    pre { background:#f6f8fa; padding:1rem; border-radius:6px; overflow:auto; }
    code { background:#f6f8fa; padding:.1rem .3rem; border-radius:4px; }
    pre code { background:none; padding:0; }
    ul.checklist { list-style:none; padding-left:0; }
    ul.checklist > li::before { content:"☐ "; color:var(--muted); }
    ul.checklist > li[data-checked="true"]::before { content:"☑ "; color:var(--done); }
    .validate { color:var(--done); font-weight:600; }
  </style>
</head>
<body>
  <!-- Phase doc content goes here -->
</body>
</html>
```

## Phase Doc Content

Fill the `<body>` with the following, using the required shapes above. The HTML comments show where each piece goes; replace `<…>` placeholders with real content.

1. **Phase title** — `<h1 data-phase="<N>">Phase <N>: <phase name></h1>`, followed by a short `<p>` reminder:
   > The following plan should be complete, but validate documentation, codebase patterns, and task sanity before implementing. Pay special attention to naming of existing utils, types, and models — import from the right files.

2. **`<h2>Phase Description</h2>`** — what this phase delivers, its purpose, and the user/business value. If it bundles multiple features, list them and how they relate.

3. **`<h2>User Stories</h2>`** — one or more stories (standard `As a … / I want to … / So that …` format).

4. **`<h2>Problem Statement</h2>`** — the specific problem or opportunity this phase addresses.

5. **`<h2>Solution Statement</h2>`** — the proposed approach and how it solves the problem.

6. **`<h2>Phase Metadata</h2>`** — the `<dl class="phase-meta">` shown in the contract (Phase Type, Estimated Complexity, Primary Systems Affected, Dependencies). Dependencies should list external libraries/services AND prior phases this depends on.

7. **`<h2>Context References</h2>`** with these subsections:
   - **`<h3>Relevant Codebase Files — READ THESE BEFORE IMPLEMENTING</h3>`** — a `<ul>` of files with line numbers and *why*, e.g. `<li><code>path/to/file.py</code> (lines 15-45) — Why: pattern for X to mirror</li>`.
   - **`<h3>New Files to Create</h3>`** — a `<ul>` of new files and their purpose.
   - **`<h3>Relevant Documentation — READ BEFORE IMPLEMENTING</h3>`** — a `<ul>` of `<a href>` links with the specific section anchor and *why*.
   - **`<h3>Patterns to Follow</h3>`** — actual code examples from the project in `<pre><code>` (Naming Conventions, Error Handling, Logging, Other).

8. **`<h2>Implementation Plan</h2>`** — see "Implementation Plan rules" below. Start it with:
   ```html
   <p><strong>Rendering:</strong> Flat | Milestones — see Step 6. Default Flat.</p>
   <p><strong>Rationale:</strong> <why this rendering fits — task count, coupling, sub-layers></p>
   ```
   Then the tasks (and milestones, if chosen), each task as `<h4 class="task" data-task="<N>">Task <N>: <name></h4>` followed by its body.

9. **`<h2>Testing Strategy</h2>`** — Unit Tests, Integration Tests, Edge Cases (`<h3>` subsections), based on the project's framework and patterns found in Step 2.

10. **`<h2>Validation Commands</h2>`** — Levels 1–5 as `<h3>` subsections with `<pre><code>` command blocks (see "Validation Commands" detail below).

11. **`<h2>Acceptance Criteria</h2>`** — a `<ul class="checklist">` of specific, measurable criteria (all `data-checked="false"` at authoring time).

12. **`<h2>Completion Checklist</h2>`** — a `<ul class="checklist">` mirroring the per-level validation.

13. **`<h2>Notes</h2>`** — design decisions, trade-offs, and any assumptions made when ambiguities were resolved with defaults rather than user clarification.

### Implementation Plan rules

**Tasks always execute one at a time, top to bottom**, regardless of how they're rendered. Each task has its own inline VALIDATE that proves it works in isolation — this is the immediate gate after the task completes. When tasks have natural sub-groupings (data layer → service layer → API layer), they may be wrapped under a **Milestone** (`<h3 class="milestone">`) with a **compound validation checkpoint** that proves cross-task integration. The milestone checkpoint is *additive* — it does not replace any task's individual VALIDATE.

**Milestones are a planning-time artifact only.** They live in the phase doc to help a human reviewer scan the work. The autonomous routine and `progress.html` do not track milestones — only tasks. Task numbering must remain contiguous across milestones (`Task 1`, `Task 2`, … `Task N`) so it lines up one-to-one with `progress.html`.

**Task body keywords** (use information-dense keywords for clarity):

- **CREATE**: New files or components
- **UPDATE**: Modify existing files
- **ADD**: Insert new functionality into existing code
- **REMOVE**: Delete deprecated code
- **REFACTOR**: Restructure without changing behavior
- **MIRROR**: Copy pattern from elsewhere in codebase

**Task body shape** — after each `<h4 class="task" …>` heading, render a `<ul>` of the fields that apply:

```html
<h4 class="task" data-task="1">Task 1: Create config module</h4>
<p>Describe what this task accomplishes and why it's needed.</p>
<ul>
  <li><strong>IMPLEMENT:</strong> specific implementation detail</li>
  <li><strong>PATTERN:</strong> reference to existing pattern — <code>file:line</code></li>
  <li><strong>IMPORTS:</strong> required imports and dependencies</li>
  <li><strong>GOTCHA:</strong> known issues or constraints to avoid</li>
  <li><span class="validate"><strong>VALIDATE:</strong></span> <code>executable validation command — must run successfully the moment this task completes</code></li>
</ul>
```

**Milestone shape** (Milestones rendering only):

```html
<h3 class="milestone" data-milestone="1">Milestone 1: <name></h3>
<p>One-line summary of what this milestone achieves.</p>
<p><strong>Validation checkpoint:</strong> compound assertion that proves cross-task integration — tests behaviors that emerge from tasks composed together; does NOT duplicate any single task's VALIDATE.</p>
<!-- Tasks belonging to this milestone follow as <h4 class="task" …> -->
```

### Task Authoring Rules (mandatory)

These rules ensure the plan can execute without human intervention:

1. **No "ask the user" / "confirm with user" steps inside tasks.** Every decision must be made at plan time. If a task body would say "ask the user", instead pick a sensible default, document it in the Notes section as an explicit assumption, and proceed.
2. **No forward references.** A task that uses something another task creates must come *after* that task. If unavoidable in the chosen ordering (e.g., an app factory that mounts routes not yet authored), add an explicit sub-step in the earlier task to create a stub the later task fills in.
3. **Validation commands must be non-interactive and idempotent.** No `kill %1`, no bash job control, no commands that require the operator to read output and decide. If a smoke test needs a server running, write a single shell snippet that starts it in the background, polls until ready, runs the assertions, and tears it down — all in one command that exits 0 on success and non-zero on failure.
4. **Every task ends with a VALIDATE** (the `<li>` with the `validate` span). A task with no executable validation cannot be safely automated.
5. **No deferred validation.** A task's VALIDATE must be runnable *the moment that task completes* — never "see the test in Task N when it lands later." If the dedicated test harness isn't in place yet, use an inline one-liner in the project's primary language that imports and exercises the new module. Examples for a config module that loads a `Settings` object:
   ```
   # Python
   VALIDATE: python -c "from myapp.config import Settings; s = Settings(); assert s.host == '127.0.0.1' and s.api_key is None"

   # Node / TypeScript
   VALIDATE: node -e "const {Settings} = require('./src/config'); const s = new Settings(); if (s.host !== '127.0.0.1' || s.apiKey != null) process.exit(1)"

   # Go
   VALIDATE: go run ./cmd/validate-config  # tiny program that loads Settings and asserts
   ```
   The dedicated test files added later are the *durable regression suite* — they don't substitute for the per-task gate. Both belong in the plan.
6. **Milestone validation checkpoints are compound, not substitute.** If a milestone's checkpoint is the *only* validator for a task within it, that task is missing its own gate and rule 5 is violated.

### Final milestone (mandatory): Commit, push, and open PR

Every plan ends with a final task that commits, pushes the branch, and opens the PR. An autonomous execution loop only ends here. Render it as the last `<h4 class="task" …>` (under a final `<h3 class="milestone">Commit, push, and open PR</h3>` if using Milestones rendering):

```html
<h4 class="task" data-task="<last>">Task <last>: Commit, push, and open PR</h4>
<p>After every prior validation checkpoint has passed:</p>
<ul>
  <li><strong>IMPLEMENT:</strong> stage and commit uncommitted changes with a descriptive message summarizing the phase; push with <code>git push -u origin &lt;branch-name&gt;</code>; open a PR with <code>gh pr create --head &lt;branch-name&gt; --title "&lt;title&gt;" --body "&lt;body&gt;"</code> (omitting <code>--base</code> targets the repo's default branch; pass <code>--base &lt;default-branch&gt;</code> explicitly if needed).</li>
  <li><strong>PR title format:</strong> <code>Phase &lt;N&gt;: &lt;phase name&gt;</code> (e.g., <code>Phase 1: Foundation</code>) — names the phase, not any individual feature.</li>
  <li><strong>PR body format:</strong> copy the Acceptance Criteria as a checklist with each box checked off, followed by a <code>Notes</code> section listing assumptions/trade-offs decided during execution.</li>
  <li><strong>GOTCHA:</strong> if <code>gh</code> is not installed/authenticated, this step fails — document installing/authenticating <code>gh</code> as a precondition at the top of the plan if it isn't part of standard tooling.</li>
  <li><span class="validate"><strong>VALIDATE:</strong></span> <code>gh pr view --json number,title,state,headRefName</code> returns the new PR with <code>"state": "OPEN"</code> and the correct <code>headRefName</code>.</li>
</ul>
```

### Validation Commands detail

Under `<h2>Validation Commands</h2>`, emit `<h3>` subsections. Execute every command to ensure zero regressions and 100% phase correctness.

- **Level 1: Syntax & Style** — lint (0 errors) and format check, in `<pre><code>` blocks. Fill in real commands from the project's toolchain (discovered in Step 2 from `pyproject.toml` / `package.json` / `CLAUDE.md`). Examples: Python+uv+ruff → `uv run ruff check .` / `uv run ruff format --check .`; Node+ESLint+Prettier → `npm run lint` / `npm run format:check`; Go → `go vet ./...` / `gofmt -l .`. Expected: exit code 0.
- **Level 2: Unit Tests** — project-specific unit test commands.
- **Level 3: Integration Tests** — project-specific integration test commands.
- **Level 4: Manual Validation** — phase-specific, **non-interactive shell snippets** (one block, exits 0 on success / non-zero on failure). No `kill %1`, no operator-read-and-decide steps. If a server is needed, start it in the background, poll until ready (`curl -fsS` loop with timeout), run assertions, then tear down.
- **Level 5: Additional Validation (Optional)** — MCP servers or additional CLI tools if available.

### Step 6: Task Grouping (rendering decision)

**Tasks are always executed one at a time, top to bottom.** The autonomous routine does one task per run; `/execute` does one task at a time interactively. This is non-negotiable — there is no "whole phase in one pass" execution mode. Each task carries its own VALIDATE gate.

What this step decides is purely a **rendering choice**: do you wrap tasks in `<h3 class="milestone">` headings inside the phase doc, or list the `<h4 class="task">` headings flat? Milestones are organizational scaffolding for human readers; the routine and `/execute` ignore them.

First draft your tasks as a flat list (mentally or in scratch) so you can see the full work surface. Then decide:

1. **Flat (default)** — Render `<h4 class="task" data-task="N">` headings directly under `<h2>Implementation Plan</h2>` with no milestone wrappers, except for the mandatory final commit-push-PR task. Choose when:
   - The phase is small enough that grouping is noise (≤ ~6 tasks)
   - Tasks are independent (each end-to-end testable on its own)
   - The work doesn't have natural sub-layers

2. **Milestones** — Group tasks under 2+ `<h3 class="milestone">` headings, each with its own *compound* validation checkpoint that adds to (not replaces) per-task VALIDATEs. Choose when:
   - The phase has natural sub-layers (e.g., data layer → service layer → API layer → tests)
   - Some tasks are tightly coupled within a group but loosely coupled across groups
   - Mid-to-high complexity where a flat list would be too long to scan

**Default to Flat** unless the phase clearly benefits from milestone grouping.

**Apply the decision to the Implementation Plan section:**

- Fill in the **Rendering** and **Rationale** lines at the top of the section.
- Render every task as `<h4 class="task" data-task="N">Task N: <name></h4>` — under an `<h3 class="milestone">` if you chose Milestones, or directly under the Implementation Plan `<h2>` if Flat.
- For Milestones rendering: each milestone gets a one-line summary and a **Validation checkpoint** that tests integration emerging from its tasks — never just a restatement of a task's VALIDATE.
- Number tasks contiguously (no gaps, no resets across milestones) so a flat reading order is preserved and `/create-progress` can copy them into `progress.html` 1:1.
- **Always include the final "Commit, push, and open PR" task** regardless of rendering choice — this is what makes the plan terminate cleanly under unattended execution. If using Milestones, wrap it in a final `<h3 class="milestone">Commit, push, and open PR</h3>`.

## Output Format

**Filename**: `docs/phases/phase-{N}-{slug}.html`

- `{N}` is the phase number (matches the PRD's phase numbering exactly — preserve `0` if the PRD starts at Phase 0)
- `{slug}` is the kebab-case slug chosen in Step 2 (e.g., `foundation`, `auth`, `search-api`)
- Examples: `docs/phases/phase-1-foundation.html`, `docs/phases/phase-2-auth.html`, `docs/phases/phase-3-search-api.html`
- A phase may contain multiple features/tasks — the slug names the *phase*, not any individual feature inside it
- This filename is the canonical source for the phase's slug — `/create-progress` parses it back out when building `progress.html`. The autonomous routine then reads `progress.html` to find this same phase doc, so the slug must remain stable once chosen

**Directory**: Create `docs/phases/` if it doesn't exist

## Quality Criteria

### Output Validity

- [ ] Output is a valid standalone HTML document (doctype, `<head>` with embedded `<style>`, `<body>`)
- [ ] Exactly one `<h1 data-phase="<N>">` with the phase number; phase name follows the colon
- [ ] Every task is `<h4 class="task" data-task="<N>">Task <N>: …</h4>` with contiguous numbering from 1
- [ ] Dependencies are in `<dd data-field="dependencies">` inside the `<dl class="phase-meta">`
- [ ] Milestones (if any) use `<h3 class="milestone">` and don't break contiguous task numbering

### Context Completeness

- [ ] All necessary patterns identified and documented
- [ ] External library usage documented with links
- [ ] Integration points clearly mapped
- [ ] Gotchas and anti-patterns captured
- [ ] Every task has executable validation command

### Implementation Ready

- [ ] Another developer could execute without additional context
- [ ] Tasks ordered by dependency (can execute top-to-bottom)
- [ ] Each task is atomic and independently testable
- [ ] Every task has an inline executable VALIDATE that runs at the moment the task completes — no deferral to a later task or to the testing milestone
- [ ] Milestone validation checkpoints (when present) are *compound* assertions about cross-task integration, never duplicates of any single task's VALIDATE
- [ ] Pattern references include specific file:line numbers

### Unattended Execution Ready

- [ ] Zero "ask the user" / "confirm with user" steps inside tasks — all ambiguities resolved at plan time or decided with documented defaults in Notes
- [ ] No forward references between tasks — anything a task uses, an earlier task creates (or stubs out for a later task to fill in)
- [ ] No deferred validation — every task's VALIDATE runs the moment that task completes
- [ ] Validation commands are non-interactive and idempotent (no bash job control like `kill %1`, no commands requiring an operator to read output and decide)
- [ ] Final task commits, pushes the branch, and opens the PR — the plan terminates cleanly without human intervention
- [ ] PR title and body format specified explicitly in the final task

### Pattern Consistency

- [ ] Tasks follow existing codebase conventions
- [ ] New patterns justified with clear rationale
- [ ] No reinvention of existing patterns or utils
- [ ] Testing approach matches project standards

### Information Density

- [ ] No generic references (all specific and actionable)
- [ ] URLs include section anchors when applicable
- [ ] Task descriptions use codebase keywords
- [ ] Validation commands are non-interactive executable

## Success Metrics

**One-Pass Implementation**: Execution agent can complete the phase without additional research or clarification

**Validation Complete**: Every task has at least one working validation command that runs at task completion (not deferred)

**Context Rich**: The plan passes "No Prior Knowledge Test" - someone unfamiliar with codebase can implement using only plan content

## Report

After creating the plan, provide:

- Summary of the phase and approach
- Full path to created plan file (e.g., `docs/phases/phase-1-foundation.html`)
- Complexity assessment
- Key implementation risks or considerations
- Whether tasks are grouped under milestones or rendered flat, and why
