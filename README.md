# Cluster Spawn Generator - Procedural Generation

A Unity **procedural spawn system** that places objects in clusters with terrain-aware positioning, exclusion zones, and layer-based filtering.

---

## Overview | **[Tour Video](https://www.youtube.com/watch?v=4lv-HAgEDcE)**

This project provides a flexible **clustered spawn generator** for Unity that distributes objects across a 3D placement area. Instead of random scattering, objects are grouped into clusters which is useful for generating natural-looking arrangements like vegetation or enemy placements. The system uses raycasts for ground detection and supports configurable exclusion zones and layer masking to avoid spawning on undesirable surfaces.

---

## Contents

| Path | Description |
|------|-------------|
| `Assets/Spawner.cs` | Core spawn logic component: cluster generation, ground detection, exclusion handling |
| `Assets/SpawnerInterface.cs` | Inspector interface including layer exclusion, gizmos, and cluster configuration |
| `Assets/Sphere.prefab` | Default spawn object (sphere mesh, material, collider) |
| `Assets/Scenes/SampleScene.unity` | Sample scene with spawn setup |
| `Assets/Settings/` | URP render settings (PC/Mobile) |
| `Assets/InputSystem_Actions.inputactions` | Input actions asset (if bound to spawning) |

---

## Usage

### Setup

1. **Add the Spawner component** to a GameObject in your scene.
2. **Assign a prefab** to the `myObject` (Spawn Object) field, the default is `Sphere.prefab`.
3. **Configure parameters** in the Inspector (see below).

### Public Methods (callable)

- **`SpawnClusteredObjects()`** - Main entry: spawns multiple objects organized into clusters. Uses cluster generation, ground-aware placement (raycasts), exclusion zones, and layer-based filtering. Respects `config.fixedClusterCount` or uses `config.ClusterRange` when dynamic.
- **`SpawnObject()`** - Spawns a single object at a random point inside the placement area; adjusts to ground and applies a random height above the surface.

Call from scripts (e.g., on input or at runtime):

```csharp
Spawner spawner = GetComponent<Spawner>();
spawner.SpawnClusteredObjects();
// or
spawner.SpawnObject();
```

## Prefab Details

**Sphere.prefab** is the default spawn object:

- **Components**: `Transform`, `MeshFilter`, `MeshRenderer`, `SphereCollider`
- **Mesh**: Unity built-in sphere (radius 0.5)
- **Material**: `myBase` (via GUID reference)
- **Collider**: Non-trigger sphere, radius 0.5

The Spawner adds a **Rigidbody** (with gravity) to each spawned instance if one is not already present, so objects will fall and settle on the terrain.

You can replace the prefab with any GameObject; the same instantiation and optional Rigidbody logic applies.

---

## Engine Details

| Setting | Value |
|---------|--------|
| **Unity Version** | 6000.3.5f1 |
| **Render Pipeline** | Universal Render Pipeline (URP) |
| **Input System** | New Input System (1.17.0) |
| **Notable Packages** | URP 17.3.0, Visual Scripting, Timeline, uGUI |

---

## Design Intent

The spawner is designed for **procedural content generation** in games and simulations:

- **Clustered placement** - Produces grouped distributions instead of uniform randomness.
- **Terrain awareness** - Uses downward raycasts to spawn above detected ground, with configurable height ranges.
- **Spatial control** - Placement box, minimum cluster distance, and per-cluster radius give predictable but varied layouts.
- **Exclusion** - Box zones and layer-based exclusion prevent spawning on roads, water, buildings, etc.
- **Reproducibility** - Optional random seed for deterministic spawn layouts.
- **Debug visualization** - Gizmos for placement area, exclusion zones, cluster centers, and ground rays when the Spawner is selected.

Use it for: collectibles, foliage, enemy spawn points, or any scenario where clustered, terrain-aware placement is desired.
