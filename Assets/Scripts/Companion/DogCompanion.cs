using UnityEngine;
using UnityEngine.AI;
using System.Text.RegularExpressions;

namespace Morphis.Companion
{
    /// <summary>
    /// Makes a dog follow/lead the player with walk/run animations.
    /// Requires: NavMeshAgent component, Animator with AnimationID parameter
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class DogCompanion : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The player transform to follow. If null, will try to find by tag 'Player'")]
        [SerializeField] private Transform target;

        [Header("Position Offset")]
        [Tooltip("How far behind the player the dog should stay (positive = behind)")]
        [SerializeField] private float forwardOffset = 3f;
        [Tooltip("Side offset (positive = right, negative = left)")]
        [SerializeField] private float sideOffset = 0.5f;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 2f;
        [SerializeField] private float runSpeed = 5f;
        [Tooltip("Distance at which dog starts running to catch up")]
        [SerializeField] private float runThreshold = 4f;
        [Tooltip("Distance at which dog stops moving")]
        [SerializeField] private float stoppingDistance = 0.5f;

        [Header("Animation IDs (from Dog Animator Controller)")]
        [SerializeField] private int idleAnimId = 0;
        [SerializeField] private int walkAnimId = 2;
        [SerializeField] private int runAnimId = 4;

        [Header("Chat")]
        [SerializeField] private string dogName = "Buddy";
        [Tooltip("Hugging Face token for voice recognition (Whisper). Leave empty to disable voice ASR requests.")]
        [SerializeField] private string huggingFaceApiToken = "";

        [Header("Action Commands")]
        [Tooltip("聊天指令可触发的最大动作编号。例如 8 表示支持 动作1~动作8。")]
        [SerializeField] private int maxActionId = 8;
        [Tooltip("触发动作后保持该动作的时长（秒）。")]
        [SerializeField] private float actionCommandDuration = 2f;

        private NavMeshAgent _agent;
        private Animator _animator;
        private Vector3 _lastTargetPosition;
        private Vector3 _smoothedMoveDirection;
        private DogChatUIWithVoice _chatUI;
        private int _currentAnimId = -1;
        private bool _isCommandActionActive;
        private float _commandActionEndTime;
        private int _commandActionAnimId = -1;
        private static readonly int AnimationIDHash = Animator.StringToHash("AnimationID");
        public string DogName => dogName;

        private void Awake()
        {
            // Validate animation IDs - serialized values in the prefab may be stale
            if (walkAnimId != 2 || runAnimId != 4 || idleAnimId != 0)
            {
                Debug.LogWarning($"[DogCompanion] Animation IDs were incorrect (idle={idleAnimId}, walk={walkAnimId}, run={runAnimId}). " +
                    "Resetting to correct values (idle=0, walk=2, run=4). Please update the prefab in Inspector.");
                idleAnimId = 0;
                walkAnimId = 2;
                runAnimId = 4;
            }

            _agent = GetComponent<NavMeshAgent>();
            
            // Find Animator - check self first, then children (common for imported 3D models)
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
            
            if (_animator != null)
            {
                // Disable root motion so NavMeshAgent controls movement
                _animator.applyRootMotion = false;
                
                // Verify the AnimationID parameter exists
                bool hasParam = false;
                foreach (var p in _animator.parameters)
                {
                    if (p.name == "AnimationID" && p.type == AnimatorControllerParameterType.Int)
                    {
                        hasParam = true;
                        break;
                    }
                }
                
                if (!hasParam)
                {
                    Debug.LogError("[DogCompanion] Animator found but missing 'AnimationID' int parameter! " +
                        "Make sure the Dog_Animator_Controler is assigned to the Animator component.");
                }
                else
                {
                    Debug.Log($"[DogCompanion] Animator found on '{_animator.gameObject.name}' with AnimationID parameter ✓");
                }
            }
            else
            {
                Debug.LogError("[DogCompanion] No Animator found on this GameObject or its children! " +
                    "The dog will not animate. Make sure the dog model has an Animator component with Dog_Animator_Controler assigned.");
            }

            // Create chat UI with voice recognition
            _chatUI = gameObject.AddComponent<DogChatUIWithVoice>();
            _chatUI.ConfigureVoiceApiToken(huggingFaceApiToken);
            _chatUI.SetLocalCommandHandler(HandleDogActionCommand);
            _chatUI.SetModelActionHandler(HandleModelSuggestedAction);

            AutoDetectActionRange();
            DogChatAPI.SetActionCategoryCount(maxActionId);

            // Ensure collider exists for click detection
            EnsureCollider();

            // Prevent dog from pushing the player
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

            // Sync NavMeshAgent update with Animator for smoother movement
            _agent.updatePosition = true;
            _agent.updateRotation = true;

            // Ignore collisions between dog and player layers at the physics level
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
        }

