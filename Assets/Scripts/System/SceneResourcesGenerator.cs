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

        int spawnedAmount = 0;

        for (int nodeIndex = 0; nodeIndex < resource.amount; nodeIndex++)
        {
            bool wasPlaced = false;

            for (int attempt = 0; attempt < maxPlacementAttemptsPerNode; attempt++)
            {
                Vector2 localPosition = GetRandomLocalPosition(random);

                if (!CanPlaceAt(localPosition))
                {
                    continue;
                }

                GameObject resourceNode = Instantiate(resource.prefab, transform);
                resourceNode.name = $"{resource.prefab.name}_{nodeIndex + 1:000}";
                resourceNode.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
                resourceNode.transform.localRotation = Quaternion.identity;

                spawnedAmount++;
                wasPlaced = true;
                break;
            }

            if (!wasPlaced)
            {
                Debug.LogWarning(
                    $"Could only place {spawnedAmount} of {resource.amount} '{resource.prefab.name}' nodes. " +
                    "Increase the spawn area or placement attempts, or reduce the minimum spacing.",
                    this);
                break;
            }
        }
    }

    private Vector2 GetRandomLocalPosition(System.Random random)
    {
        float halfWidth = spawnAreaSize.x * 0.5f;
        float halfHeight = spawnAreaSize.y * 0.5f;

        return spawnAreaCenter + new Vector2(
            Mathf.Lerp(-halfWidth, halfWidth, (float)random.NextDouble()),
            Mathf.Lerp(-halfHeight, halfHeight, (float)random.NextDouble()));
    }

    private bool CanPlaceAt(Vector2 localPosition)
    {
        if (Vector2.Distance(localPosition, spawnAreaCenter) < centerClearRadius)
        {
            return false;
        }

        Vector3 worldPosition = transform.TransformPoint(localPosition);
        return Physics2D.OverlapCircle(worldPosition, minimumSpacing, blockingLayers) == null;
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
