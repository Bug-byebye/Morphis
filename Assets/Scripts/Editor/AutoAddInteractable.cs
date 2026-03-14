using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool to automatically add InteractableObject component to all objects with colliders
/// </summary>
public class AutoAddInteractable : EditorWindow
{
    [MenuItem("Tools/Add InteractableObject to All Colliders")]
    static void AddInteractableToAll()
    {
        int count = 0;
        
        // Find all objects with colliders
        Collider[] allColliders = GameObject.FindObjectsOfType<Collider>();
        
        foreach (Collider col in allColliders)
        {
            // Skip if it's a player or UI element
            if (col.gameObject.name.Contains("Player") || 
                col.gameObject.layer == LayerMask.NameToLayer("UI"))
                continue;
            
            // Add InteractableObject if it doesn't exist
            if (col.GetComponent<InteractableObject>() == null)
            {
                col.gameObject.AddComponent<InteractableObject>();
                count++;
                Debug.Log($"Added InteractableObject to: {col.gameObject.name}");
            }
        }
        
        Debug.Log($"✅ Added InteractableObject to {count} objects");
    }
}
