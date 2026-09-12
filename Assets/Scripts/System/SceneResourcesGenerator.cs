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

        [Tooltip("Minimum number of nodes generated in each bundle.")]
        [Min(1)]
        public int minimumBundleSize;

        [Tooltip("Maximum number of nodes generated in each bundle.")]
        [Min(1)]
        public int maximumBundleSize;

        [Tooltip("Maximum distance a node can grow from the bundle center.")]
        [Min(0f)]
        public float bundleRadius;

        [Tooltip("Approximate center-to-center distance between neighboring nodes.")]
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

    [Header("Spawn Area")]
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(240f, 165f);
    [SerializeField] private Vector2 spawnAreaCenter;
    [SerializeField, Min(0f)] private float centerClearRadius = 12f;
    [SerializeField, Min(0f)] private float collisionClearance = 0.25f;
    [SerializeField, Min(0f)] private float minimumBundleSeparation = 8f;
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

    [ContextMenu("Generate Resources")]
    public void GenerateResources()
    {
        if (resources == null || resources.Length == 0)
        {
            Debug.LogWarning("No resource prefabs are configured.", this);
            return;
        }

        System.Random random = new System.Random(randomSeed);
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

            if (!TryGetBundleCenter(bundleRadius, placedBundles, random, out Vector2 bundleCenter))
            {
                LogPlacementWarning(resource, spawnedAmount);
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

    private bool TryGetBundleCenter(
        float bundleRadius,
        List<PlacedBundle> placedBundles,
        System.Random random,
        out Vector2 bundleCenter)
    {
        for (int attempt = 0; attempt < maxPlacementAttemptsPerNode; attempt++)
        {
            Vector2 candidate = GetRandomLocalPosition(bundleRadius, random);

            if (CanPlaceAt(candidate) &&
                IsSeparatedFromOtherBundles(candidate, bundleRadius, placedBundles))
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
                !CanPlaceAt(candidate))
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

    private Vector2 GetRandomLocalPosition(float bundleRadius, System.Random random)
    {
        Vector2 halfSize = spawnAreaSize * 0.5f - Vector2.one * bundleRadius;
        halfSize.x = Mathf.Max(0f, halfSize.x);
        halfSize.y = Mathf.Max(0f, halfSize.y);

        return spawnAreaCenter + new Vector2(
            Mathf.Lerp(-halfSize.x, halfSize.x, (float)random.NextDouble()),
            Mathf.Lerp(-halfSize.y, halfSize.y, (float)random.NextDouble()));
    }

    private bool CanPlaceAt(Vector2 localPosition)
    {
        Vector2 halfSize = spawnAreaSize * 0.5f;
        Vector2 offsetFromAreaCenter = localPosition - spawnAreaCenter;

        if (Mathf.Abs(offsetFromAreaCenter.x) > halfSize.x ||
            Mathf.Abs(offsetFromAreaCenter.y) > halfSize.y ||
            offsetFromAreaCenter.magnitude < centerClearRadius)
        {
            return false;
        }

        Vector3 worldPosition = transform.TransformPoint(localPosition);
        Collider2D[] colliders = Physics2D.OverlapCircleAll(
            worldPosition,
            collisionClearance,
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

    private bool IsSeparatedFromOtherBundles(
        Vector2 candidate,
        float candidateRadius,
        List<PlacedBundle> placedBundles)
    {
        foreach (PlacedBundle bundle in placedBundles)
        {
            float requiredDistance =
                candidateRadius + bundle.radius + minimumBundleSeparation;

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

    private void LogPlacementWarning(ResourceSpawnDefinition resource, int spawnedAmount)
    {
        Debug.LogWarning(
            $"Could only place {spawnedAmount} of {resource.amount} '{resource.prefab.name}' nodes. " +
            "Increase the spawn area or placement attempts, reduce bundle separation, " +
            "or reduce the configured resource amount.",
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
