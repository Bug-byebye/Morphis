using UnityEngine;

namespace Morphis.UI.HotBar
{
    /// <summary>
    /// 运行时模型预览图生成器
    /// 使用临时 Camera 和 RenderTexture 渲染模型缩略图
    /// </summary>
    public static class ModelPreviewGenerator
    {
        private static Camera _previewCamera;
        private static RenderTexture _renderTexture;
        private static int _currentSize;

        /// <summary>
        /// 生成模型的预览 Sprite
        /// </summary>
        /// <param name="target">要渲染的 GameObject</param>
        /// <param name="size">预览图尺寸（像素）</param>
        /// <param name="backgroundColor">背景颜色</param>
        /// <returns>生成的 Sprite，失败返回 null</returns>
        public static Sprite GeneratePreview(GameObject target, int size = 128, Color? backgroundColor = null)
        {
            if (target == null) return null;

            EnsureCamera(size, backgroundColor ?? new Color(0.2f, 0.2f, 0.2f, 0f));

            // 计算物体边界
            var bounds = CalculateBounds(target);
            if (bounds.size == Vector3.zero)
            {
                // 没有 Renderer，使用默认边界
                bounds = new Bounds(target.transform.position, Vector3.one);
            }

            // 设置相机位置 - 从斜上方看向物体
            var center = bounds.center;
            var maxDim = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            var distance = maxDim * 2f;
            
            // 45度角从前上方查看
            var cameraDirection = new Vector3(0.5f, 0.7f, -1f).normalized;
            _previewCamera.transform.position = center + cameraDirection * distance;
            _previewCamera.transform.LookAt(center);
            
            // 设置相机参数
            _previewCamera.orthographic = true;
            _previewCamera.orthographicSize = maxDim * 0.7f;
            _previewCamera.nearClipPlane = 0.01f;
            _previewCamera.farClipPlane = distance * 3f;

            // 临时启用目标物体的所有层
            var originalLayers = new System.Collections.Generic.Dictionary<Transform, int>();
            SetLayerRecursively(target.transform, 31, originalLayers); // 使用第31层作为预览层
            _previewCamera.cullingMask = 1 << 31;

            // 渲染
            _previewCamera.targetTexture = _renderTexture;
            _previewCamera.Render();

            // 恢复原始层
            RestoreLayers(originalLayers);

            // 转换为 Texture2D
            var oldRT = RenderTexture.active;
            RenderTexture.active = _renderTexture;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            texture.Apply();

            RenderTexture.active = oldRT;

            // 创建 Sprite
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f
            );

            return sprite;
        }

        private static void EnsureCamera(int size, Color backgroundColor)
        {
            if (_previewCamera == null)
            {
                var cameraGO = new GameObject("_PreviewCamera");
                cameraGO.hideFlags = HideFlags.HideAndDontSave;
                _previewCamera = cameraGO.AddComponent<Camera>();
                _previewCamera.enabled = false;
                _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            }

            _previewCamera.backgroundColor = backgroundColor;

            if (_renderTexture == null || _currentSize != size)
            {
                if (_renderTexture != null)
                {
                    _renderTexture.Release();
                    Object.Destroy(_renderTexture);
                }

                _renderTexture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
                _renderTexture.antiAliasing = 4;
                _currentSize = size;
            }
        }

        private static Bounds CalculateBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return new Bounds(target.transform.position, Vector3.zero);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static void SetLayerRecursively(Transform t, int layer, System.Collections.Generic.Dictionary<Transform, int> original)
        {
            original[t] = t.gameObject.layer;
            t.gameObject.layer = layer;

            foreach (Transform child in t)
            {
                SetLayerRecursively(child, layer, original);
            }
        }

        private static void RestoreLayers(System.Collections.Generic.Dictionary<Transform, int> original)
        {
            foreach (var kvp in original)
            {
                if (kvp.Key != null)
                    kvp.Key.gameObject.layer = kvp.Value;
            }
        }

        /// <summary>
        /// 清理静态资源（可选，用于场景卸载时）
        /// </summary>
        public static void Cleanup()
        {
            if (_previewCamera != null)
            {
                Object.Destroy(_previewCamera.gameObject);
                _previewCamera = null;
            }

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Object.Destroy(_renderTexture);
                _renderTexture = null;
            }
        }
    }
}
