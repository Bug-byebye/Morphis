using UnityEngine;
using UnityEngine.Rendering;

namespace Morphis.Utils
{
    public static class RuntimePreviewGenerator
    {
        // Global preview camera and lights, reused to save performance
        private static Camera previewCamera;
        private static Light previewLight;
        private static Transform previewRoot;
        private const int PREVIEW_LAYER = 21; // Check if this layer is free, or use a far offset

        public static Texture2D GenerateModelPreview(GameObject prefab, int width = 256, int height = 256)
        {
            if (prefab == null) return null;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;

            SetupPreviewScene();

            // Instantiate model
            GameObject instance = Object.Instantiate(prefab, previewRoot);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0, 135, 0); // Isolate rotation
            
            // Set layer for all children
            SetLayerRecursively(instance, PREVIEW_LAYER);

            // Calculate bounds
            Bounds bounds = CalculateBounds(instance);
            
            // Position camera
            float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float dist = maxDim / (2.0f * Mathf.Tan(0.5f * previewCamera.fieldOfView * Mathf.Deg2Rad));
            
            // Move camera to look at bounds center
            Vector3 center = instance.transform.position + bounds.center; // local is 0, bounds are local-ish if no parent scaling
            // Actually bounds.center is world space if using Renderer.bounds.
            // But we parented to previewRoot which is far away.
            
            previewCamera.transform.position = bounds.center + new Vector3(0, maxDim * 0.5f, -dist * 2.0f);
            previewCamera.transform.LookAt(bounds.center);

            // Render
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24);
            previewCamera.targetTexture = rt;
            previewCamera.Render();

            // Read to Texture2D
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            // Cleanup
            previewCamera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            
            Object.DestroyImmediate(instance);

            return tex;
        }

        private static void SetupPreviewScene()
        {
            if (previewCamera != null) return;

            // Create root far away
            GameObject root = new GameObject("PreviewGeneratorRoot");
            root.transform.position = new Vector3(-5000, -5000, -5000);
            Object.DontDestroyOnLoad(root);
            previewRoot = root.transform;

            // Camera
            GameObject camObj = new GameObject("PreviewCamera");
            camObj.transform.SetParent(previewRoot, false);
            previewCamera = camObj.AddComponent<Camera>();
            previewCamera.cullingMask = 1 << PREVIEW_LAYER;
            previewCamera.clearFlags = CameraClearFlags.Color;
            previewCamera.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0); // Transparent-ish background? Or Grey.
            previewCamera.enabled = false; // We call Render() manually

            // Light
            GameObject lightObj = new GameObject("PreviewLight");
            lightObj.transform.SetParent(previewRoot, false);
            previewLight = lightObj.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.color = Color.white;
            previewLight.intensity = 1.0f;
            lightObj.transform.localRotation = Quaternion.Euler(50, -30, 0);
            lightObj.layer = PREVIEW_LAYER;
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static Bounds CalculateBounds(GameObject obj)
        {
            Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }
            return bounds;
        }
    }
}
