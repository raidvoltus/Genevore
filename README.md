# Genevore

**3D Open-World Mobile Game (Android)**  
Core Mechanic: **Melahap (Devour) + Limitless Evolution**

Target: Snapdragon 720G / 4 GB RAM / ≤ 1.2 GB VRAM.

## Status Tracker

- [x] Tahap 1 — Prototipe Arsitektur & Benchmark Core
- [x] Tahap 2 — Vertical Slice (Core Loop Sandbox)
- [x] Tahap 3 — Arsitektur Open-World & Optimasi Mobile
- [x] **Tahap 4 — Pembangunan Konten & Sistem Balancing** ← *current*
- [ ] Tahap 5 — Hardening, Profiling Termal & QA
- [ ] Tahap 6 — Deployment & Release Readiness

---

## Tahap 4 — Content & Balancing Systems

### Modules

| File | Responsibility |
|------|----------------|
| `BiomassMetabolism.cs` | Metabolic decay balancer. Non-linear drain curve forces hunting. Move-speed penalty scales with biomass. |
| `ProceduralScaleAdapter.cs` | Resizes CharacterController (height/radius/center) with gene count / scale. Primitive colliders only. Ground correction + ComputePenetration anti-clip. |
| `GeneDatabase.cs` + `GeneCatalogSO` | Metadata-only registry (50+ genes). Addressables for catalog + on-demand BodyModuleSO. No synchronous visual loads. |
| `MobilePlayerController.cs` (updated) | Implements `IMetabolismSpeedReceiver` for runtime speed override. |

### Metabolic Decay Formula (pseudo-code)

```csharp
// biomass >= minBiomassClamp (0.5)  → never zero / negative
float b      = max(minBiomassClamp, biomass);
float excess = max(0, b - 1);

// Slow exponential, hard-capped
float arg    = min(decayExponent * excess * 0.15, 8.0);  // prevent exp overflow
float mult   = min(exp(arg), maxDrainMultiplier);        // e.g. max 12×
float drain  = max(0, baseDrainPerSecond * mult);        // HP/sec

// Move speed (hyperbolic, denom always ≥ 1)
float speed  = max(minMoveSpeed, baseMoveSpeed / (1 + speedPenaltyK * excess));
```

**Safety analysis**
- No divide-by-zero: denominator `1 + k*excess` ≥ 1; biomass clamped > 0.
- No negative drain: `max(0, …)`.
- No exp overflow: argument clamped to 8 before `Exp`.
- No underflow to NaN: all inputs finite floats from clamped sources.

**Design target:** At theoretical Apex biomass, full-HP idle survival ≤ **180 s** (3 minutes). Tunable via `baseDrainPerSecond`, `decayExponent`, `maxDrainMultiplier`.

### Physics Integrity

- CharacterController only (Capsule). **No MeshCollider**.
- On scale change: disable CC → write height/radius/center → re-enable → raycast ground probe → lift feet → optional `Physics.ComputePenetration` lateral resolve.
- `DebugForceScaleSteps(20)` helper for exit-criteria stress (20 sequential scale-ups).

### GeneDatabase Memory

- Boot path loads **metadata only** (`GeneMeta` structs + small strings) into `Dictionary<int, GeneMeta>`.
- Visual `BodyModuleSO` loaded via Addressables **only on equip**.
- 50 entries estimated ≪ 2 MB (typically < 50 KB metadata).

### Exit Criteria

| Metric | Target |
|--------|--------|
| Apex idle survival (no hunting) | ≤ 3 minutes |
| Clipping incidents over 20 sequential scale-ups | **0** |
| Gene metadata RAM (50 entries) at init | **< 2 MB** |

### Confidence

| Component | % |
|-----------|---|
| BiomassMetabolism math + safety | 94% |
| ProceduralScaleAdapter anti-clip | 90% |
| GeneDatabase metadata / Addressables | 92% |
| **Overall Stage 4** | **91%** |
