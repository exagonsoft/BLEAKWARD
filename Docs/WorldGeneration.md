# BLEAKWARD level-driven world generation

## Architecture

Verified against Unity **6000.5.10f1**, URP **17.6.0**, and the project's **Renderer2D**. The build scene is `Assets/Scenes/Light the Ward.unity`. Its existing `BackgroundSpriteTiled` SpriteRenderer, sprite, sorting layer, and terrain material asset are retained. Generation bounds remain 240 × 165 units; the ground extends 10 units beyond each edge (260 × 185).

```text
LevelSO
  ├─ TerrainSettingsSO → TerrainController → MaterialPropertyBlock → tiled SpriteRenderer
  ├─ SceneDetailListSO → existing SceneDetailSO definitions
  └─ ResourceGenerationSettingsSO → ResourceSpawnRule[] → existing ResourceTypeSO + node prefabs
                         ↓
              SceneResourcesGenerator
                         ↓
              GeneratedWorld [ownership marker]
                ├─ Resources / prefab name / resource nodes
                └─ Scene Details / detail name / detail instances
```

`SceneResourcesGenerator` remains the single generation entry point. No competing world/resource/detail manager was added. `GenerateResources()` remains as a compatibility wrapper for existing callers and UnityEvents. `ResourceGenerator` continues to handle building income; it is separate from world node placement and was not changed.

## LevelSO responsibilities

| Section | Fields |
| --- | --- |
| Identity | Level name, stable level ID, default seed |
| World | Generator-local XY size and center, protected center radius, visual terrain border |
| Terrain | Reusable `TerrainSettingsSO` reference |
| Environment | Existing `SceneDetailListSO` reference and overall density multiplier; null means no details |
| Resources | Reusable `ResourceGenerationSettingsSO` reference |
| Placement | Maximum attempts per node and blocking physics layers |

The scene stores the assigned level, the existing terrain controller reference, generate-on-start, and an optional seed override. It no longer serializes its own resource distribution, detail selection, bounds, or exclusion settings. Invalid level/resource configurations report an error before clearing the previous world.

## Terrain shader

`BLEAKWARD/BLEAKWARD_Terrain` is one reusable URP 2D lit shader. All terrain textures and tuning values come from `TerrainSettingsSO` via a reused property block. Applying a profile does not edit the shared material or allocate new materials.

Four tileable texture inputs have no hardcoded biome meanings. Missing B/C/D textures fall back to A. Broad smooth world-XY noise blends the four surfaces. Mirrored/offset sampling and a second primary-texture frequency soften recognizable repetitions. A separate noise frequency controls macro tint. Brightness, saturation, contrast, blend softness, and optional subtle detail overlay remain data driven. There is no time or camera-position input to the terrain pattern. Screen-space coordinates are used only for URP's standard 2D light textures.

Use standalone tileable textures rather than sprite-atlas subregions. A repeat sampler handles their wrapping independently of the authored sprite's alpha sampler. Fine-detail strength becomes zero when its texture is absent. The flat normals pass supports 2D lights using normal maps, and the forward preview pass displays the same surface outside the 2D lit pass.

## Resource generation

`ResourceTypeSO` still describes resource identity/UI metadata. Each reusable distribution profile has one rule per resource identity, with a matching node prefab, total node amount, minimum/maximum bundle size, maximum bundles, bundle radius, node spacing, bundle separation, and starting-zone flags. Amount and bundle limits express scarcity without introducing a second weighting system.

Guaranteed center bundles are placed first, then scarce resources before abundant ones. Guarantees count toward the total. Rules retain exclusion from the center resource zone, the protected HQ radius, collision clearance, and placement attempts. Physics transforms are synchronized during generation so subsequent collision queries see newly placed colliders.

## Detail generation

The level chooses the existing `SceneDetailListSO` and scales each definition's amount with `detailDensity`. `SceneDetailSO` retains prefab variants, bundle behavior, spacing, physical clearance, scale and rotation ranges, and random flips. Resources are generated first so details avoid them. No detail prefab is hardcoded in the generator. Null/empty detail sets intentionally produce no dressing.

The current repository contains bush detail prefabs, not a complete industrial debris set. Industrial Test deliberately uses a sparse bush placeholder set. Replace its detail-set entries with authored scrap, concrete, or pipe definitions when those assets exist.

## Determinism and regeneration

