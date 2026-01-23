using UnityEngine;
using UnityEditor;

public class FixPinkMaterials
{
    [MenuItem("Tools/Fix Pink Materials")]
    public static void Fix()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Lowpoly_Holiday_House" });
        
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
        foreach (string guid in guids)
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
                    // Skip
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
        Debug.Log($"Fixed {count} materials in Lowpoly_Holiday_House");
    }
}