# Agentes y entorno local

## Installation
Copy the files to **Unity project root** (alongside `Assets/`, `Packages/`, `ProjectSettings/`). Codex discovers `.agents/skills/*/SKILL.md` when started inside the repository. `AGENTS.md` is the persistent instruction file; `ARCHITECTURE.md` holds design decisions. Verify installation by asking Codex to list project skills and summarize architecture. Restart Codex if skills are not detected.

## Privacy / version control
Recommended: **version** `ARCHITECTURE.md`, `IMPLEMENTATION_PLAN.md` and task documents. Decide whether to version `AGENTS.md` and `.agents/skills` based on whether the team benefits; neither contains secrets. If you want them local only, add to `.git/info/exclude` (not `.gitignore`) and confirm `git status`. Do not exclude `Assets/*.meta`, `Packages/` or `ProjectSettings/`.

## Gentle AI
Open Gentle Shell in project root if it needs to read the repo. Ask it to propose changes against `ARCHITECTURE.md` without automatic edits. Do not assume it automatically discovers Codex skills: provide file paths explicitly.

## Codex first prompt
Read `AGENTS.md`, `ARCHITECTURE.md`, `IMPLEMENTATION_PLAN.md` and discover all project skills under `.agents/skills`. Do a read-only gap analysis of Phase 0 and Phase 1 against the actual Unity project. Do not edit, launch Unity, install packages, commit or push. Identify Inspector steps, readiness blockers and acceptance tests for AR horizontal plane detection + anchored cube placement in XR Simulation and Android.

## MCP
No MCP server is mandatory. Only add a Unity integration after checking publisher, permissions, version compatibility and ability to run safely with an Editor session. Avoid generic third-party setup scripts before core AR functionality runs.
