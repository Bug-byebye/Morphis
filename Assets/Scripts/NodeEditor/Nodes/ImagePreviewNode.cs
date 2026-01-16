using System;
using UnityEngine;
using UnityEngine.UI;

namespace AIPipeline.Nodes
{
    /// <summary>
    /// 图片预览节点 - 显示生成的图片
    /// </summary>
    public class ImagePreviewNode : PipelineNode
    {
        [Header("Preview Settings")]
        public RawImage targetRawImage;  // 可选：指定 UI RawImage 显示
        public SpriteRenderer targetSpriteRenderer;  // 可选：指定 SpriteRenderer 显示
        
        [Header("Runtime")]
        public Texture2D currentTexture;
        public Sprite currentSprite;
        
        [Header("World Display")]
        public bool createWorldQuad = true;
        public Vector3 displayPosition = Vector3.zero;
        public float displayScale = 2f;
        public GameObject displayQuad;
        
        public override PortType? InputType => PortType.Image;
        public override PortType? OutputType => PortType.Image; // 可以继续传递给其他节点
        
        private void Awake()
        {
            nodeName = "Image Preview";
            nodeColor = new Color(0.4f, 0.9f, 0.7f); // 青绿色
        }
        
        public override void Execute(Action<object> onComplete, Action<string> onError)
        {
            Texture2D texture = GetInputData<Texture2D>();
            if (texture == null)
            {
                onError?.Invoke("No image data received");
                return;
            }
            
            currentTexture = texture;
            
            // 显示到 UI RawImage
            if (targetRawImage != null)
            {
                targetRawImage.texture = texture;
                Debug.Log("[ImagePreview] Displayed on RawImage");
            }
            
            // 显示到 SpriteRenderer
            if (targetSpriteRenderer != null)
            {
                currentSprite = Sprite.Create(
                    texture, 
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );
                targetSpriteRenderer.sprite = currentSprite;
                Debug.Log("[ImagePreview] Displayed on SpriteRenderer");
            }
            
            // 在世界空间创建 Quad 显示
            if (createWorldQuad)
            {
                CreateWorldDisplay(texture);
            }
            
            outputData = texture;
            onComplete?.Invoke(texture);
        }
        
        private void CreateWorldDisplay(Texture2D texture)
        {
            // 清除旧的
            if (displayQuad != null)
            {
                Destroy(displayQuad);
            }
            
            // 计算位置（相机前方）
            if (Camera.main != null && displayPosition == Vector3.zero)
            {
                displayPosition = Camera.main.transform.position + 
                                 Camera.main.transform.forward * 3f;
            }
            
            // 创建 Quad
            displayQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            displayQuad.name = "ImagePreview_" + DateTime.Now.Ticks;
            displayQuad.transform.position = displayPosition;
            
            // 保持图片比例
            float aspectRatio = (float)texture.width / texture.height;
            displayQuad.transform.localScale = new Vector3(displayScale * aspectRatio, displayScale, 1f);
            
            // 让 Quad 面向相机
            if (Camera.main != null)
            {
                displayQuad.transform.LookAt(Camera.main.transform);
                displayQuad.transform.Rotate(0, 180, 0); // 翻转面向
            }
            
            // 创建材质并应用纹理
            Material mat = new Material(Shader.Find("Unlit/Texture"));
            mat.mainTexture = texture;
            displayQuad.GetComponent<Renderer>().material = mat;
            
            // 移除碰撞体（可选）
            var collider = displayQuad.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
            
            Debug.Log($"[ImagePreview] Created world quad at {displayPosition}");
        }
        
        private void OnDestroy()
        {
            if (displayQuad != null)
            {
                Destroy(displayQuad);
            }
            if (currentSprite != null)
            {
                Destroy(currentSprite);
            }
        }
    }
}
