# Contributing Guide — CarpentryWorkshopVR

## The Golden Rule

> **Never push directly to `main`. Ever.**

`main` is the stable branch. All work goes through branches and Pull Requests (PRs). Breaking this rule breaks the project for everyone.

---

## One-Time Setup

Do this once when you first get access to the project.

```bash
# Clone the repository to your machine
git clone https://github.com/carpentryWorkshop/CarpentryWorkshopVR.git

# Go into the project folder
cd CarpentryWorkshopVR

# Set your name and email (shows up in commit history)
git config user.name "Your Name"
git config user.email "you@example.com"
```

Then open the project in Unity by pointing it at the `CarpentryWorkshopVR/` folder.

---

## Daily Workflow

Follow these steps every single time you start working. Do not skip steps.

### Step 1 — Get the latest version of main

Before you do anything, always sync with the latest code:

```bash
git checkout main
git pull origin main
```

### Step 2 — Create your branch

Never work directly on `main`. Create a new branch from it:

```bash
git checkout -b feature/my-feature-name
```

Name your branch using one of these prefixes:

| Prefix | When to use |
|---|---|
| `feature/` | Adding something new (a tool, a mechanic, a scene) |
| `fix/` | Fixing a bug or a broken asset |
| `chore/` | Cleanup, renaming files, updating settings |
| `refactor/` | Reorganizing without changing behaviour |

Examples:
```
feature/player-grab-mechanic
fix/missing-material-paintbooth
chore/rename-scene-files
```

Keep names short, lowercase, and use hyphens — no spaces.

### Step 3 — Do your work in Unity

Open Unity, make your changes. When you are done (or at a good checkpoint):

- **Save your scene** in Unity (`Ctrl+S`)
- **Close Unity** before running git commands (Unity locks files while open)

### Step 4 — Stage and commit your changes

```bash
# See what changed
git status

# Stage everything
git add .

# Commit with a clear message
git commit -m "feat: add player grab mechanic for tools"
```

**Commit message format:**

```
<type>: short description of what you did
```

Types: `feat`, `fix`, `chore`, `refactor`

Keep the message short but descriptive. Bad: `"changes"`. Good: `"fix: restore missing material on PaintBooth prefab"`.

Commit often — small commits are easier to review and easier to undo.

### Step 5 — Push your branch

```bash
git push origin feature/my-feature-name
```

If it's the first time pushing this branch, git may tell you to set the upstream — just run the command it suggests, or add `-u`:

```bash
git push -u origin feature/my-feature-name
```

### Step 6 — Open a Pull Request

1. Go to the repository on GitHub: https://github.com/carpentryWorkshop/CarpentryWorkshopVR
2. GitHub will show a banner: **"Your branch was recently pushed — Compare & pull request"**. Click it.
3. Set:
   - **Base branch:** `main`
   - **Title:** a clear description of what you did
   - **Description:** explain what changed and why
4. Assign at least **1 reviewer** from the team
5. Click **"Create Pull Request"**

Or from the command line:

```bash
gh pr create --base main --title "feat: add player grab mechanic" --body "Describe what you did and why"
```

### Step 7 — Wait for review, then merge

- **Do not merge your own PR** without at least 1 approval from a teammate
- The reviewer will either approve or leave comments for you to fix
- Once approved, click **"Merge pull request"** on GitHub
- After merging, **delete the branch** (GitHub shows a button for this after merge)

### Step 8 — Sync back to main

After your PR is merged, update your local `main`:

```bash
git checkout main
git pull origin main
```

You are now ready to start the next task from Step 1.

---

## PR Rules

- **1 approval required** before merging
- **Never merge your own PR** without a review (unless you are alone and unblocked with no reviewer available — tell the team)
- **Delete the branch** after merging — keep the repo clean
- **Never force push** (`git push --force`) to `main` or anyone else's branch

---

## Unity-Specific Rules

### Always commit `.meta` files

Every asset in Unity has a paired `.meta` file that stores its internal ID (GUID). If you add, move, or rename an asset, the `.meta` must be committed alongside it. Missing `.meta` files cause broken references (pink materials, missing prefabs).

```bash
# Wrong — only adds the asset, not the meta
git add Assets/Models/Table.fbx

# Correct — adds both
git add Assets/Models/Table.fbx Assets/Models/Table.fbx.meta

# Or just stage everything
git add .
```

### Never move or rename assets outside of Unity

If you rename or move a file using Windows Explorer / Finder / the terminal `mv` command, Unity loses the connection to the `.meta` file and breaks all references to that asset.

**Always rename and move assets inside Unity's Project window.** Unity will handle the `.meta` file for you. Then commit both files with `git add .`.

### Do not commit the Library/, Temp/, or Obj/ folders

These are already in `.gitignore` and should never be committed. They are auto-generated by Unity on every machine and are often hundreds of megabytes.

### Close Unity before running git commands

Unity locks files while it is open. Run `git add`, `git commit`, and `git push` with Unity closed to avoid file conflicts.

---

## Common Mistakes and How to Fix Them

### "I accidentally started working on main instead of a branch"

Don't panic. Move your changes to a new branch without losing anything:

```bash
# Create a new branch — your uncommitted changes come with it
git checkout -b feature/my-feature-name

# Now commit normally
git add .
git commit -m "feat: my work"
git push origin feature/my-feature-name
```

### "I forgot to pull before creating my branch — now I have merge conflicts"

```bash
# Fetch the latest main
git fetch origin main

# Merge it into your branch
git merge origin/main
```

Git will tell you which files have conflicts. Open them, look for the `<<<<<<`, `=======`, `>>>>>>>` markers, resolve them, then:

```bash
git add .
git commit -m "chore: resolve merge conflicts with main"
```

### "I pushed to the wrong branch"

First, push to the correct branch:

```bash
git push origin HEAD:feature/correct-branch-name
```

Then delete the wrong remote branch (only if it was your own branch — never delete someone else's):

```bash
git push origin --delete wrong-branch-name
```

### "I need to undo my last commit (not yet pushed)"

```bash
# Undo the commit but keep your changes staged
git reset --soft HEAD~1
```

### "I need to see what branch I'm on"

```bash
git status
# or
git branch
```

The current branch is shown at the top of `git status` and marked with `*` in `git branch`.

---

## Quick Reference

```bash
# Start of every task
git checkout main
git pull origin main
git checkout -b feature/your-feature

# Save your work
git add .
git commit -m "feat: what you did"

# Share your work
git push origin feature/your-feature

# Open PR from CLI
gh pr create --base main --title "feat: what you did"

# After PR is merged
git checkout main
git pull origin main
```
