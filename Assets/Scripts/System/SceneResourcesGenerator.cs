using System;
using System.Collections.Generic;
using UnityEngine;


[ExecuteAlways]
public class SceneResourcesGenerator : MonoBehaviour
{
    private struct PlacedBundle
    {
        public Vector2 center;
        public float radius;

        public PlacedBundle(Vector2 center, float radius)
        {
            this.center = center;
            this.radius = radius;
        }
    }

    private struct ResourcePlacementState
    {
        public int spawnedAmount;
        public int bundleIndex;
        public int generatedBundles;

        public ResourcePlacementState(
            int spawnedAmount,
            int bundleIndex,
            int generatedBundles)
        {
            this.spawnedAmount = spawnedAmount;
            this.bundleIndex = bundleIndex;
            this.generatedBundles = generatedBundles;
        }
    }

    [Header("Level")]
    [SerializeField] private LevelSO level;
    [SerializeField] private TerrainController terrainController;
    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool overrideSeed;
    [SerializeField] private int seedOverride = 1337;

    public LevelSO Level => level;
    public int EffectiveSeed => overrideSeed ? seedOverride : level != null ? level.defaultSeed : 1337;
    public TerrainController Terrain => terrainController;
#if UNITY_EDITOR
    // Editor buttons opt in; automated fixtures must not populate the user's Undo history.
    public bool RecordGenerationUndo { get; set; }
#endif
    private ResourceSpawnRule[] resources => level.resourceSettings.resources;
    private SceneDetailListSO sceneDetailList => level.detailSet;
    private Vector2 spawnAreaSize => level.worldSize;
    private Vector2 spawnAreaCenter => level.worldCenter;
    private float centerClearRadius => level.centerSafeRadius;
    private float centerResourceZoneRadius => level.resourceSettings.centerResourceZoneRadius;
    private int centerBundlePlacementAttempts => Mathf.Max(1, level.resourceSettings.centerBundlePlacementAttempts);
    private bool requireCenterResources => level.resourceSettings.requireCenterResources;
    private float resourceCollisionClearance => level.resourceSettings.resourceCollisionClearance;
    private float defaultResourceBundleSeparation => level.resourceSettings.defaultResourceBundleSeparation;
    private int maxPlacementAttemptsPerNode => Mathf.Max(1, level.maxPlacementAttemptsPerNode);
    private LayerMask blockingLayers => level.blockingLayers;
    private const string resourcesParentName = "Resources";
    private const string detailsParentName = "Scene Details";
    private readonly List<Collider2D> detailCollisionResults = new List<Collider2D>(16);

    public void SetLevel(LevelSO definition, int? seed = null)
    {
        level = definition;
        overrideSeed = seed.HasValue;
        seedOverride = seed ?? 1337;
        PreviewTerrain();
    }

    public void PreviewTerrain()
    {
        if (terrainController != null && level != null)
            terrainController.ApplyLevel(level, EffectiveSeed, transform);
    }

    private void OnEnable() => PreviewTerrain();

    private void Start()
    {
        if (Application.isPlaying && generateOnStart)
        {
            GenerateResources();
        }
    }

    [ContextMenu("Generate World")]
    public void GenerateResources() => GenerateWorld(); // Preserve existing callers and UnityEvents.

    public void GenerateWorld(int? seed = null)
    {
        if (level == null)
        {
            Debug.LogError("Assign a LevelSO before generating the world.", this);
            return;
        }
        if (!level.TryValidate(out string error))
        {
            Debug.LogError($"Cannot generate '{level.name}': {error}", this);
            return; // Keep the previous world when the new configuration is invalid.
        }
        if (terrainController == null)
        {
            Debug.LogError("Assign the existing terrain's TerrainController.", this);
            return;
        }
        int generationSeed = seed ?? EffectiveSeed;
        terrainController.ApplyLevel(level, generationSeed, transform);
        ClearGeneratedContent();
        Physics2D.SyncTransforms();
        // Each detail family derives its own stream; resource RNG remains unchanged.
        GenerateResourceNodes(new System.Random(generationSeed));
        Physics2D.SyncTransforms();
        GenerateSceneDetails(generationSeed);
        Physics2D.SyncTransforms();
    }

    [ContextMenu("Clear Generated World")]
    public void ClearGeneratedContent()
    {
        foreach (var root in GetComponentsInChildren<GeneratedWorldRoot>(true))
            if (root.owner == this) DestroyGeneratedObject(root.gameObject);
    }

