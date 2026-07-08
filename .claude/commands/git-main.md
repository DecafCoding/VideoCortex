---
description: Switch to the default branch (main or master) and pull latest changes
---

# Switch to Default Branch

## Step 1: Check for Uncommitted Changes

Run `git status --porcelain` to check for staged or unstaged changes.

If there is any output, warn the user that they have uncommitted changes and list the affected files. Ask if they want to proceed before continuing. If they decline, stop.

## Step 2: Determine the Default Branch

Detect the repo's default branch instead of assuming `main` or `master`:

```bash
git remote show origin | sed -n '/HEAD branch/s/.*: //p'
```

If there is no `origin` remote (or the command returns nothing), fall back to whichever of `main` or `master` exists locally: `git rev-parse --verify main` then `git rev-parse --verify master`. Use the first that resolves.

Refer to the resolved name as `<default-branch>` below.

## Step 3: Switch to the Default Branch

Run `git checkout <default-branch>`.

## Step 4: Pull Latest

Run `git pull` to get the latest changes.

## Step 5: Report to User

Confirm:
- **Now on branch:** `<default-branch>`
- **At commit:** short SHA and commit message from HEAD
- **Pulled:** how many new commits came in (or "already up to date")
