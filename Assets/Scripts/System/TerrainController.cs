using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
public sealed class TerrainController : MonoBehaviour
{
    [SerializeField, Tooltip("The one shared BLEAKWARD terrain material. Parameters live in the level's terrain profile.")]
    private Material terrainMaterial;
    private MaterialPropertyBlock properties;
    private SpriteRenderer terrainRenderer;
    public SpriteRenderer Renderer => terrainRenderer != null ? terrainRenderer : terrainRenderer = GetComponent<SpriteRenderer>();

    public void ApplyLevel(LevelSO level, int seed, Transform worldOrigin)
    {
        if (level == null || level.terrain == null || terrainMaterial == null) return;
        var settings = level.terrain;
        var renderer = Renderer;
        renderer.sharedMaterial = terrainMaterial;
        // Reuse the authored tiled sprite, layer and sorting order.
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.size = level.worldSize + Vector2.one * (2 * level.terrainBorder);
        transform.position = worldOrigin.TransformPoint(level.worldCenter);
        transform.rotation = worldOrigin.rotation;
        properties ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        Texture primary = settings.terrainA != null ? settings.terrainA : Texture2D.grayTexture;
        properties.SetTexture("_TerrainA", primary);
        properties.SetTexture("_TerrainB", settings.terrainB != null ? settings.terrainB : primary);
        properties.SetTexture("_TerrainC", settings.terrainC != null ? settings.terrainC : primary);
        properties.SetTexture("_TerrainD", settings.terrainD != null ? settings.terrainD : primary);
        properties.SetTexture("_DetailTex", settings.detailTexture != null ? settings.detailTexture : Texture2D.grayTexture);
        properties.SetFloat("_BaseTiling", settings.baseTiling);
        properties.SetFloat("_MacroScale", settings.macroScale);
        properties.SetFloat("_MacroStrength", settings.macroStrength);
        properties.SetFloat("_BlendSoftness", settings.blendSoftness);
        properties.SetColor("_MacroColorA", settings.macroColorA);
        properties.SetColor("_MacroColorB", settings.macroColorB);
        properties.SetFloat("_MacroColorScale", settings.macroColorScale);
        properties.SetFloat("_MacroColorStrength", settings.macroColorStrength);
        properties.SetFloat("_Brightness", settings.brightness);
        properties.SetFloat("_Saturation", settings.saturation);
        properties.SetFloat("_Contrast", settings.contrast);
        properties.SetFloat("_DetailTiling", settings.detailTiling);
        properties.SetFloat("_DetailStrength", settings.detailTexture != null ? settings.detailStrength : 0);
        // Integer hashing is stable and doesn't consume placement RNG or UnityEngine.Random.
        uint hash = unchecked((uint)seed * 747796405u + 2891336453u);
        properties.SetVector("_SeedOffset", new Vector4((hash & 65535) * 0.03125f, (hash >> 16) * 0.03125f, 0, 0));
        renderer.SetPropertyBlock(properties);
    }
}
