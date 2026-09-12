using System;
using System.Collections.Generic;
using UnityEngine;

public class SceneResourcesGenerator : MonoBehaviour
{
    [Serializable]
    private struct ResourceSpawnDefinition
    {
        public GameObject prefab;

        [Min(0)]
        public int amount;

        [Min(1)]
        public int minimumBundleSize;

        [Min(1)]
        public int maximumBundleSize;

        [Min(0f)]
        public float bundleRadius;

        [Min(0.1f)]
        public float nodeSpacing;
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

    [Header("Resources")]
    [SerializeField] private ResourceSpawnDefinition[] resources;

    [Header("Scene Details")]
    [Tooltip("Loaded from Resources/SceneDetailListSO when not assigned.")]
    [SerializeField] private SceneDetailListSO sceneDetailList;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(240f, 165f);
    [SerializeField] private Vector2 spawnAreaCenter;
    [SerializeField, Min(0f)] private float centerClearRadius = 12f;
    [SerializeField, Min(0f)] private float resourceCollisionClearance = 0.2f;
    [SerializeField, Min(0f)] private float minimumResourceBundleSeparation = 9f;
    [SerializeField, Min(1)] private int maxPlacementAttemptsPerNode = 100;
    [SerializeField] private LayerMask blockingLayers = Physics2D.AllLayers;

    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private int randomSeed = 1337;

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
        System.Random random = new System.Random(randomSeed);
        GenerateResourceNodes(random);

        Physics2D.SyncTransforms();
        GenerateSceneDetails(random);
    }

    private void GenerateResourceNodes(System.Random random)
    {
        if (resources == null || resources.Length == 0)
        {
            Debug.LogWarning("No resource prefabs are configured.", this);
            return;
        }

        List<PlacedBundle> placedBundles = new List<PlacedBundle>();

        foreach (ResourceSpawnDefinition resource in resources)
        {
            GenerateResourceType(resource, random, placedBundles);
        }
    }

    private void GenerateResourceType(
        ResourceSpawnDefinition resource,
        System.Random random,
        List<PlacedBundle> placedBundles)
    {
        if (resource.prefab == null || resource.amount <= 0)
        {
            return;
        }

        int minimumBundleSize = Mathf.Max(1, resource.minimumBundleSize);
        int maximumBundleSize = Mathf.Max(minimumBundleSize, resource.maximumBundleSize);
        float nodeSpacing = Mathf.Max(0.1f, resource.nodeSpacing);
        float bundleRadius = Mathf.Max(nodeSpacing, resource.bundleRadius);
        int spawnedAmount = 0;
        int bundleIndex = 0;

        while (spawnedAmount < resource.amount)
        {
            int remainingAmount = resource.amount - spawnedAmount;
            int bundleSize = Mathf.Min(
                remainingAmount,
                random.Next(minimumBundleSize, maximumBundleSize + 1));

            if (!TryGetBundleCenter(
                    bundleRadius,
                    minimumResourceBundleSeparation,
                    placedBundles,
                    random,
                    CanPlaceResource,
                    out Vector2 bundleCenter))
            {
                LogPlacementWarning(resource.prefab.name, resource.amount, spawnedAmount);
                return;
            }

            List<Vector2> bundlePositions = new List<Vector2>(bundleSize)
            {
                bundleCenter
            };

            SpawnResourceNode(resource.prefab, bundleCenter, bundleIndex, 0);
            spawnedAmount++;

            for (int nodeIndex = 1; nodeIndex < bundleSize; nodeIndex++)
            {
                if (!TryGrowBundle(
                        bundleCenter,
                        bundleRadius,
                        nodeSpacing,
                        bundlePositions,
                        random,
                        CanPlaceResource,
                        out Vector2 localPosition))
                {
                    break;
                }

                bundlePositions.Add(localPosition);
                SpawnResourceNode(resource.prefab, localPosition, bundleIndex, nodeIndex);
                spawnedAmount++;
            }

            placedBundles.Add(new PlacedBundle(bundleCenter, bundleRadius));
            bundleIndex++;
        }
    }

    private void GenerateSceneDetails(System.Random random)
    {
        SceneDetailListSO details = sceneDetailList != null
            ? sceneDetailList
            : Resources.Load<SceneDetailListSO>(nameof(SceneDetailListSO));

        if (details == null || details.list == null || details.list.Count == 0)
        {
            Debug.LogWarning("No SceneDetailListSO is configured or available in Resources.", this);
            return;
        }

        Transform detailsParent = GetOrCreateDetailsParent();
        List<PlacedBundle> placedBundles = new List<PlacedBundle>();

        foreach (SceneDetailSO detail in details.list)
        {
            GenerateDetailType(detail, detailsParent, random, placedBundles);
        }
    }

