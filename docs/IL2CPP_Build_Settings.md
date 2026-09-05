# IL2CPP & Stripping — Stage 6

| Setting | Value |
|---------|--------|
| Scripting Backend | **IL2CPP** |
| Managed Stripping Level | Medium → High after QA |
| Strip Engine Code | Enabled |
| Architectures | ARM64 required |

`Assets/link.xml` preserves GeneDatabase SO + Addressables.

Validate: gene equip + catalog load on device after Medium strip before raising to High.

Use `ObfuscatedFloat` for biomass in RAM.
