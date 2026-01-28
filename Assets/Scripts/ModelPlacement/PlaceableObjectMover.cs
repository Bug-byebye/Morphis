using UnityEngine;
using UnityEngine.EventSystems;

namespace Morphis.ModelPlacement
{
    /// <summary>
    /// Updated: Uses offset to prevent snapping center; uses smart fallback plane to avoid Y=0 jumps.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlaceableObjectMover : MonoBehaviour
    {
        [Header("Move")]
        [SerializeField] private float groundY = 0f;

        private Camera _cam;
        private bool _dragging;
        private Collider[] _colliders;
        
        // Offset from the "floor hit point" to the "object anchor"
        private Vector3 _dragOffset; 

        private void Awake()
        {
            _cam = Camera.main;
            _colliders = GetComponentsInChildren<Collider>();
        }

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                TryStartDrag();
            }
            else if (Input.GetMouseButtonUp(0))
            {
                StopDrag();
            }

            if (_dragging)
            {
                UpdateDrag();
            }
        }

        private void TryStartDrag()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit))
            {
                if (IsHitSelf(hit.collider))
                {
                    Debug.Log($"[PlaceableObjectMover] Started dragging {name}");
                    _dragging = true;
                    
                    // Temporarily disable colliders to raycast "through" ourselves to find the floor
                    SetCollidersEnabled(false);
                    
                    // Calculate initial offset: Where is the floor under the cursor?
                    if (GetCursorGroundPoint(out var groundPoint, out var _))
                    {
                        // The offset is the difference between current object pos and the ground point under cursor
                        _dragOffset = transform.position - groundPoint;
                    }
                    else
                    {
                        // Fallback: just use 0 offset if we are floating in void
                        _dragOffset = Vector3.zero;
                    }
                }
            }
        }

        private void UpdateDrag()
        {
            if (GetCursorGroundPoint(out var groundPoint, out var groundHeight))
            {
                // New position = floor point + offset
                var targetPos = groundPoint + _dragOffset;
                
                // Snap Y again to ensure we don't drift? 
                // Actually, if we use offset, we preserve the original "On Floor" relationship.
                // But ModelLibraryUI.SnapToGround enforces sticking to the floor.
                // Let's apply targetPos first.
                transform.position = targetPos;
                
                // Optional: Force re-snap to exact ground height if we assume object is resting
                // ModelLibraryUI.SnapToGround(gameObject, groundHeight); 
                // Re-snapping might fight with offset if offset included some Y lift. 
                // Let's assume SnapToGround is authoritative for "resting" objects.
                ModelLibraryUI.SnapToGround(gameObject, groundHeight);
            }
        }

        private bool GetCursorGroundPoint(out Vector3 point, out float height)
        {
            point = Vector3.zero;
            height = groundY;
            
            var ray = _cam.ScreenPointToRay(Input.mousePosition);

            // 1. Try hitting environment
            if (Physics.Raycast(ray, out var hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                height = hit.point.y;
                return true;
            }

            // 2. Fallback: Use plane at *current* object height (or slightly below) to avoid jumping to Y=0
            // If we are dragging, we use current Y. If not, use config groundY.
            float fallbackY = _dragging ? transform.position.y : groundY;
            
            // If we are in "demo" scene (known floating), maybe use a heuristic? 
            // Better: just use the camera's forward distance or a plane at current Y.
            
            var plane = new Plane(Vector3.up, new Vector3(0, fallbackY, 0));
            if (plane.Raycast(ray, out var enter))
            {
                point = ray.GetPoint(enter);
                height = fallbackY;
                return true;
            }

            return false;
        }

        private void StopDrag()
        {
            if (_dragging)
            {
                Debug.Log($"[PlaceableObjectMover] Stopped dragging {name}");
                _dragging = false;
                SetCollidersEnabled(true);
            }
        }

        private bool IsHitSelf(Collider c)
        {
            return c.transform == transform || c.transform.IsChildOf(transform);
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (_colliders == null) return;
            foreach (var c in _colliders)
            {
                if (c != null) c.enabled = enabled;
            }
        }
    }
}
