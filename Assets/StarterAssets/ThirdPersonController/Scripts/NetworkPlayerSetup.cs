using UnityEngine;
using Mirror;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    /// <summary>
    /// Simple network component that handles local player setup for Mirror.
    /// Add this alongside ThirdPersonController to handle multiplayer camera and input.
    /// This does NOT replace ThirdPersonController - it works WITH it.
    /// </summary>
    public class NetworkPlayerSetup : NetworkBehaviour
    {
        [Header("Components to disable for remote players")]
        [Tooltip("These components will be disabled for remote players")]
        public MonoBehaviour[] componentsToDisableForRemote;

        [Header("Cinemachine")]
        [Tooltip("The follow target for Cinemachine (usually PlayerCameraRoot)")]
        public Transform cinemachineFollowTarget;

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            
            // This is the local player - setup camera to follow us
            SetupCameraForLocalPlayer();
            
            // Enable all input-related components
            EnableComponents(true);
            
            Debug.Log($"[NetworkPlayerSetup] Local player setup complete: {gameObject.name}");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (!isLocalPlayer)
            {
                // This is a remote player - disable input and camera control
                DisableRemotePlayerComponents();
                Debug.Log($"[NetworkPlayerSetup] Remote player disabled input: {gameObject.name}");
            }
            else
            {
                // Safety: ensure local player always has components enabled
                EnableComponents(true);
                Debug.Log($"[NetworkPlayerSetup] Local player components ensured enabled: {gameObject.name}");
            }
        }

        private void DisableRemotePlayerComponents()
        {
            // Disable PlayerInput for remote players
#if ENABLE_INPUT_SYSTEM
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = false;
            }
#endif

            // Disable StarterAssetsInputs for remote players
            var starterInputs = GetComponent<StarterAssetsInputs>();
            if (starterInputs != null)
            {
                starterInputs.enabled = false;
            }

            // Disable ThirdPersonController for remote players (movement handled by NetworkTransform)
            var thirdPersonController = GetComponent<ThirdPersonController>();
            if (thirdPersonController != null)
            {
                thirdPersonController.enabled = false;
            }

            // Disable any additional components specified
            if (componentsToDisableForRemote != null)
            {
                foreach (var component in componentsToDisableForRemote)
                {
                    if (component != null)
                    {
                        component.enabled = false;
                    }
                }
            }

            // Disable CharacterController for remote players (position synced via NetworkTransform)
            var characterController = GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }
        }

        private void EnableComponents(bool enable)
        {
#if ENABLE_INPUT_SYSTEM
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = enable;
            }
#endif

            var starterInputs = GetComponent<StarterAssetsInputs>();
            if (starterInputs != null)
            {
                starterInputs.enabled = enable;
            }

            var thirdPersonController = GetComponent<ThirdPersonController>();
            if (thirdPersonController != null)
            {
                thirdPersonController.enabled = enable;
            }

            var characterController = GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = enable;
            }
        }

        private void SetupCameraForLocalPlayer()
        {
            if (cinemachineFollowTarget == null)
            {
                // Try to find PlayerCameraRoot child
                var cameraRoot = transform.Find("PlayerCameraRoot");
                if (cameraRoot != null)
                {
                    cinemachineFollowTarget = cameraRoot;
                }
                else
                {
                    Debug.LogWarning("[NetworkPlayerSetup] cinemachineFollowTarget is null and couldn't find PlayerCameraRoot");
                    return;
                }
            }

            // Find all Cinemachine virtual cameras and set follow target
            // Only set Follow, don't set LookAt (the original ThirdPersonController doesn't use LookAt)
            var virtualCameras = FindObjectsByType<Cinemachine.CinemachineVirtualCamera>(FindObjectsSortMode.None);
            foreach (var vc in virtualCameras)
            {
                vc.Follow = cinemachineFollowTarget;
                // Don't set LookAt - this can cause different camera behavior
                // vc.LookAt = cinemachineFollowTarget;
                Debug.Log($"[NetworkPlayerSetup] Cinemachine camera '{vc.name}' now following: {cinemachineFollowTarget.name}");
            }
        }
    }
}
