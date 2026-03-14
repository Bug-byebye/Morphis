using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 可交互对象组件 - 附加到生成的3D模型上
/// 支持留言、边缘光环（默认）与旧版整物体发光（可选）
/// </summary>
public class InteractableObject : MonoBehaviour
{
    private const float LegacyHaloWidth = 0.02f;
    private const float LegacyGlowIntensity = 1.0f;
    private const float DefaultHaloWidth = 0.035f;
    private const float DefaultGlowIntensity = 1.8f;

    [Header("留言内容")]
    public string comment = "";

    [Header("高亮设置")]
    [Tooltip("勾选后使用边缘光环（不改物体本体颜色）。取消则使用旧版整物体发光。")]
    public bool useEdgeHalo = true;
    public Color glowColor = new Color(1f, 0.9f, 0.4f, 0.9f);
    public float glowIntensity = DefaultGlowIntensity;
    [Range(0.001f, 0.08f)] public float haloWidth = DefaultHaloWidth;

    public bool HasComment => !string.IsNullOrWhiteSpace(comment);

    // 旧版发光缓存（fallback）
    private Color[] originalEmissionColors;
    private bool[] wasEmissionEnabled;

    // 边缘光环
    private const string OutlineShaderName = "Morphis/CommentOutlineURP";
    private bool outlineInitialized;
    private Material outlineMaterial;
    private readonly List<OutlinePair> outlinePairs = new List<OutlinePair>();

    private struct OutlinePair
    {
        public Renderer source;
        public Renderer outline;
    }

    private void Start()
    {
        UpgradeLegacyDefaults();
        if (!useEdgeHalo)
            CacheOriginalMaterials();
        UpdateGlow();
    }

    private void OnEnable()
    {
        UpgradeLegacyDefaults();
        if (!useEdgeHalo && (originalEmissionColors == null || wasEmissionEnabled == null))
            CacheOriginalMaterials();
        UpdateGlow();
    }

    private void LateUpdate()
    {
        if (!useEdgeHalo || !outlineInitialized) return;

        bool shouldShow = HasComment;
        for (int i = 0; i < outlinePairs.Count; i++)
        {
            var pair = outlinePairs[i];
            if (pair.source == null || pair.outline == null) continue;
            pair.outline.enabled = shouldShow && pair.source.enabled && pair.source.gameObject.activeInHierarchy;
        }
    }

    private void OnDestroy()
    {
        if (outlineMaterial != null)
            Destroy(outlineMaterial);
    }

    private void UpgradeLegacyDefaults()
    {
        // Auto-migrate existing scene objects that still carry old script defaults.
        if (Mathf.Abs(haloWidth - LegacyHaloWidth) < 0.0001f)
            haloWidth = DefaultHaloWidth;
        if (Mathf.Abs(glowIntensity - LegacyGlowIntensity) < 0.0001f)
            glowIntensity = DefaultGlowIntensity;
    }

    public void SetComment(string newComment)
    {
        comment = newComment;
        UpdateGlow();
    }

    public void ClearComment()
    {
        comment = "";
        UpdateGlow();
    }

    public void UpdateGlow()
    {
        if (useEdgeHalo)
        {
            UpdateEdgeHalo();
            return;
        }

        UpdateEmissionGlow();
    }

    private void UpdateEdgeHalo()
    {
        if (!EnsureOutlineInitialized()) return;
        UpdateOutlineMaterialProperties();

        bool shouldShow = HasComment;
        for (int i = 0; i < outlinePairs.Count; i++)
        {
            var pair = outlinePairs[i];
            if (pair.source == null || pair.outline == null) continue;
            pair.outline.enabled = shouldShow && pair.source.enabled && pair.source.gameObject.activeInHierarchy;
        }
    }

    private bool EnsureOutlineInitialized()
    {
        if (outlineInitialized) return true;

        Shader outlineShader = Shader.Find(OutlineShaderName);
        if (outlineShader == null)
        {
            Debug.LogError($"[InteractableObject] Missing shader '{OutlineShaderName}'.");
            return false;
        }

        outlineMaterial = new Material(outlineShader)
        {
            name = "CommentOutlineRuntimeMat"
        };
        UpdateOutlineMaterialProperties();

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var sourceRenderer in renderers)
        {
            if (sourceRenderer == null) continue;
            if (sourceRenderer.GetComponent<CommentOutlineMarker>() != null) continue;

            Renderer outlineRenderer = CreateOutlineRenderer(sourceRenderer);
            if (outlineRenderer == null) continue;

            outlineRenderer.enabled = false;
            outlinePairs.Add(new OutlinePair
            {
                source = sourceRenderer,
                outline = outlineRenderer
            });
        }