    private void GenerateDetailType(
        SceneDetailSO detail,
        Transform detailsParent,
        System.Random random,
        List<PlacedBundle> placedBundles)
    {
        if (detail == null || detail.prefabs == null || detail.prefabs.Count == 0 || detail.amount <= 0)
        {
            return;
        }

        int minimumBundleSize = detail.generateInBundles
            ? Mathf.Max(1, detail.minimumBundleSize)
            : 1;
        int maximumBundleSize = detail.generateInBundles
            ? Mathf.Max(minimumBundleSize, detail.maximumBundleSize)
            : 1;
        float nodeSpacing = Mathf.Max(0.1f, detail.nodeSpacing);
        float bundleRadius = detail.generateInBundles
            ? Mathf.Max(nodeSpacing, detail.bundleRadius)
            : 0f;
        int spawnedAmount = 0;
        int bundleIndex = 0;

        while (spawnedAmount < detail.amount)
        {
            int remainingAmount = detail.amount - spawnedAmount;
            int bundleSize = Mathf.Min(
                remainingAmount,
                random.Next(minimumBundleSize, maximumBundleSize + 1));

            bool CanPlaceDetailAt(Vector2 position)
            {
                return CanPlaceDetail(position, detail.collisionClearance);
            }

            if (!TryGetBundleCenter(
                    bundleRadius,
                    detail.minimumBundleSeparation,
                    placedBundles,
                    random,
                    CanPlaceDetailAt,
                    out Vector2 bundleCenter))
            {
                LogPlacementWarning(detail.detailName, detail.amount, spawnedAmount);
                return;
            }

            List<Vector2> bundlePositions = new List<Vector2>(bundleSize)
            {
                bundleCenter
            };

            SpawnDetail(detail, detailsParent, bundleCenter, bundleIndex, 0, random);
            spawnedAmount++;

            for (int nodeIndex = 1; nodeIndex < bundleSize; nodeIndex++)
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

                bundlePositions.Add(localPosition);
                SpawnDetail(detail, detailsParent, localPosition, bundleIndex, nodeIndex, random);
                spawnedAmount++;
            }

            placedBundles.Add(new PlacedBundle(bundleCenter, bundleRadius));
            bundleIndex++;
        }
    }

    private bool TryGetBundleCenter(
        float bundleRadius,
        float minimumBundleSeparation,
        List<PlacedBundle> placedBundles,
        System.Random random,
        Func<Vector2, bool> canPlace,
        out Vector2 bundleCenter)
    {
        for (int attempt = 0; attempt < maxPlacementAttemptsPerNode; attempt++)
        {
            Vector2 candidate = GetRandomLocalPosition(bundleRadius, random);

            if (canPlace(candidate) &&
                IsSeparatedFromOtherBundles(
                    candidate,
                    bundleRadius,
                    minimumBundleSeparation,
                    placedBundles))
            {
                bundleCenter = candidate;
                return true;
            }
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
        float minimumNodeDistance = nodeSpacing * 0.7f;

        for (int attempt = 0; attempt < maxPlacementAttemptsPerNode; attempt++)
        {
            Vector2 anchor = bundlePositions[random.Next(bundlePositions.Count)];
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = nodeSpacing * Mathf.Lerp(0.85f, 1.15f, (float)random.NextDouble());
            Vector2 candidate = anchor + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            if (Vector2.Distance(candidate, bundleCenter) > bundleRadius ||
                !IsFarEnoughFromBundleNodes(candidate, bundlePositions, minimumNodeDistance) ||
                !canPlace(candidate))
            {
                continue;
            }

            localPosition = candidate;
            return true;
        }

        localPosition = default;
        return false;
    }

    private void SpawnResourceNode(
        GameObject prefab,
        Vector2 localPosition,
        int bundleIndex,
        int nodeIndex)
    {
        GameObject resourceNode = Instantiate(prefab, transform);
        resourceNode.name = $"{prefab.name}_Bundle_{bundleIndex + 1:00}_Node_{nodeIndex + 1:00}";
        resourceNode.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        resourceNode.transform.localRotation = Quaternion.identity;
    }

    private void SpawnDetail(
        SceneDetailSO detail,
        Transform detailsParent,
        Vector2 localPosition,
        int bundleIndex,
        int nodeIndex,
        System.Random random)
    {
        GameObject prefab = GetRandomPrefab(detail.prefabs, random);

        if (prefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(prefab, detailsParent);
        instance.name =
            $"{detail.detailName}_Bundle_{bundleIndex + 1:00}_Detail_{nodeIndex + 1:00}";
        instance.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);

        float rotation = Mathf.Lerp(
            detail.rotationRange.x,
            detail.rotationRange.y,
            (float)random.NextDouble());
        instance.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        float minimumScale = Mathf.Max(0.01f, Mathf.Min(detail.scaleRange.x, detail.scaleRange.y));
        float maximumScale = Mathf.Max(minimumScale, Mathf.Max(detail.scaleRange.x, detail.scaleRange.y));
        float scale = Mathf.Lerp(minimumScale, maximumScale, (float)random.NextDouble());
        instance.transform.localScale = Vector3.one * scale;

        foreach (SpriteRenderer spriteRenderer in instance.GetComponentsInChildren<SpriteRenderer>())
        {
            if (detail.randomFlipX)
            {
                spriteRenderer.flipX = random.Next(0, 2) == 1;
            }

            if (detail.randomFlipY)
            {
                spriteRenderer.flipY = random.Next(0, 2) == 1;
            }
        }
    }

    private static GameObject GetRandomPrefab(List<GameObject> prefabs, System.Random random)
    {
        for (int attempt = 0; attempt < prefabs.Count; attempt++)
        {
            GameObject prefab = prefabs[random.Next(prefabs.Count)];

            if (prefab != null)
            {
                return prefab;
            }
        }

        return null;
    }

    private Transform GetOrCreateDetailsParent()
    {
        Transform existingParent = transform.Find("Scene Details");

        if (existingParent != null)
        {
            return existingParent;
        }

        GameObject detailsObject = new GameObject("Scene Details");
        detailsObject.transform.SetParent(transform, false);
        return detailsObject.transform;
    }

    private Vector2 GetRandomLocalPosition(float bundleRadius, System.Random random)
    {
        Vector2 halfSize = spawnAreaSize * 0.5f - Vector2.one * bundleRadius;
        halfSize.x = Mathf.Max(0f, halfSize.x);
        halfSize.y = Mathf.Max(0f, halfSize.y);

        return spawnAreaCenter + new Vector2(
            Mathf.Lerp(-halfSize.x, halfSize.x, (float)random.NextDouble()),
            Mathf.Lerp(-halfSize.y, halfSize.y, (float)random.NextDouble()));
    }

    private bool CanPlaceResource(Vector2 localPosition)
    {
        if (!IsInsideAllowedArea(localPosition))
        {
            return false;
        }

        Vector3 worldPosition = transform.TransformPoint(localPosition);
        Collider2D[] colliders = Physics2D.OverlapCircleAll(
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

    private bool CanPlaceDetail(Vector2 localPosition, float clearance)
    {
        if (!IsInsideAllowedArea(localPosition))
        {
            return false;
        }

        Vector3 worldPosition = transform.TransformPoint(localPosition);
        return Physics2D.OverlapCircle(
            worldPosition,
            Mathf.Max(0f, clearance),
            blockingLayers) == null;
    }

    private bool IsInsideAllowedArea(Vector2 localPosition)
    {
        Vector2 halfSize = spawnAreaSize * 0.5f;
        Vector2 offsetFromAreaCenter = localPosition - spawnAreaCenter;

        return Mathf.Abs(offsetFromAreaCenter.x) <= halfSize.x &&
               Mathf.Abs(offsetFromAreaCenter.y) <= halfSize.y &&
               offsetFromAreaCenter.magnitude >= centerClearRadius;
    }

    private static bool IsSeparatedFromOtherBundles(
        Vector2 candidate,
        float candidateRadius,
        float minimumSeparation,
        List<PlacedBundle> placedBundles)
    {
        foreach (PlacedBundle bundle in placedBundles)
        {
            float requiredDistance = candidateRadius + bundle.radius + minimumSeparation;

            if (Vector2.Distance(candidate, bundle.center) < requiredDistance)
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
            if (Vector2.Distance(candidate, position) < minimumDistance)
            {
                return false;
            }
        }

        return true;
    }

    private void LogPlacementWarning(string itemName, int requestedAmount, int spawnedAmount)
    {
        string displayName = string.IsNullOrWhiteSpace(itemName) ? "Unnamed" : itemName;

        Debug.LogWarning(
            $"Could only place {spawnedAmount} of {requestedAmount} '{displayName}' items. " +
            "Increase the spawn area or placement attempts, reduce bundle separation, " +
            "or reduce the configured amount.",
            this);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 worldCenter = transform.TransformPoint(spawnAreaCenter);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(worldCenter, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0f));

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(worldCenter, centerClearRadius);
    }
}