    private Transform GetGeneratedRoot()
    {
        foreach (var root in GetComponentsInChildren<GeneratedWorldRoot>(true))
            if (root.owner == this) return root.transform;
        var container = new GameObject("GeneratedWorld");
        container.transform.SetParent(transform, false);
        container.AddComponent<GeneratedWorldRoot>().owner = this;
        return container.transform;
    }

    // ========================================================================
    // RESOURCES
    // ========================================================================

    private void GenerateResourceNodes(
        System.Random random)
    {
        if (resources == null ||
            resources.Length == 0)
        {
            return;
        }

        Transform resourcesParent =
            GetOrCreateResourcesParent();

        /*
         * This list contains every successfully placed resource bundle.
         *
         * Each resource type has independent generation rules, but all
         * resources share this physical occupancy list.
         */
        List<PlacedBundle> occupiedResourceBundles =
            new List<PlacedBundle>();

        /*
         * ------------------------------------------------------------
         * PHASE 1
         * ------------------------------------------------------------
         *
         * Guarantee the resources that belong near the center.
         *
         * This is intentionally done BEFORE normal resource generation.
         */
        if (requireCenterResources)
        {
            GenerateGuaranteedCenterResources(
                resources,
                resourcesParent,
                random,
                occupiedResourceBundles);
        }

        /*
         * ------------------------------------------------------------
         * PHASE 2
         * ------------------------------------------------------------
         *
         * Generate all remaining resource bundles normally.
         *
         * Iron/Gold will automatically be kept outside the center zone
         * through their resource configuration.
         */
        List<ResourceSpawnRule> placementOrder =
            new List<ResourceSpawnRule>(resources);

        // Reserve good locations for scarce resources first. Abundant
        // resources and details can then fill the remaining space naturally.
        placementOrder.Sort((left, right) =>
        {
            int scarcity = left.amount.CompareTo(right.amount);
            return scarcity != 0 ? scarcity : string.CompareOrdinal(left.resourceType.name, right.resourceType.name);
        });

        foreach (ResourceSpawnRule resource in placementOrder)
        {
            GenerateResourceType(
                resource,
                resourcesParent,
                random,
                occupiedResourceBundles);
        }
    }

    private void GenerateGuaranteedCenterResources(
        ResourceSpawnRule[] definitions,
        Transform resourcesParent,
        System.Random random,
        List<PlacedBundle> occupiedResourceBundles)
    {
        /*
         * First pass explicitly searches for resources marked as guaranteed.
         */
        foreach (ResourceSpawnRule resource in definitions)
        {
            if (!resource.guaranteedCenterBundle ||
                resource.prefab == null ||
                resource.amount <= 0)
            {
                continue;
            }

            string resourceName =
                resource.prefab.name;

            Transform resourceParent =
                GetOrCreateResourceTypeParent(
                    resourcesParent,
                    resourceName);

            bool generated =
                TryGenerateGuaranteedCenterBundle(
                    resource,
                    resourceParent,
                    random,
                    occupiedResourceBundles);

            if (!generated)
            {
                Debug.LogWarning(
                    $"Could not guarantee a center bundle for " +
                    $"resource '{resourceName}'. " +
                    $"Increase Center Resource Zone Radius, " +
                    $"reduce bundle separation, or increase placement attempts.",
                    this);
            }

        }
    }

