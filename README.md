# Genevore

**3D Open-World Mobile Game (Android)**  
Core Mechanic: **Melahap (Devour) + Limitless Evolution**

Target hardware baseline: Snapdragon 720G / 4 GB RAM.

## Status Tracker

- [x] **Tahap 1** — Prototipe Arsitektur & Benchmark Core (Zero-Allocation + Bone Re-Binding)
- [x] **Tahap 2** — Vertical Slice (Core Loop Sandbox) ← *current*
- [ ] Tahap 3 — Arsitektur Open-World & Optimasi Mobile
- [ ] Tahap 4 — Pembangunan Konten & Sistem Balancing
- [ ] Tahap 5 — Hardening, Profiling Termal & QA
- [ ] Tahap 6 — Deployment & Release Readiness

---

## Tahap 1 Recap

Zero-allocation foundation. See previous commit for `ModuleObjectPool`, `CreatureAssembly`, `GenomeManager`, `DevourController`, `AutomatedBenchmark`.

## Tahap 2 — Vertical Slice (Core Loop Sandbox)

Integrates Stage-1 infrastructure with Mobile Input, autonomous Enemy AI, Combat, and event-driven Genome HUD. Proves continuous play (Explore → Fight → Devour → Mutate) for ≥ 10 minutes without state corruption or memory leaks.

### New Modules

| File | Responsibility |
|------|----------------|
| `MobilePlayerController.cs` | CharacterController movement driven by Virtual Joystick. No Rigidbody. Contextual Devour via existing `DevourController`. |
| `VirtualJoystick.cs` | Lightweight UI joystick; only mutates `anchoredPosition`. |
| `EnemyAISandbox.cs` | FSM: Wander → Flee (HP < 20%) → Dead. NavMeshAgent with LowQuality avoidance. Implements `IPoolable`; released back to pool after Devour. |
| `EnemySpawnerSandbox.cs` | Continuous spawn from `ModuleObjectPool` (max 15 alive). |
| `IDamageable.cs` + `DamageableEntity.cs` + `CombatDamageSystem.cs` | Interface + concrete HP component. Damage resolution uses `StatBlock` only. C# events, no string params, no Lists. |
| `GenomeHUD.cs` | Event-driven 6-slot UI. Listens to `OnGeneEquipped` / `OnGeneRemoved` / `OnStatsRecalculated`. No per-frame Canvas rebuild. |
| `GenomeManager.cs` (updated) | Added `EquipGene` / `UnequipGeneAt` + events for Stage-2 UI/combat. Backward-compatible with Stage-1 benchmark. |

### Exit Criteria (10-minute continuous play)

- Core Loop stable: ≥ 50 sequential Devours; module swap works; zero NullReferenceException.
- Memory: GC Alloc ≤ 50 B/frame for UI, **0 B** for gameplay logic hot path.
- Animation stability: no T-Pose / fatal bone clipping after dozens of equip/unequip cycles.

### Integration Notes & Risk Analysis

1. **NavMesh dependency**  
   Enemies require a baked NavMesh. If the sandbox scene has none, agents will fail `isOnNavMesh`. Mitigation: bake a simple plane NavMesh in the Vertical Slice scene; agents use LowQuality avoidance to keep CPU cost low on mid-range SoCs.

2. **Pool vs NavMeshAgent**  
   `NavMeshAgent` is disabled on `OnDespawn` and re-enabled on `OnSpawn` to avoid residual pathing state when the GameObject is reused. This prevents the common “agent teleports after pool recycle” bug.

3. **GenomeManager events**  
   Stage-1 code still works via the legacy `TryAddGene` path (now routes through `EquipGene`). AutomatedBenchmark continues to function.

4. **Bone re-binding after many cycles**  
   Stage-1 already clears `SkinnedMeshRenderer.bones` on release. Stage-2 does not touch assembly logic; risk of T-Pose remains the same (explicit null-out on detach). Confidence that the existing path is sufficient: **90%**.

### Confidence

- MobilePlayerController + Joystick: **92%**
- EnemyAISandbox (FSM + pool lifecycle): **88%** (NavMesh bake is the external variable)
- CombatDamageSystem / IDamageable: **95%**
- GenomeHUD event-driven path: **93%**

Overall Stage-2 architectural confidence: **90%**.

### Folder Layout (additive)

```
Assets/Scripts/
  Player/
    MobilePlayerController.cs
  Enemy/
    EnemyAISandbox.cs
    EnemySpawnerSandbox.cs
  Combat/
    IDamageable.cs          (also under Interfaces)
    DamageableEntity.cs
    CombatDamageSystem.cs
  UI/
    VirtualJoystick.cs
    GenomeHUD.cs
  Core/
    GenomeManager.cs        (updated with events)
```
