# Genevore

**3D Open-World Mobile Game (Android)**  
Core Mechanic: **Melahap (Devour) + Limitless Evolution**

Target: Snapdragon 720G / 4 GB RAM / ≤ 1.2 GB VRAM / **30 FPS** / **Base AAB ≤ 149 MB**.

## Status Tracker

- [x] Tahap 1 — Prototipe Arsitektur & Benchmark Core
- [x] Tahap 2 — Vertical Slice (Core Loop Sandbox)
- [x] Tahap 3 — Arsitektur Open-World & Optimasi Mobile
- [x] Tahap 4 — Pembangunan Konten & Sistem Balancing
- [x] Tahap 5 — Hardening, Profiling Termal & QA
- [x] **Tahap 6 — Deployment & Release Readiness** ← *final*

## Stage 6 artifacts

| Item | Path |
|------|------|
| CloudSaveSync (binary GPGS) | `Assets/Scripts/Cloud/CloudSaveSync.cs` |
| Biomass obfuscation | `Assets/Scripts/Security/BiomassObfuscation.cs` |
| IL2CPP link.xml | `Assets/link.xml` |
| PAD profile | `docs/PAD_Addressables_Profile.md` |
| IL2CPP checklist | `docs/IL2CPP_Build_Settings.md` |

Gold Master requires device Pre-Launch Report + Base AAB ≤ 149 MB + GPGS login ≤ 3.5s + PR merge to whatman42/Genevore.
