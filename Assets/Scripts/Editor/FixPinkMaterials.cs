using UnityEngine;
using UnityEditor;

public class FixPinkMaterials
{
    [MenuItem("Tools/Fix Pink Materials")]
    public static void Fix()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/StylizedNatureBundle" });
        
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
                    mat.shader = urpLit;
                    
                    if (mat.HasProperty("_MainTex") && mat.HasProperty("_BaseMap"))
                    {
                        mat.SetTexture("_BaseMap", mat.GetTexture("_MainTex"));
                    }
                     if (mat.HasProperty("_Color") && mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", mat.GetColor("_Color"));
                    }
                }
                
                EditorUtility.SetDirty(mat);
                count++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Fixed {count} materials in StylizedNatureBundle");
    }
}