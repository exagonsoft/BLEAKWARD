using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class SceneResourcesGenerator : MonoBehaviour
{
    [Serializable]
    private struct ResourceSpawnDefinition
    {
        [Tooltip("Resource prefab to spawn.")]
        public GameObject prefab;

        [Tooltip("Total number of resource nodes to generate.")]
        [Min(0)]
        public int amount;

        [Tooltip("Minimum number of nodes in a resource bundle.")]
        [Min(1)]
        public int minimumBundleSize;

        [Tooltip("Maximum number of nodes in a resource bundle.")]
        [Min(1)]
        public int maximumBundleSize;

        [Tooltip("Maximum number of bundles this resource type can create.")]
        [Min(1)]
        public int maximumBundles;

        [Tooltip("Maximum radius used when growing a bundle.")]
        [Min(0f)]
        public float bundleRadius;

        [Tooltip("Preferred distance between nodes in the same bundle.")]
        [Min(0.1f)]
        public float nodeSpacing;

        [Tooltip("Minimum distance between this resource's bundles and other resource bundles.")]
        [Min(0f)]
        public float minimumBundleSeparation;

        [Header("Center Zone")]

        [Tooltip(
            "Allows this resource to have its first guaranteed bundle " +
            "inside the center resource zone.")]
        public bool allowedInCenterZone;

        [Tooltip(
            "Forces this resource to have at least one bundle inside " +
            "the center resource zone.")]
        public bool guaranteedCenterBundle;

        [Tooltip(
            "Prevents this resource from spawning inside the center " +
            "resource zone.")]
        public bool excludeFromCenterZone;
    }

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

    [Header("Resources")]
    [Tooltip("Each entry represents one independently generated resource type.")]
    [SerializeField]
    private ResourceSpawnDefinition[] resources;

    [Header("Scene Details")]
    [Tooltip("Loaded from Resources/SceneDetailListSO when not assigned.")]
    [SerializeField]
    private SceneDetailListSO sceneDetailList;

    [Header("Spawn Area")]
    [SerializeField]
    private Vector2 spawnAreaSize = new Vector2(240f, 165f);

    [SerializeField]
    private Vector2 spawnAreaCenter;

    [Tooltip(
        "Nothing can spawn inside this radius from the center. " +
        "This is separate from the resource center zone.")]
    [SerializeField, Min(0f)]
    private float centerClearRadius = 12f;

    [Header("Center Resource Zone")]
    [Tooltip(
        "Radius around the spawn center where Wood and Stone are guaranteed " +
        "to have at least one bundle.")]
    [SerializeField, Min(0f)]
    private float centerResourceZoneRadius = 35f;

    [Tooltip(
        "Maximum attempts used when searching for a guaranteed center bundle.")]
    [SerializeField, Min(1)]
    private int centerBundlePlacementAttempts = 250;

    [Tooltip(
        "When enabled, Wood and Stone must have a bundle inside the center zone " +
        "or generation will report a warning.")]
    [SerializeField]
    private bool requireCenterResources = true;

    [Header("Resource Placement")]
    [Tooltip(
        "Additional physical clearance required when placing a resource node.")]
    [SerializeField, Min(0f)]
    private float resourceCollisionClearance = 0.2f;

    [Tooltip(
        "Fallback separation used when a resource has no per-resource value configured.")]
    [SerializeField, Min(0f)]
    [FormerlySerializedAs("minimumResourceBundleSeparation")]
    private float defaultResourceBundleSeparation = 9f;

    [Tooltip(
    "Maximum number of attempts made when searching for a valid resource position.")]
    [SerializeField, Min(1)]
    private int maxPlacementAttemptsPerNode = 100;

    [Tooltip("Layers considered when checking resource collisions.")]
    [SerializeField]
    private LayerMask blockingLayers = Physics2D.AllLayers;

    [Header("Generation")]
    [SerializeField]
    private bool generateOnStart = true;

    [SerializeField]
    private int randomSeed = 1337;

    [Tooltip(
        "Remove previously generated resources and details before generating again.")]
    [SerializeField]
    private bool clearBeforeGenerate = true;

    [Header("Generated Hierarchy")]
    [SerializeField]
    private string resourcesParentName = "Resources";

    [SerializeField]
    private string detailsParentName = "Scene Details";

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateResources();
        }
    }

    [ContextMenu("Generate Resources and Details")]
    public void GenerateResources()
    {
        System.Random random =
            new System.Random(randomSeed);

        if (clearBeforeGenerate)
        {
            ClearGeneratedContent();
        }

        GenerateResourceNodes(random);

        Physics2D.SyncTransforms();

        GenerateSceneDetails(random);
    }

    [ContextMenu("Clear Generated Resources and Details")]
    public void ClearGeneratedContent()
    {
        Transform resourcesParent =
            transform.Find(resourcesParentName);

        if (resourcesParent != null)
        {
            DestroyGeneratedObject(
                resourcesParent.gameObject);
        }

        Transform detailsParent =
            transform.Find(detailsParentName);

        if (detailsParent != null)
        {
            DestroyGeneratedObject(
                detailsParent.gameObject);
        }
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
            Debug.LogWarning(
                "No resource prefabs are configured.",
                this);

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
        List<ResourceSpawnDefinition> placementOrder =
            new List<ResourceSpawnDefinition>(resources);

        // Reserve good locations for scarce resources first. Abundant
        // resources and details can then fill the remaining space naturally.
        placementOrder.Sort(
            (left, right) => left.amount.CompareTo(right.amount));

        foreach (ResourceSpawnDefinition resource in placementOrder)
        {
            GenerateResourceType(
                resource,
                resourcesParent,
                random,
                occupiedResourceBundles);
        }
    }

    private void GenerateGuaranteedCenterResources(
        ResourceSpawnDefinition[] definitions,
        Transform resourcesParent,
        System.Random random,
        List<PlacedBundle> occupiedResourceBundles)
    {
        /*
         * First pass explicitly searches for resources marked as guaranteed.
         */
        foreach (ResourceSpawnDefinition resource in definitions)
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
        ResourceSpawnDefinition resource,
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
        ResourceSpawnDefinition resource,
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
        ResourceSpawnDefinition resource,
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

        resourceNode.transform.localRotation =
            Quaternion.identity;
    }

    // ========================================================================
    // SCENE DETAILS
    // ========================================================================

    private void GenerateSceneDetails(
        System.Random random)
    {
        SceneDetailListSO details =
            sceneDetailList != null
                ? sceneDetailList
                : Resources.Load<SceneDetailListSO>(
                    nameof(SceneDetailListSO));

        if (details == null ||
            details.list == null ||
            details.list.Count == 0)
        {
            Debug.LogWarning(
                "No SceneDetailListSO is configured or available in Resources.",
                this);

            return;
        }

        Transform detailsParent =
            GetOrCreateDetailsParent();

        List<PlacedBundle> placedDetailBundles =
            new List<PlacedBundle>();

        foreach (SceneDetailSO detail in details.list)
        {
            GenerateDetailType(
                detail,
                detailsParent,
                random,
                placedDetailBundles);
        }
    }

    private void GenerateDetailType(
        SceneDetailSO detail,
        Transform detailsParent,
        System.Random random,
        List<PlacedBundle> placedDetailBundles)
    {
        if (detail == null ||
            detail.prefabs == null ||
            detail.prefabs.Count == 0 ||
            detail.amount <= 0)
        {
            return;
        }

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
               detail.amount)
        {
            int remainingAmount =
                detail.amount -
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
                    detail.collisionClearance);
            }

            if (!TryGetBundleCenter(
                    bundleRadius,
                    detail.minimumBundleSeparation,
                    placedDetailBundles,
                    random,
                    CanPlaceDetailAt,
                    out Vector2 bundleCenter))
            {
                LogPlacementWarning(
                    detail.detailName,
                    detail.amount,
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
                    detailsParent,
                    localPosition,
                    bundleIndex,
                    nodeIndex,
                    random);

                spawnedAmount++;
            }

            placedDetailBundles.Add(
                new PlacedBundle(
                    bundleCenter,
                    bundleRadius));

            bundleIndex++;
        }
    }

    private void SpawnDetail(
        SceneDetailSO detail,
        Transform detailsParent,
        Vector2 localPosition,
        int bundleIndex,
        int nodeIndex,
        System.Random random)
    {
        GameObject prefab =
            GetRandomPrefab(
                detail.prefabs,
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
        float clearance)
    {
        if (!IsInsideAllowedArea(
                localPosition))
        {
            return false;
        }

        Vector3 worldPosition =
            transform.TransformPoint(
                localPosition);

        return Physics2D.OverlapCircle(
            worldPosition,
            Mathf.Max(
                0f,
                clearance),
            blockingLayers) == null;
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

    private static bool IsResourceNamed(
        ResourceSpawnDefinition resource,
        params string[] names)
    {
        if (resource.prefab == null)
        {
            return false;
        }

        string resourceName =
            resource.prefab.name.Trim();

        foreach (string name in names)
        {
            if (string.Equals(
                    resourceName,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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

    private Transform GetOrCreateResourcesParent()
    {
        Transform existingParent =
            transform.Find(
                resourcesParentName);

        if (existingParent != null)
        {
            return existingParent;
        }

        GameObject resourcesObject =
            new GameObject(
                resourcesParentName);

        resourcesObject.transform.SetParent(
            transform,
            false);

        return resourcesObject.transform;
    }

    private Transform GetOrCreateResourceTypeParent(
        Transform resourcesParent,
        string resourceName)
    {
        Transform existingParent =
            resourcesParent.Find(
                resourceName);

        if (existingParent != null)
        {
            return existingParent;
        }

        GameObject resourceObject =
            new GameObject(
                resourceName);

        resourceObject.transform.SetParent(
            resourcesParent,
            false);

        return resourceObject.transform;
    }

    private Transform GetOrCreateDetailsParent()
    {
        Transform existingParent =
            transform.Find(
                detailsParentName);

        if (existingParent != null)
        {
            return existingParent;
        }

        GameObject detailsObject =
            new GameObject(
                detailsParentName);

        detailsObject.transform.SetParent(
            transform,
            false);

        return detailsObject.transform;
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
            target.transform.SetParent(null);
            target.name = $"{target.name} (Pending Destruction)";
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
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
