---
description: Scan codebase to give AI context understanding
---

# Scan Current Project Status and Context

## Objective

Build comprehensive understanding of the codebase by analyzing structure, recent activity, and environment health. CLAUDE.md is already in context — focus on what it does not cover.

## Process

### 1. Analyze Project Structure

Run `git ls-files` to list all tracked files. Use this to understand the directory layout, module boundaries, and what exists in each architectural layer.

### 2. Read Core Documentation (in parallel)

Read all of the following simultaneously:
- `docs/prd.html` (the PRD; fall back to `.agents/PRD.md` or `docs/PRD.md` if missing) — produced by `/create-prd`
- `docs/progress.html` (the phase/task tracker the autonomous routine drives) — produced by `/create-progress`
- `docs/phases/phase-*.html` (per-phase plans) — produced by `/plan-phase`; read at least the in-progress phase doc
- `.agents/DevPlan.md`
- `.agents/Summary.md`
- `README.md`
- `pyproject.toml` — dependencies, scripts, ruff config

These are standalone HTML documents with `data-phase` / `data-task` / `data-status` attributes — read those to determine which phase is active and which tasks are `done` vs. `todo`.

Skip CLAUDE.md — it is already loaded into context.

### 3. Read Key Source Files (in parallel)

Based on the file listing from step 1, read simultaneously:
- Main entry points (`src/main.py`, `src/app.py`, or equivalent)
- `src/config.py`
- `src/db/models.py` and `src/db/queries.py` (schema and data access)
- One representative file from each architectural layer (`src/engine/`, `src/synthesis/`, `src/scoring/`, `src/personas/`, `src/api/`)

Limit to ~15 files maximum. Prioritize files that define interfaces between layers.

### 4. Environment Health

Run these checks in parallel:
- `uv sync --check` — verify dependencies are installed and lock file is current
- Check whether `.env` exists (do not read its contents)
- `git status`
- `git log -15 --oneline`

Note any issues (missing deps, no .env, uncommitted work).

## Output Report

Provide a concise summary covering:

### Project Overview
- Purpose and current development phase
- What is built vs. what remains

### Architecture Status
- Which layers have real implementations vs. stubs/placeholders
- Key interfaces between layers
- Any deviations from the layer hierarchy defined in CLAUDE.md

### Tech Stack
- Languages, versions, and frameworks
- Package manager and build tools
- Testing setup and coverage status

### Environment Health
- Dependency state (synced or not)
- Configuration state (.env present or missing)
- Any warnings from the checks above

### Current State
- Active branch and uncommitted changes
- Recent development focus (from git log)
- Observations or concerns worth flagging

**Keep it scannable — bullet points, clear headers, no filler.**
