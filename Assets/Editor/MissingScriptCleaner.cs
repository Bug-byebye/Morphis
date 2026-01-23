using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MissingScriptCleaner : EditorWindow
{
    [MenuItem("Morphis/Debug/Cleanup Missing Scripts")]
    public static void ShowWindow()
    {
        GetWindow(typeof(MissingScriptCleaner));
    }

    public void OnGUI()
    {
        if (GUILayout.Button("Find and Remove Missing Scripts in Selected GameObjects"))
        {
            RemoveMissingScriptsInSelected();
        }
        
        if (GUILayout.Button("Find and Remove Missing Scripts in Entire Scene"))
        {
            RemoveMissingScriptsInScene();
        }
    }

    private static void RemoveMissingScriptsInSelected()
    {
        GameObject[] go = Selection.gameObjects;
        int compCount = 0;
        int goCount = 0;
        foreach (GameObject g in go)
        {
            var componentsRemoved = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(g);
            if (componentsRemoved > 0)
            {
                compCount += componentsRemoved;
                goCount++;
                Debug.Log($"Removed {componentsRemoved} missing scripts from {g.name}", g);
            }
        }
        Debug.Log($"Total removed: {compCount} scripts from {goCount} GameObjects");
    }

    private static void RemoveMissingScriptsInScene()
    {
        int totalRemoved = 0;
        int goCount = 0;
        
        // Find all GameObjects in scene
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in allObjects)
        {
            // Only inspect objects in valid scenes (not assets)
            if (go.hideFlags == HideFlags.NotEditable || go.hideFlags == HideFlags.HideAndDontSave)
                continue;
                
             if (!EditorUtility.IsPersistent(go.transform.root.gameObject))
             {
                 var count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                 if (count > 0)
                 {
                     totalRemoved += count;
                     goCount++;
                     Debug.Log($"Cleaned {go.name}: removed {count} missing scripts", go);
                 }
             }
        }
        
        Debug.Log($"<b>Cleanup Complete:</b> Removed {totalRemoved} missing scripts from {goCount} objects.");
    }
}
