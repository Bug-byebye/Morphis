using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds MeshColliders to all mesh-bearing descendants of the selected object.
/// Useful for quickly making imported static buildings walkable/blocking in-scene.
/// </summary>
public static class MakeSelectedSolidTool
{
    [MenuItem("Tools/Make Selected Solid (Mesh Colliders)")]
    private static void MakeSelectedSolid()
    {
        var selection = Selection.gameObjects;
        if (selection == null || selection.Length == 0)
        {
            Debug.LogWarning("Select one or more scene objects first.");
            return;
        }

        int visitedMeshes = 0;
        int addedColliders = 0;
        int updatedColliders = 0;

        foreach (var root in selection)
        {
            if (root == null) continue;

            foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                visitedMeshes++;

                var go = meshFilter.gameObject;
                var collider = go.GetComponent<MeshCollider>();
                if (collider == null)
                {
                    collider = Undo.AddComponent<MeshCollider>(go);
                    addedColliders++;
                }
                else
                {
                    updatedColliders++;
                }

                Undo.RecordObject(collider, "Configure Mesh Collider");
                collider.sharedMesh = meshFilter.sharedMesh;
                collider.convex = false;
                EditorUtility.SetDirty(collider);
            }

            foreach (var skinnedRenderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null) continue;

                visitedMeshes++;

                var go = skinnedRenderer.gameObject;
                var collider = go.GetComponent<MeshCollider>();
                if (collider == null)
                {
                    collider = Undo.AddComponent<MeshCollider>(go);
                    addedColliders++;
                }
                else
                {
                    updatedColliders++;
                }

                Undo.RecordObject(collider, "Configure Mesh Collider");
                collider.sharedMesh = skinnedRenderer.sharedMesh;
                collider.convex = false;
                EditorUtility.SetDirty(collider);
            }
        }

        Debug.Log(
            $"[MakeSelectedSolid] Visited {visitedMeshes} mesh objects, " +
            $"added {addedColliders} MeshColliders, updated {updatedColliders} existing MeshColliders."
        );
    }

    [MenuItem("Tools/Make Selected Solid (Mesh Colliders)", true)]
    private static bool ValidateMakeSelectedSolid()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }

    [MenuItem("Morphis/Make Selected Solid (Mesh Colliders)")]
    private static void MakeSelectedSolidMorphis()
    {
        MakeSelectedSolid();
    }

    [MenuItem("Morphis/Make Selected Solid (Mesh Colliders)", true)]
    private static bool ValidateMakeSelectedSolidMorphis()
    {
        return ValidateMakeSelectedSolid();
    }
}
