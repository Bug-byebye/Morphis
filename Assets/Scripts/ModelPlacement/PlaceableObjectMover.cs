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
        [SerializeField] private float maxMoveRadius = 5.0f; // Limit range around player

        private Camera _cam;
        private Collider[] _colliders;
        // private Vector3 _dragOffset; // Removed to ensure center alignment
        
        private Transform _playerTransform;
        private bool _moveMode = false;
        private bool _dragging = false; 

        private void Awake()
        {
            _cam = Camera.main;
            _colliders = GetComponentsInChildren<Collider>();
            FindPlayer();
        }

        private void FindPlayer()
        {
            if (_playerTransform != null) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerTransform = player.transform;
            else
            {
                // Fallback
                var controller = FindObjectOfType<StarterAssets.ThirdPersonController>();
                if (controller != null) _playerTransform = controller.transform;
            }
        }

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // Block interaction ONLY if strictly over UI elements (Layer: UI)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                var pointerData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                var results = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);
                
                bool blockedByUI = false;
                foreach (var result in results)
                {
                    if (result.gameObject.layer == 5) // UI layer
                    {
                        blockedByUI = true;
                        break;
                    }
                }
                
                if (blockedByUI) return;
            }

            // Mode 1: Moving (Dragging)
            if (_moveMode)
            {
                UpdateDrag();

                if (Input.GetMouseButtonDown(0))
                {
                    TryPlace();
                }
            }
            // Mode 2: Idle (Click to show menu)
            else
            {
                if (Input.GetMouseButtonDown(0))
                {
                    TryShowContextMenu();
                }
            }
        }

        private void TryShowContextMenu()
        {
            // Range check for opening menu too? 
            // Optional: User might want to click distant objects. 
            // For now, allow clicking, but restrict movement range once started.

            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit))
            {
                if (IsHitSelf(hit.collider))
                {
                    if (ObjectContextMenu.Instance != null)
                    {
                        ObjectContextMenu.Instance.ShowMenu(
                            gameObject,
                            onMoveSelected: EnterMoveMode,
                            onMessageSelected: ShowMessageDialog
                        );
                    }
                }
            }
        }

        public void EnterMoveMode()
        {
            _moveMode = true;
            _dragging = true;
            SetCollidersEnabled(false);
            
            // We want snappier controls, so snapping center to mouse immediately
            // _dragOffset = Vector3.zero; 
            
            FindPlayer(); // Ensure we have player ref
            Debug.Log($"[PlaceableObjectMover] Entered Move Mode for {name}");
        }

        private void TryPlace()
        {
            _moveMode = false;
            _dragging = false;
            SetCollidersEnabled(true);
            Debug.Log($"[PlaceableObjectMover] Placed {name}");
        }

        private void ShowMessageDialog()
        {
            // Auto-add InteractableObject if missing
            var interactable = GetComponent<InteractableObject>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<InteractableObject>();
            }

            // Auto-create ObjectInteractionManager if missing
            if (ObjectInteractionManager.Instance == null)
            {
                var managerObj = new GameObject("ObjectInteractionManager");
                managerObj.AddComponent<ObjectInteractionManager>();
            }

            // Now show
            if (interactable != null && ObjectInteractionManager.Instance != null)
            {
                ObjectInteractionManager.Instance.OnObjectClicked(interactable);
            }
            else
            {
                Debug.LogError("[PlaceableObjectMover] Failed to open message dialog.");
            }
        }

        private void UpdateDrag()
        {
            if (GetCursorGroundPoint(out var groundPoint, out var groundHeight))
            {
                // Align center to cursor (no offset)
                var targetPos = groundPoint;

                // Restrict to Player Radius
                if (_playerTransform != null)
                {
                    Vector3 playerPos = _playerTransform.position;
                    // Planar distance check
                    Vector3 objPosPlane = new Vector3(targetPos.x, 0, targetPos.z);
                    Vector3 plyPosPlane = new Vector3(playerPos.x, 0, playerPos.z);
                    
                    float dist = Vector3.Distance(objPosPlane, plyPosPlane);
                    if (dist > maxMoveRadius)
                    {
                        // Clamp to border
                        Vector3 dir = (objPosPlane - plyPosPlane).normalized;
                        Vector3 clampedPlane = plyPosPlane + dir * maxMoveRadius;
                        targetPos.x = clampedPlane.x;
                        targetPos.z = clampedPlane.z;
                        // Y remains from ground point
                    }
                }

                transform.position = targetPos;
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
