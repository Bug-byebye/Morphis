using UnityEngine;
using Mirror;

/// <summary>
/// 修复版NetworkPlayerController - 解决切换场景后无法移动的问题
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class NetworkPlayerController : NetworkBehaviour
{
    private MonoBehaviour _thirdPersonController;
    private MonoBehaviour _inputs;
    private Transform _cinemachineTarget;

    [Header("备用控制（无 ThirdPersonController 时）")]
    [SerializeField] private Transform _cameraTargetFallback;
    [SerializeField] private float _topClamp = 70f;
    [SerializeField] private float _bottomClamp = -30f;
    [SerializeField] private float _sensitivity = 1f;
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _sprintSpeed = 5.335f;
    [SerializeField] private float _rotationSmoothTime = 0.12f;
    [SerializeField] private float _speedChangeRate = 10f;
    [SerializeField] private float _gravity = -15f;
    [SerializeField] private float _jumpHeight = 1.2f;
    [SerializeField] private float _groundedOffset = -0.14f;
    [SerializeField] private float _groundedRadius = 0.28f;
    [SerializeField] private LayerMask _groundLayers = ~0;

    private CharacterController _controller;
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;
    private float _speed;
    private float _targetRotation;
    private float _rotationVelocity;
    private float _verticalVelocity;
    private const float TerminalVelocity = 53f;
    private const float Threshold = 0.01f;
    private bool _useFallback;
    private bool _isInitialized = false;

    public static Camera LocalPlayerCamera { get; private set; }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();

        var tpcType = System.Type.GetType("StarterAssets.ThirdPersonController, Assembly-CSharp");
        var inputsType = System.Type.GetType("StarterAssets.StarterAssetsInputs, Assembly-CSharp");
        _thirdPersonController = tpcType != null ? (GetComponent(tpcType) as MonoBehaviour) : null;
        _inputs = inputsType != null ? (GetComponent(inputsType) as MonoBehaviour) : null;

        _useFallback = _thirdPersonController == null || _inputs == null;
        
        Debug.Log($"[NetworkPlayerController] Awake - ThirdPersonController: {(_thirdPersonController != null ? "Found" : "Not Found")}, UsesFallback: {_useFallback}");
        
        if (_useFallback)
        {
            if (_cameraTargetFallback == null)
                _cameraTargetFallback = transform.Find("CinemachineCameraTarget");
            if (_cameraTargetFallback == null)
            {
                var go = new GameObject("CinemachineCameraTarget");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                _cameraTargetFallback = go.transform;
            }
        }
        else
        {
            // 先禁用，等OnStartLocalPlayer时再启用
            if (_thirdPersonController != null) _thirdPersonController.enabled = false;
            if (_inputs != null) _inputs.enabled = false;
        }
    }

    private void Start()
    {
        // 单机模式 - 直接激活控制器
        if (!NetworkClient.active && !NetworkServer.active)
        {
            Debug.Log("[NetworkPlayerController] 单机模式 - 激活控制器");
            ActivateControllers();
        }
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Debug.Log("[NetworkPlayerController] OnStartLocalPlayer called");
        ActivateControllers();
    }

    private void ActivateControllers()
    {
        if (_isInitialized)
        {
            Debug.Log("[NetworkPlayerController] Already initialized, skipping");
            return;
        }

        _isInitialized = true;

        Transform followTarget = null;
        if (_useFallback)
        {
            Debug.Log("[NetworkPlayerController] Using fallback controls");
            followTarget = _cameraTargetFallback;
            _cinemachineTargetYaw = transform.eulerAngles.y;
            _targetRotation = _cinemachineTargetYaw;
            _cinemachineTargetPitch = 0f;
        }
        else
        {
            Debug.Log("[NetworkPlayerController] Using ThirdPersonController");
            _cinemachineTarget = GetCinemachineCameraTarget(_thirdPersonController);
            followTarget = _cinemachineTarget;
            
            // 启用ThirdPersonController和StarterAssetsInputs
            if (_thirdPersonController != null)
            {
                _thirdPersonController.enabled = true;
                Debug.Log("[NetworkPlayerController] ThirdPersonController enabled");
            }
            if (_inputs != null)
            {
                _inputs.enabled = true;
                Debug.Log("[NetworkPlayerController] StarterAssetsInputs enabled");
            }
        }

        if (followTarget != null)
            SetSceneVCamFollow(followTarget);

        LocalPlayerCamera = Camera.main;
        
        // 保持鼠标可见
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        Debug.Log("[NetworkPlayerController] Local player activated successfully");
    }

    public override void OnStopLocalPlayer()
    {
        base.OnStopLocalPlayer();
        Debug.Log("[NetworkPlayerController] OnStopLocalPlayer called");

        if (!_useFallback)
        {
            if (_thirdPersonController != null) _thirdPersonController.enabled = false;
            if (_inputs != null) _inputs.enabled = false;
        }
        if (LocalPlayerCamera != null) LocalPlayerCamera = null;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        _isInitialized = false;
    }

    private void Update()
    {
        // 单机模式检查
        bool isSinglePlayer = !NetworkClient.active && !NetworkServer.active;
        bool canControl = isSinglePlayer || isLocalPlayer;
        
        if (!canControl) return;
        
        // 如果还没初始化，尝试初始化
        if (!_isInitialized && isSinglePlayer)
        {
            ActivateControllers();
        }

        // 如果不是使用备用控制，就不处理（ThirdPersonController会处理）
        if (!_useFallback) return;

        // 备用控制逻辑
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        
        // 只在鼠标锁定时才移动（备用模式）
        if (Cursor.lockState != CursorLockMode.Locked && _useFallback) return;

        FallbackJumpAndGravity();
        FallbackMove();
    }

    private void LateUpdate()
    {
        bool isSinglePlayer = !NetworkClient.active && !NetworkServer.active;
        bool canControl = isSinglePlayer || isLocalPlayer;
        
        if (!canControl || !_useFallback || _cameraTargetFallback == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        FallbackCameraRotation();
    }

    private void FallbackCameraRotation()
    {
        float lookX = Input.GetAxis("Mouse X") * _sensitivity;
        float lookY = Input.GetAxis("Mouse Y") * _sensitivity;

        if (lookX * lookX + lookY * lookY >= Threshold * Threshold)
        {
            _cinemachineTargetYaw += lookX;
            _cinemachineTargetPitch += lookY;
        }

        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, _bottomClamp, _topClamp);

        _cameraTargetFallback.rotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0f);
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }

    private bool FallbackGroundedCheck()
    {
        var p = transform.position;
        var spherePosition = new Vector3(p.x, p.y - _groundedOffset, p.z);
        return Physics.CheckSphere(spherePosition, _groundedRadius, _groundLayers, QueryTriggerInteraction.Ignore);
    }

    private void FallbackJumpAndGravity()
    {
        bool grounded = FallbackGroundedCheck();

        if (grounded)
        {
            if (_verticalVelocity < 0f) _verticalVelocity = -2f;
            if (Input.GetButtonDown("Jump"))
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
        }

        if (_verticalVelocity < TerminalVelocity)
            _verticalVelocity += _gravity * Time.deltaTime;
    }

    private void FallbackMove()
    {
        if (_controller == null) return;

        float targetSpeed = Input.GetKey(KeyCode.LeftShift) ? _sprintSpeed : _moveSpeed;
        Vector2 inputMove = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        if (inputMove.sqrMagnitude < Threshold * Threshold) targetSpeed = 0f;

        float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude;
        float inputMagnitude = 1f;
        float speedOffset = 0.1f;

        if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
        {
            _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * _speedChangeRate);
            _speed = Mathf.Round(_speed * 1000f) / 1000f;
        }
        else
        {
            _speed = targetSpeed;
        }

        Vector3 inputDirection = new Vector3(inputMove.x, 0f, inputMove.y).normalized;

        if (inputMove.sqrMagnitude >= Threshold * Threshold)
        {
            float cameraYaw = _cinemachineTargetYaw;
            _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraYaw;
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, _rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, rotation, 0f);
        }

        Vector3 targetDirection = Quaternion.Euler(0f, _targetRotation, 0f) * Vector3.forward;
        _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0f, _verticalVelocity * Time.deltaTime, 0f));
    }

    private static Transform GetCinemachineCameraTarget(MonoBehaviour thirdPersonController)
    {
        if (thirdPersonController == null) return null;
        var t = thirdPersonController.GetType();
        var prop = t.GetProperty("CinemachineCameraTarget");
        if (prop == null) return null;
        var go = prop.GetValue(thirdPersonController) as GameObject;
        return go != null ? go.transform : null;
    }

    private static void SetSceneVCamFollow(Transform followTarget)
    {
        var vcamType = System.Type.GetType("Cinemachine.CinemachineVirtualCamera,Cinemachine");
        if (vcamType == null) return;
#pragma warning disable 0618
        var vcam = Object.FindObjectOfType(vcamType);
#pragma warning restore 0618
        if (vcam == null)
        {
            Debug.LogWarning("[NetworkPlayerController] 场景中未找到 CinemachineVirtualCamera");
            return;
        }
        var followProp = vcamType.GetProperty("Follow");
        if (followProp != null)
        {
            followProp.SetValue(vcam, followTarget);
            Debug.Log("[NetworkPlayerController] Cinemachine follow set successfully");
        }
    }

    public static Ray GetLocalPlayerScreenPointToRay(Vector3 screenPosition)
    {
        if (LocalPlayerCamera != null)
            return LocalPlayerCamera.ScreenPointToRay(screenPosition);
        Debug.LogWarning("[NetworkPlayerController] LocalPlayerCamera is null");
        return new Ray();
    }

    public static Vector3 GetCameraForward()
    {
        if (LocalPlayerCamera != null)
            return LocalPlayerCamera.transform.forward;
        return Vector3.forward;
    }

    public static Vector3 GetCameraRight()
    {
        if (LocalPlayerCamera != null)
            return LocalPlayerCamera.transform.right;
        return Vector3.right;
    }

    public static Vector3 GetCameraPosition()
    {
        if (LocalPlayerCamera != null)
            return LocalPlayerCamera.transform.position;
        return Vector3.zero;
    }
}
