using UnityEngine;

namespace Morphis.Motion
{
    /// <summary>
    /// Auto-attaches MDMMotionRetargeter to the current player avatar at runtime.
    /// </summary>
    public static class MDMMotionBootstrap
    {
        private static bool created;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRunner()
        {
            if (created) return;

            if (Object.FindFirstObjectByType<MDMMotionRetargeter>() != null)
            {
                created = true;
                return;
            }

            var go = new GameObject("MDMMotionAttachRunner");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<MDMMotionAttachRunner>();
            created = true;
        }
    }

    public class MDMMotionAttachRunner : MonoBehaviour
    {
        [SerializeField] private float retryIntervalSeconds = 0.8f;
        private float timer;

        private void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = retryIntervalSeconds;

            var target = FindTargetAnimator();
            if (target == null) return;

            if (target.GetComponent<MDMMotionRetargeter>() == null)
            {
                target.gameObject.AddComponent<MDMMotionRetargeter>();
                Debug.Log("[MDM Retarget] Attached to " + target.gameObject.name);
            }

            Destroy(gameObject);
        }

        private Animator FindTargetAnimator()
        {
            var go = GameObject.Find("PlayerArmature_Network");
            if (go == null) go = GameObject.Find("PlayerArmature");

            if (go != null)
            {
                var animator = go.GetComponent<Animator>();
                if (animator == null) animator = go.GetComponentInChildren<Animator>();
                if (animator != null) return animator;
            }

            var animators = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
            foreach (var animator in animators)
            {
                if (animator == null || animator.avatar == null) continue;
                if (!animator.avatar.isHuman) continue;
                if (animator.gameObject.name.Contains("Dog")) continue;
                return animator;
            }

            return null;
        }
    }
}
