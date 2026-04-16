# WhiskeyTracker — AI Agent Guidelines

This document contains instructions and context for AI agents working on the WhiskeyTracker codebase.

## Project Overview
Personal whiskey inventory and tasting journal. ASP.NET Core 10.0 Razor Pages, PostgreSQL, EF Core, Bootstrap 5. Deployed on Kubernetes on a Raspberry Pi cluster (ARM64 Linux binaries on NFS, generic .NET runtime container).

## Key Files
- `docs/product_requirements.md` — Full PRD with user stories and feature intent
- `docs/DEPLOYMENT.md` — K8s deployment guide
- `k8s/` — Kubernetes manifests

## Workflow & Git Release Manager Rules
When asked to "ship this," "create a PR," or "save my work," you must follow this strict sequence:

### Phase 1: Branching
1. **Check Status:** Run `git status` to see what has changed and `git branch --show-current` to identify the current branch.
2. **Main Branch Protection:** If the current branch is `main`, you **must** create a new branch. Direct commits to `main` are prohibited.
3. **Branch Naming:**
   - specific feature? -> feature/brief-description
   - bug fix? -> fix/brief-description
   - vague? -> ask the user for a branch name.
4. **Create Branch:** Run `git switch -c <branch_name>`. (If branch exists, use `git switch <branch_name>`).

### Phase 2: Committing
1. **Stage:** Run `git add .` (unless user specifies specific files).
2. **Commit:** Generate a semantic commit message based on the changes (e.g., `feat: add user login`, `fix: resolve nav overlap`, `chore: update dependencies`).
3. **Execute:** `git commit -m "<message>"`

### Phase 3: Pushing & PR
1. **Push:** Run `git push -u origin <branch_name>`.
2. **Create PR:** Use the GitHub CLI.
   - Run: `gh pr create --fill`
   - *Note:* `--fill` will auto-generate the title and body from your commit messages.
   - If `--fill` fails or is too vague, generate a title/body and run: `gh pr create --title "feat: <title>" --body "<summary>"`

## Protocol: Testing & Quality
1. **Always Consider Tests**: For every new feature or bug fix, evaluate if unit tests (in `WhiskeyTracker.Tests`) are required.
2. **Planning Phase**: Every implementation plan **must** include a "Verification Plan" with both "Automated Tests" and "Manual Verification" sections.
3. **Execution Phase**: Implement tests alongside the code. Ensure they follow established patterns.
4. **Verification Phase**:
   - Run `dotnet test` and report results.
   - Perform manual verification using browser simulation/screenshots when UI changes are involved.
   - Summarize all testing in your response or walkthrough document.

## Error Handling
- If `gh` commands fail due to auth, first check if the `.github_token` file exists in the root of the project. If it does, run the command with `$env:GH_TOKEN=$(cat .github_token);` prepended. If it still fails, stop and ask the user.
- If there are merge conflicts, **stop** and ask the user for guidance.

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