Resource placement uses `System.Random(effectiveSeed)`. Each detail family uses its own stream derived from the effective seed and its serialized stable identity using FNV-1a. Families generate in placement-layer order with stable-ID ties; list reordering does not change layouts. See [DetailEcology.md](DetailEcology.md) for the exact hash, occurrence, reservation, and collision rules. Terrain offsets use a deterministic unsigned integer hash of the same seed. No generation code consumes `UnityEngine.Random`.

The effective seed is the level default unless the scene override is enabled. Code can call `SetLevel(definition, optionalSeed)` and `GenerateWorld()`, or supply a one-generation override with `GenerateWorld(seed)`.

Reproducibility assumes identical definitions, prefab assets, generator transform, and authored blocking colliders. Moving buildings or enemies legitimately changes collision-aware results. Exact cross-platform floating-point/physics equivalence is not promised. Resource settings are migrated unchanged; the detail stream is now independent, so its layout intentionally differs from the old combined RNG stream.

Regeneration always clears only roots marked `GeneratedWorldRoot` and owned by this generator. Matching an object name is not permission to delete it. Deferred runtime destruction first disables and detaches the old hierarchy so its colliders cannot affect same-frame regeneration. Put manually authored content outside the owned generated root. The terrain remains an authored scene object.

## Editor workflow

1. Create a profile through **Assets → Create → BLEAKWARD → Terrain Settings**. Assign a primary texture and optional B/C/D/detail inputs; tune the muted surface and macro values.
2. Create **BLEAKWARD → Resource Generation Settings**, or duplicate an existing distribution. Reference existing resource identities and matching node prefabs, then tune amounts, bundles, scarcity, and center rules.
3. Create/reuse **Scriptable Objects → Scene Detail List** and **Scene Detail** assets. Asset-specific placement rules stay in the individual definitions.
4. Create **BLEAKWARD → Level**, set identity/seed/bounds/safe area, and assign those reusable profiles.
5. In Light the Ward, select `-----------------SCENE RESOURCESS--------`. On its existing `SceneResourcesGenerator`, assign the level and optionally override its seed.
6. Click **Generate World**. Changing the assigned level previews terrain immediately; **Preview Terrain** reapplies edits made to a referenced terrain profile without regenerating objects. **Generate World** applies all changes. Generation and clearing through these buttons participate in editor Undo.
7. Enter Play Mode. Generate-on-start rebuilds the world once. Runtime regeneration is explicit, never per-frame.

Use the authored unit-scale XY generation origin and tiled terrain setup as the starting point for new scenes. Historical recovery scenes still contain the old serialized configuration; assign the new level/controller when bringing one forward rather than treating those snapshots as another authoritative source.

## Development examples

| Level | Terrain | Wood | Stone | Iron | Gold | Details |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| LVL_LightTheWard | Muted olive/brown ash, weeds, cracked earth, stony soil | 220 | 110 | 60 | 24 | 1080 |
| LVL_IndustrialTest | Colder/desaturated cracked earth, stone, scorched ground, mud | 70 | 110 | 110 | 12 | 72 |

Both share the same shader, material, generator code, resource identities, and existing detail behavior. Counts are validation results for seed 1337 in the gameplay scene.

## Validation

Run **BLEAKWARD → Validation → Validate World Generation** outside Play Mode. This opens the gameplay scene and leaves a Ward preview generated without saving that generated hierarchy. A dirty starting scene is preserved as a timestamped copy under `Temp/WorldGeneration` before opening the gameplay scene. Reports and camera captures go under `Logs/WorldGeneration` (local, ignored output).

The integration checks cover exact requested resource counts, center guarantees, bounds/exclusion, same-seed transforms and flips, changed-seed layouts, switching profiles and back, property-block application, unchanged shared material, untouched Unity RNG state, preservation of manually authored objects, shader compilation, and matching-world-position pixels after a camera pan. Captures use URP's full standard render request so the camera volume stack is initialized correctly.

Validated on 2026-09-30: all editor checks passed with the counts above, no shader errors, and a matching-world-position camera-pan mean RGB error of 0.000269 (threshold 0.015). The ecology suite also passed without Unity warnings/errors.

Play Mode smoke checks passed for manager initialization, enemy wave startup, and immediate repeated regeneration. The existing building-placement method accepted a starting wood-harvester location; an instantiated harvester detected the generated nodes and a forced income tick increased wood. No warnings/errors were recorded during that run. This does not substitute for extended combat/building balance testing.

