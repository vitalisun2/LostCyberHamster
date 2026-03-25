# Repository Workflow

Single source of truth for ALL AI agents working in this repository: Claude Code, Codex, GitHub Copilot.

## Agent-Specific Setup

| Agent | How this file is loaded |
|---|---|
| Codex | Reads `AGENTS.md` automatically |
| Claude Code | Reads `CLAUDE.md` which includes this file via reference |
| GitHub Copilot | Reads `.github/copilot-instructions.md` which references this file |

All agents follow the same workflow rules. Agent-specific tool syntax may differ, but the process and discipline must be identical.

## Complementary Files

This file defines task isolation, parallel execution, merge safety, and delivery discipline. It complements:

- `docs/rules/ai_workflow.md` — iterative implementation cycle, testing, reporting
- `docs/rules/ai_workflow_lessons.md` — accumulated lessons (MUST read before substantial work)
- `docs/rules/agent_tools.md` — project tool catalog (automation bridge, log reader, etc.)
- `docs/architecture_knowledge_base.md` — architecture decisions and patterns
- `.github/copilot-instructions.md` — Unity/project-specific coding conventions

## Repository Layout

- Git root: repository root directory
- Main Unity project: `LostCyberHamster/`
- Shared docs and plans: `docs/`
- Task worktrees: `.claude/worktrees/<slug>/` (auto-ignored by git)

## Required Context Before Editing

Before substantial code or content changes, read (do not guess):

1. `docs/rules/ai_workflow_lessons.md` (mandatory every conversation)
2. `docs/architecture_knowledge_base.md`
3. `docs/rules/agent_tools.md`
4. The active plan in `docs/Planning/current/`, if the task is plan-driven
5. The relevant Unity scripts, editor tools, assets, and tests for the task

## Task Isolation

### Branch Naming

Unified prefix for all agents: `task/<slug>`.

Examples: `task/fix-switchlane-safety`, `task/add-levelkey-struct`, `task/cleanup-roofrun`.

Do not use agent-specific prefixes (`codex/`, `claude/`, `copilot/`).

### Worktree Rules

1. Every task that changes files gets its own dedicated git worktree and task branch.
2. Place worktrees under `.claude/worktrees/<slug>`.
3. Work only inside the dedicated worktree. Do not edit files in the main worktree while a task worktree is active.
4. Do not reuse a dirty worktree for a different task.
5. If the main worktree has uncommitted changes unrelated to the task, create a new worktree instead of trying to separate changes.

### Worktree Lifecycle

```
create worktree + branch
        |
    implement
        |
    validate
        |
   merge to main (sequential, one task at a time)
        |
   delete worktree + branch
```

After a task branch is merged to main:
- Delete the worktree directory.
- Delete the local task branch.
- Delete the remote task branch.
- Only the main worktree (repository root) should remain between tasks.

Small changes (docs-only, single-file fixes) may be committed directly to main when the user explicitly requests it.

## Delivery Workflow

1. Read the request and inspect relevant code, assets, plans, and tools before editing.
2. Implement the requested change with the smallest complete delta that matches existing project patterns.
3. If the task changes gameplay, bot behavior, progression, loading, saving, Addressables, UI flow, editor tooling, or other user-visible logic, add or update the minimum necessary automated tests or reproducible validation assets.
4. Run relevant validation for changed files.
5. If validation fails, fix the issue when directly related to the task. If it cannot be fixed safely, stop and report the blocker. Do not commit or push failing work.
6. Commit only the task's files from the dedicated worktree.
7. Push the task branch.
8. Merge back to main sequentially after validation passes.
9. Clean up worktree and branch after merge.

## Orchestrator Pattern

When receiving a complex task, the orchestrator (the main agent in conversation) should:

1. **Analyze** the task scope — identify independent sub-tasks.
2. **Split** by conflict risk — sub-tasks that touch different file sets can run in parallel.
3. **Assign** each sub-task to a background agent with clear scope, deliverable, and output location.
4. **Collect** results from all sub-agents when they complete.
5. **Synthesize** a comprehensive result from the collected outputs.

### When To Orchestrate

- The task requires analyzing multiple subsystems or files that exceed a single agent's practical context.
- The task can be decomposed into independent slices with no file overlap.
- The user explicitly asks for parallel execution.
- Research or analysis across the codebase would benefit from focused, scoped sub-queries.

### When NOT To Orchestrate

- The task is small or linear (single file, single concern).
- Sub-tasks share the same files — serialize instead of parallelize.
- The overhead of coordination exceeds the time saved.

### Analysis Workflow

