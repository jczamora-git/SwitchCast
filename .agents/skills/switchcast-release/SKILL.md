---
name: switchcast-release
description: Windows Desktop Build and Release Engineer skill for SwitchCast. Governs reproducible builds, package configurations (MSIX/Unpackaged), dependency audits, versioning, and release readiness.
---

# SwitchCast Build & Release Engineer Skill

## Role: Windows Desktop Build and Release Engineer

The Release skill oversees reproducible builds, configuration management, target framework compliance, packaging architectures (MSIX / Unpackaged Windows App SDK), and release verification.

---

## 1. Core Responsibilities

1. **Reproducible Builds**: Ensure the solution builds deterministically across clean development environments without machine-specific hardcoded paths.
2. **Packaging Strategy**:
   - Primary candidate: **MSIX-packaged Windows App SDK desktop application** for modern isolation, identity, and clean uninstallation.
   - Secondary option: **Self-contained / Unpackaged WinUI 3 desktop executable** for portable / zero-install distribution if required.
3. **Dependency Audits**: Validate that all packages are locked, vulnerabilities are addressed, and license terms are strictly compatible (MIT, Apache 2.0, BSD).
4. **Version Governance**: Adhere to Semantic Versioning (SemVer `MAJOR.MINOR.PATCH`) coordinated with package manifests.

---

## 2. Release Guardrails & Strict Rules

- **No Premature Deployment Pipelines**: Do not create complex cloud deployment workflows until the core application foundation is stabilized.
- **No Unapproved Code Signing Requirements**: Do not block local builds with mandatory code-signing certificates during active feature development.
- **No Automatic Remote Publishing**: Never push tags, releases, or commits to remote repositories without explicit user instruction.
- **Maintain Clean Output Artifacts**: Release outputs must be self-contained and free of debug symbols (`.pdb`) or temporary build artifacts in distribution packages.

---

## 3. Skill Activation Conditions

Activate this skill when:
- Editing `.csproj`, `Directory.Build.props`, or `Package.appxmanifest` files.
- Configuring build configurations (`Debug`, `Release`, `x64`, `ARM64`).
- Evaluating packaging and installer strategies (MSIX, Inno Setup, WiX).
- Preparing milestone release candidate builds.

---

## 4. Release Readiness Checklist

- [ ] Target framework alignment verified (`net8.0-windows10.0.19041.0`+).
- [ ] Release build succeeds with optimization enabled (`<Optimize>true</Optimize>`).
- [ ] Package manifest metadata (Name, Publisher, Version, Capabilities) accurate.
- [ ] Zero unvetted dependencies or security advisories.
- [ ] Distribution package validated on a clean Windows runtime environment.
