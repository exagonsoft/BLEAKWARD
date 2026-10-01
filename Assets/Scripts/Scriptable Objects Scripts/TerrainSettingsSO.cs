using UnityEngine;

[CreateAssetMenu(fileName = "TER_NewSurface", menuName = "BLEAKWARD/Terrain Settings")]
public sealed class TerrainSettingsSO : ScriptableObject
{
    [Header("Tileable Textures (standalone textures, not atlas regions)")]
    public Texture2D terrainA;
    [Tooltip("Optional layers fall back to A. Their meanings are chosen by the level designer.")]
    public Texture2D terrainB;
    public Texture2D terrainC;
    public Texture2D terrainD;

    [Header("Surface")]
    [Range(0.01f, 2)] public float baseTiling = 0.12f;
    [Range(0, 2)] public float brightness = 0.8f;
    [Range(0, 2)] public float saturation = 0.55f;
    [Range(0.1f, 2)] public float contrast = 0.9f;

    [Header("Macro Variation")]
    [Range(0.001f, 0.2f), Tooltip("World-space noise frequency. Lower values create broader patches.")]
    public float macroScale = 0.035f;
    [Range(0, 1)] public float macroStrength = 0.7f;
    [Range(0.05f, 0.5f)] public float blendSoftness = 0.3f;
    public Color macroColorA = new Color(0.74f, 0.76f, 0.67f);
    public Color macroColorB = new Color(0.55f, 0.59f, 0.58f);
    [Range(0.001f, 0.2f)] public float macroColorScale = 0.012f;
    [Range(0, 1)] public float macroColorStrength = 0.3f;

    [Header("Fine Detail")]
    public Texture2D detailTexture;
    [Range(0.01f, 4)] public float detailTiling = 0.6f;
    [Range(0, 0.3f)] public float detailStrength = 0.06f;
}
