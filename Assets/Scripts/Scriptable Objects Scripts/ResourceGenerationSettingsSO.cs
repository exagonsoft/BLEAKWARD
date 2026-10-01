using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ResourceSpawnRule
{
    [Tooltip("Resource identity. Must match the ResourceNode on the prefab.")]
    public ResourceTypeSO resourceType;
    public GameObject prefab;
    [Min(0), Tooltip("Total nodes, including any guaranteed starting bundle. This controls scarcity.")]
    public int amount;
    [Min(1)] public int minimumBundleSize;
    [Min(1)] public int maximumBundleSize;
    [Min(1)] public int maximumBundles;
    [Min(0)] public float bundleRadius;
    [Min(0.1f)] public float nodeSpacing;
    [Min(0)] public float minimumBundleSeparation;
    [Header("Starting Economy")]
    public bool allowedInCenterZone;
    public bool guaranteedCenterBundle;
    public bool excludeFromCenterZone;
    // Future resource dressing can reference a reusable definition here without changing resource identity.
}

[CreateAssetMenu(fileName = "RES_NewDistribution", menuName = "BLEAKWARD/Resource Generation Settings")]
public sealed class ResourceGenerationSettingsSO : ScriptableObject
{
    [Header("Distribution")]
    public ResourceSpawnRule[] resources = Array.Empty<ResourceSpawnRule>();
    [Header("Starting Economy")]
    [Min(0), Tooltip("Outer radius for guaranteed bundles; the Level's safe area remains empty.")]
    public float centerResourceZoneRadius = 34;
    [Min(1)] public int centerBundlePlacementAttempts = 500;
    public bool requireCenterResources = true;
    [Header("Placement")]
    [Min(0)] public float resourceCollisionClearance = 0.2f;
    [Min(0)] public float defaultResourceBundleSeparation = 8;

    public bool TryValidate(float safeRadius, out string error)
    {
        error = null;
        var types = new HashSet<ResourceTypeSO>();
        var prefabs = new HashSet<GameObject>();
        foreach (var rule in resources ?? Array.Empty<ResourceSpawnRule>())
        {
            var node = rule.prefab == null ? null : rule.prefab.GetComponentInChildren<ResourceNode>();
            if (rule.resourceType == null || node == null || node.resourceType != rule.resourceType)
                error = "Every resource rule needs an identity and a matching ResourceNode prefab.";
            else if (!types.Add(rule.resourceType) || !prefabs.Add(rule.prefab))
                error = "Use one distribution rule per resource type and prefab.";
            else if (rule.amount < 0 || rule.minimumBundleSize < 1 ||
                     rule.maximumBundleSize < rule.minimumBundleSize || rule.maximumBundles < 1 ||
                     rule.nodeSpacing <= 0 || rule.bundleRadius < 0)
                error = "Resource amounts, bundle sizes, spacing and limits must be valid.";
            else if (rule.guaranteedCenterBundle && requireCenterResources && rule.amount > 0 &&
                     (!rule.allowedInCenterZone || rule.excludeFromCenterZone ||
                      centerResourceZoneRadius < safeRadius + 2 * Mathf.Max(rule.bundleRadius, rule.nodeSpacing)))
                error = "Guaranteed bundles must be allowed in the center zone and fit outside the safe area.";
            if (error != null) return false;
        }
        return true;
    }
}
