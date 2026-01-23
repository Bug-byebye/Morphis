using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using GLTFast;

namespace AIPipeline.Nodes
{
    /// <summary>
    /// 3D 预览节点 - 在预览窗口中显示模型，可选择放置到场景
    /// </summary>
    public class Preview3DNode : PipelineNode
    {
        [Header("Preview Settings")]
        public Vector2 previewWindowSize = new Vector2(400, 400);
        public float rotationSpeed = 50f;
        public float zoomSpeed = 0.5f;
        public float minZoom = 1f;
        public float maxZoom = 10f;
        
        [Header("Scene Placement")]
        public Vector3 scenePosition = Vector3.zero;
        public Vector3 sceneScale = Vector3.one;
        
        [Header("Runtime")]
        public GameObject previewModel;
        public GameObject sceneModel;
        public bool isPreviewOpen = false;
        
        // Preview camera setup
        private Camera previewCamera;
        private RenderTexture previewTexture;
        private GameObject previewRoot;
        private GameObject previewUI;
        private float currentZoom = 3f;
        private float rotationX = 0f;
        private float rotationY = 0f;
        private byte[] cachedGlbData;
        
        public override PortType? InputType => PortType.Model3D;
        public override PortType? OutputType => null; // 终端节点
        
        private void Awake()
        {
            nodeName = "3D Preview";
            nodeColor = new Color(0.6f, 1f, 0.6f); // 浅绿色
        }
        
        public override void Execute(Action<object> onComplete, Action<string> onError)
        {
            byte[] glbData = GetInputData<byte[]>();
            if (glbData == null || glbData.Length == 0)
            {
                onError?.Invoke("No GLB data received");
                return;
            }
            
            cachedGlbData = glbData;
            StartCoroutine(LoadAndPreview(glbData, onComplete, onError));
        }
        
        private IEnumerator LoadAndPreview(byte[] glbData, Action<object> onComplete, Action<string> onError)
        {
            var gltf = new GltfImport();
            
            var loadTask = gltf.LoadGltfBinary(glbData);
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }
            
            if (!loadTask.Result)
            {
                onError?.Invoke("Failed to parse GLB data");
                yield break;
            }
            
            // 创建预览环境
            SetupPreviewEnvironment();
            
            // 清除之前的预览模型
            if (previewModel != null)
            {
                Destroy(previewModel);
            }
            
            // 在预览环境中创建模型
            previewModel = new GameObject("PreviewModel");
            previewModel.transform.SetParent(previewRoot.transform);
            previewModel.transform.localPosition = Vector3.zero;
            previewModel.layer = LayerMask.NameToLayer("Preview");
            
            var instantiateTask = gltf.InstantiateMainSceneAsync(previewModel.transform);
            while (!instantiateTask.IsCompleted)
            {
                yield return null;
            }
            
            if (!instantiateTask.Result)
            {
                Destroy(previewModel);
                onError?.Invoke("Failed to instantiate model");
                yield break;
            }
            
            // 设置所有子物体到 Preview 层
            SetLayerRecursively(previewModel, LayerMask.NameToLayer("Preview"));
            
            // 自动调整模型大小以适应预览
            FitModelToPreview();
            
            // 显示预览窗口
            ShowPreviewWindow();
            
            Debug.Log($"[Preview3D] Model loaded in preview window");
            onComplete?.Invoke(previewModel);
        }
        
        private void SetupPreviewEnvironment()
        {
            if (previewRoot != null) return;
            
            // 创建预览根对象（在场景外部）
            previewRoot = new GameObject("Preview3D_Environment");
            previewRoot.transform.position = new Vector3(1000, 1000, 1000); // 远离主场景
            
            // 创建预览相机
            var camObj = new GameObject("PreviewCamera");
            camObj.transform.SetParent(previewRoot.transform);
            camObj.transform.localPosition = new Vector3(0, 0, -currentZoom);
            
            previewCamera = camObj.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            previewCamera.cullingMask = 1 << LayerMask.NameToLayer("Preview");
            previewCamera.fieldOfView = 45f;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 100f;
            
            // 创建 RenderTexture
            previewTexture = new RenderTexture((int)previewWindowSize.x, (int)previewWindowSize.y, 24);
            previewCamera.targetTexture = previewTexture;
            
            // 添加预览灯光
            var lightObj = new GameObject("PreviewLight");
            lightObj.transform.SetParent(previewRoot.transform);
            lightObj.transform.localPosition = new Vector3(2, 3, -2);
            lightObj.transform.LookAt(previewRoot.transform.position);
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.cullingMask = 1 << LayerMask.NameToLayer("Preview");
        }
        
