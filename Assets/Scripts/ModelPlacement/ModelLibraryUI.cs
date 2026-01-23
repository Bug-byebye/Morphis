using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GLTFast;

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
            _toggleBtn = CreateButton(_canvas.transform, "Models", new Color(0.22f, 0.55f, 0.95f));
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
            var title = CreateText(panelGO.transform, "Model Library", 18, FontStyles.Bold);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1);
            titleRt.sizeDelta = new Vector2(0, 44);
            titleRt.anchoredPosition = new Vector2(0, -8);
            title.alignment = TextAlignmentOptions.Center;

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
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 8;
            layout.childControlHeight = true;
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
            var show = !_panel.gameObject.activeSelf;
            _panel.gameObject.SetActive(show);
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
                var row = CreateButton(_listRoot, item.DisplayName, new Color(0.18f, 0.18f, 0.24f));
                // VerticalLayoutGroup 需要 LayoutElement 来提供高度，否则可能被压成 0 导致“看起来空白”
                var le = row.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 44;
                le.minHeight = 44;

                var drag = row.gameObject.AddComponent<PlaceableDragSource>();
                drag.Init(this, item);
            }
        }

        internal bool TryPlace(PlaceableDefinition def, Vector2 screenPos)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return false;

            Vector3 worldPos;
            var ray = _cam.ScreenPointToRay(screenPos);

            // Prefer collider hit
            if (Physics.Raycast(ray, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                worldPos = hit.point;
            }
            else
            {
                var plane = new Plane(Vector3.up, new Vector3(0, groundY, 0));
                if (!plane.Raycast(ray, out var enter)) return false;
                worldPos = ray.GetPoint(enter);
            }

            // 1) Prefab
            if (def.Prefab != null)
            {
                var go = Instantiate(def.Prefab, worldPos, Quaternion.identity);
                EnsureColliderFromRenderers(go);
                NormalizeScaleAndSnapToGround(go, groundY, targetSize: 1.0f);
                EnsurePlaceableComponents(go);
                Debug.Log($"[ModelLibrary] Placed prefab: {def.DisplayName} at {worldPos}");
                return true;
            }

            // 2) GLB (TextAsset bytes) - runtime load via glTFast
            if (def.GlbAsset != null)
            {
                Debug.Log($"[ModelLibrary] Loading GLB: {def.DisplayName} ({def.GlbAsset.bytes?.Length ?? 0} bytes) at {worldPos}");
                StartCoroutine(LoadGlbAndPlace(def.GlbAsset, def.DisplayName, worldPos));
                return true;
            }

            // 3) Primitive fallback
            {
                var go = GameObject.CreatePrimitive(def.FallbackPrimitive);
                go.name = def.DisplayName;
                go.transform.position = worldPos;
                NormalizeScaleAndSnapToGround(go, groundY, targetSize: 1.0f);
                EnsurePlaceableComponents(go);
                Debug.Log($"[ModelLibrary] Placed primitive: {def.DisplayName} at {worldPos}");
                return true;
            }
        }

        private IEnumerator LoadGlbAndPlace(TextAsset glb, string displayName, Vector3 worldPos)
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
            NormalizeScaleAndSnapToGround(root, groundY, targetSize: 1.0f);
            EnsurePlaceableComponents(root);

            Debug.Log($"[ModelLibrary] Placed GLB: {displayName} at {worldPos}");
        }

        private void EnsurePlaceableComponents(GameObject go)
        {
            if (go == null) return;

            if (go.GetComponent<PlaceableObjectMover>() == null)
                go.AddComponent<PlaceableObjectMover>();
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

        private static void NormalizeScaleAndSnapToGround(GameObject root, float groundY, float targetSize)
        {
            if (root == null) return;

            var bounds = CalculateRendererBounds(root, out var hasBounds);
            if (!hasBounds) return;

            // 缩放到一个“可见的”合理尺寸（戒指这类常常非常小）
            var size = bounds.size;
            var maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (maxDim > 0.0001f)
            {
                var scaleFactor = targetSize / maxDim;
                // 限制极端缩放
                scaleFactor = Mathf.Clamp(scaleFactor, 0.01f, 1000f);
                root.transform.localScale *= scaleFactor;
            }

            // 重新计算 bounds，用于贴地
            bounds = CalculateRendererBounds(root, out hasBounds);
            if (!hasBounds) return;

            // 贴到地面：让 bounds.min.y 落到 groundY
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
            }

            public void OnDrag(PointerEventData eventData)
            {
                UpdateDragIcon(eventData);
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (_dragIconRt != null)
                {
                    Destroy(_dragIconRt.gameObject);
                    _dragIconRt = null;
                }

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
        }
    }
}

