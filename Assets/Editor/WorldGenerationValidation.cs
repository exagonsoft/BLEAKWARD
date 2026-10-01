using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Integration checks against the actual gameplay scene and authored profiles.</summary>
public static class WorldGenerationValidation
{
    private const string LevelFolder = "Assets/Scriptable Objects/Levels/";
    private const string Output = "Logs/WorldGeneration";
    private static bool running;

    [MenuItem("BLEAKWARD/Validation/Validate World Generation", true)]
    private static bool CanRun() => !running && !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem("BLEAKWARD/Validation/Validate World Generation")]
    public static async void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run the editor checks outside Play Mode.");
        if (running) return;
        running = true;
        Directory.CreateDirectory(Output);
        var report = new StringBuilder();
        var errors = new List<string>();
        void TrackErrors(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(message);
        }
        Application.logMessageReceived += TrackErrors;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            report.AppendLine("PASS: " + message);
        }
        var previous = SceneManager.GetActiveScene();
        if (previous.isDirty)
        {
            Directory.CreateDirectory("Temp/WorldGeneration");
            string backup = "Temp/WorldGeneration/BeforeValidation-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
            EditorSceneManager.SaveScene(previous, backup, true);
            report.AppendLine("Saved pre-validation scene copy to " + backup);
        }
        if (previous.path != "Assets/Scenes/Light the Ward.unity")
            EditorSceneManager.OpenScene("Assets/Scenes/Light the Ward.unity");
        var generator = UnityEngine.Object.FindAnyObjectByType<SceneResourcesGenerator>();
        var ward = AssetDatabase.LoadAssetAtPath<LevelSO>(LevelFolder + "LVL_LightTheWard.asset");
        var industrial = AssetDatabase.LoadAssetAtPath<LevelSO>(LevelFolder + "LVL_IndustrialTest.asset");
        try
        {
            Check(generator != null && generator.Terrain != null, "Gameplay scene has one generator and the existing terrain controller.");
            Check(ward.TryValidate(out var error), "Ward profile valid: " + error);
            Check(industrial.TryValidate(out error), "Industrial profile valid: " + error);
            var manual = new GameObject("Resources");
            manual.transform.SetParent(generator.transform, false);
            generator.SetLevel(ward);
            var materialBefore = EditorJsonUtility.ToJson(generator.Terrain.Renderer.sharedMaterial);
            var randomBefore = UnityEngine.Random.state;
            generator.GenerateWorld();
            Check(UnityEngine.Random.state.Equals(randomBefore), "Generation does not consume UnityEngine.Random state.");
            var original = Snapshot(generator);
            ValidateLayout(generator, Check);
            var counts = Counts(generator);
            report.AppendLine("Ward counts: " + counts);
            await Task.Delay(200); // Let URP finish the frame before each independent capture.
            Capture(Camera.main, Output + "/LightTheWard.png");
            generator.GenerateWorld();
            Check(Snapshot(generator) == original, "Same seed reproduces resource/detail positions, scales, rotations and sprite flips.");
            Check(manual != null && manual.transform.parent == generator.transform, "Regeneration preserves manually authored name-matched objects.");
            UnityEngine.Object.DestroyImmediate(manual);
            generator.GenerateWorld(1338);
            Check(Snapshot(generator) != original, "Changing the seed changes the layout.");
            generator.SetLevel(industrial);
            generator.GenerateWorld();
            ValidateLayout(generator, Check);
            Check(Counts(generator) != counts, "Changing the level changes resource/detail counts.");
            report.AppendLine("Industrial counts: " + Counts(generator));
            var properties = new MaterialPropertyBlock();
            generator.Terrain.Renderer.GetPropertyBlock(properties);
            Check(properties.GetTexture("_TerrainA") == industrial.terrain.terrainA &&
                  Mathf.Approximately(properties.GetFloat("_Saturation"), industrial.terrain.saturation),
                  "Level switch applies terrain textures and appearance via property block.");
            await Task.Delay(200);
            Capture(Camera.main, Output + "/IndustrialTest.png");
            var shader = generator.Terrain.Renderer.sharedMaterial.shader;
            Check(shader.name == "BLEAKWARD/BLEAKWARD_Terrain" && !ShaderUtil.ShaderHasError(shader), "Terrain shader compiles and renders in URP.");
            foreach (var message in ShaderUtil.GetShaderMessages(shader)) report.AppendLine("Shader: " + message.message);
            generator.SetLevel(ward);
            generator.GenerateWorld();
            Check(Snapshot(generator) == original, "Switching back restores the original deterministic world.");
            Check(EditorJsonUtility.ToJson(generator.Terrain.Renderer.sharedMaterial) == materialBefore,
                "Level application leaves the shared terrain material unchanged.");
            await CheckWorldSpaceSampling(generator, Check, report);
            Check(errors.Count == 0, "No Unity errors during validation. " + string.Join("; ", errors));
            Selection.activeGameObject = generator.gameObject;
            SceneView.RepaintAll();
            report.AppendLine("Editor validation completed; Ward preview remains generated, scene not saved by validation.");
        }
        catch (Exception exception)
        {
            report.AppendLine("FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            Application.logMessageReceived -= TrackErrors;
            running = false;
            File.WriteAllText(Output + "/editor-validation.txt", report.ToString());
        }
    }

    public static string Snapshot(SceneResourcesGenerator generator)
    {
        var text = new StringBuilder();
        foreach (var root in generator.GetComponentsInChildren<GeneratedWorldRoot>())
        foreach (var t in root.GetComponentsInChildren<Transform>())
        {
            text.Append(t.name).Append('|').Append(t.localPosition.ToString("F5", CultureInfo.InvariantCulture))
                .Append(t.localRotation.ToString("F5", CultureInfo.InvariantCulture))
                .Append(t.localScale.ToString("F5", CultureInfo.InvariantCulture));
            foreach (var sprite in t.GetComponents<SpriteRenderer>())
            {
                text.Append(sprite.flipX).Append(sprite.flipY).Append(sprite.color.ToString("F5"));
                if (sprite.sprite != null)
                    text.Append(AssetDatabase.GetAssetPath(sprite.sprite)).Append(sprite.sprite.name);
            }
        }
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static string Counts(SceneResourcesGenerator generator) => string.Join(", ",
        generator.GetComponentsInChildren<ResourceNode>().GroupBy(n => n.resourceType.resourceType)
            .Select(g => g.Key + "=" + g.Count())) + ", Details=" + generator.GetComponentsInChildren<DetailNode>().Length;

    private static void ValidateLayout(SceneResourcesGenerator generator, Action<bool, string> check)
    {
        var level = generator.Level;
        var nodes = generator.GetComponentsInChildren<ResourceNode>();
        foreach (var rule in level.resourceSettings.resources)
        {
            var matching = nodes.Where(n => n.resourceType == rule.resourceType).ToArray();
            check(matching.Length == rule.amount, level.name + ": requested count for " + rule.resourceType.resourceType);
            if (rule.guaranteedCenterBundle && rule.amount > 0)
                check(matching.Any(n => ((Vector2)generator.transform.InverseTransformPoint(n.transform.position) - level.worldCenter).magnitude <= level.resourceSettings.centerResourceZoneRadius), "Starting bundle exists: " + rule.resourceType.resourceType);
        }
        foreach (var root in generator.GetComponentsInChildren<GeneratedWorldRoot>())
        foreach (var t in root.GetComponentsInChildren<Transform>())
        {
            if (t.GetComponent<ResourceNode>() == null && t.GetComponent<DetailNode>() == null) continue;
            var p = (Vector2)generator.transform.InverseTransformPoint(t.position) - level.worldCenter;
            if (p.magnitude < level.centerSafeRadius || Mathf.Abs(p.x) > level.worldSize.x / 2 || Mathf.Abs(p.y) > level.worldSize.y / 2)
                throw new InvalidOperationException("Generated item violates protected area or world bounds: " + t.name);
        }
        check(true, "Generated nodes respect bounds and center exclusion.");
        check(generator.GetComponentsInChildren<GeneratedWorldRoot>().Length == 1, "Exactly one owned generated hierarchy.");
    }

    private static async Task CheckWorldSpaceSampling(SceneResourcesGenerator generator, Action<bool, string> check, StringBuilder report)
    {
        var terrain = generator.Terrain.Renderer;
        int previousLayer = terrain.gameObject.layer;
        var cameraObject = new GameObject("Terrain Validation Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 16;
        camera.aspect = 1;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        // Include the scene's Light2D objects as well as the ground; excluding their
        // GameObject layer would make the lit terrain black and invalidate this test.
        camera.cullingMask = ~0;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        terrain.gameObject.layer = 31;
        Texture2D a = null, b = null;
        try
        {
            camera.transform.position = new Vector3(0, 0, -10);
            await Task.Delay(200);
            a = Render(camera, 512, 512);
            camera.transform.position += Vector3.right * 4; // Exactly 64 pixels at this projection.
            await Task.Delay(200);
            b = Render(camera, 512, 512);
            double error = 0, signal = 0;
            int count = 0;
            for (int y = 40; y < 472; y += 4)
            for (int x = 100; x < 460; x += 4)
            {
                Color first = a.GetPixel(x, y), second = b.GetPixel(x - 64, y);
                error += Mathf.Abs(first.r-second.r) + Mathf.Abs(first.g-second.g) + Mathf.Abs(first.b-second.b);
                signal += first.grayscale;
                count++;
            }
            report.AppendLine($"World-space camera-pan mean RGB error: {error / (count * 3):F6}; mean luminance: {signal / count:F4}");
            check(signal / count > 0.005 && error / (count * 3) < 0.015, "Camera movement keeps terrain sampling fixed at matching world positions.");
            File.WriteAllBytes(Output + "/TerrainCloseup.png", a.EncodeToPNG());
        }
        finally
        {
            terrain.gameObject.layer = previousLayer;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            if (a != null) UnityEngine.Object.DestroyImmediate(a);
            if (b != null) UnityEngine.Object.DestroyImmediate(b);
        }
    }

    private static Texture2D Render(Camera camera, int width, int height)
    {
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        try
        {
            // StandardRequest runs URP's complete camera/volume lifecycle. A single-camera
            // request skips volume initialization and can retain a disposed stack after reload.
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            return texture;
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
    }

    public static void Capture(Camera camera, string path)
    {
        var texture = Render(camera, 1280, 720);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }
}
