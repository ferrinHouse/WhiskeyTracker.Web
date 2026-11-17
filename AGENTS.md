# WhiskeyTracker — AI Agent Guidelines

This document contains instructions and context for AI agents working on the WhiskeyTracker codebase.

## Project Overview
Personal whiskey inventory and tasting journal. ASP.NET Core 10.0 Razor Pages, PostgreSQL, EF Core, Bootstrap 5. Deployed on Kubernetes (a Raspberry Pi cluster plus an amd64 node) from a multi-arch container image published to GHCR by CI; see `docs/DEPLOYMENT.md`.

## Key Files
- `docs/product_requirements.md` — Full PRD with user stories and feature intent
- `docs/DEPLOYMENT.md` — K8s deployment guide
- `k8s/` — Kubernetes manifests

## Workflow & Git Release Manager Rules
When asked to "ship this," "create a PR," or "save my work," follow this sequence:

### Phase 1: Branching
1. **Check Status:** Run `git status` to see what has changed and `git branch --show-current` to identify the current branch.
2. **Main Branch Protection:** `main` is protected, so if you are on it, create a new branch first; never commit to `main` directly.
3. **Branch Naming:**
   - specific feature? -> `feature/<brief-description>`
   - bug fix? -> `fix/<brief-description>`
   - vague? -> ask the user for a branch name.
4. **Create Branch:** Run `git switch -c <branch_name>`. (If branch exists, use `git switch <branch_name>`).

### Phase 2: Committing
Goal: commit only the files that belong to this change.
1. **Stage by name:** Run `git status --short`, then `git add <file> <file>`. Use `git add .` only if the user says "add everything".
2. **Never stage:** `.github_token`, `.env` files, `*.log`, `*.sql` backups, `appsettings.Local*.json`, or anything under `bin/` or `obj/`. If one shows up as untracked, stop and tell the user.
3. **Commit:** Generate a semantic commit message (e.g., `feat: add user login`, `fix: resolve nav overlap`, `chore: update dependencies`) and run `git commit -m "<message>"`.
4. **Check:** `git show --stat HEAD` lists only the files this change touched. Do this before pushing.

### Phase 3: Pushing & PR
1. **Push:** Run `git push -u origin <branch_name>`.
2. **Create PR:** Use the GitHub CLI.
   - Run: `gh pr create --fill`
   - If `--fill` fails or is too vague, generate a title/body and run: `gh pr create --title "feat: <title>" --body "<summary>"`

## Testing & Quality
For every feature or bug fix, decide whether unit tests are needed in `WhiskeyTracker.Tests`, and write them alongside the code following the existing patterns. Implementation plans include a "Verification Plan" with "Automated Tests" and "Manual Verification" sections.

Before reporting work as done, run `dotnet test` and report the actual result; for UI changes, also check the affected page in the running app (browser simulation/screenshots). Summarize all testing in your response or walkthrough document.

## Error Handling
- If `gh` commands fail due to auth, first check if the `.github_token` file exists in the root of the project. If it does, run the command with `$env:GH_TOKEN=$(cat .github_token);` prepended. If it still fails, stop and ask the user.
- If there are merge conflicts, stop and ask the user how to resolve them.

## What's Built vs. Planned
**Implemented:** Whiskey library CRUD, bottle inventory, tasting wizard, tasting sessions, multi-user collections with invitations, infinity bottle tracking, admin dashboard, Google OAuth, role-based auth, image upload, search/filter.

**Not yet built (see PRD for details):**
- Structured whiskey categorization (Country/Region/Mashbill as data fields)
- Granular tasting impressions (Nose/Palate/Finish as separate fields)
- Customizable dashboard with analytics modules
- Data export (CSV, printable reports)
- Saved filter combinations + dynamic filtering
- Global ratings averaged across users
- Stats engine (cost per dram, regional preferences)
- Social/sharing features, REST API, mobile/PWA
