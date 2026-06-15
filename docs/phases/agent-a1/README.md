# Phase A1: Agent Foundation

> The AI layer begins.

## Goal

Establish agent infrastructure: interfaces, registry, context, and UI panels.

## Entry Condition

- **Phase 8 (UI Polish) must be complete.**
- Do not start before Phase 8. Layout changes after agent UI is built will force rework.

## Exit Condition

- IAgent interface is defined and stable
- AgentRegistry can register/unregister agents
- WorkspaceContext gathers open files, cursor, diagnostics, git diff
- AgentPanel shows connected agents with status
- AgentChat UI has input, message list, and agent colors
- Agent settings (endpoints, API keys, CLI paths) are configurable

## Checklist

- [ ] **IAgent interface** — Id, Name, Kind (CLI/API/Local), Role (Frontend/Backend)
- [ ] **AgentRegistry** — discover/register/unregister agents
- [ ] **WorkspaceContext** — gather open files, cursor, diagnostics, git diff
- [ ] **AgentPanel UI** — sidebar listing connected agents with status dots
- [ ] **AgentChat UI** — bottom panel with chat input, message list, agent colors
- [ ] Agent configuration in settings (endpoint URLs, API keys, CLI paths)

## Related Documents

- `docs/architecture/AGENT_ORCHESTRATION.md` — IAgent, AgentRegistry, WorkspaceContext
- `docs/architecture/OVERVIEW.md` — Agent layer architecture

## Notes

- This phase is all infrastructure. No real agent communication yet.
- AgentPanel and AgentChat reuse existing panel system from Phase 8.
- Settings for agents extend the existing settings system.
