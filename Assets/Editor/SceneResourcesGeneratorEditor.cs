using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(SceneResourcesGenerator))]
public sealed class SceneResourcesGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var generator = (SceneResourcesGenerator)target;
        if (DrawDefaultInspector())
        {
            generator.PreviewTerrain();
            SceneView.RepaintAll();
        }
        EditorGUILayout.HelpBox("LevelSO owns the world instructions. Generate replaces only content owned by this generator. Terrain stays on the authored tiled sprite.", MessageType.Info);
        if (generator.Level != null && !generator.Level.TryValidate(out string error))
            EditorGUILayout.HelpBox(error, MessageType.Error);
        using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || generator.Level == null))
        {
            if (GUILayout.Button("Generate World")) RunWithUndo(generator, true);
            if (GUILayout.Button("Preview Terrain"))
            {
                generator.PreviewTerrain();
                SceneView.RepaintAll();
            }
        }
        if (GUILayout.Button("Clear Generated World")) RunWithUndo(generator, false);
    }

    private static void RunWithUndo(SceneResourcesGenerator generator, bool generate)
    {
        if (!Application.isPlaying)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(generate ? "Generate World" : "Clear Generated World");
            // Full hierarchy snapshot restores owned content, including child renderer variation.
            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "World Generation");
            if (generator.Terrain != null)
                Undo.RegisterFullObjectHierarchyUndo(generator.Terrain.gameObject, "Terrain Preview");
        }
        generator.RecordGenerationUndo = !Application.isPlaying;
        try
        {
            if (generate) generator.GenerateWorld(); else generator.ClearGeneratedContent();
        }
        finally { generator.RecordGenerationUndo = false; }
        if (!Application.isPlaying)
        {
            foreach (var root in generator.GetComponentsInChildren<GeneratedWorldRoot>())
                if (root.owner == generator) Undo.RegisterCreatedObjectUndo(root.gameObject, "Generated World");
            EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        }
        SceneView.RepaintAll();
    }
}
