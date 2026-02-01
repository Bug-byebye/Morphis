using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GLTFast;
using Morphis.WorldSnapshot;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Morphis.UI.HotBar
{
    /// <summary>
    /// HotBar 管理器 - 管理底部物品栏的显示、分页和物品数据
    /// </summary>
    public class HotBarManager : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private string resourcesPath = "Placeables";

        [Header("Spawn Settings")]
        [SerializeField] private float groundY = 0f;

        [Header("UI References")]
        [Tooltip("Slot 容器 (GridLayoutGroup 父物体)")]
        [SerializeField] private Transform slotContainer;
        
        [Tooltip("左翻页按钮")]
        [SerializeField] private Button prevPageButton;
        
        [Tooltip("右翻页按钮")]
        [SerializeField] private Button nextPageButton;
        
        [Tooltip("页码显示文本 (可选)")]
        [SerializeField] private TMP_Text pageText;

        [Header("Preview Settings")]
        [SerializeField] private int previewSize = 128;
        [SerializeField] private Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0f);

        private readonly List<PlaceableItem> _items = new();
        private readonly List<HotBarSlot> _slots = new();
        private int _currentPage = 0;
        private int _slotsPerPage = 11;
        private Camera _cam;
        private Canvas _canvas;

        // 预览图缓存
        private readonly Dictionary<string, Sprite> _previewCache = new();

        public Canvas Canvas => _canvas;
        public Camera MainCamera => _cam;

        private void Awake()
        {
            _cam = Camera.main;
            _canvas = GetComponentInParent<Canvas>();
            
            // 收集所有 Slot
            CollectSlots();
            
            // 绑定翻页按钮
            if (prevPageButton != null)
                prevPageButton.onClick.AddListener(PrevPage);
            if (nextPageButton != null)
                nextPageButton.onClick.AddListener(NextPage);
            
            // 加载物品
            LoadPlaceables();
            RefreshDisplay();
        }

        private void CollectSlots()
        {
            _slots.Clear();
            if (slotContainer == null)
            {
                // 自动查找 - 假设 HotBar 就是 slotContainer
                slotContainer = transform;
            }

            foreach (Transform child in slotContainer)
            {
                var slot = child.GetComponent<HotBarSlot>();
                if (slot == null)
                    slot = child.gameObject.AddComponent<HotBarSlot>();
                
                slot.Init(this);
                _slots.Add(slot);
            }

            _slotsPerPage = _slots.Count;
            Debug.Log($"[HotBarManager] Found {_slotsPerPage} slots");
        }

        private void LoadPlaceables()
        {
            _items.Clear();

            // 加载 Prefabs
            var prefabs = Resources.LoadAll<GameObject>(resourcesPath);
            foreach (var prefab in prefabs)
            {
                if (prefab == null) continue;
                _items.Add(new PlaceableItem(prefab.name, prefab));
            }

            // 加载 GLB 文件
            var glbs = Resources.LoadAll<TextAsset>(resourcesPath);
            foreach (var glb in glbs)
            {
                if (glb == null) continue;
                _items.Add(new PlaceableItem(glb.name, glb));
            }

            // 无物品时添加默认基元
            if (_items.Count == 0)
            {
                _items.Add(new PlaceableItem("Cube", null, PrimitiveType.Cube));
                _items.Add(new PlaceableItem("Sphere", null, PrimitiveType.Sphere));
                _items.Add(new PlaceableItem("Capsule", null, PrimitiveType.Capsule));
            }

            Debug.Log($"[HotBarManager] Loaded {_items.Count} items from Resources/{resourcesPath}");
        }

        /// <summary>
        /// 刷新物品（编辑器中热更新或运行时动态添加后调用）
        /// </summary>
        public void RefreshItems()
        {
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            LoadPlaceables();
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            int totalPages = Mathf.CeilToInt((float)_items.Count / _slotsPerPage);
            _currentPage = Mathf.Clamp(_currentPage, 0, Mathf.Max(0, totalPages - 1));

            int startIndex = _currentPage * _slotsPerPage;

            for (int i = 0; i < _slots.Count; i++)
            {
                int itemIndex = startIndex + i;
                if (itemIndex < _items.Count)
                {
                    var item = _items[itemIndex];
                    _slots[i].SetItem(item);
                    
                    // 异步生成预览图
                    StartCoroutine(GeneratePreviewAsync(item, _slots[i]));
                }
                else
                {
                    _slots[i].SetEmpty();
                }
            }

            // 更新翻页按钮状态
            if (prevPageButton != null)
                prevPageButton.interactable = _currentPage > 0;
            if (nextPageButton != null)
                nextPageButton.interactable = _currentPage < totalPages - 1;
            
            // 更新页码文本
            if (pageText != null)
                pageText.text = $"{_currentPage + 1}/{Mathf.Max(1, totalPages)}";
        }

        private void PrevPage()
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                RefreshDisplay();
            }
        }

        private void NextPage()
        {
            int totalPages = Mathf.CeilToInt((float)_items.Count / _slotsPerPage);
            if (_currentPage < totalPages - 1)
            {
                _currentPage++;
                RefreshDisplay();
            }
        }

        private IEnumerator GeneratePreviewAsync(PlaceableItem item, HotBarSlot slot)
        {
            // 检查缓存
            if (_previewCache.TryGetValue(item.Name, out var cached))
            {
                slot.SetPreview(cached);
                yield break;
            }

            // 创建临时物体用于渲染
            GameObject previewObject = null;
            
            if (item.Prefab != null)
            {
                previewObject = Instantiate(item.Prefab);
            }
            else if (item.GlbAsset != null)
            {
                previewObject = new GameObject(item.Name);
                var gltf = new GltfImport();
                var loadTask = gltf.LoadGltfBinary(item.GlbAsset.bytes);
                while (!loadTask.IsCompleted) yield return null;
                
                if (loadTask.Result)
                {
                    var instTask = gltf.InstantiateMainSceneAsync(previewObject.transform);
                    while (!instTask.IsCompleted) yield return null;
                }
            }
            else
            {
                previewObject = GameObject.CreatePrimitive(item.FallbackPrimitive);
            }

            if (previewObject == null)
            {
                yield break;
            }

            // 移到屏幕外的位置
            previewObject.transform.position = new Vector3(10000, 10000, 10000);
            previewObject.SetActive(true);

            // 等待一帧让物体初始化
            yield return null;

            // 生成预览图
            var sprite = ModelPreviewGenerator.GeneratePreview(previewObject, previewSize, backgroundColor);
            
            // 销毁临时物体
            Destroy(previewObject);

            if (sprite != null)
            {
                _previewCache[item.Name] = sprite;
                slot.SetPreview(sprite);
            }
        }

        /// <summary>
        /// 放置物品到世界
        /// </summary>
        public bool TryPlace(PlaceableItem item, Vector2 screenPos)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return false;

            Vector3 worldPos;
            var ray = _cam.ScreenPointToRay(screenPos);

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

            if (item.Prefab != null)
            {
                var go = Instantiate(item.Prefab, worldPos, Quaternion.identity);
                EnsureColliderFromRenderers(go);
                NormalizeScaleAndSnapToGround(go, groundY, targetSize: 1.0f);
                EnsurePlaceableComponents(go);
                WorldSnapshotBuilder.EnsureWorldObjectForSnapshot(go, $"{resourcesPath}/{item.Name}");
                return true;
            }

            if (item.GlbAsset != null)
            {
                StartCoroutine(LoadGlbAndPlace(item.GlbAsset, item.Name, worldPos));
                return true;
            }

            {
                var go = GameObject.CreatePrimitive(item.FallbackPrimitive);
                go.name = item.Name;
                go.transform.position = worldPos;
                NormalizeScaleAndSnapToGround(go, groundY, targetSize: 1.0f);
                EnsurePlaceableComponents(go);
                WorldSnapshotBuilder.EnsureWorldObjectForSnapshot(go, $"primitive:{item.FallbackPrimitive}");
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
                Debug.LogError($"[HotBarManager] Failed to load GLB: {displayName}");
                Destroy(root);
                yield break;
            }

            var instTask = gltf.InstantiateMainSceneAsync(root.transform);
            while (!instTask.IsCompleted) yield return null;
            if (!instTask.Result)
            {
                Debug.LogError($"[HotBarManager] Failed to instantiate GLB: {displayName}");
                Destroy(root);
                yield break;
            }

            EnsureColliderFromRenderers(root);
            NormalizeScaleAndSnapToGround(root, groundY, targetSize: 1.0f);
            EnsurePlaceableComponents(root);
            WorldSnapshotBuilder.EnsureWorldObjectForSnapshot(root, $"glb:{displayName}");
        }

        private void EnsurePlaceableComponents(GameObject go)
        {
            if (go == null) return;
            
            // 添加 PlaceableObjectMover 如果存在
            var moverType = Type.GetType("Morphis.ModelPlacement.PlaceableObjectMover, Assembly-CSharp");
            if (moverType != null && go.GetComponent(moverType) == null)
                go.AddComponent(moverType);
            
            // 添加 InteractableObject 如果存在
            var interactType = Type.GetType("InteractableObject, Assembly-CSharp");
            if (interactType != null && go.GetComponent(interactType) == null)
                go.AddComponent(interactType);
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
            var centerLocal = root.transform.InverseTransformPoint(bounds.center);
            box.center = centerLocal;
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

            var size = bounds.size;
            var maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (maxDim > 0.0001f)
            {
                var scaleFactor = targetSize / maxDim;
                scaleFactor = Mathf.Clamp(scaleFactor, 0.01f, 1000f);
                root.transform.localScale *= scaleFactor;
            }

            bounds = CalculateRendererBounds(root, out hasBounds);
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
    }

    /// <summary>
    /// 可放置物品的数据结构
    /// </summary>
    [Serializable]
    public readonly struct PlaceableItem
    {
        public readonly string Name;
        public readonly GameObject Prefab;
        public readonly TextAsset GlbAsset;
        public readonly PrimitiveType FallbackPrimitive;

        public PlaceableItem(string name, GameObject prefab, PrimitiveType fallback = PrimitiveType.Cube)
        {
            Name = name;
            Prefab = prefab;
            GlbAsset = null;
            FallbackPrimitive = fallback;
        }

        public PlaceableItem(string name, TextAsset glbAsset)
        {
            Name = name;
            Prefab = null;
            GlbAsset = glbAsset;
            FallbackPrimitive = PrimitiveType.Cube;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Name);
    }
}
