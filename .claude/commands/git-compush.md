---
description: Commit staged changes and push to the current remote branch
---

# Commit and Push

## Step 0: Branch Safety Check

Run `git rev-parse --abbrev-ref HEAD` to see the current branch.

If the branch is `main`, `master`, `trunk`, or another protected default, **stop and ask the user before doing anything else**. Most repos use a PR-based workflow and committing straight to the default branch sidesteps review entirely. Confirm one of:

- "Yes, commit directly to `<branch>` — this repo allows it"
- "Create a feature branch first" — then ask for a branch name (or propose one based on the change), run `git checkout -b <name>`, and continue from Step 1

Skip the prompt only if the user explicitly directed the commit to the default branch in their current request (e.g. "push the hotfix straight to main").

## Step 1: Stage All Changes

Run `git status --porcelain` to see every modified and untracked file.

If the tree is clean, stop and tell the user there is nothing to commit.

Otherwise, stage all changes by name — list the paths explicitly in `git add <paths...>`. Do NOT use `git add -A` / `git add .` / `git add -u`; naming paths keeps accidental inclusions visible.

Before staging, scan the list for anything that looks sensitive or unrelated:

- Secrets: `.env`, `*.key`, `*.pem`, `credentials.*`, `*secret*`, `*token*`
- Large binaries, build artifacts, `__pycache__/`, `.venv/`, `node_modules/`
- Files that clearly belong to a different in-progress task

If any appear, skip them and flag to the user before continuing.

## Step 2: Review the Staged Diff

Run `git diff --cached --stat` and `git diff --cached` to confirm the staged set matches what you intended from Step 1. Use this to understand what changed and why.

## Step 3: Check Recent Commit Style

Run `git log --oneline -5` to see recent commit messages and match the project's conventional commit style.

## Step 4: Write the Commit Message

Draft a commit message following the conventional commit format used in this project:

```
<type>: <short summary>

<optional body — only if the change needs more explanation>

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
```

Types: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`

Keep the subject line under 72 characters. Focus on *why*, not *what*.

## Step 5: Commit

Run `git commit -m` with the message via a heredoc to preserve formatting.

## Step 6: Push

Run `git push` to push the current branch to its remote tracking branch. If the branch has no upstream yet, use `git push -u origin <branch-name>`.

## Step 7: Report to User

Provide a concise summary covering:
- **Branch** pushed to
- **Files changed** (count + key files)
- **What changed** — a plain-English summary of the staged changes
- The **commit message** used
