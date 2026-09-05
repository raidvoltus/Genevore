# Mobile Asset Pipeline — Stage 3 Constraints

Target device: Snapdragon 720G / 4 GB system RAM / ≤ 1.2 GB VRAM budget.

## Texture Rules (mandatory)

| Asset class | Max resolution | Compression | Notes |
|-------------|----------------|-------------|-------|
| Player / Hero modules | 1024 × 1024 | ASTC 6×6 | Keep albedo + normal; avoid specular maps where possible |
| Enemy modules | 1024 × 1024 | ASTC 6×6 | Same as player |
| Environment (terrain, rock, tree) | 512 × 512 | ASTC 8×8 | Prefer atlases |
| UI / Icons | 256 × 256 | ASTC 6×6 or ETC2 | |

- **Never** ship uncompressed or DXT/BC formats on Android.
- Enable **Crunch** only if build size is critical; prefer pure ASTC for runtime decode cost.
- Generate Mip Maps for all world textures; disable for pure UI.

## Material Rules

1. Enable **GPU Instancing** on every environment material (terrain layers, props, foliage).
2. Prefer URP Lit / Simple Lit. Avoid complex multi-pass shaders.
3. Maximum 2 texture samples per environment material where possible.
4. No real-time reflection probes in open world; use baked or none.

## Mesh Rules

- Environment prop LODs: LOD0 ≤ 1500 tris, LOD1 ≤ 500, LOD2 ≤ 150.
- Skinned modules (player evolution pieces): keep bone count ≤ 40 per module.
- Enable **Read/Write Off** on all meshes that do not need runtime CPU access (saves RAM).

## Addressables Organisation

```
Addressables Groups
├── WorldChunks_Local          (chunks near starting area — local build)
├── WorldChunks_Remote         (distant biomes — downloadable)
├── EnemyModules               (pooled body parts)
└── UI
```

- Each world chunk is either:
  - a Prefab (small props + terrain tile), or
  - an Additive Scene (key ends with `_scene`).
- Label chunks by biome for future remote catalog.

## Project Settings Checklist (Unity)

- Quality: set target frame rate 30 on mobile builds.
- URP Asset: 
  - HDR Off (or On only if needed)
  - MSAA 2x max
  - Shadow Distance ≤ 40 m
  - Additional Lights ≤ 1
- Player Settings → Android:
  - Min API 24
  - Target Architectures: ARM64 only
  - Graphics Jobs: On
  - Multithreaded Rendering: On

## Validation Commands (Editor)

1. Window → Analysis → Frame Debugger — verify instancing batches.
2. Memory Profiler — confirm texture memory stays under budget after streaming 5 chunks.
3. Profiler (device): watch `Addressables.LoadAssetAsync` and `NavMeshAgent` samples during cross-chunk run.
