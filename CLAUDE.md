# WhiskeyTracker.Web — Claude Code Guide

## Project Overview
Personal whiskey inventory and tasting journal. ASP.NET Core 10 Razor Pages,
PostgreSQL, EF Core, Bootstrap 5. Deployed on Kubernetes on a Raspberry Pi
cluster (ARM64 Linux binaries on NFS, generic .NET runtime container).

## Workflow Rules
- Never commit directly to `main`. Branch as `feature/<desc>` or `fix/<desc>`.
- Use semantic commit messages: `feat:`, `fix:`, `chore:`, etc.
- PRs via `gh pr create --fill`. If GH CLI fails, check for `.github_token` in project root.
- Every feature/fix must evaluate whether unit tests in `WhiskeyTracker.Tests` are needed.
- Run `dotnet test` before shipping.

## Key Files
- `docs/product_requirements.md` — Full PRD with user stories and feature intent
- `docs/DEPLOYMENT.md` — K8s deployment guide
- `.antigravity/rules.md` — Workflow rules for the Antigravity agent (mirrors this file)
- `k8s/` — Kubernetes manifests

## What's Built vs. Planned
**Implemented:** Whiskey library CRUD, bottle inventory, tasting wizard, tasting
sessions, multi-user collections with invitations, infinity bottle tracking,
admin dashboard, Google OAuth, role-based auth, image upload, search/filter.

**Not yet built (see PRD for details):**
- Structured whiskey categorization (Country/Region/Mashbill as data fields)
- Granular tasting impressions (Nose/Palate/Finish as separate fields)
- Customizable dashboard with analytics modules
- Data export (CSV, printable reports)
- Saved filter combinations + dynamic filtering
- Global ratings averaged across users
- Stats engine (cost per dram, regional preferences)
- Social/sharing features, REST API, mobile/PWA
