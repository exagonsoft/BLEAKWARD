# Scene detail ecology

## Architecture

`LevelSO.detailSet → SceneDetailListSO → SceneDetailSO → SceneResourcesGenerator` remains the data flow. Resources generate first. Detail families then generate in composition order without replacing the existing bundle-growth algorithm. Terrain and resource identity remain separate concerns.

## SceneDetailSO fields

| Field | Responsibility / migration default |
| --- | --- |
| `stableId` | Hidden, serialized family identity; created once when missing. Renaming does not change it. |
| `placementLayer` | Landmark → LargeProp → SmallProp → GroundCover generation priority; default SmallProp. |
| `reservesBundleSpace` | Checks and records composition spacing against other reserving bundles; default true. |
| `avoidResources` | Rejects resource colliders, including children/triggers, regardless of blocking layer mask; default true. |
| `avoidBlockingObjects` | Rejects non-resource colliders on the level's blocking layers; default true. |
| `generationChance` | Probability that the entire family participates, once per generation; default 1. |

Existing amounts, bundle sizes/radius/spacing, variant prefabs, scale, rotation, flips, and clearance remain authoritative. The level density multiplies the family amount. Inspector validation normalizes negative amounts/radii, invalid bundle ranges, chance, and scale ranges. Null prefab variants are filtered before placement; an empty family produces nothing.

Placement layers establish order only: the three behavior flags are explicit artist decisions. Non-reserving ground cover neither checks bundle reservations nor reserves space for later families. Physical collision checks remain separate. Disabling resource avoidance allows overlap with resources even when generic blocking avoidance is enabled. Disabling both avoidance flags still respects world bounds and the protected center.

In Unity 6000.5, the global trigger-query switch also gates explicit-filter overlap queries. The synchronous detail query enables it within `try/finally`, then restores its previous value. The validation explicitly covers this case; no persistent project physics setting is changed.

## Determinism

Each family gets `new System.Random(detail.DeriveSeed(worldSeed))`. The derivation is unchecked 32-bit FNV-1a: initialize 2166136261, process the seed's four bytes least significant first, then each saved ID UTF-16 code unit as low byte followed by high byte. For every byte, XOR and multiply by 16777619; reinterpret the final bits as a signed integer. The independent test vector for seed 1337 and ID `ecology-test-family` is -2002696033.

No instance IDs, runtime string hashes, or Unity random calls are used. A temporary deduplicated list is sorted by descending placement layer, then ordinal stable ID. The asset list is never reordered. Occurrence uses the same family stream; chance zero and one have exact behavior. Renaming and list reordering preserve layouts. Other families' amounts cannot consume this family's RNG. They can still legitimately change its accepted positions through physical collisions or reservations; independent streams do not eliminate environmental dependencies.

## Migration and authoring

Older definitions receive conservative defaults: SmallProp, reserving, both avoidance flags enabled, occurrence 1. Bundle settings remain intact. The independent stream causes a one-time layout change from the previous shared detail stream.

New assets receive an ID once. Duplicating an asset also copies its ID: for a genuinely new family, use the asset Inspector context menu **Assign New Identity to Duplicated Family**, then save. Never use that command merely to rename or retune a family. Missing or duplicate identities across different definitions produce a warning and stop the detail pass, avoiding ambiguous ordering. Repeated references to the same definition are deduplicated.

To add a family, create a Scene Detail asset, assign valid prefab variants, choose a layer, set reservation/avoidance behavior, tune bundle and variation settings, and add it to the level's referenced detail list. Generate through the existing generator Inspector. Resource and terrain configuration remain on their reusable level profiles.

## Light the Ward configuration

The only existing detail family is `Assets/Scriptable Objects/Scene Details/Bushes.asset`, using the four existing bush prefabs. It is now GroundCover, non-reserving, occurrence 1, and retains resource/blocker avoidance. Its saved identity is `9f4d71a9ace25606dd60a52c5f941734`. Current user tuning of amount 360 and Ward density 3 is preserved. No invented rock, ruin, or industrial prefab families were added. Industrial Test uses the same available family at density 0.2.

## Validation

Run **BLEAKWARD → Validation → Validate Detail Ecology** outside Play Mode. Temporary fixture objects and cloned data live in an additive scene and are removed afterward. The checks cover stable identity serialization, a known hash vector, invalid-value normalization, occurrence, complete seeded snapshots, ordering/list preservation, family RNG independence, reservation overlap/separation, clearing, child resource trigger detection, independent avoidance flags, and safe bounds. The report is `Logs/WorldGeneration/ecology-validation.txt`.

The separate **Validate World Generation** menu exercises the actual gameplay scene, level switching, resource counts, determinism, material/shader behavior, and camera-fixed terrain. See [WorldGeneration.md](WorldGeneration.md) for architecture, performance, the complete file manifest, and gameplay validation scope.

On 2026-09-30 all ecology checks passed with no Unity warnings/errors. Actual-scene validation produced 1080 Ward details and 72 Industrial Test details; resource counts matched their profiles. Play Mode passed startup, repeated regeneration, a valid wood-harvester placement, generated-resource detection, and an income tick.

## Remaining limitations and next extension

Collision clearance is a circle around the candidate pivot, tested against existing colliders. It does not compute the candidate prefab's rotated/scaled compound footprint. Large or offset props require conservative clearance. Full candidate footprints are deferred because variant/scale selection currently occurs after candidate acceptance; implementing them correctly requires restructuring that placement step. No physics-based guarantee is made for collider-free art.

Environmental zones/patches can later provide a deterministic candidate suitability filter or density modifier before bundle-center acceptance and bundle growth. Keep reusable zone definitions in level data and preserve family RNG/order. This task does not implement zones, terrain state, resource dressing, weather, streaming, or biome simulation.
