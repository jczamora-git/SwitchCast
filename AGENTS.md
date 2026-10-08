# SWITCHCAST — AI DEVELOPMENT AGENT INSTRUCTIONS

Welcome to the SwitchCast development harness. This document is the **primary instruction entry point** and strict operational guideline for all AI agents working on the SwitchCast codebase.

All AI agents MUST read and adhere strictly to the rules, workflow, and quality gates defined in this document before inspecting, planning, or modifying any code.

---

## 1. CORE MISSION & IDENTITY

SwitchCast is a high-performance, privacy-first Windows desktop application for screen sharing and presentation management. It enables presenters to capture multiple running application windows or connected monitors and switch seamlessly between them inside a dedicated, shareable presentation output window without stopping or restarting screen-sharing sessions in conferencing tools (Zoom, Microsoft Teams, Google Meet).

### Primary Technology Stack:
- **Language**: C# (.NET 8.0+)
- **UI Framework**: WinUI 3 (Windows App SDK)
- **Capture API**: `Windows.Graphics.Capture` (WinRT Interop)
- **Rendering Pipeline**: Direct3D 11 Swapchain composition
- **Architecture**: MVVM with Clean Architecture boundaries (Core, Application, Infrastructure, UI)
- **Platform**: Windows 10 (version 1903 / build 18362 or later) & Windows 11

---

## 2. THE 12 MANDATORY RULES

Every AI agent operating in this repository MUST comply with these 12 immutable rules:

### Rule 1 — Read Before Writing
Before implementing any task:
1. Read this [AGENTS.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/AGENTS.md).
2. Read the active skills under [.agents/skills/](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/skills/).
3. Read [.agents/PROJECT_STATE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/PROJECT_STATE.md) to understand current progress and blocking items.
4. Read [.agents/HANDOFF.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/HANDOFF.md) to understand the context left by the preceding agent.
5. Inspect the affected source files and their references.
6. Never modify code without understanding its responsibilities and potential side effects.

### Rule 2 — Preserve Working Functionality
Existing working features must not be broken by unrelated changes.
- Do not rewrite unrelated components.
- Do not delete working implementations without explicit justification.
- Do not alter public contracts without reviewing all dependents.
- Do not replace stable functionality with naive rewrites.
- Apply the smallest justified, backward-compatible change.

### Rule 3 — Single Source of Truth
Each responsibility and state domain must have one clear owner.
- Avoid duplicate capture managers, duplicate window registries, or competing session coordinators.
- Maintain single-source configuration and state pipelines.
- Shared state must have explicit ownership and lifetime management.

### Rule 4 — Follow Established Architecture
Maintain strict logical separation between layers:
- **Never** place native capture APIs or Direct3D logic inside XAML Views or ViewModels.
- **Never** place heavy business logic or state transitions inside View code-behind.
- **Never** make native Win32 calls directly inside ViewModels.
- Use interface-based abstractions (`ICaptureService`, `IWindowDiscoveryService`, `IPresentationCoordinator`) to isolate platform-specific infrastructure from application domain logic.

### Rule 5 — No Unapproved Technology Changes
Do not introduce:
- Electron, Tauri, or web-view shells (WebView2 as UI).
- WPF or Windows Forms rewrites.
- Unnecessary JavaScript runtimes or node dependencies.
- Cloud databases, cloud telemetry, or authentication services.
- Unjustified third-party NuGet dependencies.
Every new dependency requires explicit architectural justification and documentation in an ADR.

### Rule 6 — No Fake Implementations
Never claim functionality works when it is only a placeholder, stub, or simulation.
- Never invent API compatibility.
- Never hardcode simulated production results.
- Never silently swallow exceptions to make a flow seem operational.
- Never report tests as passed without running them and receiving verification.
- Clearly categorize all work as: `Implemented`, `Tested`, `Partially Implemented`, `Planned`, or `Blocked`.

### Rule 7 — Strict Scope Control
Implement ONLY what the current task requests.
- No opportunistic refactoring of untouched files.
- No spontaneous UI redesigns or re-theming.
- No unrequested package upgrades or version bumps.
- If unrelated bugs or technical debt are discovered, document them in [.agents/PROJECT_STATE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/PROJECT_STATE.md) under "Technical Debt" or "Known Issues" instead of fixing them out of scope.

### Rule 8 — Validation Is Mandatory
After every code change, execute relevant validation:
- Build and compilation verification.
- Static analysis and compiler diagnostic checks (0 errors, 0 new warnings).
- Unit and integration tests.
- Never fabricate validation results. If the execution environment lacks Windows graphics or interactive desktop capabilities, explicitly state the limitation.

