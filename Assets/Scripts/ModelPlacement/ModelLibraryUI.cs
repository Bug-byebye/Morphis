using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GLTFast;
using Morphis.WorldSnapshot;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Morphis.ModelPlacement
{
    /// <summary>
    /// 左侧模型库 UI：点击按钮展开弹窗，显示可放置模型列表；支持拖拽到场景中放置。
    /// 资源约定：把可放置的 prefab 放在 Resources/Placeables 下（Assets/Resources/Placeables/*.prefab）
    /// </summary>
    public class ModelLibraryUI : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private string resourcesPath = "Placeables";

        [Header("Spawn")]
        [SerializeField] private float groundY = 0f;

        private Canvas _canvas;
        private RectTransform _panel;
        private Button _toggleBtn;
        private Button _closeBtn;
        private Transform _listRoot;

        private Camera _cam;
        private readonly List<PlaceableDefinition> _items = new();

        private void Awake()
        {
            _cam = Camera.main;
            BuildUI();
            LoadPlaceables();
            RebuildList();
        }

        private void BuildUI()
        {
            // Canvas
            var canvasGO = new GameObject("ModelLibraryCanvas");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Left toggle button
            _toggleBtn = CreateButton(_canvas.transform, "模型", new Color(0.22f, 0.55f, 0.95f));
            var btnRt = _toggleBtn.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0, 0.5f);
            btnRt.anchorMax = new Vector2(0, 0.5f);
            btnRt.pivot = new Vector2(0, 0.5f);
            btnRt.sizeDelta = new Vector2(110, 44);
            btnRt.anchoredPosition = new Vector2(16, 0);
            _toggleBtn.onClick.AddListener(TogglePanel);

            // Panel
            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(_canvas.transform, false);
            _panel = panelGO.AddComponent<RectTransform>();
            // 让面板随窗口高度自适应（用户 Game 视图可随意拉伸时，固定高度容易把滚动区域挤成 0 导致“空白”）
            _panel.anchorMin = new Vector2(0, 0.06f);
            _panel.anchorMax = new Vector2(0, 0.94f);
            _panel.pivot = new Vector2(0, 0.5f);
            _panel.sizeDelta = new Vector2(320, 0);
            _panel.anchoredPosition = new Vector2(16, 0);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.08f, 0.11f, 0.92f);
            panelGO.AddComponent<Outline>().effectColor = new Color(0.2f, 0.6f, 1f, 0.35f);

            // Title
            var title = CreateText(panelGO.transform, "模型库", 18, FontStyles.Bold);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1);
            titleRt.sizeDelta = new Vector2(0, 44);
            titleRt.anchoredPosition = new Vector2(0, -8);
            title.alignment = TextAlignmentOptions.Center;

            // Close button (X) on the top-right of panel
            _closeBtn = CreateButton(panelGO.transform, "X", new Color(0.4f, 0.15f, 0.2f));
            var closeRt = _closeBtn.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1, 1);
            closeRt.anchorMax = new Vector2(1, 1);
            closeRt.pivot = new Vector2(1, 1);
            closeRt.sizeDelta = new Vector2(28, 28);
            closeRt.anchoredPosition = new Vector2(-8, -8);
            _closeBtn.onClick.AddListener(() => SetPanelVisible(false));

            // Scroll view root
            var scrollGO = new GameObject("Scroll");
            scrollGO.transform.SetParent(panelGO.transform, false);
            var scrollRt = scrollGO.AddComponent<RectTransform>();
            // 顶部预留标题区域，底部留一点 padding；避免在小窗口下出现负高度
            scrollRt.anchorMin = new Vector2(0, 0);
            scrollRt.anchorMax = new Vector2(1, 1);
            scrollRt.offsetMin = new Vector2(12, 12);
            scrollRt.offsetMax = new Vector2(-12, -60);
            scrollGO.AddComponent<Image>().color = new Color(0, 0, 0, 0);

            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGO.transform, false);
            var vpRt = viewport.AddComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = Vector2.zero;
            // 用 RectMask2D 做裁剪，比 Mask(Stencil) 更不容易在透明 Graphic 下出问题
            viewport.AddComponent<RectMask2D>();
            // 仍然挂一个透明 Image 方便调试/接收射线（可见性不受影响）
            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            scroll.viewport = vpRt;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0, 0);

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 10;
            layout.childControlHeight = false; // We set height manually on items
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRt;
            _listRoot = content.transform;

            // Start collapsed
            _panel.gameObject.SetActive(false);
        }

        private void TogglePanel()
        {
            bool willBeVisible = !_panel.gameObject.activeSelf;
            SetPanelVisible(willBeVisible);
            
            // 每次打开面板时刷新资源列表
            if (willBeVisible)
            {
                RefreshPlaceables();
            }
        }

        private void SetPanelVisible(bool visible)
        {
            _panel.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 刷新可放置资源列表（重新从 Resources 加载）
        /// </summary>
        private void RefreshPlaceables()
        {
#if UNITY_EDITOR
            // 在 Editor 模式下刷新 AssetDatabase，确保新保存的文件被识别
            AssetDatabase.Refresh();
#endif
            LoadPlaceables();
            RebuildList();
        }

        private void Update()
        {
            // 按下 Esc 关闭面板（如果当前已打开）
            if (_panel != null && _panel.gameObject.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    SetPanelVisible(false);
                }
            }
        }

        private void LoadPlaceables()
        {
            _items.Clear();

            // Load prefabs from Resources
            var prefabs = Resources.LoadAll<GameObject>(resourcesPath);
            foreach (var prefab in prefabs)
            {
                if (prefab == null) continue;
                _items.Add(new PlaceableDefinition(prefab.name, prefab));
            }

            // Load .glb (as TextAsset) from Resources
            // 约定：Assets/Resources/Placeables/*.glb
            var glbs = Resources.LoadAll<TextAsset>(resourcesPath);
            foreach (var glb in glbs)
            {
                if (glb == null) continue;
                // TextAsset.name 不含扩展名；这里默认它就是 glb 资源
                _items.Add(new PlaceableDefinition(glb.name, glb));
            }

            // Fallbacks so it works out of the box
            if (_items.Count == 0)
            {
                _items.Add(new PlaceableDefinition("Cube", null, PrimitiveType.Cube));
                _items.Add(new PlaceableDefinition("Sphere", null, PrimitiveType.Sphere));
                _items.Add(new PlaceableDefinition("Capsule", null, PrimitiveType.Capsule));
            }

            Debug.Log($"[ModelLibrary] Loaded items: {_items.Count} (Resources/{resourcesPath})");
        }

        private void RebuildList()
        {
            for (int i = _listRoot.childCount - 1; i >= 0; i--)
                Destroy(_listRoot.GetChild(i).gameObject);

            foreach (var item in _items)
            {
                // Container (Row Button)
                var rowGO = new GameObject(item.DisplayName);
                rowGO.transform.SetParent(_listRoot, false);
                
                // Layout Element for fixed height
                var le = rowGO.AddComponent<LayoutElement>();
                le.minHeight = 100;
                le.preferredHeight = 100;
                
                var img = rowGO.AddComponent<Image>();
                img.color = new Color(0.18f, 0.18f, 0.24f, 0.9f);

                var btn = rowGO.AddComponent<Button>();
                btn.targetGraphic = img;

                // 3D Preview (Left side)
                var previewGO = new GameObject("Preview");
                previewGO.transform.SetParent(rowGO.transform, false);
                var rtPreview = previewGO.AddComponent<RectTransform>();
                // Stick to left, square
                rtPreview.anchorMin = new Vector2(0, 0); 
                rtPreview.anchorMax = new Vector2(0, 1);
                rtPreview.pivot = new Vector2(0, 0.5f);
                rtPreview.sizeDelta = new Vector2(100, 0); // width 100
                rtPreview.anchoredPosition = new Vector2(0, 0);
                
                // Inner Padding for image
                var previewInner = new GameObject("Image");
                previewInner.transform.SetParent(previewGO.transform, false);
                var rtInner = previewInner.AddComponent<RectTransform>();
                rtInner.anchorMin = Vector2.zero;
                rtInner.anchorMax = Vector2.one;
                rtInner.offsetMin = new Vector2(5, 5);
                rtInner.offsetMax = new Vector2(-5, -5);
                
                var raw = previewInner.AddComponent<RawImage>();
                raw.color = Color.white;
                
                // Generate Preview
                if (item.Prefab != null)
                {
                    raw.texture = Morphis.Utils.RuntimePreviewGenerator.GenerateModelPreview(item.Prefab, 256, 256);
                }
                else
                {
                    raw.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                }

                // Name Label (Right side)
                var lbl = CreateText(rowGO.transform, item.DisplayName, 24, FontStyles.Bold);
                var lblRt = lbl.GetComponent<RectTransform>();
                lblRt.anchorMin = new Vector2(0, 0);
                lblRt.anchorMax = new Vector2(1, 1);
                lblRt.pivot = new Vector2(0, 0.5f);
                // Start after the preview image (100px) + some padding
                lblRt.offsetMin = new Vector2(110, 0); 
                lblRt.offsetMax = new Vector2(-10, 0);
                
                lbl.alignment = TextAlignmentOptions.MidlineLeft;
                lbl.enableAutoSizing = false; // Use fixed large size

                // Drag functionality
                var drag = rowGO.AddComponent<PlaceableDragSource>();
                drag.Init(this, item);
                
                // Click to spawn at center (fallback behavior)
                btn.onClick.AddListener(() => {
                    TryPlace(item, new Vector2(Screen.width/2f, Screen.height/2f));
                });
            }
        }

        internal bool TryPlace(PlaceableDefinition def, Vector2 screenPos)
        {
            if (!GetPlacementInfo(screenPos, out var worldPos, out var targetBaseY))
                return false;

            // 1) Prefab
            if (def.Prefab != null)
            {
                var go = Instantiate(def.Prefab, worldPos, Quaternion.identity);
                EnsureColliderFromRenderers(go);
                NormalizeScale(go, targetSize: 1.0f);
                SnapToGround(go, targetBaseY);
                EnsurePlaceableComponents(go);
                EnsureWorldObjectForSnapshot(go, $"{resourcesPath}/{def.DisplayName}");
                Debug.Log($"[ModelLibrary] Placed prefab: {def.DisplayName} at {worldPos}");
                return true;
            }

            // 2) GLB (TextAsset bytes) - runtime load via glTFast
            if (def.GlbAsset != null)
            {
                Debug.Log($"[ModelLibrary] Loading GLB: {def.DisplayName} ({def.GlbAsset.bytes?.Length ?? 0} bytes) at {worldPos}");
                StartCoroutine(LoadGlbAndPlace(def.GlbAsset, def.DisplayName, worldPos, targetBaseY));
                return true;
            }

            // 3) Primitive fallback
            {
                var go = GameObject.CreatePrimitive(def.FallbackPrimitive);
                go.name = def.DisplayName;
                go.transform.position = worldPos;
                NormalizeScale(go, targetSize: 1.0f);
                SnapToGround(go, targetBaseY);
                EnsurePlaceableComponents(go);
                EnsureWorldObjectForSnapshot(go, $"primitive:{def.FallbackPrimitive}");
                Debug.Log($"[ModelLibrary] Placed primitive: {def.DisplayName} at {worldPos}");
                return true;
            }
        }

        private IEnumerator LoadGlbAndPlace(TextAsset glb, string displayName, Vector3 worldPos, float targetBaseY)
        {
            if (glb == null) yield break;

            var root = new GameObject(displayName);
            root.transform.position = worldPos;

            var gltf = new GltfImport();
            var loadTask = gltf.LoadGltfBinary(glb.bytes);
            while (!loadTask.IsCompleted) yield return null;
            if (!loadTask.Result)
            {
                Debug.LogError($"[ModelLibrary] Failed to load GLB: {displayName}");
                Destroy(root);
                yield break;
            }

            var instTask = gltf.InstantiateMainSceneAsync(root.transform);
            while (!instTask.IsCompleted) yield return null;
            if (!instTask.Result)
            {
                Debug.LogError($"[ModelLibrary] Failed to instantiate GLB: {displayName}");
                Destroy(root);
                yield break;
            }

            EnsureColliderFromRenderers(root);
            NormalizeScale(root, targetSize: 1.0f);
            SnapToGround(root, targetBaseY);
            EnsurePlaceableComponents(root);
            EnsureWorldObjectForSnapshot(root, $"glb:{displayName}");

            Debug.Log($"[ModelLibrary] Placed GLB: {displayName} at {worldPos}");
        }

        public bool GetPlacementInfo(Vector2 screenPos, out Vector3 worldPos, out float targetBaseY)
        {
            worldPos = Vector3.zero;
            targetBaseY = groundY;

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return false;

            var ray = _cam.ScreenPointToRay(screenPos);

            // Prefer collider hit
            if (Physics.Raycast(ray, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                worldPos = hit.point;
                targetBaseY = hit.point.y; 
                return true;
            }
            
            // Fallback to plane
            var plane = new Plane(Vector3.up, new Vector3(0, groundY, 0));
            if (plane.Raycast(ray, out var enter))
            {
                worldPos = ray.GetPoint(enter);
                return true;
            }

            return false;
        }

        private void EnsurePlaceableComponents(GameObject go)
        {
            if (go == null) return;

            // 可拖拽移动
            if (go.GetComponent<PlaceableObjectMover>() == null)
                go.AddComponent<PlaceableObjectMover>();

            // 可交互留言/高亮
            if (go.GetComponent<InteractableObject>() == null)
                go.AddComponent<InteractableObject>();
        }

        /// <summary> 为场景保存/与后端同步：给可放置物体添加 WorldObject 并设置 prefab_id </summary>
        private static void EnsureWorldObjectForSnapshot(GameObject go, string prefabId)
        {
            WorldSnapshotBuilder.EnsureWorldObjectForSnapshot(go, prefabId);
        }

        private static void EnsureColliderFromRenderers(GameObject root)
        {
            if (root == null) return;
            if (root.GetComponent<Collider>() != null) return;

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                root.AddComponent<BoxCollider>();
                return;
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var box = root.AddComponent<BoxCollider>();
            // 将 world bounds 转为 local
            var centerLocal = root.transform.InverseTransformPoint(bounds.center);
            box.center = centerLocal;
            // 近似缩放：用 lossyScale 折算到 local size
            var ls = root.transform.lossyScale;
            box.size = new Vector3(
                ls.x != 0 ? bounds.size.x / ls.x : bounds.size.x,
                ls.y != 0 ? bounds.size.y / ls.y : bounds.size.y,
                ls.z != 0 ? bounds.size.z / ls.z : bounds.size.z
            );
        }

        public static void NormalizeScale(GameObject root, float targetSize)
        {
            if (root == null) return;

            var bounds = CalculateRendererBounds(root, out var hasBounds);
            if (!hasBounds) return;

            var size = bounds.size;
            var maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (maxDim > 0.0001f)
            {
                var scaleFactor = targetSize / maxDim;
                scaleFactor = Mathf.Clamp(scaleFactor, 0.01f, 1000f);
                root.transform.localScale *= scaleFactor;
            }
        }

        public static void SnapToGround(GameObject root, float groundY)
        {
             if (root == null) return;
             var bounds = CalculateRendererBounds(root, out var hasBounds);
             if (!hasBounds) return;

             var deltaY = groundY - bounds.min.y;
             root.transform.position += new Vector3(0, deltaY, 0);
        }

        private static Bounds CalculateRendererBounds(GameObject root, out bool hasBounds)
        {
            hasBounds = false;
            if (root == null) return default;
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return default;

            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            hasBounds = true;
            return b;
        }

        private static TMP_Text CreateText(Transform parent, string text, float size, FontStyles style)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            return tmp;
        }

        private static Button CreateButton(Transform parent, string text, Color color)
        {
            var go = new GameObject($"Button_{text}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 44);

            var img = go.AddComponent<Image>();
            img.color = color;

            var btn = go.AddComponent<Button>();

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var labelRT = label.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            var tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 16;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return btn;
        }

        [Serializable]
        internal readonly struct PlaceableDefinition
        {
            public readonly string DisplayName;
            public readonly GameObject Prefab;
            public readonly TextAsset GlbAsset;
            public readonly PrimitiveType FallbackPrimitive;

            public PlaceableDefinition(string displayName, GameObject prefab, PrimitiveType fallback = PrimitiveType.Cube)
            {
                DisplayName = displayName;
                Prefab = prefab;
                GlbAsset = null;
                FallbackPrimitive = fallback;
            }

            public PlaceableDefinition(string displayName, TextAsset glbAsset)
            {
                DisplayName = displayName;
                Prefab = null;
                GlbAsset = glbAsset;
                FallbackPrimitive = PrimitiveType.Cube;
            }
        }

        private sealed class PlaceableDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            private ModelLibraryUI _owner;
            private PlaceableDefinition _def;

            private RectTransform _dragIconRt;
            private Canvas _dragCanvas;
            
            // 3D Preview
            private GameObject _previewObject;

            public void Init(ModelLibraryUI owner, PlaceableDefinition def)
            {
                _owner = owner;
                _def = def;
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (_owner == null) return;

                _dragCanvas = _owner._canvas;
                if (_dragCanvas == null) return;

                // 2D Icon
                var icon = new GameObject("DragIcon");
                icon.transform.SetParent(_dragCanvas.transform, false);
                _dragIconRt = icon.AddComponent<RectTransform>();
                _dragIconRt.sizeDelta = new Vector2(160, 36);

                var img = icon.AddComponent<Image>();
                img.color = new Color(0.15f, 0.15f, 0.20f, 0.9f);

                var label = new GameObject("Text");
                label.transform.SetParent(icon.transform, false);
                var labelRt = label.AddComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;

                var tmp = label.AddComponent<TextMeshProUGUI>();
                tmp.text = _def.DisplayName;
                tmp.fontSize = 14;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;

                UpdateDragIcon(eventData);
                CreatePreview();
            }

            public void OnDrag(PointerEventData eventData)
            {
                UpdateDragIcon(eventData);
                UpdatePreview(eventData);
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (_dragIconRt != null)
                {
                    Destroy(_dragIconRt.gameObject);
                    _dragIconRt = null;
                }
                
                DestroyPreview();

                // 仅当松手仍在“模型库面板区域”内时才视为取消放置。
                // 不能用 IsPointerOverGameObject()：在新 Input System/复杂 UI 下容易误判，导致永远不放置。
                if (_owner != null && _owner._panel != null && _owner._canvas != null)
                {
                    if (RectTransformUtility.RectangleContainsScreenPoint(_owner._panel, eventData.position, _owner._canvas.worldCamera))
                        return;
                }

                _owner?.TryPlace(_def, eventData.position);
            }

            private void UpdateDragIcon(PointerEventData eventData)
            {
                if (_dragIconRt == null) return;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragCanvas.transform as RectTransform,
                    eventData.position,
                    _dragCanvas.worldCamera,
                    out var localPos
                );
                _dragIconRt.anchoredPosition = localPos;
            }

            private void CreatePreview()
            {
                if (_previewObject != null) return;

                // Create ghost based on type
                if (_def.Prefab != null)
                {
                    _previewObject = Instantiate(_def.Prefab);
                }
                else if (_def.GlbAsset != null)
                {
                    // For GLB, we can't easily sync-load a preview if it's large. 
                    // Fallback to a placeholder cube or try async load (complex for drag).
                    // Let's use a subtle placeholder cube or sphere for now, 
                    // OR if Glb logic allows fast load (it doesn't without coroutine).
                    // So we use a Placeholder Primitive.
                    _previewObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _previewObject.name = "Preview_Placeholder";
                }
                else
                {
                    _previewObject = GameObject.CreatePrimitive(_def.FallbackPrimitive);
                }

                if (_previewObject == null) return;

                // Scale it
                ModelLibraryUI.NormalizeScale(_previewObject, 1.0f);

                // Disable colliders so raycast ignores it
                var colliders = _previewObject.GetComponentsInChildren<Collider>();
                foreach (var c in colliders) c.enabled = false;

                // Make it semi-transparent (Ghost)
                var renderers = _previewObject.GetComponentsInChildren<Renderer>();
                var ghostMat = new Material(Shader.Find("Standard")); // Or URP/Lit
                ghostMat.SetFloat("_Mode", 3); // Transparent
                ghostMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                ghostMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                ghostMat.SetInt("_ZWrite", 0);
                ghostMat.DisableKeyword("_ALPHATEST_ON");
                ghostMat.EnableKeyword("_ALPHABLEND_ON");
                ghostMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                ghostMat.renderQueue = 3000;
                ghostMat.color = new Color(0.5f, 0.8f, 1f, 0.5f);

                foreach (var r in renderers)
                {
                    r.sharedMaterial = ghostMat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                
                // Note: Standard shader might not work in URP/HDRP perfectly transparently 
                // without proper setup, but it's a good "best effort" for generic proj.
            }

            private void UpdatePreview(PointerEventData eventData)
            {
                if (_previewObject == null) return;
                if (_owner == null) return;

                if (_owner.GetPlacementInfo(eventData.position, out var worldPos, out var groundY))
                {
                    _previewObject.transform.position = worldPos;
                    // Snap visually
                    ModelLibraryUI.SnapToGround(_previewObject, groundY);
                    _previewObject.SetActive(true);
                }
                else
                {
                    // Hide if invalid
                    _previewObject.SetActive(false);
                }
            }

            private void DestroyPreview()
            {
                if (_previewObject != null)
                {
                    Destroy(_previewObject);
                    _previewObject = null;
                }
            }
        }
    }
}

