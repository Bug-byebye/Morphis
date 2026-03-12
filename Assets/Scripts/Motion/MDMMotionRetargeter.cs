using System;
using System.IO;
using UnityEngine;

namespace Morphis.Motion
{
    [Serializable]
    public class MDMotionClipData
    {
        public int version;
        public string source;
        public int sampleIndex;
        public int frames;
        public int joints;
        public float fps;
        public int length;
        public string text;
        public float[] positions;
    }

    /// <summary>
    /// Retargets MDM joint positions (22-joint format) to a Unity Humanoid avatar at runtime.
    /// Input JSON is expected from Backend/tools/convert_mdm_result_to_json.py.
    /// </summary>
    public class MDMMotionRetargeter : MonoBehaviour
    {
        [Header("Motion Input")]
        [SerializeField] private string motionResourcePath = "Motions/result_motion";
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private bool loadDefaultMotionOnStart = false;
        [SerializeField] private bool playOnStart = true;

        [Header("Playback")]
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playGeneratedAsOneShot = true;
        [SerializeField] private float playbackSpeed = 1f;
        [SerializeField] private bool disableAnimatorWhilePlaying = true;

        [Header("Retarget Tuning")]
        [SerializeField] private Vector3 motionAxisScale = Vector3.one;
        [SerializeField] private bool lockRootY = true;
        [SerializeField] private float rootScaleMultiplier = 1f;
        [SerializeField] private bool logInfo = true;

        private static readonly HumanBodyBones[] JointToHumanoidBone = new HumanBodyBones[]
        {
            HumanBodyBones.Hips,           // 0
            HumanBodyBones.LeftUpperLeg,   // 1
            HumanBodyBones.RightUpperLeg,  // 2
            HumanBodyBones.Spine,          // 3
            HumanBodyBones.LeftLowerLeg,   // 4
            HumanBodyBones.RightLowerLeg,  // 5
            HumanBodyBones.Chest,          // 6
            HumanBodyBones.LeftFoot,       // 7
            HumanBodyBones.RightFoot,      // 8
            HumanBodyBones.UpperChest,     // 9 (fallback to Chest if missing)
            HumanBodyBones.LeftToes,       // 10
            HumanBodyBones.RightToes,      // 11
            HumanBodyBones.Neck,           // 12
            HumanBodyBones.LeftShoulder,   // 13
            HumanBodyBones.RightShoulder,  // 14
            HumanBodyBones.Head,           // 15
            HumanBodyBones.LeftUpperArm,   // 16
            HumanBodyBones.RightUpperArm,  // 17
            HumanBodyBones.LeftLowerArm,   // 18
            HumanBodyBones.RightLowerArm,  // 19
            HumanBodyBones.LeftHand,       // 20
            HumanBodyBones.RightHand       // 21
        };

        // Child used to build orientation vector for each joint.
        private static readonly int[] JointChild = new int[]
        {
            3, 4, 5, 6, 7, 8, 9, 10, 11, 12, -1, -1, 15, 16, 17, -1, 18, 19, 20, 21, -1, -1
        };

        private MDMotionClipData motionData;
        private Transform[] jointTransforms;
        private Quaternion[] bindWorldRotations;
        private Vector3[] bindWorldDirections;

        private bool isPlaying;
        private float frameCursor;
        private Vector3 rootStartPosition;
        private Vector3 motionFirstHip;
        private float motionToAvatarScale = 1f;

        private void Awake()
        {
            if (targetAnimator == null)
            {
                targetAnimator = GetComponentInChildren<Animator>();
            }
        }

        private void Start()
        {
            if (targetAnimator == null)
            {
                Debug.LogWarning("[MDM Retarget] Missing Animator, abort.");
                enabled = false;
                return;
            }

            if (loadDefaultMotionOnStart && LoadMotionFromResources())
            {
                PrepareAfterMotionLoaded();
                if (playOnStart)
                {
                    Play();
                }
            }
        }

