using UnityEngine;
using UnityEngine.UI;

namespace AIPipeline.UI
{
    /// <summary>
    /// 节点间的连接线（贝塞尔曲线）
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class NodeConnectionLine : Graphic
    {
        [Header("Connection")]
        public VisualNode fromNode;
        public VisualNode toNode;
        
        [Header("Style")]
        public Color lineColor = new Color(1f, 0.5f, 0.7f, 0.8f); // 粉色
        public float lineWidth = 3f;
        public int segments = 20;
        
        private Vector2[] points;
        
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            
            if (fromNode == null || toNode == null)
                return;
            
            // 获取端口位置
            Vector2 startPos = GetLocalPosition(fromNode.OutputPortTransform);
            Vector2 endPos = GetLocalPosition(toNode.InputPortTransform);
            
            // 计算贝塞尔曲线控制点
            float distance = Vector2.Distance(startPos, endPos);
            float tangent = Mathf.Min(distance * 0.5f, 100f);
            
            Vector2 control1 = startPos + Vector2.right * tangent;
            Vector2 control2 = endPos + Vector2.left * tangent;
            
            // 生成曲线点
            points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                points[i] = CalculateBezierPoint(t, startPos, control1, control2, endPos);
            }
            
            // 绘制线段
            for (int i = 0; i < segments; i++)
            {
                DrawLineSegment(vh, points[i], points[i + 1], lineWidth, lineColor);
            }
        }
        
        private Vector2 GetLocalPosition(RectTransform target)
        {
            if (target == null)
                return Vector2.zero;
            
            // 转换到本地坐标
            Vector3 worldPos = target.position;
            return transform.InverseTransformPoint(worldPos);
        }
        
        private Vector2 CalculateBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            
            Vector2 point = uuu * p0;
            point += 3 * uu * t * p1;
            point += 3 * u * tt * p2;
            point += ttt * p3;
            
            return point;
        }
        
        private void DrawLineSegment(VertexHelper vh, Vector2 start, Vector2 end, float width, Color color)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * width * 0.5f;
            
            int startIndex = vh.currentVertCount;
            
            vh.AddVert(start + perpendicular, color, Vector2.zero);
            vh.AddVert(start - perpendicular, color, Vector2.zero);
            vh.AddVert(end - perpendicular, color, Vector2.zero);
            vh.AddVert(end + perpendicular, color, Vector2.zero);
            
            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }
        
        public void Initialize(VisualNode from, VisualNode to)
        {
            fromNode = from;
            toNode = to;
            UpdateLine();
        }
        
        public void UpdateLine()
        {
            SetVerticesDirty();
        }
    }
}
