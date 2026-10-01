using UnityEngine;

/// <summary>The authoritative instructions for one procedural region.</summary>
[CreateAssetMenu(fileName = "LVL_NewRegion", menuName = "BLEAKWARD/Level")]
public sealed class LevelSO : ScriptableObject
{
    [Header("Identity")]
    public string levelName = "New Region";
    [Tooltip("Stable identifier for saves and future campaign selection.")]
    public string levelId = "new-region";
    public int defaultSeed = 1337;

    [Header("World (generator-local XY units)")]
    public Vector2 worldSize = new Vector2(240, 165);
    public Vector2 worldCenter;
    [Min(0), Tooltip("Protected radius around World Center for the HQ and construction.")]
    public float centerSafeRadius = 10;
    [Min(0), Tooltip("Visual ground extends this far beyond each edge of the generation bounds.")]
    public float terrainBorder = 10;

    [Header("Terrain")]
    public TerrainSettingsSO terrain;

    [Header("Environment")]
    [Tooltip("Null means no details. Individual definitions retain their own placement and variation rules.")]
    public SceneDetailListSO detailSet;
    [Range(0, 3), Tooltip("Multiplies each detail definition's amount without modifying the shared asset.")]
    public float detailDensity = 1;

    [Header("Resources")]
    public ResourceGenerationSettingsSO resourceSettings;

    [Header("Placement")]
    [Min(1)] public int maxPlacementAttemptsPerNode = 500;
    [Tooltip("Scene colliders on these layers block generation. Resources precede details.")]
    public LayerMask blockingLayers = Physics2D.AllLayers;

    public bool TryValidate(out string error)
    {
        error = null;
        if (worldSize.x <= 0 || worldSize.y <= 0 || centerSafeRadius < 0 ||
            centerSafeRadius * 2 >= Mathf.Min(worldSize.x, worldSize.y))
            error = "World bounds must be positive and larger than the center safe area.";
        else if (terrain == null || terrain.terrainA == null)
            error = "Assign a terrain profile with a primary texture.";
        else if (resourceSettings == null)
            error = "Assign resource generation settings (an empty rules array is allowed).";
        else if (!resourceSettings.TryValidate(centerSafeRadius, out error))
            return false;
        return error == null;
    }
}
