using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Focused integration checks using small, temporary fixture families in an isolated area.</summary>
public static class SceneDetailEcologyValidation
{
    private static bool running;
    [MenuItem("BLEAKWARD/Validation/Validate Detail Ecology", true)]
    private static bool CanRun() => !running && !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem("BLEAKWARD/Validation/Validate Detail Ecology")]
    public static async void Run()
    {
        if (!CanRun()) throw new InvalidOperationException("Run ecology checks outside Play Mode.");
        running = true;
        var report = new StringBuilder();
        var temporary = new List<UnityEngine.Object>();
        var diagnostics = new List<string>();
        bool previousTriggers = Physics2D.queriesHitTriggers;
        Scene previousScene = SceneManager.GetActiveScene();
        Scene fixtureScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            report.AppendLine("PASS: " + message);
        }
        void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                diagnostics.Add(type + ": " + message);
        }
        Application.logMessageReceived += Log;
        try
        {
            var root = new GameObject("Ecology Test World");
            SceneManager.MoveGameObjectToScene(root, fixtureScene);
            root.transform.position = new Vector3(10000, 10000, 0);
            var generator = root.AddComponent<SceneResourcesGenerator>();
            var source = AssetDatabase.LoadAssetAtPath<LevelSO>("Assets/Scriptable Objects/Levels/LVL_LightTheWard.asset");
            var level = UnityEngine.Object.Instantiate(source);
            temporary.Add(level);
            level.detailDensity = 1;
            level.worldSize = new Vector2(240, 165);
            level.worldCenter = Vector2.zero;
            level.centerSafeRadius = 2;
            level.blockingLayers = 1 << 0;
            var resourceSettings = UnityEngine.Object.Instantiate(source.resourceSettings);
            temporary.Add(resourceSettings);
            resourceSettings.resources = Array.Empty<ResourceSpawnRule>();
            resourceSettings.requireCenterResources = false;
            level.resourceSettings = resourceSettings;
            var list = ScriptableObject.CreateInstance<SceneDetailListSO>();
            temporary.Add(list);
            list.list = new List<SceneDetailSO>();
            level.detailSet = list;

            var ground = new GameObject("Fixture Terrain", typeof(SpriteRenderer), typeof(TerrainController));
            ground.transform.SetParent(root.transform, false);
            var controller = ground.GetComponent<TerrainController>();
            var controllerData = new SerializedObject(controller);
            controllerData.FindProperty("terrainMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Terrain.mat");
            controllerData.ApplyModifiedPropertiesWithoutUndo();
            var generatorData = new SerializedObject(generator);
            generatorData.FindProperty("terrainController").objectReferenceValue = controller;
            generatorData.FindProperty("generateOnStart").boolValue = false;
            generatorData.ApplyModifiedPropertiesWithoutUndo();
            generator.SetLevel(level, 1337);

            var variantA = new GameObject("Variant A", typeof(SpriteRenderer), typeof(DetailNode));
            var variantB = new GameObject("Variant B", typeof(SpriteRenderer), typeof(DetailNode));
            foreach (var variant in new[] { variantA, variantB })
            {
                variant.transform.SetParent(root.transform, false);
                variant.transform.localPosition = new Vector3(1000, 1000, 0);
            }
            variantA.GetComponent<SpriteRenderer>().color = Color.red;
            variantB.GetComponent<SpriteRenderer>().color = Color.blue;
            SceneDetailSO Family(string id, SceneDetailPlacementLayer layer)
            {
                var detail = ScriptableObject.CreateInstance<SceneDetailSO>();
                temporary.Add(detail);
                detail.name = detail.detailName = id;
                var serialized = new SerializedObject(detail);
                serialized.FindProperty("stableId").stringValue = id;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                detail.placementLayer = layer;
                detail.prefabs = new List<GameObject> { variantA, variantB };
                detail.amount = 12;
                detail.reservesBundleSpace = false;
                detail.avoidResources = false;
                detail.avoidBlockingObjects = false;
                return detail;
            }
            var grass = Family("a-grass", SceneDetailPlacementLayer.GroundCover);
            var rocks = Family("b-rocks", SceneDetailPlacementLayer.SmallProp);
            var machinery = Family("c-machinery", SceneDetailPlacementLayer.LargeProp);
            var landmark = Family("d-landmark", SceneDetailPlacementLayer.Landmark);
            var peer = Family("e-peer", SceneDetailPlacementLayer.SmallProp);
            var golden = Family("ecology-test-family", SceneDetailPlacementLayer.SmallProp);
            var fresh = ScriptableObject.CreateInstance<SceneDetailSO>();
            temporary.Add(fresh);
            var validate = typeof(SceneDetailSO).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
            validate.Invoke(fresh, null);
            string freshId = fresh.StableId;
            validate.Invoke(fresh, null);
            var reloaded = ScriptableObject.CreateInstance<SceneDetailSO>();
            temporary.Add(reloaded);
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(fresh), reloaded);
            Check(!string.IsNullOrEmpty(freshId) && fresh.StableId == freshId && reloaded.StableId == freshId,
                "Missing identity is created once and survives serialization/validation.");
            Check(golden.DeriveSeed(1337) == -2002696033, "Stable seed matches an independently calculated FNV-1a test vector.");
            Check(golden.DeriveSeed(1337) != golden.DeriveSeed(1338), "Level seed changes the family seed.");
            Check(golden.DeriveSeed(1337) != grass.DeriveSeed(1337), "Stable family ID changes the stream.");
            string savedId = golden.StableId;
            golden.name = "Renamed asset";
            golden.detailName = "Renamed display label";
            typeof(SceneDetailSO).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(golden, null);
            Check(golden.StableId == savedId && golden.DeriveSeed(1337) == -2002696033, "Validation and renaming retain the saved identity and stream.");
            golden.minimumBundleSize = 6; golden.maximumBundleSize = 2;
            golden.bundleRadius = -1; golden.nodeSpacing = -2; golden.generationChance = 5;
            golden.scaleRange = new Vector2(2, 0.5f);
            typeof(SceneDetailSO).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(golden, null);
            Check(golden.maximumBundleSize == 6 && golden.bundleRadius == 0 && golden.nodeSpacing >= 0.1f &&
                  golden.generationChance == 1 && golden.scaleRange == new Vector2(0.5f, 2), "Artist values are normalized without exceptions.");
            golden.generationChance = 0;
            Check(!golden.Participates(new System.Random(1)), "Zero chance never participates.");
            golden.generationChance = 1;
            Check(golden.Participates(new System.Random(1)), "Chance one always participates.");
            golden.generationChance = 0.35f;
            int participating = 0;
            for (int seed = 0; seed < 100; seed++)
            {
                bool a = golden.Participates(new System.Random(golden.DeriveSeed(seed)));
                bool b = golden.Participates(new System.Random(golden.DeriveSeed(seed)));
                CheckQuiet(a == b, "Occurrence changed for the same seed.");
                if (a) participating++;
            }
            Check(participating > 0 && participating < 100, "Intermediate occurrence is deterministic and varies across seeds.");

            list.list = new List<SceneDetailSO> { grass, peer, machinery, rocks, landmark };
            generator.GenerateWorld();
            string baseline = WorldGenerationValidation.Snapshot(generator);
            generator.GenerateWorld();
            Check(WorldGenerationValidation.Snapshot(generator) == baseline, "Seed 1337 reproduces positions, variants, scales, rotations and flips.");
            var groups = generator.transform.Find("GeneratedWorld/Scene Details");
            Check(groups.GetChild(0).name == landmark.detailName && groups.GetChild(1).name == machinery.detailName &&
                  groups.GetChild(2).name == rocks.detailName && groups.GetChild(3).name == peer.detailName &&
                  groups.GetChild(4).name == grass.detailName, "Restrictive layers precede ground cover; ties use ordinal stable ID.");
            list.list.Reverse();
            var listOrder = list.list.ToArray();
            generator.GenerateWorld();
            Check(WorldGenerationValidation.Snapshot(generator) == baseline, "Reordering the SO list does not alter generated layouts.");
            Check(list.list.SequenceEqual(listOrder), "Generator does not mutate the SO list order.");
            string FamilySnapshot(SceneDetailSO detail)
            {
                var group = generator.transform.Find("GeneratedWorld/Scene Details/" + detail.detailName);
                return string.Join("|", group.GetComponentsInChildren<Transform>().Select(t =>
                    t.name + t.localPosition.ToString("F5") + t.localRotation.ToString("F5") + t.localScale.ToString("F5"))) +
                    string.Join("|", group.GetComponentsInChildren<SpriteRenderer>().Select(r => r.color.ToString("F5") + r.flipX + r.flipY));
            }
            string stableRocks = FamilySnapshot(rocks), stablePeer = FamilySnapshot(peer), stableLandmark = FamilySnapshot(landmark);
            grass.amount = 36;
            generator.GenerateWorld();
            Check(FamilySnapshot(rocks) == stableRocks && FamilySnapshot(landmark) == stableLandmark, "Changing ground-cover amount leaves other family layouts unchanged.");
            rocks.amount = 24;
            generator.GenerateWorld();
            Check(FamilySnapshot(peer) == stablePeer, "Changing an earlier family in the same layer does not perturb the next family's RNG.");
            grass.generationChance = 0;
            generator.GenerateWorld();
            Check(generator.transform.Find("GeneratedWorld/Scene Details/" + grass.detailName) == null, "Category chance zero skips the entire family in the generator.");
            grass.generationChance = 1;
            generator.GenerateWorld();
            Check(generator.transform.Find("GeneratedWorld/Scene Details/" + grass.detailName).childCount == 36, "Category chance one generates the requested amount.");

            // Overlap is guaranteed geometrically: each radius-70 center fits inside a small
            // inset rectangle. No colliders, so this tests composition reservation alone.
            list.list = new List<SceneDetailSO> { landmark, machinery };
            level.centerSafeRadius = 0;
            foreach (var detail in list.list)
            {
                detail.amount = 1; detail.minimumBundleSize = detail.maximumBundleSize = 1;
                detail.bundleRadius = 70; detail.minimumBundleSeparation = 4;
            }
            landmark.reservesBundleSpace = true;
            machinery.reservesBundleSpace = false;
            generator.GenerateWorld();
            Check(DetailCount(generator) == 2, "Non-reserving detail can overlap an earlier reserved bundle radius.");
            landmark.reservesBundleSpace = false;
            machinery.reservesBundleSpace = true;
            generator.GenerateWorld();
            Check(DetailCount(generator) == 2, "Non-reserving details do not reserve space against later reserving families.");
            landmark.reservesBundleSpace = true;
            foreach (var detail in list.list) { detail.bundleRadius = 10; detail.amount = 2; detail.minimumBundleSeparation = 12; }
            generator.GenerateWorld();
            var positions = generator.GetComponentsInChildren<GeneratedWorldRoot>().Single().GetComponentsInChildren<DetailNode>().Select(d => d.transform.position).ToArray();
            bool separated = positions.Length == 4;
            for (int i = 0; i < positions.Length; i++)
            for (int j = i + 1; j < positions.Length; j++) separated &= Vector2.Distance(positions[i], positions[j]) >= 32;
            Check(separated, "Reserving families respect radius plus separation from other reservations.");

            generator.ClearGeneratedContent();
            Check(generator.GetComponentsInChildren<GeneratedWorldRoot>().Length == 0, "Clear removes all owned generated objects.");
            level.centerSafeRadius = 2;
            var resource = new GameObject("Resource fixture", typeof(ResourceNode));
            resource.transform.SetParent(root.transform, false);
            resource.transform.localPosition = new Vector3(10, 0, 0);
            var resourceCollider = new GameObject("Resource child collider", typeof(CircleCollider2D));
            resourceCollider.layer = 8; // Deliberately outside the level's Default-only mask.
            resourceCollider.transform.SetParent(resource.transform, false);
            resourceCollider.GetComponent<CircleCollider2D>().isTrigger = true;
            var blocker = new GameObject("Blocking fixture", typeof(BoxCollider2D));
            blocker.transform.SetParent(root.transform, false);
            blocker.transform.localPosition = new Vector3(15, 0, 0);
            await Task.Delay(200); // Allow Editor physics to register newly added fixture colliders.
            Physics2D.SyncTransforms();
            Physics2D.queriesHitTriggers = false;
            var canPlace = typeof(SceneResourcesGenerator).GetMethod("CanPlaceDetail", BindingFlags.Instance | BindingFlags.NonPublic);
            bool Allows(Vector2 point) => (bool)canPlace.Invoke(generator, new object[] { point, grass });
            grass.avoidResources = true; grass.avoidBlockingObjects = false;
            Check(!Allows(new Vector2(10, 0)), "Resource avoidance sees child trigger colliders outside blocking layers, even with queriesHitTriggers disabled.");
            Check(!Physics2D.queriesHitTriggers, "Placement restores the project's trigger-query setting.");
            grass.avoidResources = false; grass.avoidBlockingObjects = true;
            resourceCollider.layer = 0;
            Physics2D.SyncTransforms();
            Check(Allows(new Vector2(10, 0)), "Permissive resource flag is not overridden by generic blocking avoidance.");
            Check(!Allows(new Vector2(15, 0)), "Blocking-object avoidance remains active independently.");
            grass.avoidBlockingObjects = false;
            Check(Allows(new Vector2(15, 0)), "Permissive ground cover may coexist with blocking footprints.");
            Check(!Allows(Vector2.zero) && !Allows(new Vector2(500, 0)), "Permissive flags never bypass center safety or world bounds.");
            Check(diagnostics.Count == 0, "No Unity warnings/errors during ecology validation. " + string.Join("; ", diagnostics));
            report.AppendLine("Detail ecology validation completed.");
        }
        catch (Exception exception)
        {
            report.AppendLine("FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            Physics2D.queriesHitTriggers = previousTriggers;
            EditorSceneManager.CloseScene(fixtureScene, true);
            foreach (var item in temporary) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            if (previousScene.IsValid()) SceneManager.SetActiveScene(previousScene);
            Application.logMessageReceived -= Log;
            Directory.CreateDirectory("Logs/WorldGeneration");
            File.WriteAllText("Logs/WorldGeneration/ecology-validation.txt", report.ToString());
            running = false;
        }
    }

    private static int DetailCount(SceneResourcesGenerator generator) =>
        generator.GetComponentsInChildren<GeneratedWorldRoot>().Sum(root => root.GetComponentsInChildren<DetailNode>().Length);
    private static void CheckQuiet(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
