# Genevore

**3D Open-World Mobile Game (Android)**  
Core Mechanic: **Melahap (Devour) + Limitless Evolution**

Target hardware baseline: Snapdragon 720G / 4 GB RAM.

## Stage 1 — Prototipe Arsitektur & Benchmark Core

Zero-allocation foundation before mass art production.

### Modules Implemented

| File | Responsibility |
|------|----------------|
| `ModuleObjectPool.cs` | Zero-allocation pool keyed by `int` hash. `IPoolable` reset on release. No `Instantiate`/`Destroy` during gameplay. |
| `CreatureAssembly.cs` | Runtime bone re-binding onto a single Master Skeleton. Proactive `Dictionary<string, Transform>` built in `Awake`. Never duplicates bones. |
| `GenomeManager.cs` | Genetic data via `ScriptableObject` (`GeneDataSO`, `BodyModuleSO`). Max 6 slots. Static array only. All stat math on `StatBlock` struct. |
| `DevourController.cs` | FSM: Idle → TargetLocked → Executing → Cooldown. Detection via `Physics.OverlapSphereNonAlloc` + static buffer. |
| `AutomatedBenchmark.cs` | Spawns 15 entities, forces 100 sequential Devour/Mutation cycles for Profiler validation. |

### Exit Criteria (must be measured on physical device)

- **GC Alloc** (mutation / devour frames): **0 Bytes**
- **CPU Main Thread**: ≤ 33.3 ms
- **Frame Rate**: ≥ 30 FPS stable with 15 entities on screen
- **Draw Calls / Batches**: ≤ 80 (isolated scene)

### Highest Risk

Failure to clear `SkinnedMeshRenderer.bones` on module release can create dangling references and memory leaks. Both `ModularBodyPart.OnDespawn` and `CreatureAssembly.DetachModule` explicitly null the bone arrays.

### Folder Layout

```
Assets/
  Scripts/
    Interfaces/IPoolable.cs
    Data/
      StatBlock.cs
      GeneDataSO.cs
      BodyModuleSO.cs
    Core/
      ModuleObjectPool.cs
      ModularBodyPart.cs
      CreatureAssembly.cs
      GenomeManager.cs
      DevourController.cs
    Benchmark/
      AutomatedBenchmark.cs
```

### Usage Notes

1. Create `GeneDataSO` / `BodyModuleSO` assets via Create menu.
2. Assign prefab hashes (stable `int`) in both SO and pool config.
3. Attach `ModularBodyPart` + `IPoolable` to every module prefab.
4. Pre-warm pools in `ModuleObjectPool` inspector.
5. Run `AutomatedBenchmark` scene on device and capture Profiler data.

Confidence level of this specification: **95%**.
