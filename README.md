# Genevore

**3D Open-World Mobile Game (Android)**  
Core Mechanic: **Melahap (Devour) + Limitless Evolution**

Target: Snapdragon 720G / 4 GB RAM / ≤ 1.2 GB VRAM / **30 FPS hard cap**.

## Status Tracker

- [x] Tahap 1 — Prototipe Arsitektur & Benchmark Core
- [x] Tahap 2 — Vertical Slice (Core Loop Sandbox)
- [x] Tahap 3 — Arsitektur Open-World & Optimasi Mobile
- [x] Tahap 4 — Pembangunan Konten & Sistem Balancing
- [x] **Tahap 5 — Hardening, Profiling Termal & QA** ← *current*
- [ ] Tahap 6 — Deployment & Release Readiness

---

## Tahap 5 — Stability Modules (feature freeze)

| File | Role |
|------|------|
| `ThermalAdaptiveSystem.cs` | 10s rolling FPS window; degrade quality / shadow / AI radius if avg < 26 FPS. `Application.targetFrameRate = 30`. |
| `AppLifecycleHandler.cs` | `OnApplicationPause` / `Focus`: dump biomass+genes+pos to buffer, mute audio, halt Devour, `timeScale=0`. Resume restores without NRE. |
| `EnduranceTestRunner.cs` | 45 min autonomous run: move + devour + gene swap. Logs FPS/RAM at min 5 vs min 35. |
| `AbstractAISimulator` | `SetMaterialiseRadius` for thermal downgrade. |

### Exit criteria (device)

| Metric | Pass |
|--------|------|
| 1% low FPS after 30 min endurance | ≥ 25 |
| Resume from 1 min background | ≤ 2 s, state intact |
| Crash rate over 10×45 min | 0 |

### Thermal report fields (EnduranceTestRunner log)

- avgFPS @ min 5 vs min 35
- session min FPS
- Δ RAM (flag if > 15 MB over 30 min → suspect leak)
- quality tier + degrade event count
