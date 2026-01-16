using UnityEngine;

/// <summary>
/// 可交互对象组件 - 附加到生成的3D模型上
/// 支持点击留言、光晕显示、悬浮提示
/// </summary>
public class InteractableObject : MonoBehaviour
{
    [Header("留言内容")]
    public string comment = "";
    
    [Header("光晕设置")]
    public Color glowColor = new Color(1f, 0.9f, 0.4f, 1f); // 柔和黄色
    public float glowIntensity = 0.5f;
    
    // 状态
    public bool HasComment => !string.IsNullOrEmpty(comment);
    private bool isHovered = false;
    
    // 原始材质缓存
    private Material[] originalMaterials;
    private Color[] originalEmissionColors;
    private bool[] wasEmissionEnabled;
    
    void Start()
    {
        CacheOriginalMaterials();
    }
    
    void CacheOriginalMaterials()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        int totalMaterials = 0;
        foreach (var r in renderers) totalMaterials += r.materials.Length;
        
        originalMaterials = new Material[totalMaterials];
        originalEmissionColors = new Color[totalMaterials];
        wasEmissionEnabled = new bool[totalMaterials];
        
        int index = 0;
        foreach (var r in renderers)
        {
            foreach (var mat in r.materials)
            {
                originalMaterials[index] = mat;
                if (mat.HasProperty("_EmissionColor"))
                {
                    originalEmissionColors[index] = mat.GetColor("_EmissionColor");
                    wasEmissionEnabled[index] = mat.IsKeywordEnabled("_EMISSION");
                }
                index++;
            }
        }
    }
    
    /// <summary>
    /// 设置留言
    /// </summary>
    public void SetComment(string newComment)
    {
        comment = newComment;
        UpdateGlow();
    }
    
    /// <summary>
    /// 清除留言
    /// </summary>
    public void ClearComment()
    {
        comment = "";
        UpdateGlow();
    }
    
    /// <summary>
    /// 更新光晕效果
    /// </summary>
    public void UpdateGlow()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        
        foreach (var r in renderers)
        {
            foreach (var mat in r.materials)
            {
                if (HasComment)
                {
                    // 检查是否支持自发光，如果不支持则替换为 URP Lit Shader
                    if (!mat.HasProperty("_EmissionColor"))
                    {
                        Debug.LogWarning($"[Glow] Material {mat.name} (Shader: {mat.shader.name}) missing _EmissionColor. Attempting swap to URP Lit.");
                        
                        // 优先尝试 URP Lit，如果失败尝试 Simple Lit
                        Shader targetShader = Shader.Find("Universal Render Pipeline/Lit");
                        if (targetShader == null) 
                        {
                            Debug.LogWarning("[Glow] URP Lit not found, trying Simple Lit...");
                            targetShader = Shader.Find("Universal Render Pipeline/Simple Lit");
                        }
                        
                        // 最后尝试 Standard
                        if (targetShader == null) 
                        {
                            Debug.LogWarning("[Glow] URP Simple Lit not found, getting desperate... trying Standard.");
                            targetShader = Shader.Find("Standard");
                        }
                        
                        if (targetShader == null)
                        {
                            Debug.LogError("[Glow] CRITICAL: Could not find ANY compatible shader (URP Lit, Simple Lit, or Standard)!");
                        }
                        else
                        {
                            Debug.Log($"[Glow] Success! Found shader: {targetShader.name}");
                        
                            // 保存旧纹理和颜色
                            Texture mainTex = null;
                            Color baseColor = Color.white;
                            
                            // 查找纹理
                            if (mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");
                            else if (mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");
                            else if (mat.HasProperty("baseColorTexture")) mainTex = mat.GetTexture("baseColorTexture");
                            
                            // 查找颜色
                            if (mat.HasProperty("_BaseColor")) baseColor = mat.GetColor("_BaseColor");
                            else if (mat.HasProperty("_Color")) baseColor = mat.GetColor("_Color");
                            else if (mat.HasProperty("baseColorFactor")) baseColor = mat.GetColor("baseColorFactor");

                            if (mainTex != null) Debug.Log($"[Glow] Found texture: {mainTex.name}");
                            
                            // 替换 Shader
                            mat.shader = targetShader;
                            Debug.Log($"[Glow] Swapped shader to {targetShader.name} on {r.gameObject.name}");
                            
                            // 恢复纹理和颜色 (URP Lit)
                            if (mainTex != null)
                            {
                                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
                                else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mainTex);
                            }
                            
                            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
                            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
                            
                            // 确保启用必要的关键字
                            mat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
                        }
                    }

                    // 启用光晕
                    mat.EnableKeyword("_EMISSION");
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        // 降低强度，避免过曝变成纯白
                        Color finalColor = glowColor * 0.5f; 
                        mat.SetColor("_EmissionColor", finalColor);
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; // 避免影响 GI，只发光
                    }
                }
                else
                {
                    // 关闭光晕
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", Color.black);
                        mat.DisableKeyword("_EMISSION"); 
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    }
                }
            }
        }
    }
    
}
