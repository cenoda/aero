# Phase A4: Agent-to-Agent Pipeline

> Agents talk to each other. You talk once.

## Goal

Enable agents to pass tasks and outputs between each other automatically.

## Entry Condition

- Phase A3 complete (Multi-Agent Routing)

## Exit Condition

- Agent A output can be auto-routed to Agent B
- Pipeline configurations are definable (e.g., "Cline plans → Copilot codes → Cline reviews")
- Frontend/Backend split works: one agent talks to user, others work silently
- Agent activity indicators show which agent is working
- Inter-agent message log is viewable

## Checklist

- [ ] **Relay** — Agent A output auto-routed to Agent B
- [ ] Pipeline configuration: "Cline plans → Copilot codes → Cline reviews"
- [ ] **Frontend/Backend split** — Cline talks to user, Copilot does silent work
- [ ] Agent activity indicators in AgentPanel (which agent is working right now)
- [ ] Inter-agent message log viewer

## Related Documents

- `docs/architecture/AGENT_ORCHESTRATION.md` — Routing Strategies (Pipeline), Example end-to-end flow
- `docs/architecture/AGENT_ORCHESTRATION.md` — AgentRouter.RelayAsync

## Notes

- Pipeline is the hardest part of the agent track. Start with simple 2-agent chains.
- Relay logic must handle failures: if Agent B crashes, what happens to Agent A's output?
- Frontend/Backend split requires UI coordination — only Frontend agent messages appear in chat.
- Inter-agent log is for debugging pipelines. Hide by default, show on demand.