        private void Update()
        {
            if (!isPlaying || motionData == null) return;

            float fps = motionData.fps > 0.01f ? motionData.fps : 20f;
            frameCursor += Time.deltaTime * fps * Mathf.Max(0.01f, playbackSpeed);

            int maxFrame = Mathf.Max(1, motionData.frames);
            int frameIndex;

            if (loop)
            {
                frameIndex = Mathf.FloorToInt(frameCursor) % maxFrame;
            }
            else
            {
                frameIndex = Mathf.Clamp(Mathf.FloorToInt(frameCursor), 0, maxFrame - 1);
                if (frameIndex >= maxFrame - 1)
                {
                    ApplyFrame(frameIndex);
                    Stop();
                    return;
                }
            }

            ApplyFrame(frameIndex);
        }

        private void OnDisable()
        {
            if (targetAnimator != null && disableAnimatorWhilePlaying)
            {
                targetAnimator.enabled = true;
            }
        }

        public void Play()
        {
            if (motionData == null) return;

            frameCursor = 0f;
            isPlaying = true;
            rootStartPosition = targetAnimator.transform.position;

            if (disableAnimatorWhilePlaying)
            {
                targetAnimator.enabled = false;
            }
        }

        public void Stop()
        {
            isPlaying = false;
            if (targetAnimator != null && disableAnimatorWhilePlaying)
            {
                targetAnimator.enabled = true;
            }
        }

        public bool LoadAndPlayFromJsonFile(string jsonFilePath)
        {
            if (!LoadMotionFromJsonFile(jsonFilePath))
            {
                return false;
            }

            PrepareAfterMotionLoaded();
            if (playGeneratedAsOneShot) loop = false;
            Play();
            return true;
        }

        public bool LoadMotionFromJsonFile(string jsonFilePath)
        {
            if (string.IsNullOrWhiteSpace(jsonFilePath))
            {
                Debug.LogWarning("[MDM Retarget] JSON file path is empty.");
                return false;
            }

            if (!File.Exists(jsonFilePath))
            {
                Debug.LogWarning($"[MDM Retarget] JSON file not found: {jsonFilePath}");
                return false;
            }

            try
            {
                string json = File.ReadAllText(jsonFilePath);
                motionData = JsonUtility.FromJson<MDMotionClipData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MDM Retarget] Failed to read JSON file: {e.Message}");
                return false;
            }

            return ValidateLoadedMotion();
        }

        private bool LoadMotionFromResources()
        {
            TextAsset asset = Resources.Load<TextAsset>(motionResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[MDM Retarget] Motion JSON not found at Resources/{motionResourcePath}");
                return false;
            }

            motionData = JsonUtility.FromJson<MDMotionClipData>(asset.text);
            return ValidateLoadedMotion();
        }

        private void PrepareAfterMotionLoaded()
        {
            CacheRigBindPose();
            EstimateScaleFromLegLength();
        }

        private bool ValidateLoadedMotion()
        {
            if (motionData == null || motionData.positions == null || motionData.positions.Length == 0)
            {
                Debug.LogWarning("[MDM Retarget] Motion JSON parse failed or empty.");
                return false;
            }

            if (motionData.joints != 22)
            {
                Debug.LogWarning($"[MDM Retarget] Expected 22 joints, got {motionData.joints}. Will still try.");
            }

            motionFirstHip = ConvertMotionVector(GetJointPosition(0, 0));

            if (logInfo)
            {
                Debug.Log($"[MDM Retarget] Loaded motion: frames={motionData.frames}, fps={motionData.fps}, text='{motionData.text}'");
            }

            return true;
        }

        private void CacheRigBindPose()
        {
            int jointCount = JointToHumanoidBone.Length;
            jointTransforms = new Transform[jointCount];
            bindWorldRotations = new Quaternion[jointCount];
            bindWorldDirections = new Vector3[jointCount];

            for (int i = 0; i < jointCount; i++)
            {
                jointTransforms[i] = ResolveHumanoidBone(i);
                if (jointTransforms[i] != null)
                {
                    bindWorldRotations[i] = jointTransforms[i].rotation;
                }
            }

            for (int i = 0; i < jointCount; i++)
            {
                int child = JointChild[i];
                if (child < 0) continue;
                if (jointTransforms[i] == null || jointTransforms[child] == null) continue;

                Vector3 dir = jointTransforms[child].position - jointTransforms[i].position;
                bindWorldDirections[i] = dir.sqrMagnitude > 0.000001f ? dir.normalized : Vector3.forward;
            }
        }

