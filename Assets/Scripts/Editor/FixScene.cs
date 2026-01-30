using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class FixScene : EditorWindow
{
    [MenuItem("Morphis/Fix Scene")]
    public static void RunFix()
    {
        // Operate on the currently open scene
        Scene currentScene = SceneManager.GetActiveScene();
        Debug.Log($"Fixing scene: {currentScene.path}");

        // Find the player object (checking common names)
        GameObject player = GameObject.Find("PlayerArmature");
        if (player == null) player = GameObject.Find("Player");
        
        if (player != null)
        {
            // Destroy immediately to remove it from the scene data
            DestroyImmediate(player);
            Debug.Log("Removed Player object from scene (will be spawned by NetworkManager).");
            
            // Mark scene as dirty
            EditorSceneManager.MarkSceneDirty(currentScene);
        }
        else
        {
            Debug.Log("No Player object found to remove. Ensuring scene is saved...");
        }

        // Save the scene to persist changes and generate sceneIds if any other objects need it
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Scene saved and fixed.");
    }
}
