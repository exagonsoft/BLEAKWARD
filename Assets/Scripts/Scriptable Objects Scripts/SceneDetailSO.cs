using System.Collections.Generic;
using UnityEngine;

public enum SceneDetailPlacementLayer
{
    GroundCover,
    SmallProp,
    LargeProp,
    Landmark
}

[CreateAssetMenu(fileName = "SceneDetailSO", menuName = "Scriptable Objects/Scene Detail")]
public class SceneDetailSO : ScriptableObject
{
    [Header("Identity")]
    public string detailName;

    [SerializeField, HideInInspector] private string stableId;
    public string StableId => stableId;

    [Tooltip("Visual prefab variants selected randomly during generation.")]
    public List<GameObject> prefabs;

    [Header("Occurrence")]
    [Min(0)]
    public int amount = 100;

    [Range(0, 1), Tooltip("Probability that this entire detail family participates in a generated world, not a per-prefab weight.")]
    public float generationChance = 1;

    [Header("Placement Behavior")]
    [Tooltip("Determines generation priority: Landmark, LargeProp, SmallProp, GroundCover. The flags below remain authoritative.")]
    public SceneDetailPlacementLayer placementLayer = SceneDetailPlacementLayer.SmallProp;
    [Tooltip("Reserve bundle space against other reserving detail bundles. Disable for layered ground cover.")]
    public bool reservesBundleSpace = true;
    [Tooltip("Avoid ResourceNode colliders, including child colliders and triggers, independently of the level blocking mask.")]
    public bool avoidResources = true;
    [Tooltip("Avoid non-resource colliders on the level blocking layers. Resource avoidance is controlled separately.")]
    public bool avoidBlockingObjects = true;

    [Header("Bundles")]
    public bool generateInBundles = true;

    [Min(1)]
    public int minimumBundleSize = 2;

    [Min(1)]
    public int maximumBundleSize = 6;

    [Min(0f)]
    public float bundleRadius = 2.5f;

    [Min(0.1f)]
    public float nodeSpacing = 0.8f;

    [Header("Placement")]
    [Min(0f)]
    [Tooltip("Placement padding around the candidate pivot. Currently a circle check, not full prefab-footprint testing; larger props need conservative clearance.")]
    public float collisionClearance = 0.25f;

    [Min(0f)]
    [Tooltip("Composition gap between reserving bundles. Physical collision is controlled separately.")]
    public float minimumBundleSeparation = 2f;

    [Header("Variation")]
    public Vector2 scaleRange = new Vector2(0.85f, 1.15f);
    public Vector2 rotationRange = new Vector2(-5f, 5f);
    public bool randomFlipX = true;
    public bool randomFlipY;

    /// <summary>FNV-1a: seed bytes followed by the stable ID's UTF-16 code units, little endian.</summary>
    public int DeriveSeed(int generationSeed)
    {
        unchecked
        {
            uint hash = 2166136261u;
            uint seed = (uint)generationSeed;
            for (int i = 0; i < 4; i++) { hash = (hash ^ (byte)seed) * 16777619u; seed >>= 8; }
            foreach (char value in stableId ?? string.Empty)
            {
                hash = (hash ^ (byte)value) * 16777619u;
                hash = (hash ^ (byte)(value >> 8)) * 16777619u;
            }
            return (int)hash;
        }
    }

    public bool Participates(System.Random random)
    {
        float chance = float.IsNaN(generationChance) ? 0 : Mathf.Clamp01(generationChance);
        return chance >= 1 || (chance > 0 && random.NextDouble() < chance);
    }

    public static int CompareGenerationOrder(SceneDetailSO left, SceneDetailSO right)
    {
        int layer = right.placementLayer.CompareTo(left.placementLayer);
        return layer != 0 ? layer : string.CompareOrdinal(left.stableId, right.stableId);
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        // Pure managed ID creation is safe during import validation. Never replace a populated ID.
        if (string.IsNullOrEmpty(stableId)) stableId = System.Guid.NewGuid().ToString("N");
#endif
        amount = Mathf.Max(0, amount);
        minimumBundleSize = Mathf.Max(1, minimumBundleSize);
        maximumBundleSize = Mathf.Max(minimumBundleSize, maximumBundleSize);
        bundleRadius = NonNegative(bundleRadius);
        nodeSpacing = Mathf.Max(0.1f, NonNegative(nodeSpacing));
        collisionClearance = NonNegative(collisionClearance);
        minimumBundleSeparation = NonNegative(minimumBundleSeparation);
        generationChance = Mathf.Clamp01(NonNegative(generationChance));
        float low = Mathf.Max(0.01f, NonNegative(Mathf.Min(scaleRange.x, scaleRange.y)));
        float high = Mathf.Max(low, NonNegative(Mathf.Max(scaleRange.x, scaleRange.y)));
        scaleRange = new Vector2(low, high);
        rotationRange = new Vector2(Mathf.Min(rotationRange.x, rotationRange.y), Mathf.Max(rotationRange.x, rotationRange.y));
    }

    private static float NonNegative(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Max(0, value);

#if UNITY_EDITOR
    [ContextMenu("Assign New Identity to Duplicated Family")]
    private void AssignNewIdentity()
    {
        UnityEditor.Undo.RecordObject(this, "Assign Detail Identity");
        stableId = System.Guid.NewGuid().ToString("N");
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
