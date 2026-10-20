# Security Policy — SwitchCast

## 1. Supported Versions

Security fixes and maintenance patches are provided for the following versions of SwitchCast:

| Version | Supported | Notes |
| :--- | :--- | :--- |
| **v1.5.x** | **Yes** | Current stable production release line |
| **v1.4.x** | No | Legacy release; users should upgrade to v1.5.x |
| **v1.3.x** | No | Legacy release; users should upgrade to v1.5.x |
| **< v1.3.0** | No | End of Life; unsupported |

---

## 2. Architecture & Security Model

SwitchCast is designed from the ground up as a **privacy-first, local-only presentation switcher** for Windows 10 and Windows 11. Understanding the system architecture helps evaluate potential security concerns:

- **100% Offline Execution**: SwitchCast operates exclusively on the presenter's local machine. It initiates zero network requests, transmits no analytics, contains no background telemetry, and makes no external egress.
- **Local-Only Frame Pipeline**: Captured application frames and display textures reside entirely in local system and GPU memory (Direct3D 11 swapchains). Frames are never saved to disk or transmitted over networks.
- **Standard User Privileges**: The application executes under standard, unprivileged user credentials. It does not require and does not request Windows administrator (UAC) elevation during standard operation.
- **Windows Display Affinity**: The application respects OS-enforced window protection flags (`WDA_MONITOR` and `WDA_EXCLUDEFROMCAPTURE`) via standard Windows Graphics Capture (WGC) interop.
- **Presenter Isolation**: The Presentation Output window is architecturally isolated from the presenter's Control Dashboard and SwitchDeck floating dock to prevent accidental mirroring of private controls.

For an extensive threat model and mitigation mapping, refer to [docs/SECURITY_MODEL.md](docs/SECURITY_MODEL.md).

---

## 3. Reporting a Vulnerability

If you discover a potential security vulnerability in SwitchCast, we encourage you to report it responsibly.

### How to Report
Please report security vulnerabilities using one of the following private channels:
1. **GitHub Private Vulnerability Reporting (Preferred)**: Navigate to the [Security Advisories](https://github.com/jczamora-git/SwitchCast/security/advisories) tab of the repository and click **"Report a vulnerability"**. This allows secure, private communication directly with the maintainers without exposing vulnerability details publicly.
2. **Direct Maintainer Contact**: If GitHub Private Vulnerability Reporting is unavailable, contact the project maintainer ([@jczamora-git](https://github.com/jczamora-git)) privately.

### What to Include in Your Report
To help us understand and resolve the issue quickly, please provide:
- A descriptive title and summary of the vulnerability.
- Detailed step-by-step instructions to reproduce the issue.
- The affected component, file, or API surface (e.g., Direct3D swapchain, window enumeration, installer).
- A minimal Proof of Concept (PoC), sample code, or crash log if applicable.
- The version of SwitchCast and Windows build where the issue was observed.
- Your assessment of the potential impact and severity.

### Public Disclosure Policy
- Please allow the maintainers a reasonable opportunity to investigate, reproduce, and release a fix before disclosing any details publicly or to third parties.
- Do not open public GitHub issues for undisclosed security vulnerabilities.

---

## 4. Policy Clarifications & Boundaries

To set clear and realistic expectations:
- **No Bug Bounty**: SwitchCast is an open-source project maintained on an independent basis. There is **no commercial bug bounty program**, and monetary compensation is not offered for vulnerability submissions.
- **No Response Guarantees or SLAs**: While maintainers endeavor to review reports in good faith, the project does not offer legally binding response times, Service Level Agreements (SLAs), or guaranteed remediation deadlines.
- **Out of Scope Scenarios**:
  - Vulnerabilities requiring physical access to an unlocked, compromised presenter workstation.
  - Vulnerabilities or security limitations inherent to third-party conferencing platforms (Zoom, Microsoft Teams, Google Meet, Discord, OBS).
  - General Windows OS compositor, DirectX driver, or GPU hardware flaws outside the application's control.
  - Social engineering or phishing targeting presenters.
  - Denial of Service caused by exhausting physical hardware resources (e.g., out-of-memory from hundreds of simultaneously opened 4K video files).
