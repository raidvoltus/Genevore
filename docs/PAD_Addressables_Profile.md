# Play Asset Delivery (PAD) + Addressables — Stage 6

## Goal
Base module **AAB ≤ 149 MB**.

## Group mapping

| Addressables Group | PAD delivery | Contents |
|--------------------|--------------|----------|
| `InstallTime_Core` | **Install-Time** | UI, player base mesh, sandbox enemies, GeneCatalog metadata |
| `FastFollow_World` | **Fast-Follow** | First 4 world chunks, ambient audio |
| `OnDemand_World` | **On-Demand** | Remaining chunks, rare gene modules, VFX |

## Build steps
1. Addressables Groups → create three groups above
2. Add Schema → Play Asset Delivery per group
3. Addressables Build → Default Build Script
4. Build App Bundle (Google Play)
5. Validate base module ≤ 149 MB in Play Console

## Runtime
`WorldChunkManager` loads by Addressable key. On-Demand: `DownloadDependenciesAsync` before instantiate when pack not present.
