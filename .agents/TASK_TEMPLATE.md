# SWITCHCAST — TASK EXECUTION TEMPLATE

Every agent undertaking a development task in SwitchCast must structure their work according to this template.

---

## 1. Task Objective
- **Task Name**: `[e.g., Implement IWindowDiscoveryService]`
- **Target Phase**: `[e.g., Phase 2 — Window and Monitor Discovery]`
- **Primary Goal**: `[Brief 1-2 sentence description of what will be achieved]`

---

## 2. Relevant Existing Behavior
- **Current State**: `[Describe existing classes, interfaces, or null implementations]`
- **Dependencies**: `[Services, UI elements, or native APIs touched]`

---

## 3. Scope & Boundaries
- **In-Scope**:
  - `[Item 1]`
  - `[Item 2]`
- **Explicitly Out-of-Scope**:
  - `[Item 1 (e.g., Do not touch capture pipeline)]`
  - `[Item 2 (e.g., Do not modify presentation window)]`

---

## 4. Affected Components
- `[Path to file 1]`
- `[Path to file 2]`
- `[Path to test file]`

---

## 5. Implementation Plan
1. **Step 1**: `[e.g., Define interface in SwitchCast.Core]`
2. **Step 2**: `[e.g., Implement Win32 window enumeration in SwitchCast.Infrastructure.Windows]`
3. **Step 3**: `[e.g., Wire into DI container and expose to ViewModel]`
4. **Step 4**: `[e.g., Add unit and mock integration tests]`

---

## 6. Regression & Security Risks
- **Regression Risks**: `[What existing functionality could break?]`
- **Security & Privacy Risks**: `[Could this expose protected windows, sensitive titles, or memory leaks?]`
- **Mitigations**: `[Explicit handling, try/catch, resource disposal]`

---

## 7. Acceptance Criteria
- [ ] Criterion 1
- [ ] Criterion 2
- [ ] Criterion 3

---

## 8. Sequential Validation
1. **Level 1 (Compilation)**: `dotnet build` passes with 0 errors.
2. **Level 2 (Static Analysis)**: No new compiler or analyzer warnings.
3. **Level 3 (Unit Tests)**: `dotnet test` passes with 100% success on affected suites.
4. **Level 4 (Integration Tests)**: Interface contracts and lifecycle tests pass.
5. **Level 5 (Manual Verification)**: If required, verify interactive behavior on Windows desktop.

---

## 9. Documentation and Handoff
- Update [.agents/PROJECT_STATE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/PROJECT_STATE.md)
- Update [.agents/HANDOFF.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/HANDOFF.md)
- Update [.agents/CHANGELOG.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/CHANGELOG.md)