        private Transform ResolveHumanoidBone(int jointIndex)
        {
            HumanBodyBones bone = JointToHumanoidBone[jointIndex];
            if (bone == HumanBodyBones.UpperChest)
            {
                Transform upper = targetAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (upper != null) return upper;
                return targetAnimator.GetBoneTransform(HumanBodyBones.Chest);
            }

            return targetAnimator.GetBoneTransform(bone);
        }

        private void EstimateScaleFromLegLength()
        {
            if (motionData == null || motionData.frames < 1)
            {
                motionToAvatarScale = 1f;
                return;
            }

            Transform lUpper = targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform lLower = targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform lFoot = targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rUpper = targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            Transform rLower = targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            Transform rFoot = targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot);

            if (lUpper == null || lLower == null || lFoot == null || rUpper == null || rLower == null || rFoot == null)
            {
                motionToAvatarScale = 1f;
                return;
            }

            float avatarLeft = Vector3.Distance(lUpper.position, lLower.position) + Vector3.Distance(lLower.position, lFoot.position);
            float avatarRight = Vector3.Distance(rUpper.position, rLower.position) + Vector3.Distance(rLower.position, rFoot.position);
            float avatarLeg = (avatarLeft + avatarRight) * 0.5f;

            Vector3 m1 = ConvertMotionVector(GetJointPosition(0, 1));
            Vector3 m4 = ConvertMotionVector(GetJointPosition(0, 4));
            Vector3 m7 = ConvertMotionVector(GetJointPosition(0, 7));
            Vector3 m2 = ConvertMotionVector(GetJointPosition(0, 2));
            Vector3 m5 = ConvertMotionVector(GetJointPosition(0, 5));
            Vector3 m8 = ConvertMotionVector(GetJointPosition(0, 8));
            float motionLeft = Vector3.Distance(m1, m4) + Vector3.Distance(m4, m7);
            float motionRight = Vector3.Distance(m2, m5) + Vector3.Distance(m5, m8);
            float motionLeg = (motionLeft + motionRight) * 0.5f;

            if (motionLeg > 0.0001f && avatarLeg > 0.0001f)
            {
                motionToAvatarScale = avatarLeg / motionLeg;
            }
            else
            {
                motionToAvatarScale = 1f;
            }
        }

        private void ApplyFrame(int frame)
        {
            if (motionData == null || jointTransforms == null) return;

            Vector3 frameHip = ConvertMotionVector(GetJointPosition(frame, 0));
            Vector3 delta = (frameHip - motionFirstHip) * (motionToAvatarScale * rootScaleMultiplier);
            if (lockRootY) delta.y = 0f;
            targetAnimator.transform.position = rootStartPosition + delta;

            for (int i = 0; i < jointTransforms.Length; i++)
            {
                Transform t = jointTransforms[i];
                int child = JointChild[i];
                if (t == null || child < 0 || child >= jointTransforms.Length) continue;

                Vector3 bindDir = bindWorldDirections[i];
                if (bindDir.sqrMagnitude < 0.000001f) continue;

                Vector3 p0 = ConvertMotionVector(GetJointPosition(frame, i));
                Vector3 p1 = ConvertMotionVector(GetJointPosition(frame, child));
                Vector3 targetDir = p1 - p0;
                if (targetDir.sqrMagnitude < 0.000001f) continue;

                Quaternion deltaRot = Quaternion.FromToRotation(bindDir, targetDir.normalized);
                t.rotation = deltaRot * bindWorldRotations[i];
            }
        }

        private Vector3 ConvertMotionVector(Vector3 v)
        {
            return new Vector3(v.x * motionAxisScale.x, v.y * motionAxisScale.y, v.z * motionAxisScale.z);
        }

        private Vector3 GetJointPosition(int frame, int joint)
        {
            int joints = Mathf.Max(1, motionData.joints);
            int frames = Mathf.Max(1, motionData.frames);
            frame = Mathf.Clamp(frame, 0, frames - 1);
            joint = Mathf.Clamp(joint, 0, joints - 1);

            int idx = (frame * joints + joint) * 3;
            if (idx + 2 >= motionData.positions.Length) return Vector3.zero;
            return new Vector3(
                motionData.positions[idx],
                motionData.positions[idx + 1],
                motionData.positions[idx + 2]
            );
        }
    }
}
