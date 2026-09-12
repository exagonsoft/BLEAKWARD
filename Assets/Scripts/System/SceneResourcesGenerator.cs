using System;
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

        [Tooltip("Maximum distance from the bundle center.")]
        [Min(0f)]
        public float bundleRadius;
    }

    [Header("Resources")]
    [SerializeField] private ResourceSpawnDefinition[] resources;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(240f, 165f);
    [SerializeField] private Vector2 spawnAreaCenter;
    [SerializeField, Min(0f)] private float minimumSpacing = 2.5f;
    [SerializeField, Min(0f)] private float centerClearRadius = 12f;
    [SerializeField, Min(1)] private int maxPlacementAttemptsPerNode = 50;
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

        foreach (ResourceSpawnDefinition resource in resources)
        {
            GenerateResourceType(resource, random);
        }
    }

    private void GenerateResourceType(ResourceSpawnDefinition resource, System.Random random)
    {
        if (resource.prefab == null || resource.amount <= 0)
        {
            return;
        }

        int minimumBundleSize = Mathf.Max(1, resource.minimumBundleSize);
        int maximumBundleSize = Mathf.Max(minimumBundleSize, resource.maximumBundleSize);
        float bundleRadius = Mathf.Max(minimumSpacing, resource.bundleRadius);
        int spawnedAmount = 0;
        int bundleIndex = 0;

        while (spawnedAmount < resource.amount)
        {
            int remainingAmount = resource.amount - spawnedAmount;
            int bundleSize = Mathf.Min(
                remainingAmount,
                random.Next(minimumBundleSize, maximumBundleSize + 1));

            if (!TryGetBundleCenter(random, out Vector2 bundleCenter))
            {
                LogPlacementWarning(resource, spawnedAmount);
                return;
            }

            int nodesSpawnedInBundle = 0;

            for (int nodeIndex = 0; nodeIndex < bundleSize; nodeIndex++)
            {
                if (!TryGetPositionInBundle(bundleCenter, bundleRadius, random, out Vector2 localPosition))
                {
                    break;
                }

                spawnedAmount++;
                nodesSpawnedInBundle++;

                GameObject resourceNode = Instantiate(resource.prefab, transform);
                resourceNode.name =
                    $"{resource.prefab.name}_Bundle_{bundleIndex + 1:00}_Node_{nodesSpawnedInBundle:00}";
                resourceNode.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
                resourceNode.transform.localRotation = Quaternion.identity;
            }

            if (nodesSpawnedInBundle == 0)
            {
                LogPlacementWarning(resource, spawnedAmount);
                return;
            }

            bundleIndex++;
        }
    }

    private bool TryGetBundleCenter(System.Random random, out Vector2 bundleCenter)
    {
        for (int attempt = 0; attempt < maxPlacementAttemptsPerNode; attempt++)
        {
            Vector2 candidate = GetRandomLocalPosition(random);

            if (CanPlaceAt(candidate))
            {
                bundleCenter = candidate;
                return true;
            }
        }

        bundleCenter = default;
        return false;
    }

    private bool TryGetPositionInBundle(
        Vector2 bundleCenter,
        float bundleRadius,
        System.Random random,
        out Vector2 localPosition)
    {
        for (int attempt = 0; attempt < maxPlacementAttemptsPerNode; attempt++)
        {
            Vector2 candidate = attempt == 0
                ? bundleCenter
                : bundleCenter + GetRandomPointInCircle(bundleRadius, random);

            if (CanPlaceAt(candidate))
            {
                localPosition = candidate;
                return true;
            }
        }

        localPosition = default;
        return false;
    }

    private Vector2 GetRandomLocalPosition(System.Random random)
    {
        float halfWidth = spawnAreaSize.x * 0.5f;
        float halfHeight = spawnAreaSize.y * 0.5f;

        return spawnAreaCenter + new Vector2(
            Mathf.Lerp(-halfWidth, halfWidth, (float)random.NextDouble()),
            Mathf.Lerp(-halfHeight, halfHeight, (float)random.NextDouble()));
    }

    private static Vector2 GetRandomPointInCircle(float radius, System.Random random)
    {
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        float distance = Mathf.Sqrt((float)random.NextDouble()) * radius;

        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }

    private bool CanPlaceAt(Vector2 localPosition)
    {
        Vector2 halfSize = spawnAreaSize * 0.5f;
        Vector2 offsetFromAreaCenter = localPosition - spawnAreaCenter;

        if (Mathf.Abs(offsetFromAreaCenter.x) > halfSize.x ||
            Mathf.Abs(offsetFromAreaCenter.y) > halfSize.y)
        {
            return false;
        }

        if (offsetFromAreaCenter.magnitude < centerClearRadius)
        {
            return false;
        }

        Vector3 worldPosition = transform.TransformPoint(localPosition);
        return Physics2D.OverlapCircle(worldPosition, minimumSpacing, blockingLayers) == null;
    }

    private void LogPlacementWarning(ResourceSpawnDefinition resource, int spawnedAmount)
    {
        Debug.LogWarning(
            $"Could only place {spawnedAmount} of {resource.amount} '{resource.prefab.name}' nodes. " +
            "Increase the spawn area, bundle radius, or placement attempts, or reduce the minimum spacing.",
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
