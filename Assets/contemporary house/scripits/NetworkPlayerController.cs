using UnityEngine;
using Mirror;
using LittleDog;

public class NetworkPlayerController : NetworkBehaviour
{
    private PlayerController playerController;
    private Camera playerCamera;
    private Transform cameraPivot;

    [Header("第三人称鼠标视角")]
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 80f;
    private float verticalAngle;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera != null)
        {
            cameraPivot = playerCamera.transform;
            playerCamera.enabled = false; // 默认禁用，仅本地玩家在 OnStartLocalPlayer 中启用
        }
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (playerController != null)
            PlayerController.canMove = true;

        // 仅本地玩家：启用相机、初始化 CameraPivot、锁定并隐藏鼠标
        if (playerCamera != null)
        {
            playerCamera.enabled = true;
            verticalAngle = cameraPivot.localEulerAngles.x;
            if (verticalAngle > 180f) verticalAngle -= 360f;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnStopLocalPlayer()
    {
        base.OnStopLocalPlayer();

        if (playerController != null)
            PlayerController.canMove = false;

        if (playerCamera != null)
            playerCamera.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void LateUpdate()
    {
        if (!isLocalPlayer || cameraPivot == null) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Mouse X：Player 本体绕 Y 轴旋转（左右看）
        transform.Rotate(Vector3.up * mouseX);

        // Mouse Y：CameraPivot 绕 X 轴旋转（上下看），并限制角度
        verticalAngle -= mouseY;
        verticalAngle = Mathf.Clamp(verticalAngle, minPitch, maxPitch);
        cameraPivot.localRotation = Quaternion.Euler(verticalAngle, 0f, 0f);
    }
}
