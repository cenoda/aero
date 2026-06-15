# Phase A2: Single Agent (Cline via CLI)

> Talk to one agent. See it respond.

## Goal

Connect a single CLI-based agent (Cline) to the AgentChat UI.

## Entry Condition

- Phase A1 complete (Agent Foundation)

## Exit Condition

- Cline spawns as child process and communicates via JSON-line protocol
- AgentChat shows streamed responses from Cline
- Code blocks in responses have "Apply to editor" button
- Active file + cursor context is auto-injected before every prompt

## Checklist

- [ ] **CliAgentAdapter** — spawn Cline as child process over pty
- [ ] JSON-line protocol: send prompt, receive streamed response
- [ ] AgentChat hooked to Cline — type prompt, see response stream in
- [ ] "Apply to editor" button on code blocks
- [ ] Context auto-injection (active file + cursor before every prompt)

## Related Documents

- `docs/architecture/AGENT_ORCHESTRATION.md` — CLI Adapter section
- `docs/architecture/CORE_INFRASTRUCTURE.md` — MessageBus patterns

## Notes

- Cline must be installed separately. Document installation and CLI path configuration.
- JSON-line protocol is simple: one JSON object per line over stdin/stdout.
- "Apply to editor" should create or modify files based on code block suggestions.
- Context injection happens in AgentRouter before sending to any agent.
