# AI Rules

These rules are the shared source of truth for Codex, Claude Code, and Gemini when working on Amanah Drive.

## Project Source Of Truth

The main project plan is [README.md](../README.md). Read it before significant work and treat it as the primary reference for architecture, technologies, scope, goals, repository layout, and implementation order.

Do not rewrite, replace, or significantly modify the project plan unless the human developer explicitly asks for that. Keep future documentation aligned with meaningful architectural changes.

## General Behavior

- Read the project plan before significant work.
- Understand existing code before editing it.
- Follow the architecture defined by the project plan.
- Do not make unrelated changes.
- Prefer simple solutions over unnecessary abstractions.
- Reuse existing patterns before creating new ones.
- Keep changes scoped to the requested task.

## Planning

For significant changes:

1. Inspect relevant code.
2. Understand the current implementation.
3. Compare the requested change with the project plan.
4. Create a short implementation plan before editing.
5. Identify affected files.

Small and obvious fixes do not require an unnecessarily large planning phase.

## Dependencies

- Do not introduce dependencies unless necessary.
- Check whether an existing dependency already solves the problem.
- Explain why a new dependency is necessary before adding it.
- Do not automatically upgrade unrelated dependencies.

## Git

Agents must never automatically:

- force push
- rewrite shared Git history
- delete branches
- push directly to `main`
- commit secrets
- perform destructive Git operations

Inspect `git status` and `git diff` when useful. Do not create commits unless the human developer explicitly asks for one.

## Sensitive And Destructive Operations

Require explicit user approval before:

- deleting significant files
- changing database migrations
- destructive database operations
- changing production infrastructure
- modifying secrets
- changing environment credentials
- deploying
- force pushing
- resetting Git history

## Code Quality

- Follow existing naming conventions.
- Follow existing project structure.
- Keep responsibilities separated according to the architecture.
- Avoid duplicated logic.
- Avoid large unrelated refactors.
- Add comments only when they explain something that is not obvious from the code.

## Documentation

- Keep documentation aligned with meaningful architectural changes.
- Do not rewrite the existing project plan unless explicitly asked.
- Treat the existing project plan as the primary architectural reference.
- Prefer short navigation documents over duplicating the full project plan.

## Multi-Agent Workflow

Default responsibilities are guidance, not hard limitations.

Claude Code primary responsibilities:

- architecture analysis
- repository exploration
- difficult debugging
- root-cause analysis
- implementation planning
- independent code review

Codex primary responsibilities:

- implementation
- refactoring
- bug fixes
- repetitive coding work
- integrating confirmed review findings

Gemini primary responsibilities:

- external documentation research
- API verification
- SDK/platform research
- alternative approaches
- second-opinion technical analysis

For significant features:

1. Claude analyzes the repository and proposed change.
2. Claude produces an implementation plan without modifying code.
3. Gemini may verify external APIs, SDKs, documentation, or platform assumptions when relevant.
4. Codex implements the approved plan.
5. Claude independently reviews the resulting changes.
6. Gemini may independently verify external integration assumptions.
7. Codex applies confirmed review findings.
8. The human developer reviews the final Git diff.
9. The human developer decides whether to commit or merge.

Agents must not assume that another agent's conclusions are correct. Reviews should be independent.

## Preventing Agent Collisions

- Do not have multiple agents modify the same files simultaneously in the same working tree.
- Sequential work in one working tree is preferred initially.
- For genuinely parallel implementation later, use separate Git branches or worktrees.
- The repository documentation and Git history are the shared communication mechanism between agents.
- Do not create Git worktrees unless the human developer explicitly asks.

## Completion Reports

When an agent finishes a coding task, report:

1. What changed
2. Files changed
3. Important decisions made
4. Anything that could not be completed
5. Remaining risks or concerns

Do not require test execution in completion reports. Testing expectations should be decided per task by the human developer.
