using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class FixPinkMaterials
{
    [MenuItem("Tools/Fix Pink Materials")]
    public static void Fix()
    {
        // 要修复的文件夹列表
        string[] folders = new string[] 
        { 
            "Assets/Lowpoly_Holiday_House",
            "Assets/contemporary house"
        };
        
        List<string> allGuids = new List<string>();
        foreach (string folder in folders)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
                allGuids.AddRange(guids);
            }
        }
        
        // Find URP shaders
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Universal Render Pipeline/Simple Lit"); 
        
        Shader urpParticles = Shader.Find("Universal Render Pipeline/Particles/Unlit"); 
        
        if (urpLit == null) 
        {
            Debug.LogError("URP Lit shader not found! Are you sure URP is installed?");
            return;
        }

        int count = 0;
        foreach (string guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            
            if (mat != null)
            {
                Undo.RecordObject(mat, "Fix Pink Material");
                
                if (path.Contains("Particle") || path.Contains("Fire") || path.Contains("Smoke"))
                {
                     if (urpParticles != null) mat.shader = urpParticles;
                }
                else if (path.Contains("Skybox")) 
                {
                    // Skip skybox materials
                }
                else
                {
                    // 1. Cache old values BEFORE switching shader
                    Texture mainTex = null;
                    if (mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");
                    else if (mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");
                    
                    Color mainColor = Color.white;
                    if (mat.HasProperty("_Color")) mainColor = mat.GetColor("_Color");
                    else if (mat.HasProperty("_BaseColor")) mainColor = mat.GetColor("_BaseColor");

                    // 2. Switch Shader
                    mat.shader = urpLit;
                    
                    // 3. Restore values to URP properties
                    if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                    mat.SetColor("_BaseColor", mainColor);
                }
                
                EditorUtility.SetDirty(mat);
                count++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Fixed {count} materials in asset packs");
    }
}