        private void ShowPreviewWindow()
        {
            if (previewUI != null)
            {
                previewUI.SetActive(true);
                isPreviewOpen = true;
                return;
            }
            
            // 创建预览 UI
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("PreviewCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }
            
            previewUI = new GameObject("Preview3D_Window");
            previewUI.transform.SetParent(canvas.transform, false);
            
            var rect = previewUI.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(previewWindowSize.x + 20, previewWindowSize.y + 80);
            rect.anchoredPosition = Vector2.zero;
            
            // 背景面板
            var bgImage = previewUI.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            
            // 标题栏
            var titleBar = new GameObject("TitleBar");
            titleBar.transform.SetParent(previewUI.transform, false);
            var titleRect = titleBar.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.sizeDelta = new Vector2(0, 30);
            titleRect.anchoredPosition = Vector2.zero;
            
            var titleBg = titleBar.AddComponent<Image>();
            titleBg.color = new Color(0.3f, 0.3f, 0.3f);
            
            // 标题文字
            var titleTextObj = new GameObject("TitleText");
            titleTextObj.transform.SetParent(titleBar.transform, false);
            var titleText = titleTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            titleText.text = "3D Preview - Drag to rotate, Scroll to zoom";
            titleText.fontSize = 14;
            titleText.alignment = TMPro.TextAlignmentOptions.Center;
            titleText.color = Color.white;
            var titleTextRect = titleTextObj.GetComponent<RectTransform>();
            titleTextRect.anchorMin = Vector2.zero;
            titleTextRect.anchorMax = Vector2.one;
            titleTextRect.sizeDelta = Vector2.zero;
            
            // 预览图像
            var previewImageObj = new GameObject("PreviewImage");
            previewImageObj.transform.SetParent(previewUI.transform, false);
            var previewImageRect = previewImageObj.AddComponent<RectTransform>();
            previewImageRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewImageRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewImageRect.sizeDelta = previewWindowSize;
            previewImageRect.anchoredPosition = new Vector2(0, 10);
            
            var rawImage = previewImageObj.AddComponent<RawImage>();
            rawImage.texture = previewTexture;
            
            // 添加拖拽旋转
            var dragHandler = previewImageObj.AddComponent<PreviewDragHandler>();
            dragHandler.previewNode = this;
            
            // 按钮区域
            var buttonArea = new GameObject("ButtonArea");
            buttonArea.transform.SetParent(previewUI.transform, false);
            var buttonRect = buttonArea.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0, 0);
            buttonRect.anchorMax = new Vector2(1, 0);
            buttonRect.pivot = new Vector2(0.5f, 0);
            buttonRect.sizeDelta = new Vector2(0, 40);
            buttonRect.anchoredPosition = Vector2.zero;
            
            var hlg = buttonArea.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.padding = new RectOffset(10, 10, 5, 5);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childForceExpandWidth = true;
            
            // "放置到场景" 按钮
            CreateButton(buttonArea.transform, "Place in Scene", new Color(0.3f, 0.7f, 0.3f), OnPlaceInScene);

            // "加入背包 / 模型库" 按钮
            CreateButton(buttonArea.transform, "Add to Bag", new Color(0.35f, 0.55f, 0.9f), OnAddToBag);
            
            // "关闭" 按钮
            CreateButton(buttonArea.transform, "Close", new Color(0.5f, 0.5f, 0.5f), OnClosePreview);
            
            isPreviewOpen = true;
        }
        
        private void CreateButton(Transform parent, string text, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var btnObj = new GameObject(text + "Button");
            btnObj.transform.SetParent(parent, false);
            
            var btnImage = btnObj.AddComponent<Image>();
            btnImage.color = color;
            
            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImage;
            btn.onClick.AddListener(onClick);
            
            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 30;
            
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var btnText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            btnText.text = text;
            btnText.fontSize = 14;
            btnText.alignment = TMPro.TextAlignmentOptions.Center;
            btnText.color = Color.white;
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
        }
        
