# Phase A5: Advanced Agent Features

> Agents remember, act, and compete.

## Goal

Add memory, tool use, marketplace, custom routing, and comparison mode.

## Entry Condition

- Phase A4 complete (Agent-to-Agent Pipeline)

## Exit Condition

- Agents persist conversation context per agent
- Agents can trigger IDE actions (open file, run build, git commit)
- Agent marketplace allows discovering and installing adapters
- Users can define custom routing rules
- Comparison mode sends same prompt to all agents side-by-side

## Checklist

- [ ] **Agent memory** — persistent conversation context per agent
- [ ] **Tool use** — agents can trigger IDE actions (open file, run build, git commit)
- [ ] **Agent marketplace** — discover and install community agent adapters
- [ ] **Custom routing rules** — user-defined "if task=X, route to agent Y"
- [ ] **Agent comparison mode** — send same prompt to all, compare outputs side-by-side

## Related Documents

- `docs/architecture/AGENT_ORCHESTRATION.md` — Agent memory, Tool use concepts
- `docs/architecture/AGENT_ORCHESTRATION.md` — Routing Strategies (Custom rules)

## Notes

- Agent memory requires storage backend. SQLite or JSON files in `~/.aero/agents/`.
- Tool use is powerful but dangerous. Agents can modify files and run commands. Add confirmation UI.
- Marketplace is optional. Can start with manual adapter installation.
- Comparison mode is a UI feature — split view or tabs for multiple agent responses.
