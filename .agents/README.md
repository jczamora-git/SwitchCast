# .agents/ — AI Agent Operational Infrastructure

This directory contains the operational guidelines, state tracking, handoff records, task templates, and domain-specific skills for AI agents developing SwitchCast.

---

## Directory Structure

```
.agents/
|-- README.md            # This index file
|-- PROJECT_STATE.md     # Authoritative development status and progress tracker
|-- HANDOFF.md           # Inter-agent task handoff and continuity record
|-- CHANGELOG.md         # Chronological log of development activities
|-- TASK_TEMPLATE.md     # Standardized structure for task definition and execution
|
|-- skills/              # Specialized domain skill guides for agents
    |-- switchcast-architect/
    |   |-- SKILL.md     # System architecture, domain boundaries, ADR governance
    |-- switchcast-winui/
    |   |-- SKILL.md     # WinUI 3, XAML, MVVM, UI thread, theming
    |-- switchcast-capture/
    |   |-- SKILL.md     # Windows.Graphics.Capture, D3D11, Frame pipelines
    |-- switchcast-security/
    |   |-- SKILL.md     # Privacy, frame boundary safety, threat model
    |-- switchcast-qa/
    |   |-- SKILL.md     # 5-level test strategy, regression prevention
    |-- switchcast-release/
    |   |-- SKILL.md     # Packaging, reproducible builds, release gating
```

---

## Agent Usage Rules

1. **State Ownership**: Any agent executing a task MUST check [PROJECT_STATE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/PROJECT_STATE.md) and [HANDOFF.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/HANDOFF.md) before making code edits.
2. **Post-Task Updates**: Upon task completion or handoff, the agent MUST update:
   - [PROJECT_STATE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/PROJECT_STATE.md) (Status, Implemented list, In Progress list, Next Task)
   - [HANDOFF.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/HANDOFF.md) (Detailed report of what was changed and verified)
   - [CHANGELOG.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/CHANGELOG.md) (Standardized change entry)
3. **Skill Activation**: Before engaging in architectural, UI, capture, security, QA, or build tasks, agents must load and follow the corresponding `SKILL.md` file.
