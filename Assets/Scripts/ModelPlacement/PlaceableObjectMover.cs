using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using Morphis.AppFlow;
using Mirror;
using StarterAssets;
using Morphis.WorldSnapshot;
using Morphis.InputControl;

namespace Morphis.ModelPlacement
{
    internal enum AxisKind { X, Y, Z, PlaneXZ }
    internal enum GizmoHandleKind { MoveAxis, RotateAxis }

    [DisallowMultipleComponent]
    public class PlaceableObjectMover : MonoBehaviour
    {
        [Header("Move")]
        [SerializeField] private float groundY = 0f;
        [SerializeField] private float maxMoveRadius = 5.0f;

        [Header("Runtime Gizmo")]
        [SerializeField] private float gizmoDistanceScale = 0.12f;
        [SerializeField] private float gizmoMinSize = 0.7f;
        [SerializeField] private float gizmoMaxSize = 2.5f;

        private const float MoveAxisLength = 1.0f;
        private const float MoveAxisThickness = 0.055f;
        private const float MoveHandleSize = 0.16f;
        private const float PlaneHandleSize = 0.22f;
        private const float RotateRingRadius = 1.15f;
        private const float RotateRingThickness = 0.065f;
        private const int RotateRingSegments = 28;

        private Camera _cam;
        private Collider[] _colliders;
        private Vector3 _pendingTargetPos;
        private float _pendingGroundHeight;

        private enum EditMode { None, Move, Rotate, Scale }

        private EditMode _currentMode = EditMode.None;
        private Transform _playerTransform;

        private Vector2 _lastMousePos;
        private Quaternion _initialRotation;
        private Vector3 _initialScale;
        private Vector3 _initialPosition;

        private bool _waitForMouseRelease;
        private bool _isDraggingHandle;
        private float _groundOffsetFromSurface;

        private Plane _dragPlane;
        private Vector3 _dragStartPosition;
        private Quaternion _dragStartRotation;
        private float _dragStartAxisProjection;
        private Vector3 _dragStartPlanePoint;
        private Vector3 _dragStartRotationVector;
        private RuntimeGizmoHandle _activeHandle;

        private GameObject _gizmoRoot;
        private GameObject _moveGizmoRoot;
        private GameObject _rotateGizmoRoot;

        private void Awake()
        {
            _cam = Camera.main;
            EnsureColliderExists();
            _colliders = GetComponentsInChildren<Collider>();
            FindPlayer();
        }

        private void OnDisable()
        {
            HideEditGizmo();
            ClearHandleDrag();
            GameplayInputBlocker.SetBlocked(this, false);
            SetCollidersEnabled(true);
            _currentMode = EditMode.None;
        }

        private void OnDestroy()
        {
            GameplayInputBlocker.SetBlocked(this, false);
            DestroyEditGizmo();
        }

        private void FindPlayer()
        {
            if (_playerTransform != null) return;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
                return;
            }

            var controller = FindObjectOfType<ThirdPersonController>();
            if (controller != null)
            {
                _playerTransform = controller.transform;
            }
        }

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            if (_currentMode == EditMode.Move || _currentMode == EditMode.Rotate)
            {
                UpdateGizmoTransform();
            }

            if (IsPointerOverUi())
            {
                if (_isDraggingHandle && Input.GetMouseButtonUp(0))
                {
                    ClearHandleDrag();
                }
                return;
            }

            if (_waitForMouseRelease)
            {
                if (Input.GetMouseButtonUp(0))
                {
                    _waitForMouseRelease = false;
                }
                return;
            }

            if (_currentMode != EditMode.None && Input.GetKeyDown(KeyCode.Escape))
            {
                CancelEdit();
                return;
            }

