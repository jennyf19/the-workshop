most multi-agent setups have a relay problem. you talk to agent A, copy context to agent B, summarize what B said back to A. you become the switchboard. the agents are smart individually but blind to each other.

the-workshop is a different model. multiple AI agents (desks) sit in the same room, on the same work, each with its own memory and history. they share a workspace. you direct the work instead of relaying it.

each desk has a journal (persistent memory that carries across sessions), a signal channel (structured state — blocked, done, hands-up), and access to a shared bench. a TA coordinator sees the whole room and routes work. desks are peers — they can disagree with each other, and when they do, that disagreement surfaces as the most valuable signal in the system.

it's now available as a plugin — same repo, same commands, both ecosystems:

```
/plugin marketplace add jennyf19/the-workshop
/plugin install workshop@the-workshop
```

works in GitHub Copilot and Claude Code. one repo carries .github/plugin/ and .claude-plugin/, both pointing at the same skills and agents. you pick the tool; the workshop doesn't care.

what you get:
- workshop-ta agent (room coordinator)
- desk-open, desk-journal, signal-write, bench-read skills
- persistent desk state that survives across sessions
- structured signals for operator awareness

the key idea: desks are long-running in state, not in runtime. each session is independent — the journal is the bridge. this means you get persistent multi-agent coordination without persistent processes.

source + docs: github.com/jennyf19/the-workshop
methodology: jennyf19.github.io/agentic-devops/workshop/

#AI #AgenticAI #GitHubCopilot #ClaudeCode #MultiAgent #AgentOps
