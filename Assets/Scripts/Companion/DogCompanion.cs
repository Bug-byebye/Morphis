using UnityEngine;
using UnityEngine.AI;

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
        [Tooltip("How far behind the player the dog should stay (negative = behind)")]
        [SerializeField] private float forwardOffset = -3f;
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
        [SerializeField] private int walkAnimId = 1;
        [SerializeField] private int runAnimId = 3;

        [Header("Chat")]
        [SerializeField] private string dogName = "Buddy";

        private NavMeshAgent _agent;
        private Animator _animator;
        private Vector3 _lastTargetPosition;
        private DogChatUI _chatUI;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();
            
            // Animator might be on child object (common for imported 3D models)
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
            
            if (_animator != null)
            {
                // Disable root motion so NavMeshAgent controls movement
                _animator.applyRootMotion = false;
            }

            // Create chat UI
            _chatUI = gameObject.AddComponent<DogChatUI>();

            // Ensure collider exists for click detection
            EnsureCollider();

            // Prevent dog from pushing the player
            _agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        private void EnsureCollider()
        {
            if (GetComponent<Collider>() == null)
            {
                // Try to find collider in children
                var childCollider = GetComponentInChildren<Collider>();
                if (childCollider == null)
                {
                    // Add capsule collider if none exists
                    var capsule = gameObject.AddComponent<CapsuleCollider>();
                    capsule.center = new Vector3(0, 0.5f, 0);
                    capsule.radius = 0.5f;
                    capsule.height = 1f;
                    Debug.Log("[DogCompanion] Added CapsuleCollider for click detection");
                }
            }
        }

        /// <summary>
        /// Called when the dog is clicked (requires collider)
        /// </summary>
        private void OnMouseDown()
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
            // Continuously try to find player if not assigned (for networked games)
            if (target == null)
            {
                TryFindPlayer();
                if (target == null) return; // Still no player, wait
                
                // Player just found - initialize
                _lastTargetPosition = target.position;
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
            if (distanceToTarget > stoppingDistance && _agent.isActiveAndEnabled)
            {
                _agent.SetDestination(targetPos);
            }

            // Update animation based on agent velocity
            UpdateAnimation();

            _lastTargetPosition = target.position;
        }

        private Vector3 CalculateTargetPosition()
        {
            // Position ahead of player in their forward direction
            Vector3 forward = target.forward;
            Vector3 right = target.right;

            Vector3 offset = forward * forwardOffset + right * sideOffset;
            Vector3 targetPos = target.position + offset;

            // Sample position on NavMesh
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return targetPos;
        }

        private void UpdateAnimation()
        {
            if (_animator == null) return;

            float speed = _agent.velocity.magnitude;

            int animId;
            if (speed < 0.1f)
            {
                animId = idleAnimId;
            }
            else if (speed < walkSpeed * 1.5f)
            {
                animId = walkAnimId;
            }
            else
            {
                animId = runAnimId;
            }

            _animator.SetInteger("AnimationID", animId);
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
