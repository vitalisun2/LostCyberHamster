# Repository Workflow

## Scope

Use these instructions as the default workflow for any task that changes files in this repository.

This file complements, but does not replace:

- `README.md`
- `docs/rules/ai_workflow.md`
- `docs/rules/agent_tools.md`
- `docs/rules/ai_workflow_lessons.md`
- `.github/copilot-instructions.md`

If those files define project-specific implementation or testing steps, follow them inside each task worktree. This `AGENTS.md` defines task isolation, parallel execution, merge safety, and delivery discipline.

Read-only tasks may stop after reporting findings and must not commit or push.

## Repository Layout

- Git root: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025`
- Main Unity project: `LostCyberHamster/`
- Shared docs and plans: `docs/`
- Existing dedicated worktrees may live under `.claude/worktrees/`

## Required Context Before Editing

Before substantial code or content changes, read the relevant project guidance instead of guessing:

1. `docs/architecture_knowledge_base.md`
2. `docs/rules/ai_workflow_lessons.md`
3. `docs/rules/agent_tools.md`
4. The active plan in `docs/Planning/current/`, if the task is plan-driven
5. `.github/copilot-instructions.md`
6. The relevant Unity scripts, editor tools, assets, and tests for the task

## Thread Isolation

1. One chat or task must use its own dedicated git worktree.
2. One chat or task must use its own dedicated task branch.
3. Use `codex/<task-slug>` as the default branch naming scheme for task branches.
4. If the current worktree is shared with another task or already contains unrelated changes, create a new dedicated worktree before editing.
5. Prefer placing task worktrees under `.claude/worktrees/<task-slug>` unless the user asks for a different location.
6. Continue the task only inside that dedicated worktree.
7. Do not reuse a dirty worktree for a different task.

## Delivery Workflow

1. Read the request and inspect the relevant code, assets, plans, and tools before editing.
2. Implement the requested change with the smallest complete delta that matches existing Unity and project patterns.
3. If the task changes gameplay, bot behavior, progression, loading, saving, Addressables, UI flow, editor tooling, or other user-visible logic, add or update the minimum necessary automated tests or reproducible validation assets for that task.
4. Run the relevant validation for the files you changed.
5. If validation fails, fix the issue when it is directly related to the task. If it cannot be fixed safely, stop and report the blocker. Do not commit or push failing work.
6. Commit only the task's files from the dedicated worktree.
7. Push the task branch.
8. Merge completed task branches back sequentially, not simultaneously.
9. By default, completed task branches merge back into `main` sequentially after validation passes.

## Parallel Worktrees And Agents

- Prefer a parallel-first workflow by default: when independent work can safely proceed in parallel, do not serialize it without a concrete reason.
- Split a larger request by conflict risk first, then create separate worktrees only for non-overlapping work.
- Assign explicit ownership for each parallel slice: each agent or worktree should own a clear file set or subsystem boundary.
- Keep intersecting tasks in one shared task stream instead of spreading them across multiple worktrees that will conflict on the same files.
- If the user gives multiple distinct tasks in one message, treat that as the default signal to split the work into separate worktrees and branches when file ownership does not overlap.
- If several agents or worktrees are created for one initiative, keep them aligned on the same task assumptions and requested model settings unless the user explicitly asks otherwise.
- Run non-conflicting tasks in parallel, but merge them back sequentially after each task passes its required checks.
- While one completed task is being verified, reviewed, or merged, start the next safe non-conflicting task instead of waiting idly.
- Keep agents busy with bounded, non-overlapping work whenever there is useful parallel progress available.

## Unity-Specific Conflict Rules

- Do not assign two parallel agents to the same scene, prefab, ScriptableObject, or `.meta` file.
- Assume Unity YAML assets are merge-hostile by default; prefer one owner per serialized asset even when the edits look small.
- Treat these paths as high-conflict and single-owner unless the change is trivially partitioned:
  - `LostCyberHamster/Assets/AddressableAssetsData/`
  - `LostCyberHamster/ProjectSettings/`
  - `LostCyberHamster/Packages/manifest.json`
  - `LostCyberHamster/Packages/packages-lock.json`
  - `LostCyberHamster/Assets/**/*.unity`
  - `LostCyberHamster/Assets/**/*.prefab`
  - `LostCyberHamster/Assets/**/*.asset`
  - `LostCyberHamster/Assets/**/*.meta`
- Prefer splitting work by subsystem ownership, for example:
  - gameplay/runtime code in `Assets/Scripts/`
  - editor tooling in `Assets/Editor/`
  - UI code in `Assets/Scripts/UI/`
  - content and level data in `Assets/Content/`
  - documentation and plans in `docs/`
- If a task requires coordinated changes across code and content, keep that slice owned by one task stream instead of parallelizing it into conflicting edits.

## Interaction Continuity

- Treat active execution as the default mode: once work has started, continue unless the user explicitly asks to stop, pause, or redirect, or unless a real blocker makes continuation unsafe.
- User questions or small clarifications during execution should not pause valid background work by default.
- Keep background agents and worktrees running while answering the user when the work is still valid and non-conflicting.
- Interrupt ongoing execution only when the user clearly says to stop, the implementation path is wrong, or a decision is required that cannot be made safely from repository context.

## Quality Bar

- Before finalizing a task, run all relevant checks that exist in the repository for that kind of change.
- For C# script changes, perform at least a compilation-level validation. Use the fastest reliable check available in the environment.
- For gameplay, bot, or test-level changes, use the repository test workflow from `docs/rules/ai_workflow.md` and `docs/rules/agent_tools.md`, including the automation bridge and log analysis when applicable.
- For EditMode or PlayMode behavior changes, run the relevant Unity tests when they exist.
- For changes touching levels, Addressables, loading, or build configuration, run the relevant sync/build validation for those assets when available.
- If a needed check does not exist in the repository, do not invent heavyweight infrastructure in the same task unless the user explicitly asks for it.
- Never report a task as complete if required compile, test, or validation steps are failing.

## Git Rules

- Never commit unrelated changes that were already present in the worktree.
- Stage only files that belong to the requested task.
- Never force-push.
- Do not commit directly to `main`; create a dedicated `codex/` task branch first unless the user explicitly instructs otherwise.
- If the current worktree is dirty before the task starts, create a new dedicated worktree instead of trying to separate changes manually.
- If push fails because of authentication, permissions, branch protection, or remote divergence, stop and report the exact git error.
- Merge validated task branches back into `main` sequentially. If the user asks to postpone merge, keep the task branch and worktree until instructed otherwise.

## Final Report

Include:

- the validation result
- which checks were run
- the branch name used for the work
- the worktree path
- the commit hash, if a commit was created
- whether the push succeeded
- any remaining blockers or manual verification that still matters
