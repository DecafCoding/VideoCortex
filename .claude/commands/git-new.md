---
description: Switch to the default branch (main or master), pull latest, and create a new branch
argument-hint: <branch-name>
---

# New Branch

## Step 1: Get the Branch Name

The branch name is provided in: $ARGUMENTS

If no branch name was provided, ask the user what to name the branch before proceeding.

## Step 2: Determine the Default Branch

Detect the repo's default branch instead of assuming `master` or `main`:

```bash
git remote show origin | sed -n '/HEAD branch/s/.*: //p'
```

If there is no `origin` remote (or the command returns nothing), fall back to whichever of `main` or `master` exists locally. Refer to the resolved name as `<default-branch>` below.

## Step 3: Switch to the Default Branch and Pull

Run these sequentially:
1. `git checkout <default-branch>`
2. `git pull`

Report the result — how many commits were pulled and a one-line summary of what came in.

## Step 4: Create and Switch to the New Branch

Run `git checkout -b <branch-name>` using the name from Step 1.

## Step 5: Report to User

Confirm:
- **Now on branch:** the new branch name
- **Based on:** `<default-branch>` at the commit it was pulled to (show the short SHA and commit message)
