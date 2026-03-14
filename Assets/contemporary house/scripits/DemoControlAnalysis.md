# Demo 场景中“鼠标控制视角”与“以视角为准的前后左右”实现分析

## 1. 鼠标控制视角（Demo 实现）

- **输入来源**：StarterAssets 使用 **新 Input System**，`StarterAssetsInputs` 的 `look`（Vector2）由 `OnLook(InputValue value)` 接收，通常绑定到 **Mouse delta**（鼠标位移）。
- **视角驱动**：`ThirdPersonController.CameraRotation()` 在 **LateUpdate** 中：
  - 若有 look 输入：`_cinemachineTargetYaw += _input.look.x`，`_cinemachineTargetPitch += _input.look.y`（鼠标不乘 `Time.deltaTime`）。
  - 限制 pitch：`BottomClamp = -30`，`TopClamp = 70`。
  - 将 yaw/pitch 写入 **CinemachineCameraTarget**（玩家子物体）：  
    `CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0f)`。
- **相机跟随**：场景中的 **CinemachineVirtualCamera**（如 PlayerFollowCamera）的 **Follow** 指向该 CinemachineCameraTarget，相机自动跟随该目标的旋转与位置，实现“鼠标控制视角”。

## 2. 前后左右以视角为准（Demo 实现）

- **输入**：`StarterAssetsInputs.move`（Vector2）来自 WASD，表示“相对相机的方向”（W=前，S=后，A=左，D=右）。
- **朝向与移动**：`ThirdPersonController.Move()` 中：
  - `cameraYaw = _mainCamera.transform.eulerAngles.y`（MainCamera 即 Cinemachine 驱动的相机，其 yaw 与 CinemachineCameraTarget 一致）。
  - `_targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraYaw`  
    即：**目标朝向 = 输入在相机平面上的方向角 + 相机 yaw**，所以 W=朝相机看的方向前进，S=后退，A/D=左右平移方向。
  - 角色用 `SmoothDampAngle` 转向 `_targetRotation`，移动方向为 `Quaternion.Euler(0, _targetRotation, 0) * Vector3.forward`。

因此：**前后左右的方向完全以视角（相机 yaw）为基准**。

## 3. MainScene 如何复现

- **有 ThirdPersonController + StarterAssetsInputs**：MainScene 使用与 demo 相同的玩家预制体（含 TPC、StarterAssetsInputs、CinemachineCameraTarget），且场景中有 CinemachineVirtualCamera 且 Follow 指向本地玩家的 CinemachineCameraTarget，则行为与 demo 一致（鼠标控制视角、移动以视角为准）。
- **无 ThirdPersonController**：`NetworkPlayerController` 提供 **备用逻辑**：用 **旧 Input**（`GetAxis("Mouse X/Y")`、`Horizontal/Vertical`）驱动一个“相机目标”的 yaw/pitch（与 demo 相同的 clamp），并同样用 **cameraYaw + Atan2(输入)** 计算朝向与移动方向，使 MainScene 在任意预制体下也能具备“鼠标控制视角”和“以视角为准的前后左右”。