            switch (_currentMode)
            {
                case EditMode.Move:
                    UpdateMoveMode();
                    break;
                case EditMode.Rotate:
                    UpdateRotateMode();
                    break;
                case EditMode.Scale:
                    UpdateScale();
                    if (IsConfirmKeyDown() || Input.GetMouseButtonDown(0))
                    {
                        TryPlace();
                    }
                    break;
                default:
                    if (Input.GetMouseButtonDown(0))
                    {
                        TryShowContextMenu();
                    }
                    break;
            }
        }

        private void UpdateMoveMode()
        {
            if (IsConfirmKeyDown())
            {
                TryPlace();
                return;
            }

            if (_isDraggingHandle)
            {
                if (Input.GetMouseButton(0))
                {
                    UpdateHandleDrag();
                }
                if (Input.GetMouseButtonUp(0))
                {
                    ClearHandleDrag();
                }
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (TryBeginHandleDrag())
                {
                    return;
                }

                TryPlace();
            }
        }

        private void UpdateRotateMode()
        {
            if (IsConfirmKeyDown())
            {
                TryPlace();
                return;
            }

            if (_isDraggingHandle)
            {
                if (Input.GetMouseButton(0))
                {
                    UpdateHandleDrag();
                }
                if (Input.GetMouseButtonUp(0))
                {
                    ClearHandleDrag();
                }
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (TryBeginHandleDrag())
                {
                    return;
                }

                TryPlace();
            }
        }

        private bool IsConfirmKeyDown()
        {
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        }

        private void TryShowContextMenu()
        {
            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit))
            {
                return;
            }

            if (!IsHitSelf(hit.collider))
            {
                return;
            }

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
            var results = new List<RaycastResult>();
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
            _initialRotation = transform.rotation;
            _waitForMouseRelease = true;
            _groundOffsetFromSurface = transform.position.y - ModelLibraryUI.ResolveGroundHeightAt(
                transform.position,
                transform.position.y,
                transform);

            FindPlayer();
            SetCollidersEnabled(false);
            GameplayInputBlocker.SetBlocked(this, true);
            ClearHandleDrag();
            EnsureEditGizmo();
            SetEditGizmoVisible(showMove: true, showRotate: false);

            Debug.Log($"[PlaceableObjectMover] Entered Move Mode with gizmo for {name}");
        }

        public void EnterRotateMode()
        {
            _currentMode = EditMode.Rotate;
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            _waitForMouseRelease = true;
            SetCollidersEnabled(false);
            GameplayInputBlocker.SetBlocked(this, true);
            ClearHandleDrag();
            EnsureEditGizmo();
            SetEditGizmoVisible(showMove: false, showRotate: true);

            Debug.Log($"[PlaceableObjectMover] Entered Rotate Mode with gizmo for {name}");
        }

        public void EnterScaleMode()
        {
            _currentMode = EditMode.Scale;
            _initialScale = transform.localScale;
            _lastMousePos = Input.mousePosition;
            _waitForMouseRelease = true;
            SetCollidersEnabled(false);
            GameplayInputBlocker.SetBlocked(this, true);
            ClearHandleDrag();
            HideEditGizmo();

            Debug.Log($"[PlaceableObjectMover] Entered Scale Mode for {name}");
        }

        private void CancelEdit()
        {
            if (_currentMode == EditMode.Move)
            {
                transform.position = _initialPosition;
            }
            else if (_currentMode == EditMode.Rotate)
            {
                transform.rotation = _initialRotation;
            }
            else if (_currentMode == EditMode.Scale)
            {
                transform.localScale = _initialScale;
            }

            EndEditSession();
            Debug.Log($"[PlaceableObjectMover] Cancelled edit for {name}");
        }

        private void TryPlace()
        {
            float targetGroundHeight = ModelLibraryUI.ResolveGroundHeightAt(
                transform.position,
                transform.position.y,
                transform);

            if (_currentMode == EditMode.Move)
            {
                Vector3 finalPosition = transform.position;
                finalPosition.y = targetGroundHeight + _groundOffsetFromSurface;
                transform.position = finalPosition;
            }
            else
            {
                ModelLibraryUI.SnapToGround(gameObject, targetGroundHeight);
            }

            _pendingTargetPos = transform.position;
            _pendingGroundHeight = targetGroundHeight;

            EndEditSession();
            Debug.Log($"[PlaceableObjectMover] Placed/Confirmed {name}");

            if (NetworkClient.active)
            {
                var worldObj = GetComponent<WorldObject>();
                if (worldObj != null && NetworkPlayerSetup.Local != null)
                {
                    bool ok = NetworkPlayerSetup.Local.RequestMove(
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

        private void EndEditSession()
        {
            _currentMode = EditMode.None;
            ClearHandleDrag();
            HideEditGizmo();
            GameplayInputBlocker.SetBlocked(this, false);
            SetCollidersEnabled(true);
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
                float minScale = 0.1f;
                float maxScale = 10f;

                newScale.x = Mathf.Clamp(newScale.x, minScale, maxScale);
                newScale.y = Mathf.Clamp(newScale.y, minScale, maxScale);
                newScale.z = Mathf.Clamp(newScale.z, minScale, maxScale);

                transform.localScale = newScale;
            }

            _lastMousePos = currentMousePos;
        }

        private bool TryBeginHandleDrag()
        {
            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                var handle = hit.collider.GetComponent<RuntimeGizmoHandle>();
                if (handle == null || handle.Owner != this)
                {
                    continue;
                }

                bool modeMatches =
                    (_currentMode == EditMode.Move && handle.HandleKind == GizmoHandleKind.MoveAxis) ||
                    (_currentMode == EditMode.Rotate && handle.HandleKind == GizmoHandleKind.RotateAxis);
                if (!modeMatches)
                {
                    continue;
                }

                BeginHandleDrag(handle);
                return _isDraggingHandle;
            }

            return false;
        }

        private void BeginHandleDrag(RuntimeGizmoHandle handle)
        {
            _activeHandle = handle;
            _dragStartPosition = transform.position;
            _dragStartRotation = transform.rotation;
            _isDraggingHandle = false;

            if (handle.HandleKind == GizmoHandleKind.MoveAxis)
            {
                if (handle.Axis == AxisKind.PlaneXZ)
                {
                    _dragPlane = new Plane(Vector3.up, _dragStartPosition);
                    if (!TryGetMousePointOnPlane(_dragPlane, out _dragStartPlanePoint))
                    {
                        _activeHandle = null;
                        return;
                    }
                }
                else
                {
                    Vector3 axis = handle.WorldAxis;
                    Vector3 planeNormal = ComputeAxisDragPlaneNormal(axis);
                    _dragPlane = new Plane(planeNormal, _dragStartPosition);

                    if (!TryGetMousePointOnPlane(_dragPlane, out var point))
                    {
                        _activeHandle = null;
                        return;
                    }

                    _dragStartAxisProjection = Vector3.Dot(point - _dragStartPosition, axis);
                }
            }
            else
            {
                Vector3 axis = handle.WorldAxis;
                _dragPlane = new Plane(axis, _dragStartPosition);

                if (!TryGetMousePointOnPlane(_dragPlane, out var point))
                {
                    _activeHandle = null;
                    return;
                }

                Vector3 fromCenter = Vector3.ProjectOnPlane(point - _dragStartPosition, axis);
                if (fromCenter.sqrMagnitude < 0.0001f)
                {
                    _activeHandle = null;
                    return;
                }

                _dragStartRotationVector = fromCenter.normalized;
            }

            _isDraggingHandle = true;
        }

        private void UpdateHandleDrag()
        {
            if (_activeHandle == null)
            {
                return;
            }

            if (!TryGetMousePointOnPlane(_dragPlane, out var point))
            {
                return;
            }

            if (_activeHandle.HandleKind == GizmoHandleKind.MoveAxis)
            {
                UpdateMoveHandleDrag(point);
                return;
            }

            UpdateRotateHandleDrag(point);
        }

        private void UpdateMoveHandleDrag(Vector3 pointOnPlane)
        {
            if (_activeHandle == null)
            {
                return;
            }

            if (_activeHandle.Axis == AxisKind.PlaneXZ)
            {
                Vector3 delta = pointOnPlane - _dragStartPlanePoint;
                Vector3 planeTargetPos = _dragStartPosition + new Vector3(delta.x, 0f, delta.z);
                ApplyGroundAlignedMove(planeTargetPos);
                return;
            }

            Vector3 axis = _activeHandle.WorldAxis;
            float currentProjection = Vector3.Dot(pointOnPlane - _dragStartPosition, axis);
            float deltaAlongAxis = currentProjection - _dragStartAxisProjection;
            Vector3 targetPos = _dragStartPosition + axis * deltaAlongAxis;

            if (_activeHandle.Axis == AxisKind.Y)
            {
                ApplyVerticalMove(targetPos.y);
            }
            else
            {
                ApplyGroundAlignedMove(targetPos);
            }
        }

        private void UpdateRotateHandleDrag(Vector3 pointOnPlane)
        {
            if (_activeHandle == null)
            {
                return;
            }

            Vector3 axis = _activeHandle.WorldAxis;
            Vector3 currentVector = Vector3.ProjectOnPlane(pointOnPlane - _dragStartPosition, axis);
            if (currentVector.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float angle = Vector3.SignedAngle(_dragStartRotationVector, currentVector.normalized, axis);
            transform.rotation = Quaternion.AngleAxis(angle, axis) * _dragStartRotation;
        }

        private void ApplyGroundAlignedMove(Vector3 targetPos)
        {
            ClampPlanarToMoveRadius(ref targetPos);

            float fallbackGround = transform.position.y - _groundOffsetFromSurface;
            float groundHeight = ModelLibraryUI.ResolveGroundHeightAt(targetPos, fallbackGround, transform);
            targetPos.y = groundHeight + _groundOffsetFromSurface;

            transform.position = targetPos;
            _pendingTargetPos = transform.position;
            _pendingGroundHeight = groundHeight;
        }

        private void ApplyVerticalMove(float targetY)
        {
            Vector3 targetPos = transform.position;
            targetPos.y = targetY;

            transform.position = targetPos;

            float groundHeight = ModelLibraryUI.ResolveGroundHeightAt(
                transform.position,
                transform.position.y,
                transform);
            _groundOffsetFromSurface = transform.position.y - groundHeight;
            _pendingTargetPos = transform.position;
            _pendingGroundHeight = groundHeight;
        }

        private void ClampPlanarToMoveRadius(ref Vector3 targetPos)
        {
            if (_playerTransform == null)
            {
                return;
            }

            Vector3 playerPosPlane = new Vector3(_playerTransform.position.x, 0f, _playerTransform.position.z);
            Vector3 targetPosPlane = new Vector3(targetPos.x, 0f, targetPos.z);

            float planarDistance = Vector3.Distance(playerPosPlane, targetPosPlane);
            if (planarDistance <= maxMoveRadius)
            {
                return;
            }

            Vector3 direction = (targetPosPlane - playerPosPlane).normalized;
            Vector3 clampedPlane = playerPosPlane + direction * maxMoveRadius;
            targetPos.x = clampedPlane.x;
            targetPos.z = clampedPlane.z;
        }

        private bool TryGetMousePointOnPlane(Plane plane, out Vector3 point)
        {
            point = Vector3.zero;
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!plane.Raycast(ray, out float enter))
            {
                return false;
            }

            point = ray.GetPoint(enter);
            return true;
        }

        private Vector3 ComputeAxisDragPlaneNormal(Vector3 axis)
        {
            Vector3 cameraForward = _cam != null ? _cam.transform.forward : Vector3.forward;
            Vector3 planeNormal = Vector3.Cross(axis, Vector3.Cross(cameraForward, axis));

            if (planeNormal.sqrMagnitude < 0.0001f)
            {
                Vector3 cameraUp = _cam != null ? _cam.transform.up : Vector3.up;
                planeNormal = Vector3.Cross(axis, Vector3.Cross(cameraUp, axis));
            }

            if (planeNormal.sqrMagnitude < 0.0001f)
            {
                planeNormal = Vector3.Cross(axis, axis == Vector3.up ? Vector3.forward : Vector3.up);
            }

            return planeNormal.normalized;
        }

        private void ClearHandleDrag()
        {
            _isDraggingHandle = false;
            _activeHandle = null;
        }

        private void EnsureEditGizmo()
        {
            if (_gizmoRoot != null)
            {
                return;
            }

            _gizmoRoot = new GameObject($"{name}_RuntimeGizmo");
            _gizmoRoot.hideFlags = HideFlags.HideAndDontSave;

            _moveGizmoRoot = new GameObject("MoveGizmo");
            _moveGizmoRoot.transform.SetParent(_gizmoRoot.transform, false);
            BuildMoveGizmo(_moveGizmoRoot.transform);

            _rotateGizmoRoot = new GameObject("RotateGizmo");
            _rotateGizmoRoot.transform.SetParent(_gizmoRoot.transform, false);
            BuildRotateGizmo(_rotateGizmoRoot.transform);

            HideEditGizmo();
        }

        private void BuildMoveGizmo(Transform parent)
        {
            CreateMoveAxis(parent, AxisKind.X, Vector3.right, new Color(0.92f, 0.18f, 0.18f));
            CreateMoveAxis(parent, AxisKind.Y, Vector3.up, new Color(0.18f, 0.88f, 0.22f));
            CreateMoveAxis(parent, AxisKind.Z, Vector3.forward, new Color(0.24f, 0.36f, 0.95f));

            var planeHandle = CreateHandlePrimitive(
                PrimitiveType.Cube,
                "MovePlaneXZ",
                parent,
                Vector3.zero,
                Vector3.one * PlaneHandleSize,
                Quaternion.identity,
                new Color(1f, 0.95f, 0.35f, 0.65f));
            planeHandle.HandleKind = GizmoHandleKind.MoveAxis;
            planeHandle.Axis = AxisKind.PlaneXZ;
        }

        private void CreateMoveAxis(Transform parent, AxisKind axisKind, Vector3 axis, Color color)
        {
            Quaternion rotation = GetAxisAlignment(axis);

            var shaft = CreateHandlePrimitive(
                PrimitiveType.Cube,
                $"{axisKind}_MoveShaft",
                parent,
                axis * (MoveAxisLength * 0.5f),
                new Vector3(MoveAxisThickness, MoveAxisThickness, MoveAxisLength),
                rotation,
                color);
            shaft.HandleKind = GizmoHandleKind.MoveAxis;
            shaft.Axis = axisKind;

            var tip = CreateHandlePrimitive(
                PrimitiveType.Cube,
                $"{axisKind}_MoveTip",
                parent,
                axis * (MoveAxisLength + (MoveHandleSize * 0.55f)),
                Vector3.one * MoveHandleSize,
                rotation,
                color);
            tip.HandleKind = GizmoHandleKind.MoveAxis;
            tip.Axis = axisKind;
        }

        private void BuildRotateGizmo(Transform parent)
        {
            CreateRotateRing(parent, AxisKind.X, new Color(0.92f, 0.18f, 0.18f));
            CreateRotateRing(parent, AxisKind.Y, new Color(0.18f, 0.88f, 0.22f));
            CreateRotateRing(parent, AxisKind.Z, new Color(0.24f, 0.36f, 0.95f));
        }

        private void CreateRotateRing(Transform parent, AxisKind axisKind, Color color)
        {
            for (int i = 0; i < RotateRingSegments; i++)
            {
                float t = (float)i / RotateRingSegments;
                float angle = t * Mathf.PI * 2f;
                Vector3 position;
                Vector3 tangent;
                Vector3 normal;

                switch (axisKind)
                {
                    case AxisKind.X:
                        position = new Vector3(0f, Mathf.Cos(angle) * RotateRingRadius, Mathf.Sin(angle) * RotateRingRadius);
                        tangent = new Vector3(0f, -Mathf.Sin(angle), Mathf.Cos(angle));
                        normal = Vector3.right;
                        break;
                    case AxisKind.Y:
                        position = new Vector3(Mathf.Cos(angle) * RotateRingRadius, 0f, Mathf.Sin(angle) * RotateRingRadius);
                        tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                        normal = Vector3.up;
                        break;
                    default:
                        position = new Vector3(Mathf.Cos(angle) * RotateRingRadius, Mathf.Sin(angle) * RotateRingRadius, 0f);
                        tangent = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f);
                        normal = Vector3.forward;
                        break;
                }

                float segmentLength = 2f * Mathf.PI * RotateRingRadius / RotateRingSegments * 0.92f;
                Quaternion rotation = Quaternion.LookRotation(tangent.normalized, normal);

                var segment = CreateHandlePrimitive(
                    PrimitiveType.Cube,
                    $"{axisKind}_RotateSegment_{i}",
                    parent,
                    position,
                    new Vector3(RotateRingThickness, RotateRingThickness, segmentLength),
                    rotation,
                    color);
                segment.HandleKind = GizmoHandleKind.RotateAxis;
                segment.Axis = axisKind;
            }
        }

        private RuntimeGizmoHandle CreateHandlePrimitive(
            PrimitiveType primitiveType,
            string objectName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Color color)
        {
            var handleObject = GameObject.CreatePrimitive(primitiveType);
            handleObject.name = objectName;
            handleObject.transform.SetParent(parent, false);
            handleObject.transform.localPosition = localPosition;
            handleObject.transform.localRotation = localRotation;
            handleObject.transform.localScale = localScale;
            handleObject.hideFlags = HideFlags.HideAndDontSave;

            var collider = handleObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            var renderer = handleObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.material = CreateHandleMaterial(color);
            }

            var handle = handleObject.AddComponent<RuntimeGizmoHandle>();
            handle.Owner = this;
            return handle;
        }

        private Material CreateHandleMaterial(Color color)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");

            var material = new Material(shader);
            material.color = color;
            return material;
        }

        private Quaternion GetAxisAlignment(Vector3 axis)
        {
            Vector3 upHint = axis == Vector3.up ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(axis, upHint);
        }

        private void SetEditGizmoVisible(bool showMove, bool showRotate)
        {
            EnsureEditGizmo();
            if (_moveGizmoRoot != null) _moveGizmoRoot.SetActive(showMove);
            if (_rotateGizmoRoot != null) _rotateGizmoRoot.SetActive(showRotate);
            if (_gizmoRoot != null) _gizmoRoot.SetActive(showMove || showRotate);
            UpdateGizmoTransform();
        }

        private void HideEditGizmo()
        {
            if (_gizmoRoot != null)
            {
                _gizmoRoot.SetActive(false);
            }
        }

        private void UpdateGizmoTransform()
        {
            if (_gizmoRoot == null || !_gizmoRoot.activeSelf || _cam == null)
            {
                return;
            }

            _gizmoRoot.transform.position = transform.position;
            float distance = Vector3.Distance(_cam.transform.position, transform.position);
            float gizmoScale = Mathf.Clamp(distance * gizmoDistanceScale, gizmoMinSize, gizmoMaxSize);
            _gizmoRoot.transform.localScale = Vector3.one * gizmoScale;
        }

        private void DestroyEditGizmo()
        {
            if (_gizmoRoot != null)
            {
                Destroy(_gizmoRoot);
                _gizmoRoot = null;
                _moveGizmoRoot = null;
                _rotateGizmoRoot = null;
            }
        }

        private void ShowMessageDialog()
        {
            var interactable = GetComponent<InteractableObject>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<InteractableObject>();
            }

            if (ObjectInteractionManager.Instance == null)
            {
                var managerObj = new GameObject("ObjectInteractionManager");
                managerObj.AddComponent<ObjectInteractionManager>();
            }

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

            var worldObj = GetComponent<WorldObject>();
            if (NetworkClient.active && NetworkPlayerSetup.Local != null && worldObj != null)
            {
                bool ok = NetworkPlayerSetup.Local.RequestDelete(worldObj.ObjectId);
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
            yield return null;
            if (manager == null) yield break;
            manager.SaveWorldServer(
                onError: err => Debug.LogWarning($"[PlaceableObjectMover] Autosave failed after delete: {err}")
            );
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

    internal sealed class RuntimeGizmoHandle : MonoBehaviour
    {
        public PlaceableObjectMover Owner { get; set; }
        public GizmoHandleKind HandleKind { get; set; }
        public AxisKind Axis { get; set; }

        public Vector3 WorldAxis
        {
            get
            {
                switch (Axis)
                {
                    case AxisKind.X: return Vector3.right;
                    case AxisKind.Y: return Vector3.up;
                    case AxisKind.Z: return Vector3.forward;
                    default: return Vector3.zero;
                }
            }
        }
    }
}
