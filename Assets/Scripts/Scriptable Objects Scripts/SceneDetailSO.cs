using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SceneDetailSO", menuName = "Scriptable Objects/Scene Detail")]
public class SceneDetailSO : ScriptableObject
{
    public string detailName;

    [Tooltip("Visual prefab variants selected randomly during generation.")]
    public List<GameObject> prefabs;

    [Min(0)]
    public int amount = 100;

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
    public float collisionClearance = 0.25f;

    [Min(0f)]
    public float minimumBundleSeparation = 2f;

    [Header("Variation")]
    public Vector2 scaleRange = new Vector2(0.85f, 1.15f);
    public Vector2 rotationRange = new Vector2(-5f, 5f);
    public bool randomFlipX = true;
    public bool randomFlipY;
}