        private void EnsureCollider()
        {
            var col = GetComponent<Collider>();
            if (col == null)
            {
                // Try to find collider in children
                col = GetComponentInChildren<Collider>();
            }
            if (col == null)
            {
                // Add capsule collider if none exists
                var capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0, 0.5f, 0);
                capsule.radius = 0.5f;
                capsule.height = 1f;
                capsule.isTrigger = true;
                Debug.Log("[DogCompanion] Added trigger CapsuleCollider for click detection");
            }
            else
            {
                // Make existing collider a trigger so the dog can't push the player
                col.isTrigger = true;
            }
        }

        /// <summary>
        /// Called when the dog is clicked (requires collider)
        /// </summary>
        private void OnMouseDown()
        {
            // Block clicks when the workflow station editor is open
            if (AIPipeline.UI.SimpleNodeEditor.IsEditorOpen) return;

            if (_chatUI != null)
            {
                // Click on dog should always open chat when closed.
                // When open, keep it open (avoid accidental close from click-through).
                if (!_chatUI.IsOpen)
                {
                    _chatUI.Open();
                }
            }
        }

        public void OpenChatPanel()
        {
            if (_chatUI != null)
            {
                _chatUI.Open();
            }
        }

        public void ToggleChatPanel()
        {
            if (_chatUI != null)
            {
                _chatUI.Toggle();
            }
        }

        private void Start()
        {
            _agent.stoppingDistance = stoppingDistance;
            
            // Try to find player (may not be spawned yet in networked games)
            TryFindPlayer();

            if (target != null)
            {
                _lastTargetPosition = target.position;
                _smoothedMoveDirection = target.forward;
                EnsureOnNavMesh();
            }
        }

        /// <summary>
        /// Attempts to find the local player. Called continuously until player is found.
        /// Supports Mirror networking (finds local player specifically).
        /// </summary>
        private void TryFindPlayer()
        {
            if (target != null) return;

            // Method 1: Find by "Player" tag
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
                Debug.Log($"[DogCompanion] Found player by tag: {target.name}");
                return;
            }

            // Method 2: Find Mirror local player (using reflection to avoid compile dependency)
            try
            {
                var networkIdentityType = System.Type.GetType("Mirror.NetworkIdentity, Mirror");
                if (networkIdentityType != null)
                {
                    var allNetworkIds = FindObjectsByType(networkIdentityType, FindObjectsSortMode.None);
                    foreach (var obj in allNetworkIds)
                    {
                        var component = obj as Component;
                        if (component == null) continue;
                        
                        var isLocalPlayerProp = networkIdentityType.GetProperty("isLocalPlayer");
                        if (isLocalPlayerProp != null)
                        {
                            bool isLocal = (bool)isLocalPlayerProp.GetValue(component);
                            if (isLocal)
                            {
                                target = component.transform;
                                Debug.Log($"[DogCompanion] Found Mirror local player: {target.name}");
                                return;
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DogCompanion] Error finding Mirror player: {e.Message}");
            }

            // Method 3: Find by CharacterController
            var cc = FindFirstObjectByType<CharacterController>();
            if (cc != null)
            {
                target = cc.transform;
                Debug.Log($"[DogCompanion] Found player by CharacterController: {target.name}");
                return;
            }

            // Method 4: Find by name pattern
            var playerObj = GameObject.Find("PlayerArmature_Network");
            if (playerObj == null) playerObj = GameObject.Find("PlayerArmature");
            if (playerObj != null)
            {
                target = playerObj.transform;
                Debug.Log($"[DogCompanion] Found player by name: {target.name}");
            }
        }

        private void EnsureOnNavMesh()
        {
            if (_agent.isOnNavMesh) return;

            // Try to warp to nearest NavMesh position
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
                Debug.Log($"[DogCompanion] Warped dog to NavMesh at {hit.position}");
            }
            else
            {
                // Try to find a position near the player
                if (target != null && NavMesh.SamplePosition(target.position, out hit, 10f, NavMesh.AllAreas))
                {
                    _agent.Warp(hit.position);
                    Debug.Log($"[DogCompanion] Warped dog to NavMesh near player at {hit.position}");
                }
                else
                {
                    Debug.LogWarning("[DogCompanion] Could not find valid NavMesh position! Please bake a NavMesh.");
                }
            }
        }

        private void Update()
        {
            // If a command action is active, keep showing it and temporarily pause follow locomotion.
            if (UpdateCommandActionPlayback()) return;

            // Continuously try to find player if not assigned (for networked games)
            if (target == null)
            {
                TryFindPlayer();
                if (target == null) return; // Still no player, wait
                
                // Player just found - initialize
                _lastTargetPosition = target.position;
                _smoothedMoveDirection = target.forward;
                EnsureOnNavMesh();
            }

            // Safety check - ensure agent is on NavMesh
            if (!_agent.isOnNavMesh)
            {
                EnsureOnNavMesh();
                if (!_agent.isOnNavMesh) return; // Still not on NavMesh, skip this frame
            }

            // Calculate target position ahead of player
            Vector3 targetPos = CalculateTargetPosition();
            float distanceToTarget = Vector3.Distance(transform.position, targetPos);

            // Determine speed based on distance
            bool shouldRun = distanceToTarget > runThreshold;
            _agent.speed = shouldRun ? runSpeed : walkSpeed;

            // Set destination (only if agent is active and on NavMesh)
            bool isMoving = distanceToTarget > stoppingDistance;
            if (isMoving && _agent.isActiveAndEnabled)
            {
                _agent.SetDestination(targetPos);
            }
            else if (_agent.isActiveAndEnabled && _agent.hasPath)
            {
                // Stop the agent when close enough
                _agent.ResetPath();
            }

            // Update animation based on movement state
            UpdateAnimation(distanceToTarget, shouldRun);

            _lastTargetPosition = target.position;
        }

        private string HandleDogActionCommand(string rawMessage)
        {
            if (string.IsNullOrWhiteSpace(rawMessage)) return null;

            string message = rawMessage.Trim();
            if (message.Length == 0) return null;

            // Help/list command: "动作", "动作列表", "action", "action list"
            if (Regex.IsMatch(message, @"^(动作|action)\s*(列表|list|help)?$", RegexOptions.IgnoreCase))
            {
                return $"我会的动作是：动作1 到 动作{maxActionId}。例如输入“动作3”。";
            }

            Match match = Regex.Match(message, @"^(动作|action)\s*([0-9]+)$", RegexOptions.IgnoreCase);
            if (!match.Success) return null;

            if (!int.TryParse(match.Groups[2].Value, out int actionId))
            {
                return $"我没看懂这个动作编号。可用范围：动作1 到 动作{maxActionId}。";
            }

            if (actionId < 1 || actionId > maxActionId)
            {
                return $"这个动作超出范围啦，我现在支持 动作1 到 动作{maxActionId}。";
            }

            if (_animator == null)
            {
                return "我现在还不能做动作（缺少 Animator）。";
            }

            TriggerCommandAction(actionId);
            return $"收到，我来做动作{actionId}！";
        }

        private void TriggerCommandAction(int actionId)
        {
            _isCommandActionActive = true;
            _commandActionAnimId = actionId;
            _commandActionEndTime = Time.time + Mathf.Max(0.2f, actionCommandDuration);

            if (_agent != null && _agent.isActiveAndEnabled && _agent.hasPath)
            {
                _agent.ResetPath();
            }

            if (_animator != null)
            {
                _currentAnimId = actionId;
                _animator.SetInteger(AnimationIDHash, actionId);
                Debug.Log($"[DogCompanion] Command action triggered: 动作{actionId}");
            }
        }

        private void HandleModelSuggestedAction(int actionCategory)
        {
            if (actionCategory < 1 || actionCategory > maxActionId)
            {
                Debug.Log($"[DogCompanion] Ignored model action category {actionCategory}. Valid range is 1..{maxActionId}.");
                return;
            }

            TriggerCommandAction(actionCategory);
        }

        private bool UpdateCommandActionPlayback()
        {
            if (!_isCommandActionActive) return false;

            if (_animator == null)
            {
                _isCommandActionActive = false;
                _commandActionAnimId = -1;
                return false;
            }

            if (Time.time >= _commandActionEndTime)
            {
                _isCommandActionActive = false;
                _commandActionAnimId = -1;
                return false;
            }

            if (_agent != null && _agent.isActiveAndEnabled && _agent.hasPath)
            {
                _agent.ResetPath();
            }

            if (_currentAnimId != _commandActionAnimId)
            {
                _currentAnimId = _commandActionAnimId;
                _animator.SetInteger(AnimationIDHash, _commandActionAnimId);
            }

            return true;
        }

        private void AutoDetectActionRange()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                maxActionId = Mathf.Max(1, maxActionId);
                return;
            }

            int detectedMax = 0;
            var clips = _animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null) continue;

                // Match clip names like "Action1", "Action_2", "动作3".
                Match m = Regex.Match(clip.name, @"(?:动作|action)[\s_-]*([0-9]+)", RegexOptions.IgnoreCase);
                if (m.Success && int.TryParse(m.Groups[1].Value, out int id))
                {
                    if (id > detectedMax) detectedMax = id;
                }
            }

            if (detectedMax > 0)
            {
                maxActionId = detectedMax;
                Debug.Log($"[DogCompanion] Auto-detected action range: 动作1~动作{maxActionId}");
            }
            else
            {
                maxActionId = Mathf.Max(1, maxActionId);
                Debug.Log($"[DogCompanion] Action range fallback: 动作1~动作{maxActionId} (no ActionX clips detected)");
            }
        }

        private Vector3 CalculateTargetPosition()
        {
            // Use the player's actual movement direction to determine "behind"
            Vector3 moveDelta = target.position - _lastTargetPosition;
            moveDelta.y = 0f; // ignore vertical movement

            // If the player is moving, smoothly update the movement direction
            if (moveDelta.sqrMagnitude > 0.0001f)
            {
                _smoothedMoveDirection = Vector3.Lerp(
                    _smoothedMoveDirection,
                    moveDelta.normalized,
                    Time.deltaTime * 5f
                );
            }

            // Use movement direction when available, otherwise fall back to player's facing
            Vector3 forward = _smoothedMoveDirection.sqrMagnitude > 0.01f
                ? _smoothedMoveDirection.normalized
                : target.forward;

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            // forwardOffset is positive; negate forward to place the dog *behind* the direction of travel
            Vector3 offset = -forward * forwardOffset + right * sideOffset;
            Vector3 targetPos = target.position + offset;

            // Sample position on NavMesh
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return targetPos;
        }

        private void UpdateAnimation(float distanceToTarget, bool shouldRun)
        {
            if (_animator == null) return;

            // Use both velocity and distance to determine animation state
            // _agent.velocity can report near-zero even when the dog is actively navigating
            // so we also check desiredVelocity and distance to target
            float velocity = _agent.velocity.magnitude;
            float desiredVelocity = _agent.desiredVelocity.magnitude;
            float effectiveSpeed = Mathf.Max(velocity, desiredVelocity);

            int animId;
            if (distanceToTarget <= stoppingDistance || effectiveSpeed < 0.05f)
            {
                animId = idleAnimId;     // 0 = Breathing (idle)
            }
            else if (shouldRun)
            {
                animId = runAnimId;      // 4 = Running
            }
            else
            {
                animId = walkAnimId;     // 2 = Walking
            }

            // Only set the parameter when the animation actually changes
            if (animId != _currentAnimId)
            {
                _currentAnimId = animId;
                _animator.SetInteger(AnimationIDHash, animId);
                Debug.Log($"[DogCompanion] Animation → ID={animId} (vel={velocity:F2}, desiredVel={desiredVelocity:F2}, dist={distanceToTarget:F2}, run={shouldRun})");
            }
        }

        /// <summary>
        /// Set a new target for the dog to follow
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void OnDrawGizmosSelected()
        {
            if (target == null) return;

            // Draw target position
            Gizmos.color = Color.green;
            Vector3 targetPos = target.position + target.forward * forwardOffset + target.right * sideOffset;
            Gizmos.DrawWireSphere(targetPos, 0.3f);

            // Draw line to target
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, targetPos);
        }
    }
}