For deep investigation, bug hunting, or codebase-wide research:

1. Orchestrator identifies the areas or file groups to analyze.
2. Creates focused sub-tasks, each with:
   - Clear scope (which files/classes/subsystems to examine).
   - Specific question to answer.
   - Output format (findings summary).
3. Spawns background agents in parallel — each analyzes its slice.
4. Each agent returns findings directly to the orchestrator.
5. Orchestrator reads all findings, resolves contradictions, and produces a comprehensive report for the user.

Example decomposition for a bug investigation:

```
Orchestrator: "SwitchLane fails on pattern X"
  -> Agent 1: analyze ActionGenerator — how SwitchLane candidates are produced
  -> Agent 2: analyze StateProjector — how safety is evaluated
  -> Agent 3: analyze StepExecutor — how the action fires at runtime
  -> Agent 4: study game engine TapMechanics + CollisionController — how SwitchLane actually works
Orchestrator: synthesize findings, identify root cause
```

### Implementation Workflow (Parallel)

For large implementation tasks with independent subsystems:

1. Orchestrator creates a worktree + branch per sub-task.
2. Each sub-agent works in its own worktree.
3. Sub-agents validate their changes independently.
4. Orchestrator merges completed sub-tasks to main sequentially.
5. Clean up worktrees and branches after each merge.

## Parallel Worktrees And Agents

- Prefer parallel-first: when independent work can safely proceed in parallel, do not serialize without a concrete reason.
- Split by conflict risk first, then create separate worktrees only for non-overlapping work.
- Assign explicit ownership: each agent or worktree owns a clear file set or subsystem boundary.
- Keep intersecting tasks in one shared task stream instead of spreading them across conflicting worktrees.
- If the user gives multiple distinct tasks in one message, treat that as a signal to split into separate worktrees when file ownership does not overlap.
- Run non-conflicting tasks in parallel, but merge back sequentially after each passes validation.
- While one task is being verified or merged, start the next safe non-conflicting task instead of waiting.

## Unity-Specific Conflict Rules

- Do not assign two parallel agents to the same scene, prefab, ScriptableObject, or `.meta` file.
- Treat Unity YAML assets as merge-hostile by default; prefer one owner per serialized asset even when the edits look small.
- High-conflict, single-owner paths:
  - `LostCyberHamster/Assets/AddressableAssetsData/`
  - `LostCyberHamster/ProjectSettings/`
  - `LostCyberHamster/Packages/manifest.json`, `packages-lock.json`
  - `LostCyberHamster/Assets/**/*.unity`, `*.prefab`, `*.asset`, `*.meta`
- Prefer splitting by subsystem ownership:
  - Gameplay/runtime code: `Assets/Scripts/`
  - Editor tooling: `Assets/Editor/`
  - UI code: `Assets/Scripts/UI/`
  - Content and level data: `Assets/Content/`
  - Documentation and plans: `docs/`
- If a task requires coordinated changes across code and content, keep it in one task stream.

## Interaction Continuity

- Treat active execution as the default: continue unless the user explicitly stops or a real blocker appears.
- User questions or small clarifications during execution should not pause valid background work.
- Keep background agents running while answering the user when the work is still valid.
- Interrupt only when: the user says stop, the path is wrong, or a decision is required that cannot be made safely from context.

## Quality Bar

- Before finalizing, run all relevant checks for that kind of change.
- For C# script changes: at minimum compilation-level validation.
- For gameplay/bot/test-level changes: use the iterative cycle from `docs/rules/ai_workflow.md` (automation bridge, log analysis).
- For EditMode/PlayMode behavior changes: run relevant Unity tests when they exist.
- For Addressables/build config changes: run sync/build validation when available.
- Do not invent heavyweight infrastructure in the same task unless the user explicitly asks.
- Never report a task as complete if required validation is failing.

## Git Rules

- Never commit unrelated changes already present in the worktree.
- Stage only files belonging to the requested task.
- Never force-push.
- Do not commit directly to `main` — create a `task/<slug>` branch first, unless the user explicitly instructs otherwise.
- If push fails (auth, permissions, protection, divergence), stop and report the exact error.
- Merge validated task branches back into `main` sequentially.

## Communication

- Respond in Russian (English only on explicit request).
- Be concise, no filler phrases, no emoji unless requested.
- After each task: short retrospective, add durable lessons to `docs/rules/ai_workflow_lessons.md`.

## Final Report

After completing a task, include:

- Validation result and which checks were run.
- Branch name and worktree path used.
- Commit hash (if created).
- Whether push succeeded.
- Remaining blockers or manual verification needed.
