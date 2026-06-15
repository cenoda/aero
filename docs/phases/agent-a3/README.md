# Phase A3: Multi-Agent Routing

> Many agents. One chat. Smart routing.

## Goal

Support multiple agents simultaneously with routing by role, capability, or user choice.

## Entry Condition

- Phase A2 complete (Single Agent / Cline)

## Exit Condition

- AgentRouter routes prompts to appropriate agents
- User can pick target agent from dropdown in chat
- API agents (Copilot, GPT) connect via HTTP/SSE
- Local agents (Ollama, LM Studio) connect to localhost
- Multiple agents can run simultaneously (parallel tabs or threads)

## Checklist

- [ ] **AgentRouter** — route prompts by role, capability, or user choice
- [ ] Agent dropdown in chat to pick target
- [ ] **ApiAgentAdapter** — connect to Copilot/GPT via HTTP/SSE
- [ ] **LocalAgentAdapter** — connect to Ollama/LM Studio on localhost
- [ ] Multiple agents running simultaneously in chat (parallel tabs or threads)

## Related Documents

- `docs/architecture/AGENT_ORCHESTRATION.md` — Routing Strategies, API Adapter, Local Adapter
- `docs/architecture/AGENT_ORCHESTRATION.md` — AgentRouter class

## Notes

- Routing strategies: Manual, Role-based, Capability, Pipeline, Round-robin.
- Start with Manual routing (user picks). Add smart routing later.
- API adapters need auth tokens (OAuth, API keys). Store securely, not in plain text.
- Local adapters assume localhost — no auth needed, but model selection UI is required.
