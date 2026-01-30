using UnityEngine;
using UnityEditor;
using Mirror;
using StarterAssets;
using System.Linq;

public class MultiplayerSetupTool : EditorWindow
{
    [MenuItem("Morphis/Setup Multiplayer")]
    public static void Setup()
    {
        SetupPlayerPrefab();
        SetupScene();
        Debug.Log("Multiplayer Setup Complete!");
    }

    private static void SetupPlayerPrefab()
    {
        string prefabPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            Debug.LogError($"Could not find player prefab at {prefabPath}");
            return;
        }

        using (var editScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            GameObject root = editScope.prefabContentsRoot;

            // 1. Add NetworkIdentity
            if (root.GetComponent<NetworkIdentity>() == null)
            {
                var ni = root.AddComponent<NetworkIdentity>();
                Debug.Log("Added NetworkIdentity to Player Prefab");
            }

            // 2. Add NetworkTransformReliable
            // Note: In newer Mirror versions, check namespace. Assuming standard Mirror.
            var netTransform = root.GetComponent<Mirror.NetworkTransformReliable>();
            if (netTransform == null)
            {
                // Remove older NetworkTransform if exists
                var oldNt = root.GetComponent("NetworkTransform"); 
                if (oldNt != null) DestroyImmediate(oldNt);

                netTransform = root.AddComponent<Mirror.NetworkTransformReliable>();
                netTransform.target = root.transform; // Sync root
                Debug.Log("Added NetworkTransformReliable to Player Prefab");
            }
        }
    }

    private static void SetupScene()
    {
        // 1. Find or Create NetworkManager
        var netMan = FindObjectOfType<NetworkManager>();
        if (netMan == null)
        {
            GameObject go = new GameObject("NetworkManager");
            netMan = go.AddComponent<NetworkManager>();
            go.AddComponent<Mirror.SimpleWeb.SimpleWebTransport>(); // Or KcpTransport, checking available transports
            // Usually Mirror comes with KcpTransport as default, let's try Kcp
            if (go.GetComponent<Transport>() == null)
            {
                 // Try adding Kcp if class exists, else Telepathy, else generic Transport
                 // We will skip adding transport blindly to avoid compile errors if Kcp is missing.
                 // Mirror's NetworkManager usually adds a default transport if added via menu.
                 // We'll rely on user or standard components.
                 // Actually, let's explicitly look for KcpTransport
                 var kcpType = System.Type.GetType("kcp2k.KcpTransport, kcp2k");
                 if (kcpType != null) go.AddComponent(kcpType);
                 else go.AddComponent<Mirror.TelepathyTransport>();
            }
            
            go.AddComponent<NetworkManagerHUD>();
            Debug.Log("Created NetworkManager");
        }

        // 2. Assign Player Prefab
        string prefabPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (playerPrefab != null)
        {
            netMan.playerPrefab = playerPrefab;
            netMan.autoCreatePlayer = true;
            Debug.Log("Assigned Player Prefab to NetworkManager");
        }

        // 3. Disable existing PlayerArmature in scene
        var existingPlayer = GameObject.Find("PlayerArmature");
        if (existingPlayer != null)
        {
            existingPlayer.SetActive(false);
            Debug.Log("Disabled existing PlayerArmature in scene (to avoid duplicate)");
        }
        
        // 4. Ensure Interaction Manager exists and is persistent?
        // Actually, we should make sure specific Singletons are handled.
    }
}