    private bool TryGenerateGuaranteedCenterBundle(
        ResourceSpawnRule resource,
        Transform resourceParent,
        System.Random random,
        List<PlacedBundle> occupiedResourceBundles)
    {
        int minimumBundleSize =
            Mathf.Max(
                1,
                resource.minimumBundleSize);

        int maximumBundleSize =
            Mathf.Max(
                minimumBundleSize,
                resource.maximumBundleSize);

        float nodeSpacing =
            Mathf.Max(
                0.1f,
                resource.nodeSpacing);

        float bundleRadius =
            Mathf.Max(
                nodeSpacing,
                resource.bundleRadius);

        float bundleSeparation =
            resource.minimumBundleSeparation > 0f
                ? resource.minimumBundleSeparation
                : defaultResourceBundleSeparation;

        int requestedBundleSize =
            Mathf.Min(
                resource.amount,
                random.Next(
                    minimumBundleSize,
                    maximumBundleSize + 1));

        if (!TryGetCenterBundleCenter(
                bundleRadius,
                bundleSeparation,
                occupiedResourceBundles,
                random,
                CanPlaceResource,
                out Vector2 bundleCenter))
        {
            return false;
        }

        List<Vector2> bundlePositions =
            new List<Vector2>(
                requestedBundleSize)
            {
                bundleCenter
            };

        SpawnResourceNode(
            resource.prefab,
            resourceParent,
            bundleCenter,
            0,
            0);

        float effectiveBundleRadius =
            Mathf.Max(
                bundleRadius,
                nodeSpacing);

        for (int nodeIndex = 1;
             nodeIndex < requestedBundleSize;
             nodeIndex++)
        {
            if (!TryGrowBundle(
                    bundleCenter,
                    effectiveBundleRadius,
                    nodeSpacing,
                    bundlePositions,
                    random,
                    CanPlaceResource,
                    out Vector2 localPosition))
            {
                break;
            }

            bundlePositions.Add(localPosition);

            SpawnResourceNode(
                resource.prefab,
                resourceParent,
                localPosition,
                0,
                nodeIndex);
        }

        occupiedResourceBundles.Add(
            new PlacedBundle(
                bundleCenter,
                effectiveBundleRadius));

        return true;
    }

    private bool TryGetCenterBundleCenter(
        float bundleRadius,
        float minimumBundleSeparation,
        List<PlacedBundle> placedBundles,
        System.Random random,
        Func<Vector2, bool> canPlace,
        out Vector2 bundleCenter)
    {
        for (int attempt = 0;
             attempt < centerBundlePlacementAttempts;
             attempt++)
        {
            Vector2 candidate =
                GetRandomCenterZonePosition(
                    bundleRadius,
                    random);

            if (!CanPlaceInCenterZone(
                    candidate,
                    bundleRadius))
            {
                continue;
            }

            if (!canPlace(candidate))
            {
                continue;
            }

            if (!IsSeparatedFromOtherBundles(
                    candidate,
                    bundleRadius,
                    minimumBundleSeparation,
                    placedBundles))
            {
                continue;
            }

            bundleCenter = candidate;
            return true;
        }

        bundleCenter = default;
        return false;
    }

