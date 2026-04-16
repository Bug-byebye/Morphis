using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using Morphis.AppFlow;
using Mirror;
using StarterAssets;
using Morphis.WorldSnapshot;

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

        // Used when running as a network client: we compute the intended target but do not directly
        // mutate transforms (server-authoritative). These values can be consumed by a network layer
        // (e.g., Command/RPC) if implemented.
        private Vector3 _pendingTargetPos;
        private float _pendingGroundHeight;
        
        private enum EditMode { None, Move, Rotate, Scale }
        private EditMode _currentMode = EditMode.None;
        
        private Transform _playerTransform;
        private bool _dragging = false; 
        
        private Vector2 _lastMousePos;
        private Vector3 _initialEulerAngles;
        private Vector3 _initialScale;
        private Vector3 _initialPosition;

        private void Awake()
        {
            _cam = Camera.main;
            EnsureColliderExists();
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

        private bool _waitForMouseRelease = false;

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // Block world interaction while the pointer is over any screen UI.
            if (IsPointerOverUi())
            {
                return;
            }

            // Waiting for the user to release the mouse after clicking a menu button
            if (_waitForMouseRelease)
            {
                if (Input.GetMouseButtonUp(0))
                {
                    _waitForMouseRelease = false;
                }
                return;
            }

            // Cancel with ESC
            if (_currentMode != EditMode.None && Input.GetKeyDown(KeyCode.Escape))
            {
                CancelEdit();
                return;
            }

            if (_currentMode == EditMode.Move)
            {
                UpdateDrag();
                if (Input.GetMouseButtonDown(0)) TryPlace();
            }
            else if (_currentMode == EditMode.Rotate)
            {
                UpdateRotate();
                if (Input.GetMouseButtonDown(0)) TryPlace();
            }
            else if (_currentMode == EditMode.Scale)
            {
                UpdateScale();
                if (Input.GetMouseButtonDown(0)) TryPlace();
            }
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
                    if (ObjectContextMenu.Instance == null)
                    {
                        var menuObj = new GameObject("ObjectContextMenu");
                        menuObj.AddComponent<ObjectContextMenu>();
                    }
                    if (ObjectContextMenu.Instance == null) return;

                    ObjectContextMenu.Instance.ShowMenu(
                        gameObject,
                        onMoveSelected: EnterMoveMode,
                        onRotateSelected: EnterRotateMode,
                        onScaleSelected: EnterScaleMode,
                        onMessageSelected: ShowMessageDialog,
                        onDeleteSelected: DeleteObject
                    );
                }
            }
        }

        private bool IsPointerOverUi()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            Vector2 mousePos = Input.mousePosition;
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            }

            var pointerData = new PointerEventData(EventSystem.current) { position = mousePos };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                if (result.gameObject == null)
                {
                    continue;
                }

                if (result.gameObject.GetComponentInParent<Canvas>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        public void EnterMoveMode()
        {
            _currentMode = EditMode.Move;
            _initialPosition = transform.position;
            _dragging = true;
            _waitForMouseRelease = true;
            SetCollidersEnabled(false);
            
            FindPlayer(); // Ensure we have player ref
            Debug.Log($"[PlaceableObjectMover] Entered Move Mode for {name}");
        }

        public void EnterRotateMode()
        {
            _currentMode = EditMode.Rotate;
            _initialEulerAngles = transform.eulerAngles;
            _lastMousePos = Input.mousePosition;
            _waitForMouseRelease = true;
            SetCollidersEnabled(false);
            Debug.Log($"[PlaceableObjectMover] Entered Rotate Mode for {name}");
        }

        public void EnterScaleMode()
        {
            _currentMode = EditMode.Scale;
            _initialScale = transform.localScale;
            _lastMousePos = Input.mousePosition;
            _waitForMouseRelease = true;
            SetCollidersEnabled(false);
            Debug.Log($"[PlaceableObjectMover] Entered Scale Mode for {name}");
        }

        private void CancelEdit()
        {
            if (_currentMode == EditMode.Move) transform.position = _initialPosition;
            else if (_currentMode == EditMode.Rotate) transform.eulerAngles = _initialEulerAngles;
            else if (_currentMode == EditMode.Scale) transform.localScale = _initialScale;
            
            _currentMode = EditMode.None;
            _dragging = false;
            SetCollidersEnabled(true);
            Debug.Log($"[PlaceableObjectMover] Cancelled edit for {name}");
        }

        private void TryPlace()
        {
            float targetGroundHeight = ModelLibraryUI.ResolveGroundHeightAt(
                transform.position,
                transform.position.y,
                transform);
            ModelLibraryUI.SnapToGround(gameObject, targetGroundHeight);
            _pendingTargetPos = transform.position;
            _pendingGroundHeight = targetGroundHeight;

            _currentMode = EditMode.None;
            _dragging = false;
            SetCollidersEnabled(true);
            Debug.Log($"[PlaceableObjectMover] Placed/Confirmed {name}");

            // 联机模式：将最终 Transform 提交给服务器权威
            if (NetworkClient.active)
            {
                var worldObj = GetComponent<Morphis.WorldSnapshot.WorldObject>();
                if (worldObj != null && StarterAssets.NetworkPlayerSetup.Local != null)
                {
                    bool ok = StarterAssets.NetworkPlayerSetup.Local.RequestMove(
                        worldObj.ObjectId,
                        transform.position,
                        transform.rotation,
                        transform.localScale
                    );
                    if (!ok)
                    {
                        Debug.LogWarning($"[PlaceableObjectMover] RequestMove failed for object '{worldObj.ObjectId}'.");
                    }
                }
            }
        }

        private void UpdateRotate()
        {
            Vector2 currentMousePos = Input.mousePosition;
            float deltaX = currentMousePos.x - _lastMousePos.x;
            float scrollDelta = Input.mouseScrollDelta.y;
            
            float rotationAmount = (deltaX * -0.5f) + (scrollDelta * 10f);
            
            if (Mathf.Abs(rotationAmount) > 0.01f)
            {
                transform.Rotate(Vector3.up, rotationAmount, Space.World);
            }
            _lastMousePos = currentMousePos;
        }

        private void UpdateScale()
        {
            Vector2 currentMousePos = Input.mousePosition;
            float deltaY = currentMousePos.y - _lastMousePos.y;
            float scrollDelta = Input.mouseScrollDelta.y;
            
            float scaleMultiplier = 1f + (deltaY * 0.005f) + (scrollDelta * 0.1f);
            
            if (Mathf.Abs(scaleMultiplier - 1f) > 0.001f)
            {
                Vector3 newScale = transform.localScale * scaleMultiplier;
                
                // Clamp scale to reasonable values
                float minScale = 0.1f;
                float maxScale = 10f;
                
                newScale.x = Mathf.Clamp(newScale.x, minScale, maxScale);
                newScale.y = Mathf.Clamp(newScale.y, minScale, maxScale);
                newScale.z = Mathf.Clamp(newScale.z, minScale, maxScale);
                
                transform.localScale = newScale;
            }
            _lastMousePos = currentMousePos;
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

        private void DeleteObject()
        {
            if (ObjectInteractionManager.Instance != null)
                ObjectInteractionManager.ClearTargetsIfExists();

            Debug.Log($"[PlaceableObjectMover] Deleted {name}");

            var worldObj = GetComponent<Morphis.WorldSnapshot.WorldObject>();
            if (NetworkClient.active && StarterAssets.NetworkPlayerSetup.Local != null && worldObj != null)
            {
                bool ok = StarterAssets.NetworkPlayerSetup.Local.RequestDelete(worldObj.ObjectId);
                if (!ok)
                {
                    Debug.LogWarning($"[PlaceableObjectMover] RequestDelete failed for object '{worldObj.ObjectId}'. Falling back to local destroy.");
                    Destroy(gameObject);
                    RequestServerAutosaveNextFrame();
                }
            }
            else
            {
                Destroy(gameObject);
                RequestServerAutosaveNextFrame();
            }
        }

        private static void RequestServerAutosaveNextFrame()
        {
            if (!AppSession.IsLoggedIn) return;
            if (WorldSnapshotManager.Instance == null) return;
            WorldSnapshotManager.Instance.StartCoroutine(SaveAfterDeleteCoroutine(WorldSnapshotManager.Instance));
        }

        private static IEnumerator SaveAfterDeleteCoroutine(WorldSnapshotManager manager)
        {
            // Destroy() happens at end of frame; wait one frame to ensure snapshot no longer includes this object.
            yield return null;
            if (manager == null) yield break;
            manager.SaveWorldServer(
                onError: err => Debug.LogWarning($"[PlaceableObjectMover] Autosave failed after delete: {err}")
            );
        }

        private void UpdateDrag()
        {
            if (GetCursorGroundPoint(out var groundPoint, out var groundHeight))
            {
                // Align center to cursor (no offset)
                var targetPos = groundPoint;
                bool clampedToRadius = false;

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
                        clampedToRadius = true;
                    }
                }

                if (clampedToRadius)
                {
                    groundHeight = ModelLibraryUI.ResolveGroundHeightAt(targetPos, groundHeight, transform);
                }

                targetPos.y = groundHeight;
                transform.position = targetPos;
                ModelLibraryUI.SnapToGround(gameObject, groundHeight);
                _pendingTargetPos = transform.position;
                _pendingGroundHeight = groundHeight;
            }
        }

        private bool GetCursorGroundPoint(out Vector3 point, out float height)
        {
            point = Vector3.zero;
            height = groundY;
            
            var ray = _cam.ScreenPointToRay(Input.mousePosition);

            // 1. Prefer environment hits while skipping self / player / other movable objects.
            if (ModelLibraryUI.TryGetPlacementHit(ray, out var hit, transform))
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

        private void EnsureColliderExists()
        {
            if (GetComponentInChildren<Collider>() != null) return;

            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                gameObject.AddComponent<BoxCollider>();
                return;
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var box = gameObject.AddComponent<BoxCollider>();
            var centerLocal = transform.InverseTransformPoint(bounds.center);
            box.center = centerLocal;
            var ls = transform.lossyScale;
            box.size = new Vector3(
                ls.x != 0 ? bounds.size.x / ls.x : bounds.size.x,
                ls.y != 0 ? bounds.size.y / ls.y : bounds.size.y,
                ls.z != 0 ? bounds.size.z / ls.z : bounds.size.z
            );
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