        outlineInitialized = outlinePairs.Count > 0;
        if (!outlineInitialized)
            Debug.LogWarning($"[InteractableObject] No renderer found for outline on {name}.");
        return outlineInitialized;
    }

    private Renderer CreateOutlineRenderer(Renderer source)
    {
        if (source is MeshRenderer meshRenderer)
        {
            var srcFilter = meshRenderer.GetComponent<MeshFilter>();
            if (srcFilter == null || srcFilter.sharedMesh == null) return null;

            var outlineObj = new GameObject("__CommentOutline");
            outlineObj.AddComponent<CommentOutlineMarker>();
            outlineObj.layer = source.gameObject.layer;
            outlineObj.transform.SetParent(source.transform, false);

            var outlineFilter = outlineObj.AddComponent<MeshFilter>();
            outlineFilter.sharedMesh = srcFilter.sharedMesh;

            var outlineRenderer = outlineObj.AddComponent<MeshRenderer>();
            ConfigureOutlineRenderer(outlineRenderer, source);
            return outlineRenderer;
        }

        if (source is SkinnedMeshRenderer skinned)
        {
            if (skinned.sharedMesh == null) return null;

            var outlineObj = new GameObject("__CommentOutline");
            outlineObj.AddComponent<CommentOutlineMarker>();
            outlineObj.layer = source.gameObject.layer;
            outlineObj.transform.SetParent(source.transform, false);

            var outlineRenderer = outlineObj.AddComponent<SkinnedMeshRenderer>();
            outlineRenderer.sharedMesh = skinned.sharedMesh;
            outlineRenderer.rootBone = skinned.rootBone;
            outlineRenderer.bones = skinned.bones;
            outlineRenderer.updateWhenOffscreen = skinned.updateWhenOffscreen;
            outlineRenderer.quality = skinned.quality;
            outlineRenderer.localBounds = skinned.localBounds;
            ConfigureOutlineRenderer(outlineRenderer, source);
            return outlineRenderer;
        }

        return null;
    }

    private void ConfigureOutlineRenderer(Renderer outlineRenderer, Renderer source)
    {
        outlineRenderer.sharedMaterial = outlineMaterial;
        outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
        outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        outlineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        outlineRenderer.allowOcclusionWhenDynamic = false;
        outlineRenderer.renderingLayerMask = source.renderingLayerMask;
    }

    private void UpdateOutlineMaterialProperties()
    {
        if (outlineMaterial == null) return;

        float intensity = Mathf.Max(0f, glowIntensity);
        Color finalColor = glowColor * intensity;
        finalColor.a = glowColor.a;
        outlineMaterial.SetColor("_OutlineColor", finalColor);
        outlineMaterial.SetFloat("_OutlineWidth", Mathf.Max(0.001f, haloWidth));
    }

    private void CacheOriginalMaterials()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        int totalMaterials = 0;
        foreach (var r in renderers) totalMaterials += r.materials.Length;

        originalEmissionColors = new Color[totalMaterials];
        wasEmissionEnabled = new bool[totalMaterials];

        int index = 0;
        foreach (var r in renderers)
        {
            foreach (var mat in r.materials)
            {
                if (mat.HasProperty("_EmissionColor"))
                {
                    originalEmissionColors[index] = mat.GetColor("_EmissionColor");
                    wasEmissionEnabled[index] = mat.IsKeywordEnabled("_EMISSION");
                }
                index++;
            }
        }
    }

    // 旧版逻辑保留做 fallback
    private void UpdateEmissionGlow()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        int materialIndex = 0;

        foreach (var r in renderers)
        {
            foreach (var mat in r.materials)
            {
                if (HasComment)
                {
                    mat.EnableKeyword("_EMISSION");
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        float finalIntensity = Mathf.Max(0.1f, glowIntensity * 2f);
                        mat.SetColor("_EmissionColor", glowColor * finalIntensity);
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    }
                }
                else
                {
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        bool canRestore = originalEmissionColors != null &&
                                          wasEmissionEnabled != null &&
                                          materialIndex >= 0 &&
                                          materialIndex < originalEmissionColors.Length &&
                                          materialIndex < wasEmissionEnabled.Length;

                        if (canRestore)
                        {
                            mat.SetColor("_EmissionColor", originalEmissionColors[materialIndex]);
                            if (wasEmissionEnabled[materialIndex]) mat.EnableKeyword("_EMISSION");
                            else mat.DisableKeyword("_EMISSION");
                        }
                        else
                        {
                            mat.SetColor("_EmissionColor", Color.black);
                            mat.DisableKeyword("_EMISSION");
                        }
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    }
                }

                materialIndex++;
            }
        }
    }
}

public sealed class CommentOutlineMarker : MonoBehaviour { }
