using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AIPipeline.UI
{
    /// <summary>
    /// 节点预制件生成器
    /// 在 Editor 中调用来创建节点预制件
    /// </summary>
    public class NodePrefabCreator : MonoBehaviour
    {
        [Header("Style Settings")]
        public Color backgroundColor = new Color(0.2f, 0.2f, 0.25f, 0.95f);
        public Color headerColor = new Color(1f, 0.6f, 0.7f, 1f);
        public float nodeWidth = 200f;
        public float nodeHeight = 120f;
        public float headerHeight = 30f;
        public float portSize = 16f;
        
        /// <summary>
        /// 创建节点 UI 结构
        /// </summary>
        public static GameObject CreateNodePrefab()
        {
            // 主节点容器
            GameObject nodeObj = new GameObject("NodePrefab");
            RectTransform nodeRect = nodeObj.AddComponent<RectTransform>();
            nodeRect.sizeDelta = new Vector2(200, 120);
            
            // 背景
            Image bg = nodeObj.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
            bg.raycastTarget = true;
            
            // 圆角效果需要自定义 Shader，这里用 Outline 代替
            var outline = nodeObj.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.6f, 0.7f, 0.3f);
            outline.effectDistance = new Vector2(1, 1);
            
            // 添加 CanvasGroup 用于拖拽
            nodeObj.AddComponent<CanvasGroup>();
            
            // 添加 VisualNode 组件
            nodeObj.AddComponent<VisualNode>();
            
            // === Header ===
            GameObject header = new GameObject("Header");
            header.transform.SetParent(nodeObj.transform, false);
            RectTransform headerRect = header.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 30);
            headerRect.anchoredPosition = Vector2.zero;
            
            Image headerBg = header.AddComponent<Image>();
            headerBg.color = new Color(1f, 0.6f, 0.7f, 0.8f);
            headerBg.raycastTarget = false;
            
            // === Title Text ===
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(header.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(10, 0);
            titleRect.offsetMax = new Vector2(-10, 0);
            
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Node";
            titleText.fontSize = 14;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            titleText.raycastTarget = false;
            
            // === Content Area ===
            GameObject content = new GameObject("Content");
            content.transform.SetParent(nodeObj.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(10, 10);
            contentRect.offsetMax = new Vector2(-10, -35);
            
            // === Input Port ===
            GameObject inputPort = CreatePort("InputPort", new Color(0.6f, 0.8f, 1f, 1f));
            inputPort.transform.SetParent(nodeObj.transform, false);
            RectTransform inputRect = inputPort.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0, 0.5f);
            inputRect.anchorMax = new Vector2(0, 0.5f);
            inputRect.anchoredPosition = new Vector2(-8, 0);
            
            // === Output Port ===
            GameObject outputPort = CreatePort("OutputPort", new Color(1f, 0.6f, 0.7f, 1f));
            outputPort.transform.SetParent(nodeObj.transform, false);
            RectTransform outputRect = outputPort.GetComponent<RectTransform>();
            outputRect.anchorMin = new Vector2(1, 0.5f);
            outputRect.anchorMax = new Vector2(1, 0.5f);
            outputRect.anchoredPosition = new Vector2(8, 0);
            
            return nodeObj;
        }
        
        private static GameObject CreatePort(string name, Color color)
        {
            GameObject port = new GameObject(name);
            RectTransform rect = port.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(16, 16);
            
            Image img = port.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            
            // 圆形效果
            var outline = port.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1, 1);
            
            return port;
        }
        
        /// <summary>
        /// 创建带输入框的 TextInput 节点内容
        /// </summary>
        public static GameObject CreateTextInputContent()
        {
            GameObject content = new GameObject("TextInputContent");
            RectTransform rect = content.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(170, 60);
            
            // 输入框背景
            Image bg = content.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.12f, 1f);
            
            // 输入框
            GameObject inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(content.transform, false);
            RectTransform inputRect = inputObj.AddComponent<RectTransform>();
            inputRect.anchorMin = Vector2.zero;
            inputRect.anchorMax = Vector2.one;
            inputRect.offsetMin = new Vector2(5, 5);
            inputRect.offsetMax = new Vector2(-5, -5);
            
            TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
            
            // 文本区域
            GameObject textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputObj.transform, false);
            RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = Vector2.zero;
            textAreaRect.offsetMax = Vector2.zero;
            textArea.AddComponent<RectMask2D>();
            
            // 文本显示
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(textArea.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(5, 0);
            textRect.offsetMax = new Vector2(-5, 0);
            
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 12;
            text.color = Color.white;
            
            inputField.textComponent = text;
            inputField.textViewport = textAreaRect;
            
            // Placeholder
            GameObject placeholder = new GameObject("Placeholder");
            placeholder.transform.SetParent(textArea.transform, false);
            RectTransform phRect = placeholder.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(5, 0);
            phRect.offsetMax = new Vector2(-5, 0);
            
            TextMeshProUGUI phText = placeholder.AddComponent<TextMeshProUGUI>();
            phText.text = "Enter prompt...";
            phText.fontSize = 12;
            phText.fontStyle = FontStyles.Italic;
            phText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            
            inputField.placeholder = phText;
            
            return content;
        }
    }
}