Cleanup reload emitted a Unity AI `TextureSkeleton` serialization warning and stale `GameObjectInspector`/`TransformInspector` null-target errors, with stacks entirely inside Unity editor/package code. Refreshing selection and regenerating after runner removal succeeded. These reload diagnostics remain in `Logs/Editor.log`; the AI package itself was not modified.

## Performance

The terrain surface uses **7 texture samples per fragment**: A twice, B/C/D once each, optional detail once, and sprite alpha once. The detail sample remains a neutral sample when disabled. URP adds up to four light-texture samples depending on active blend styles. The separate normals pass uses one alpha sample. Three inexpensive interpolated value-noise evaluations require no noise textures.

There is still one tiled SpriteRenderer and one shared terrain material. The property block is reused. Physics placement queries and transform synchronization run only during explicit/startup generation; no streaming, per-frame regeneration, pooling layer, or persistent terrain simulation was introduced. Dense worlds may make synchronous editor generation slower; existing bounded placement attempts remain the controlling limit.

## Future extension points

- **Roads, construction scars, industrialization, combat damage:** add a world-space state mask/decal layer after base surface blending. Keep persistent gameplay state separate from immutable level profiles.
- **Resource dressing:** attach an optional reusable dressing definition to `ResourceSpawnRule`; consume successful bundle centers in a later dressing pass.
- **Hollow corruption:** overlay a state mask and corresponding profile colors/textures without replacing the base shader architecture.
- **Weather:** apply a separate runtime/global modifier layer instead of rewriting terrain profile assets.

These extension points are architectural boundaries only; none of those future systems is implemented here.

## File manifest

Paths below are relative to the project root. Every newly created Unity asset/source also has its accompanying `.meta` file.

### Created

- `Assets/Scripts/Scriptable Objects Scripts/LevelSO.cs` and `.meta`
- `Assets/Scripts/Scriptable Objects Scripts/TerrainSettingsSO.cs` and `.meta`
- `Assets/Scripts/Scriptable Objects Scripts/ResourceGenerationSettingsSO.cs` and `.meta`
- `Assets/Scripts/System/TerrainController.cs` and `.meta`
- `Assets/Scripts/System/GeneratedWorldRoot.cs` and `.meta`
- `Assets/Shaders/BLEAKWARD_Terrain.shader` and `.meta`
- `Assets/Editor/SceneResourcesGeneratorEditor.cs` and `.meta`
- `Assets/Editor/WorldGenerationValidation.cs` and `.meta`
- `Assets/Editor/SceneDetailEcologyValidation.cs` and `.meta`
- `Assets/Scriptable Objects/Levels/LVL_LightTheWard.asset` and `.meta`
- `Assets/Scriptable Objects/Levels/LVL_IndustrialTest.asset` and `.meta`
- `Assets/Scriptable Objects/Levels/TER_LightTheWard.asset` and `.meta`
- `Assets/Scriptable Objects/Levels/TER_IndustrialTest.asset` and `.meta`
- `Assets/Scriptable Objects/Levels/RES_LightTheWard.asset` and `.meta`
- `Assets/Scriptable Objects/Levels/RES_IndustrialTest.asset` and `.meta`
- `Assets/Scriptable Objects/Levels/DETAIL_IndustrialTest.asset` and `.meta`
- Folder metadata: `Assets/Editor.meta`, `Assets/Shaders.meta`, `Assets/Scriptable Objects/Levels.meta`
- `Docs/WorldGeneration.md`
- `Docs/DetailEcology.md`

### Modified

- `Assets/Scripts/System/SceneResourcesGenerator.cs`: level-driven entry point, owned cleanup, retained resource algorithm, deterministic ecology and collision rules.
- `Assets/Scripts/Scriptable Objects Scripts/SceneDetailSO.cs`: identity, placement layers/flags, occurrence, stable seeds, validation.
- `Assets/Scriptable Objects/Scene Details/Bushes.asset`: saved identity and GroundCover behavior; current user quantity preserved.
- `Assets/Scenes/Light the Ward.unity`: level/controller references replace embedded generation settings; controller added to existing terrain.
- `Assets/Materials/Terrain.mat`: shared reusable terrain shader.
- `BLEAKWARD.slnx`: Unity-generated Editor assembly project entry.

Local validation reports/captures are in ignored `Logs/WorldGeneration`; temporary execution scripts are in ignored `Temp/WorldGeneration`. The temporary Editor task runner used for this session is removed after validation. Other pre-existing/concurrent art, lighting, prefab, animation, and gameplay-script changes are outside this implementation and are not reverted or committed.