### Rule 9 — Documentation Must Stay Accurate
Whenever architecture, interfaces, dependencies, or operational state changes:
- Update the relevant documentation under [docs/](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/).
- Never allow documentation to state that non-existent features are implemented.

### Rule 10 — Mandatory Handoff
Every completed task MUST update:
1. [.agents/PROJECT_STATE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/PROJECT_STATE.md)
2. [.agents/HANDOFF.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/HANDOFF.md)
3. [.agents/CHANGELOG.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/CHANGELOG.md)

### Rule 11 — No Destructive Commands
Never execute destructive operations without explicit user instruction:
- `git reset --hard`
- `git clean -fd`
- Force pushes (`git push --force`)
- Deletion of user-authored source files.
- Overwriting existing project configurations without backing up or verifying dependencies.

### Rule 12 — Security and Privacy First
- Never capture user screen contents without explicit user action.
- Never transmit capture frames or metadata to external or remote servers.
- Never persist captured frames or sensitive thumbnails to disk by default.
- Respect OS-level protected content flags (`SetWindowDisplayAffinity`).
- Isolate the presentation output window so the Control Dashboard is never unintentionally captured or mirrored.

---

## 3. STANDARDIZED AGENT WORKFLOW

All agents MUST execute their tasks following this 10-step lifecycle:

```
[ STEP 1: Understand ]  --> Read AGENTS.md, PROJECT_STATE.md, HANDOFF.md
        |
[ STEP 2: Inspect ]     --> Inspect relevant source files, dependencies, build settings
        |
[ STEP 3: Classify ]    --> Identify domain (Architect, WinUI, Capture, Security, QA, Release)
        |
[ STEP 4: Activate ]    --> Read & follow the corresponding SKILL.md under .agents/skills/
        |
[ STEP 5: Plan ]        --> Formulate minimal, non-breaking execution plan
        |
[ STEP 6: Implement ]   --> Make targeted code changes adhering to Coding Standards
        |
[ STEP 7: Validate ]    --> Execute compiler checks, analyzers, and tests sequentially
        |
[ STEP 8: Review ]      --> Check scope adherence, resource disposal, and edge cases
        |
[ STEP 9: Document ]    --> Update PROJECT_STATE.md, HANDOFF.md, and CHANGELOG.md
        |
[ STEP 10: Report ]     --> Deliver structured summary with verifiable facts & next task
```

---

## 4. AGENT SPECIALIZATION SKILLS

When handling specific domains, consult and adhere to the specialized skill documentation:

| Skill | Path | Primary Focus |
| :--- | :--- | :--- |
| **Architect** | [.agents/skills/switchcast-architect/SKILL.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/skills/switchcast-architect/SKILL.md) | System boundaries, domain models, dependency rules, ADRs |
| **WinUI 3** | [.agents/skills/switchcast-winui/SKILL.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/skills/switchcast-winui/SKILL.md) | XAML views, ViewModels, MVVM bindings, UI thread, theming |
| **Capture** | [.agents/skills/switchcast-capture/SKILL.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/skills/switchcast-capture/SKILL.md) | `Windows.Graphics.Capture`, Direct3D 11, Win32 interop, frame pipelines |
| **Security** | [.agents/skills/switchcast-security/SKILL.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/skills/switchcast-security/SKILL.md) | Privacy, frame boundary safety, credential & DRM protection |
| **QA** | [.agents/skills/switchcast-qa/SKILL.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/skills/switchcast-qa/SKILL.md) | 5-level validation strategy, regression tests, edge case verification |
| **Release** | [.agents/skills/switchcast-release/SKILL.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/skills/switchcast-release/SKILL.md) | Build configurations, packaging (MSIX), dependency audits |

---

## 5. DOCUMENTATION DIRECTORY MAP

- [docs/PRODUCT_REQUIREMENTS.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/PRODUCT_REQUIREMENTS.md) — Feature specifications, MVP acceptance criteria, and scope boundaries.
- [docs/SYSTEM_ARCHITECTURE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/SYSTEM_ARCHITECTURE.md) — Logical modules, layer dependency rules, and capture-to-render pipeline.
- [docs/DEVELOPMENT_ROADMAP.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/DEVELOPMENT_ROADMAP.md) — 9-phase development plan from Phase 0 to Phase 8.
- [docs/CODING_STANDARDS.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/CODING_STANDARDS.md) — C#, XAML, WinRT, and Direct3D naming and coding guidelines.
- [docs/TESTING_STRATEGY.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/TESTING_STRATEGY.md) — Unit, integration, and manual desktop validation protocols.
- [docs/SECURITY_MODEL.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/SECURITY_MODEL.md) — Privacy policies, frame safety, and threat model.
- [docs/decisions/ADR-0001-architecture.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/decisions/ADR-0001-architecture.md) — Core architecture decision record.
