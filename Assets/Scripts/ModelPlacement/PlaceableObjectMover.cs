using UnityEngine;
using UnityEngine.EventSystems;

namespace Morphis.ModelPlacement
{
    /// <summary>
    /// 被放置到场景中的物体：允许鼠标拖拽移动（基于射线与水平地面平面）。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlaceableObjectMover : MonoBehaviour
    {
        [Header("Move")]
        [SerializeField] private float groundY = 0f;

        private Camera _cam;
        private bool _dragging;
        private Vector3 _offset;

        private void Awake()
        {
            _cam = Camera.main;
        }

        private void OnMouseDown()
        {
            // 不要抢 UI 操作
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            if (TryGetGroundHit(out var hitPoint))
            {
                _offset = transform.position - hitPoint;
                _dragging = true;
            }
        }

        private void OnMouseUp()
        {
            _dragging = false;
        }

        private void Update()
        {
            if (!_dragging) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            if (TryGetGroundHit(out var hitPoint))
            {
                transform.position = hitPoint + _offset;
            }
        }

        private bool TryGetGroundHit(out Vector3 point)
        {
            point = default;
            if (_cam == null) return false;

            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0, groundY, 0));
            if (!plane.Raycast(ray, out var enter)) return false;
            point = ray.GetPoint(enter);
            return true;
        }
    }
}

