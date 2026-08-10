# Codex Instructions

Before significant work, read [docs/AI_RULES.md](docs/AI_RULES.md) and the project plan in [README.md](README.md).

Use the README as the architectural source of truth for Amanah Drive. Follow its technology choices, repository layout, scope, and build order unless the human developer explicitly changes them.

Inspect existing implementations before modifying them. Keep changes focused on the requested task, reuse established patterns, and avoid unrelated refactors.

Do not perform destructive operations without explicit approval, including force pushes, history rewrites, deleting significant files, changing secrets, destructive database operations, deployments, or direct pushes to `main`.