        public void RotatePreview(Vector2 delta)
        {
            if (previewModel == null) return;
            
            rotationY += delta.x * rotationSpeed * Time.deltaTime;
            rotationX -= delta.y * rotationSpeed * Time.deltaTime;
            rotationX = Mathf.Clamp(rotationX, -80f, 80f);
            
            previewModel.transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0);
        }
        
        public void ZoomPreview(float delta)
        {
            currentZoom -= delta * zoomSpeed;
            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
            
            if (previewCamera != null)
            {
                previewCamera.transform.localPosition = new Vector3(0, 0, -currentZoom);
            }
        }
        
        private void FitModelToPreview()
        {
            if (previewModel == null) return;
            
            var bounds = new Bounds(previewModel.transform.position, Vector3.zero);
            var renderers = previewModel.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }
            
            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxSize > 0)
            {
                float scale = 1.5f / maxSize;
                previewModel.transform.localScale = Vector3.one * scale;
                previewModel.transform.localPosition = -bounds.center * scale;
            }
            
            currentZoom = 3f;
            if (previewCamera != null)
            {
                previewCamera.transform.localPosition = new Vector3(0, 0, -currentZoom);
            }
        }
        
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            if (layer < 0) layer = 0; // Default layer if Preview doesn't exist
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
        
        private void OnPlaceInScene()
        {
            if (cachedGlbData == null) return;
            StartCoroutine(InstantiateInScene());
        }

        /// <summary>
        /// 将当前 GLB 保存到 Resources/Placeables 目录，供 ModelLibrary 使用（“背包”功能）。
        /// 仅在 Editor 中生效：写入 Assets 并刷新 AssetDatabase。
        /// </summary>
        private void OnAddToBag()
        {
            if (cachedGlbData == null || cachedGlbData.Length == 0)
            {
                Debug.LogWarning("[Preview3D] No GLB data to add to bag.");
                return;
            }

#if UNITY_EDITOR
            try
            {
                const string relDir = "Assets/Resources/Placeables";
                if (!Directory.Exists(relDir))
                {
                    Directory.CreateDirectory(relDir);
                }

                // 生成一个相对友好的文件名：Preview_yyyyMMdd_HHmmss.glb
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"Preview_{timestamp}.glb";
                var fullPath = Path.Combine(relDir, fileName);

                File.WriteAllBytes(fullPath, cachedGlbData);

                Debug.Log($"[Preview3D] Saved GLB to bag: {fullPath}");

                // 让 Unity 导入新资源（包括 glTFast ScriptedImporter 生成的 prefab），
                // 这样 ModelLibrary 下次打开时就能在 Resources 中看到它。
                UnityEditor.AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Preview3D] Failed to save GLB to bag: {ex.Message}");
            }
#else
            Debug.LogWarning("[Preview3D] Add to Bag currently only supported in the Unity Editor (writes to Assets/Resources/Placeables).");
#endif
        }
        
        private IEnumerator InstantiateInScene()
        {
            var gltf = new GltfImport();
            var loadTask = gltf.LoadGltfBinary(cachedGlbData);
            while (!loadTask.IsCompleted) yield return null;
            
            if (!loadTask.Result) yield break;
            
            // 清除之前的场景模型
            if (sceneModel != null)
            {
                Destroy(sceneModel);
            }
            
            // 在相机前方生成
            if (Camera.main != null)
            {
                scenePosition = Camera.main.transform.position + Camera.main.transform.forward * 3f;
            }
            
            sceneModel = new GameObject("SceneModel_" + DateTime.Now.Ticks);
            sceneModel.transform.position = scenePosition;
            sceneModel.transform.localScale = sceneScale;
            
            var instantiateTask = gltf.InstantiateMainSceneAsync(sceneModel.transform);
            while (!instantiateTask.IsCompleted) yield return null;
            
            Debug.Log($"[Preview3D] Model placed in scene at {scenePosition}");
            
            OnClosePreview();
        }
        
        private void OnClosePreview()
        {
            if (previewUI != null)
            {
                previewUI.SetActive(false);
            }
            isPreviewOpen = false;
        }
        
        private void Update()
        {
            // 处理鼠标滚轮缩放
            if (isPreviewOpen)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll != 0)
                {
                    ZoomPreview(scroll * 10f);
                }
            }
        }
        
        private void OnDestroy()
        {
            if (previewRoot != null) Destroy(previewRoot);
            if (previewUI != null) Destroy(previewUI);
            if (previewTexture != null) previewTexture.Release();
        }
    }
    
    /// <summary>
    /// 预览拖拽处理器
    /// </summary>
    public class PreviewDragHandler : MonoBehaviour, UnityEngine.EventSystems.IDragHandler
    {
        public Preview3DNode previewNode;
        
        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (previewNode != null)
            {
                previewNode.RotatePreview(eventData.delta);
            }
        }
    }
}
