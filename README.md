# Genevore

**3D Open-World Mobile Game (Android)**  
Core Mechanic: **Melahap (Devour) + Limitless Evolution**

Target hardware baseline: Snapdragon 720G / 4 GB RAM / ≤ 1.2 GB VRAM.

## Status Tracker

- [x] **Tahap 1** — Prototipe Arsitektur & Benchmark Core
- [x] **Tahap 2** — Vertical Slice (Core Loop Sandbox)
- [x] **Tahap 3** — Arsitektur Open-World & Optimasi Mobile ← *current*
- [ ] Tahap 4 — Pembangunan Konten & Sistem Balancing
- [ ] Tahap 5 — Hardening, Profiling Termal & QA
- [ ] Tahap 6 — Deployment & Release Readiness (PR sync ke whatman42)

---

## Tahap 3 — World Streaming + Abstract AI + Asset Pipeline

### Modules

| File | Responsibility |
|------|----------------|
| `WorldChunkManager.cs` | Distance-based chunk load/unload via **Addressables only** (async). Hard cap **≤ 2 concurrent** loads. Queue + hysteresis radii. Supports prefab InstantiateAsync and additive scene LoadSceneAsync. |
| `AbstractAISimulator.cs` | Dual-layer AI. Physical (pooled GameObject + NavMesh) inside 50 m; abstract `struct` array outside. Simulation via **Burst IJob** (or main-thread interval fallback every 2 s). Materialise/dematerialise with hysteresis. |
| `AbstractEntityData.cs` | Value-type enemy snapshot (position, HP, state, prefab hash). |
| `AbstractPopulationSeeder.cs` | Seeds background population into the abstract layer. |
| `MobileAssetPipeline.md` | Mandatory texture/material/mesh rules (ASTC, GPU Instancing, res limits). |
| `TextureImportPreset.md` | Concrete importer settings. |

### Exit Criteria (device, cross 5 chunks)

- VRAM ≤ **1.2 GB** (no exponential spikes)
- Chunk-load frame time spike ≤ **16.6 ms** (async, no macro stutter)
- Batches / Draw Calls ≤ **100** outdoors even with dozens of abstract entities

### Race-Condition Analysis (Abstract AI ↔ Main Thread)

**Risk:** Job writes `NativeArray<AbstractEntityData>` while Main Thread materialises / dematerialises and reads the managed mirror.

**Mitigation applied:**
1. Job is scheduled only when no previous job is outstanding (`_jobScheduled` gate).
2. Main Thread calls `_pendingJob.Complete()` (or waits for `IsCompleted`) **before** any read of results or any pool Acquire/Release.
3. Materialise / Dematerialise touch only the managed dictionary + ModuleObjectPool — never the NativeArray currently owned by a running job.
4. Double-buffer pattern: managed array is the source of truth for materialisation decisions; NativeArray is a temporary job workspace that is copied back only after Complete.

This eliminates data races between background simulation and physical spawn/despawn.

### Addressables Integration Notes

- All loads use `Addressables.LoadSceneAsync` / `InstantiateAsync` / `Release` / `UnloadSceneAsync`.
- No `Resources.Load` or synchronous `Addressables.LoadAssetAsync(...).WaitForCompletion()` on the hot path.
- Concurrent load gate (`MaxConcurrentLoads = 2`) prevents Addressables callback storms and disk thrashing on mid-range storage.

### Confidence

| Component | Confidence |
|-----------|------------|
| WorldChunkManager (async + queue) | 91% |
| AbstractAISimulator + Burst Job | 87% (depends on correct Player/Burst package) |
| Materialise/Dematerialise pool path | 90% |
| Asset pipeline rules (documentation) | 95% |
| **Overall Stage 3** | **89%** |

### Folder Layout (additive)

```
Assets/Scripts/
  World/
    WorldChunkManager.cs
  AI/
    AbstractEntityData.cs
    AbstractAISimulator.cs
    AbstractPopulationSeeder.cs
  Optimization/
    MobileAssetPipeline.md
    TextureImportPreset.md
```