    private Vector2 GetRandomCenterZonePosition(
        float bundleRadius,
        System.Random random)
    {
        float minimumRadius =
            centerClearRadius + bundleRadius;

        float maximumRadius =
            Mathf.Max(
                minimumRadius,
                centerResourceZoneRadius - bundleRadius);

        /*
         * Uniform distribution across the area of the circle rather than
         * uniform distribution across its radius.
         */
        float t =
            (float)random.NextDouble();

        float radius =
            Mathf.Sqrt(
                Mathf.Lerp(
                    minimumRadius * minimumRadius,
                    maximumRadius * maximumRadius,
                    t));

        float angle =
            (float)random.NextDouble()
            * Mathf.PI
            * 2f;

        return
            spawnAreaCenter +
            new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle))
            * radius;
    }

    private bool CanPlaceInCenterZone(
        Vector2 localPosition,
        float bundleRadius)
    {
        float distance =
            Vector2.Distance(
                localPosition,
                spawnAreaCenter);

        /*
         * The whole bundle must fit inside the center zone.
         */
        return distance + bundleRadius <=
               centerResourceZoneRadius;
    }

    private void GenerateResourceType(
        ResourceSpawnRule resource,
        Transform resourcesParent,
        System.Random random,
        List<PlacedBundle> occupiedResourceBundles)
    {
        if (resource.prefab == null ||
            resource.amount <= 0)
        {
            return;
        }

        int minimumBundleSize =
            Mathf.Max(
                1,
                resource.minimumBundleSize);

        int maximumBundleSize =
            Mathf.Max(
                minimumBundleSize,
                resource.maximumBundleSize);

        int maximumBundles =
            Mathf.Max(
                1,
                resource.maximumBundles);

        float nodeSpacing =
            Mathf.Max(
                0.1f,
                resource.nodeSpacing);

        float bundleRadius =
            Mathf.Max(
                nodeSpacing,
                resource.bundleRadius);

        float bundleSeparation =
            resource.minimumBundleSeparation > 0f
                ? resource.minimumBundleSeparation
                : defaultResourceBundleSeparation;

        Transform resourceParent =
            GetOrCreateResourceTypeParent(
                resourcesParent,
                resource.prefab.name);

        ResourcePlacementState state =
            new ResourcePlacementState(
                0,
                0,
                0);

        /*
         * Determine whether this resource already received a guaranteed
         * center bundle.
         *
         * Guaranteed bundles are generated before this method.
         */
        int existingNodes =
            resource.guaranteedCenterBundle
                ? CountResourceNodes(resourceParent)
                : 0;

        bool centerBundleAlreadyGenerated =
            existingNodes > 0;

        /*
         * The guaranteed center bundle is already part of the requested
         * amount, so we need to account for it here.
         */
        if (centerBundleAlreadyGenerated)
        {
            /*
             * We don't know the exact number generated from this method,
             * so the resource amount accounting is handled by finding the
             * existing nodes under the generated parent.
             */
            state.spawnedAmount =
                existingNodes;

            state.bundleIndex = 1;
            state.generatedBundles = 1;
        }

        while (
            state.spawnedAmount < resource.amount &&
            state.generatedBundles < maximumBundles)
        {
            int remainingAmount =
                resource.amount -
                state.spawnedAmount;

            int bundleSize =
                Mathf.Min(
                    remainingAmount,
                    random.Next(
                        minimumBundleSize,
                        maximumBundleSize + 1));

            bool bundleGenerated =
                TryGenerateNormalResourceBundle(
                    resource,
                    resourceParent,
                    random,
                    occupiedResourceBundles,
                    bundleSize,
                    bundleSeparation,
                    bundleRadius,
                    nodeSpacing,
                    state.bundleIndex,
                    out int spawnedInBundle);

            if (!bundleGenerated ||
                spawnedInBundle <= 0)
            {
                LogPlacementWarning(
                    resource.prefab.name,
                    resource.amount,
                    state.spawnedAmount);

                return;
            }

            state.spawnedAmount +=
                spawnedInBundle;

            state.bundleIndex++;
            state.generatedBundles++;
        }

        if (state.spawnedAmount <
            resource.amount)
        {
            Debug.LogWarning(
                $"Resource '{resource.prefab.name}' reached its maximum " +
                $"bundle limit ({maximumBundles}) after placing " +
                $"{state.spawnedAmount} of {resource.amount} nodes.",
                this);
        }
    }

    private bool TryGenerateNormalResourceBundle(
        ResourceSpawnRule resource,
        Transform resourceParent,
        System.Random random,
        List<PlacedBundle> occupiedResourceBundles,
        int requestedBundleSize,
        float bundleSeparation,
        float bundleRadius,
        float nodeSpacing,
        int bundleIndex,
        out int spawnedInBundle)
    {
        spawnedInBundle = 0;

        /*
         * This delegate is what enforces the center-zone gameplay rule.
         *
         * Resources explicitly excluded from the center zone cannot be
         * placed there.
         */
        bool CanPlaceThisResource(Vector2 position)
        {
            if (!CanPlaceResource(position))
            {
                return false;
            }

            if (resource.excludeFromCenterZone ||
                !resource.allowedInCenterZone)
            {
                float distance =
                    Vector2.Distance(
                        position,
                        spawnAreaCenter);

                // Protect the complete center zone from the bundle footprint,
                // not only from the bundle's center point.
                if (distance - bundleRadius <
                    centerResourceZoneRadius)
                {
                    return false;
                }
            }

            return true;
        }

        if (!TryGetBundleCenter(
                bundleRadius,
                bundleSeparation,
                occupiedResourceBundles,
                random,
                CanPlaceThisResource,
                out Vector2 bundleCenter))
        {
            return false;
        }

        List<Vector2> bundlePositions =
            new List<Vector2>(
                requestedBundleSize)
            {
                bundleCenter
            };

        SpawnResourceNode(
            resource.prefab,
            resourceParent,
            bundleCenter,
            bundleIndex,
            0);

        spawnedInBundle++;

        float effectiveBundleRadius =
            Mathf.Max(
                bundleRadius,
                nodeSpacing);

        for (int nodeIndex = 1;
             nodeIndex < requestedBundleSize;
             nodeIndex++)
        {
            if (!TryGrowBundle(
                    bundleCenter,
                    effectiveBundleRadius,
                    nodeSpacing,
                    bundlePositions,
                    random,
                    CanPlaceThisResource,
                    out Vector2 localPosition))
            {
                break;
            }

            bundlePositions.Add(localPosition);

            SpawnResourceNode(
                resource.prefab,
                resourceParent,
                localPosition,
                bundleIndex,
                nodeIndex);

            spawnedInBundle++;
        }

        occupiedResourceBundles.Add(
            new PlacedBundle(
                bundleCenter,
                effectiveBundleRadius));

        return true;
    }

    private void SpawnResourceNode(
        GameObject prefab,
        Transform parent,
        Vector2 localPosition,
        int bundleIndex,
        int nodeIndex)
    {
        GameObject resourceNode =
            Instantiate(
                prefab,
                parent);

        resourceNode.name =
            $"{prefab.name}_Bundle_{bundleIndex + 1:00}_Node_{nodeIndex + 1:00}";

        resourceNode.transform.localPosition =
            new Vector3(
                localPosition.x,
                localPosition.y,
                0f);

        resourceNode.transform.localRotation = Quaternion.identity;
        Physics2D.SyncTransforms();
    }

    // ========================================================================
    // SCENE DETAILS
    // ========================================================================

    private void GenerateSceneDetails(int generationSeed)
    {
        SceneDetailListSO details = sceneDetailList;

        if (details == null ||
            details.list == null ||
            details.list.Count == 0)
        {
            return;
        }

        Transform detailsParent =
            GetOrCreateDetailsParent();

        var reservedDetailBundles = new List<PlacedBundle>();
        var orderedDetails = new List<SceneDetailSO>();
        var seenAssets = new HashSet<SceneDetailSO>();
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (SceneDetailSO detail in details.list)
        {
            if (detail == null || !seenAssets.Add(detail)) continue;
            if (string.IsNullOrEmpty(detail.StableId) || !identifiers.Add(detail.StableId))
            {
                Debug.LogWarning($"Detail '{detail.name}' needs a unique saved stable ID. Validate the asset, or use Assign New Identity to Duplicated Family on a duplicate.", detail);
                return; // Ambiguous identities must not make Inspector list order determine the winner.
            }
            orderedDetails.Add(detail);
        }
        orderedDetails.Sort(SceneDetailSO.CompareGenerationOrder);
        foreach (SceneDetailSO detail in orderedDetails)
        {
            var random = new System.Random(detail.DeriveSeed(generationSeed));
            if (!detail.Participates(random)) continue;
            GenerateDetailType(
                detail,
                detailsParent,
                random,
                reservedDetailBundles);
        }
    }

    private void GenerateDetailType(
        SceneDetailSO detail,
        Transform detailsParent,
        System.Random random,
        List<PlacedBundle> placedDetailBundles)
    {
        int requestedAmount = detail == null ? 0 : Mathf.Max(0, Mathf.RoundToInt(detail.amount * level.detailDensity));
        if (detail == null ||
            detail.prefabs == null ||
            detail.prefabs.Count == 0 ||
            requestedAmount <= 0)
        {
            return;
        }

        // Filter once per family. Missing variants must not count as successfully spawned details.
        var validPrefabs = detail.prefabs.FindAll(prefab => prefab != null);
        if (validPrefabs.Count == 0) return;
        detailsParent = GetOrCreateResourceTypeParent(detailsParent, detail.detailName);
        int minimumBundleSize =
            detail.generateInBundles
                ? Mathf.Max(
                    1,
                    detail.minimumBundleSize)
                : 1;

        int maximumBundleSize =
            detail.generateInBundles
                ? Mathf.Max(
                    minimumBundleSize,
                    detail.maximumBundleSize)
                : 1;

        float nodeSpacing =
            Mathf.Max(
                0.1f,
                detail.nodeSpacing);

        float bundleRadius =
            detail.generateInBundles
                ? Mathf.Max(
                    nodeSpacing,
                    detail.bundleRadius)
                : 0f;

        int spawnedAmount = 0;
        int bundleIndex = 0;

        while (spawnedAmount <
               requestedAmount)
        {
            int remainingAmount =
                requestedAmount -
                spawnedAmount;

            int bundleSize =
                Mathf.Min(
                    remainingAmount,
                    random.Next(
                        minimumBundleSize,
                        maximumBundleSize + 1));

            bool CanPlaceDetailAt(
                Vector2 position)
            {
                return CanPlaceDetail(
                    position,
                    detail);
            }

            if (!TryGetBundleCenter(
                    bundleRadius,
                    Mathf.Max(0, detail.minimumBundleSeparation),
                    detail.reservesBundleSpace ? placedDetailBundles : null,
                    random,
                    CanPlaceDetailAt,
                    out Vector2 bundleCenter))
            {
                LogPlacementWarning(
                    detail.detailName,
                    requestedAmount,
                    spawnedAmount);

                return;
            }

            List<Vector2> bundlePositions =
                new List<Vector2>(
                    bundleSize)
                {
                    bundleCenter
                };

            SpawnDetail(
                detail,
                validPrefabs,
                detailsParent,
                bundleCenter,
                bundleIndex,
                0,
                random);

            spawnedAmount++;

            for (int nodeIndex = 1;
                 nodeIndex < bundleSize;
                 nodeIndex++)
            {
                if (!TryGrowBundle(
                        bundleCenter,
                        bundleRadius,
                        nodeSpacing,
                        bundlePositions,
                        random,
                        CanPlaceDetailAt,
                        out Vector2 localPosition))
                {
                    break;
                }

                bundlePositions.Add(
                    localPosition);

                SpawnDetail(
                    detail,
                    validPrefabs,
                    detailsParent,
                    localPosition,
                    bundleIndex,
                    nodeIndex,
                    random);

                spawnedAmount++;
            }

            if (detail.reservesBundleSpace)
                placedDetailBundles.Add(new PlacedBundle(bundleCenter, bundleRadius));

            bundleIndex++;
        }
    }

    private void SpawnDetail(
        SceneDetailSO detail,
        List<GameObject> validPrefabs,
        Transform detailsParent,
        Vector2 localPosition,
        int bundleIndex,
        int nodeIndex,
        System.Random random)
    {
        GameObject prefab =
            GetRandomPrefab(
                validPrefabs,
                random);

        if (prefab == null)
        {
            return;
        }

        GameObject instance =
            Instantiate(
                prefab,
                detailsParent);

        instance.name =
            $"{detail.detailName}_Bundle_{bundleIndex + 1:00}_Detail_{nodeIndex + 1:00}";

        instance.transform.localPosition =
            new Vector3(
                localPosition.x,
                localPosition.y,
                0f);

        float rotation =
            Mathf.Lerp(
                detail.rotationRange.x,
                detail.rotationRange.y,
                (float)random.NextDouble());

        instance.transform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                rotation);

        float minimumScale =
            Mathf.Max(
                0.01f,
                Mathf.Min(
                    detail.scaleRange.x,
                    detail.scaleRange.y));

        float maximumScale =
            Mathf.Max(
                minimumScale,
                Mathf.Max(
                    detail.scaleRange.x,
                    detail.scaleRange.y));

        float scale =
            Mathf.Lerp(
                minimumScale,
                maximumScale,
                (float)random.NextDouble());

        instance.transform.localScale =
            Vector3.one * scale;

        foreach (
            SpriteRenderer spriteRenderer
            in instance.GetComponentsInChildren<SpriteRenderer>())
        {
            if (detail.randomFlipX)
            {
                spriteRenderer.flipX =
                    random.Next(0, 2) == 1;
            }

            if (detail.randomFlipY)
            {
                spriteRenderer.flipY =
                    random.Next(0, 2) == 1;
            }
        }
        Physics2D.SyncTransforms();
    }

    // ========================================================================
    // BUNDLE PLACEMENT
    // ========================================================================

    private bool TryGetBundleCenter(
        float bundleRadius,
        float minimumBundleSeparation,
        List<PlacedBundle> placedBundles,
        System.Random random,
        Func<Vector2, bool> canPlace,
        out Vector2 bundleCenter)
    {
        for (
            int attempt = 0;
            attempt < maxPlacementAttemptsPerNode;
            attempt++)
        {
            Vector2 candidate =
                GetRandomLocalPosition(
                    bundleRadius,
                    random);

            if (!canPlace(candidate))
            {
                continue;
            }

            if (!IsSeparatedFromOtherBundles(
                    candidate,
                    bundleRadius,
                    minimumBundleSeparation,
                    placedBundles))
            {
                continue;
            }

            bundleCenter = candidate;
            return true;
        }

        bundleCenter = default;
        return false;
    }

    private bool TryGrowBundle(
        Vector2 bundleCenter,
        float bundleRadius,
        float nodeSpacing,
        List<Vector2> bundlePositions,
        System.Random random,
        Func<Vector2, bool> canPlace,
        out Vector2 localPosition)
    {
        float minimumNodeDistance =
            nodeSpacing * 0.7f;

        for (
            int attempt = 0;
            attempt < maxPlacementAttemptsPerNode;
            attempt++)
        {
            Vector2 anchor =
                bundlePositions[
                    random.Next(
                        bundlePositions.Count)];

            float angle =
                (float)random.NextDouble()
                * Mathf.PI
                * 2f;

            float distance =
                nodeSpacing *
                Mathf.Lerp(
                    0.85f,
                    1.15f,
                    (float)random.NextDouble());

            Vector2 candidate =
                anchor +
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle))
                * distance;

            if (Vector2.Distance(
                    candidate,
                    bundleCenter) >
                bundleRadius)
            {
                continue;
            }

            if (!IsFarEnoughFromBundleNodes(
                    candidate,
                    bundlePositions,
                    minimumNodeDistance))
            {
                continue;
            }

            if (!canPlace(candidate))
            {
                continue;
            }

            localPosition = candidate;
            return true;
        }

        localPosition = default;
        return false;
    }

    // ========================================================================
    // COLLISION
    // ========================================================================

    private bool CanPlaceResource(
        Vector2 localPosition)
    {
        if (!IsInsideAllowedArea(
                localPosition))
        {
            return false;
        }

        Vector3 worldPosition =
            transform.TransformPoint(
                localPosition);

        Collider2D[] colliders =
            Physics2D.OverlapCircleAll(
                worldPosition,
                resourceCollisionClearance,
                blockingLayers);

        foreach (Collider2D collider in colliders)
        {
            if (collider.GetComponentInParent<ResourceNode>() == null)
            {
                return false;
            }
        }

        return true;
    }

    private bool CanPlaceDetail(
        Vector2 localPosition,
        SceneDetailSO detail)
    {
        if (!IsInsideAllowedArea(
                localPosition))
        {
            return false;
        }

        if (!detail.avoidResources && !detail.avoidBlockingObjects) return true;
        var filter = new ContactFilter2D { useTriggers = true };
        // Resource identity is independent of the blocking mask; a resource may live
        // on a nonblocking layer and still need protection from large props.
        filter.SetLayerMask(detail.avoidResources ? Physics2D.AllLayers : blockingLayers);
        detailCollisionResults.Clear();
        // Unity 6000.5 also gates explicit-filter overlap queries with this global
        // setting. Scope the override to this synchronous query and always restore it.
        bool previousTriggers = Physics2D.queriesHitTriggers;
        try
        {
            Physics2D.queriesHitTriggers = true;
            Physics2D.OverlapCircle(transform.TransformPoint(localPosition),
                Mathf.Max(0, detail.collisionClearance), filter, detailCollisionResults);
        }
        finally { Physics2D.queriesHitTriggers = previousTriggers; }
        foreach (Collider2D collider in detailCollisionResults)
        {
            if (collider.GetComponentInParent<ResourceNode>() != null)
            {
                if (detail.avoidResources) return false;
                continue; // avoidBlockingObjects must not silently override avoidResources=false.
            }
            if (detail.avoidBlockingObjects && (blockingLayers.value & (1 << collider.gameObject.layer)) != 0)
                return false;
        }
        return true;
    }

    // ========================================================================
    // AREA
    // ========================================================================

    private bool IsInsideAllowedArea(
        Vector2 localPosition)
    {
        Vector2 halfSize =
            spawnAreaSize * 0.5f;

        Vector2 offsetFromAreaCenter =
            localPosition -
            spawnAreaCenter;

        return
            Mathf.Abs(
                offsetFromAreaCenter.x) <=
            halfSize.x &&

            Mathf.Abs(
                offsetFromAreaCenter.y) <=
            halfSize.y &&

            offsetFromAreaCenter.magnitude >=
            centerClearRadius;
    }

    private Vector2 GetRandomLocalPosition(
        float bundleRadius,
        System.Random random)
    {
        Vector2 halfSize =
            spawnAreaSize * 0.5f -
            Vector2.one * bundleRadius;

        halfSize.x =
            Mathf.Max(
                0f,
                halfSize.x);

        halfSize.y =
            Mathf.Max(
                0f,
                halfSize.y);

        return
            spawnAreaCenter +
            new Vector2(
                Mathf.Lerp(
                    -halfSize.x,
                    halfSize.x,
                    (float)random.NextDouble()),

                Mathf.Lerp(
                    -halfSize.y,
                    halfSize.y,
                    (float)random.NextDouble()));
    }

    // ========================================================================
    // RESOURCE SEPARATION
    // ========================================================================

    private static bool IsSeparatedFromOtherBundles(
        Vector2 candidate,
        float candidateRadius,
        float minimumSeparation,
        List<PlacedBundle> placedBundles)
    {
        if (placedBundles == null) return true; // Non-reserving decorative composition.
        foreach (PlacedBundle bundle in placedBundles)
        {
            float requiredDistance =
                candidateRadius +
                bundle.radius +
                minimumSeparation;

            if (Vector2.Distance(
                    candidate,
                    bundle.center) <
                requiredDistance)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsFarEnoughFromBundleNodes(
        Vector2 candidate,
        List<Vector2> bundlePositions,
        float minimumDistance)
    {
        foreach (Vector2 position in bundlePositions)
        {
            if (Vector2.Distance(
                    candidate,
                    position) <
                minimumDistance)
            {
                return false;
            }
        }

        return true;
    }

    // ========================================================================
    // RESOURCE HELPERS
    // ========================================================================

    private static int CountResourceNodes(
        Transform resourceParent)
    {
        if (resourceParent == null)
        {
            return 0;
        }

        int count = 0;

        for (
            int i = 0;
            i < resourceParent.childCount;
            i++)
        {
            Transform child =
                resourceParent.GetChild(i);

            if (child.GetComponent<ResourceNode>() != null ||
                child.GetComponentInChildren<ResourceNode>() != null)
            {
                count++;
            }
        }

        return count;
    }

    // ========================================================================
    // PREFABS
    // ========================================================================

    private static GameObject GetRandomPrefab(
        List<GameObject> prefabs,
        System.Random random)
    {
        if (prefabs == null ||
            prefabs.Count == 0)
        {
            return null;
        }

        for (
            int attempt = 0;
            attempt < prefabs.Count;
            attempt++)
        {
            GameObject prefab =
                prefabs[
                    random.Next(
                        prefabs.Count)];

            if (prefab != null)
            {
                return prefab;
            }
        }

        return null;
    }

    // ========================================================================
    // HIERARCHY
    // ========================================================================

    private Transform GetOrCreateResourcesParent() => GetOrCreateResourceTypeParent(GetGeneratedRoot(), resourcesParentName);
    private Transform GetOrCreateDetailsParent() => GetOrCreateResourceTypeParent(GetGeneratedRoot(), detailsParentName);

    private Transform GetOrCreateResourceTypeParent(Transform parent, string groupName)
    {
        // Direct child comparison also handles asset names containing '/'.
        foreach (Transform child in parent)
            if (child.name == groupName) return child;
        var group = new GameObject(groupName);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    // ========================================================================
    // CLEANUP
    // ========================================================================

    private void DestroyGeneratedObject(
        GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            // Destroy is deferred until the end of the frame. Detach first so
            // a same-frame regeneration cannot reuse a doomed hierarchy.
            target.SetActive(false); // Remove colliders immediately, before deferred Destroy.
            target.transform.SetParent(null);
            target.name = $"{target.name} (Pending Destruction)";
            Destroy(target);
        }
        else
        {
#if UNITY_EDITOR
            if (RecordGenerationUndo) UnityEditor.Undo.DestroyObjectImmediate(target);
            else DestroyImmediate(target);
#else
            DestroyImmediate(target);
#endif
        }
    }

    // ========================================================================
    // LOGGING
    // ========================================================================

    private void LogPlacementWarning(
        string itemName,
        int requestedAmount,
        int spawnedAmount)
    {
        string displayName =
            string.IsNullOrWhiteSpace(itemName)
                ? "Unnamed"
                : itemName;

        Debug.LogWarning(
            $"Could only place {spawnedAmount} " +
            $"of {requestedAmount} '{displayName}' items. " +
            "Increase the spawn area or placement attempts, " +
            "increase the maximum bundle count, reduce bundle separation, " +
            "or reduce the configured amount.",
            this);
    }

    // ========================================================================
    // GIZMOS
    // ========================================================================

    private void OnDrawGizmosSelected()
    {
        if (level == null || level.resourceSettings == null) return;
        Vector3 worldCenter =
            transform.TransformPoint(
                spawnAreaCenter);

        // Spawn area
        Gizmos.color =
            Color.yellow;

        Gizmos.DrawWireCube(
            worldCenter,
            new Vector3(
                spawnAreaSize.x,
                spawnAreaSize.y,
                0f));

        // Gameplay center exclusion
        Gizmos.color =
            Color.red;

        Gizmos.DrawWireSphere(
            worldCenter,
            centerClearRadius);

        // Resource center zone
        Gizmos.color =
            Color.green;

        Gizmos.DrawWireSphere(
            worldCenter,
            centerResourceZoneRadius);
    }
}
