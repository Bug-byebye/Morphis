using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Morphis.Motion
{
    [Serializable]
    public class MDMGenerateRequestPayload
    {
        public string text_prompt;
        public int num_samples = 1;
        public int num_repetitions = 1;
        public float motion_length = 6f;
        public int seed = 10;
    }

    /// <summary>
    /// Runtime UI for generating motion from MDM Flask server and playing it on player avatar.
    /// </summary>
    public class MDMMotionGenerateUI : MonoBehaviour
    {
        [Header("Server")]
        [SerializeField] private string generateUrl = "http://127.0.0.1:5000/generate";
        [SerializeField] private int requestTimeoutSeconds = 180;

        [Header("Generation")]
        [SerializeField] private int numSamples = 1;
        [SerializeField] private int numRepetitions = 1;
        [SerializeField] private float motionLength = 6f;
        [SerializeField] private int seed = 10;
        [SerializeField] private int sampleIndex = 0;
        [SerializeField] private float fps = 20f;

        [Header("Local Conversion")]
        [Tooltip("Optional explicit Python executable path for npy->json conversion.")]
        [SerializeField] private string pythonExecutableOverride = "";

        [Header("UI")]
        [SerializeField] private bool expandedByDefault = true;
        [SerializeField] private string defaultPrompt = "the person walked forward and is picking up his toolbox.";

        private GameObject rootPanel;
        private TMP_InputField promptInput;
        private Button generateButton;
        private Button toggleButton;
        private TextMeshProUGUI statusText;
        private bool isBusy;

        private void Awake()
        {
            EnsureEventSystem();
            CreateUI();
            SetExpanded(expandedByDefault);
            SetStatus("Ready");
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private void CreateUI()
        {
            var canvasObj = new GameObject("MDMMotionUICanvas");
            canvasObj.transform.SetParent(transform, false);

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 260;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // Toggle button
            var toggleObj = new GameObject("MDMToggleButton");
            toggleObj.transform.SetParent(canvasObj.transform, false);
            var toggleRect = toggleObj.AddComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0f, 0.5f);
            toggleRect.anchorMax = new Vector2(0f, 0.5f);
            toggleRect.pivot = new Vector2(0f, 0.5f);
            toggleRect.sizeDelta = new Vector2(160f, 48f);
            toggleRect.anchoredPosition = new Vector2(20f, 220f);

            var toggleImg = toggleObj.AddComponent<Image>();
            toggleImg.color = new Color(0.22f, 0.45f, 0.8f, 0.95f);
            toggleButton = toggleObj.AddComponent<Button>();
            toggleButton.onClick.AddListener(() => SetExpanded(rootPanel == null || !rootPanel.activeSelf));
            var toggleLabel = CreateTextChild(toggleObj, "Label", "MDM Motion");
            toggleLabel.alignment = TextAlignmentOptions.Center;
            toggleLabel.fontSize = 20;

            // Main panel
            rootPanel = new GameObject("MDMPanel");
            rootPanel.transform.SetParent(canvasObj.transform, false);
            var panelRect = rootPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.sizeDelta = new Vector2(500f, 280f);
            panelRect.anchoredPosition = new Vector2(20f, -20f);

            rootPanel.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.13f, 0.94f);
            var outline = rootPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.35f);
            outline.effectDistance = new Vector2(2f, 2f);

            var title = CreateTextChild(rootPanel, "Title", "MDM Text-to-Motion");
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -12f);
            titleRect.sizeDelta = new Vector2(-24f, 32f);
            title.alignment = TextAlignmentOptions.Left;
            title.fontSize = 24;

            var inputGO = new GameObject("PromptInput");
            inputGO.transform.SetParent(rootPanel.transform, false);
            var inputRect = inputGO.AddComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 1f);
            inputRect.anchorMax = new Vector2(1f, 1f);
            inputRect.pivot = new Vector2(0.5f, 1f);
            inputRect.anchoredPosition = new Vector2(0f, -54f);
            inputRect.sizeDelta = new Vector2(-24f, 130f);
            inputGO.AddComponent<Image>().color = new Color(0.14f, 0.16f, 0.2f, 1f);
            promptInput = inputGO.AddComponent<TMP_InputField>();
            promptInput.lineType = TMP_InputField.LineType.MultiLineNewline;

            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(inputGO.transform, false);
            var textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10f, 10f);
            textAreaRect.offsetMax = new Vector2(-10f, -10f);
            textArea.AddComponent<RectMask2D>();

            var inputText = CreateTextChild(textArea, "Text", defaultPrompt);
            inputText.alignment = TextAlignmentOptions.TopLeft;
            inputText.fontSize = 18;
            inputText.color = Color.white;
            promptInput.textComponent = inputText;
            promptInput.textViewport = textAreaRect;
            promptInput.text = defaultPrompt;

            var placeholder = CreateTextChild(textArea, "Placeholder", "Describe what you want the character to do...");
            placeholder.alignment = TextAlignmentOptions.TopLeft;
            placeholder.fontSize = 18;
            placeholder.color = new Color(0.65f, 0.65f, 0.65f);
            placeholder.fontStyle = FontStyles.Italic;
            promptInput.placeholder = placeholder;

            var btnGO = new GameObject("GenerateButton");
            btnGO.transform.SetParent(rootPanel.transform, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0f, 0f);
            btnRect.anchorMax = new Vector2(0f, 0f);
            btnRect.pivot = new Vector2(0f, 0f);
            btnRect.sizeDelta = new Vector2(140f, 42f);
            btnRect.anchoredPosition = new Vector2(12f, 48f);
            btnGO.AddComponent<Image>().color = new Color(0.2f, 0.7f, 0.4f, 1f);
            generateButton = btnGO.AddComponent<Button>();
            generateButton.onClick.AddListener(OnClickGenerate);
            var btnText = CreateTextChild(btnGO, "Label", "Generate");
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.fontSize = 20;

            statusText = CreateTextChild(rootPanel, "Status", "Ready");
            var statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 8f);
            statusRect.sizeDelta = new Vector2(-24f, 34f);
            statusText.alignment = TextAlignmentOptions.Left;
            statusText.fontSize = 16;
            statusText.color = new Color(0.83f, 0.9f, 0.95f, 1f);
        }

        private void SetExpanded(bool expanded)
        {
            if (rootPanel != null) rootPanel.SetActive(expanded);
        }

        private void OnClickGenerate()
        {
            if (isBusy) return;
            string prompt = promptInput == null ? string.Empty : promptInput.text.Trim();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                SetStatus("Prompt is empty.");
                return;
            }
            StartCoroutine(GenerateAndPlayCoroutine(prompt));
        }

        private System.Collections.IEnumerator GenerateAndPlayCoroutine(string prompt)
        {
            isBusy = true;
            if (generateButton != null) generateButton.interactable = false;
            SetStatus("Requesting server...");

            var payload = new MDMGenerateRequestPayload
            {
                text_prompt = prompt,
                num_samples = Mathf.Max(1, numSamples),
                num_repetitions = Mathf.Max(1, numRepetitions),
                motion_length = Mathf.Max(0.5f, motionLength),
                seed = seed
            };

            byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using (var req = new UnityWebRequest(generateUrl, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = Mathf.Max(15, requestTimeoutSeconds);

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    SetStatus("Request failed: " + req.error);
                    EndBusy();
                    yield break;
                }

                byte[] npyBytes = req.downloadHandler.data;
                if (npyBytes == null || npyBytes.Length == 0)
                {
                    SetStatus("Server returned empty .npy");
                    EndBusy();
                    yield break;
                }

                string motionDir = Path.Combine(Application.persistentDataPath, "mdm_motion");
                Directory.CreateDirectory(motionDir);
                string npyPath = Path.Combine(motionDir, "result.npy");
                string jsonPath = Path.Combine(motionDir, "result_motion.json");
                File.WriteAllBytes(npyPath, npyBytes);

                SetStatus("Converting npy...");
                if (!ConvertNpyToJson(npyPath, jsonPath, out string convertError))
                {
                    SetStatus("Convert failed: " + convertError);
                    EndBusy();
                    yield break;
                }

                var retargeter = FindOrCreateRetargeter();
                if (retargeter == null)
                {
                    SetStatus("No humanoid animator found.");
                    EndBusy();
                    yield break;
                }

                bool played = retargeter.LoadAndPlayFromJsonFile(jsonPath);
                SetStatus(played ? "Playing generated motion." : "Loaded file but failed to play.");
            }

            EndBusy();
        }

        private void EndBusy()
        {
            isBusy = false;
            if (generateButton != null) generateButton.interactable = true;
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
            UnityEngine.Debug.Log("[MDM UI] " + msg);
        }

        private MDMMotionRetargeter FindOrCreateRetargeter()
        {
            var existing = FindFirstObjectByType<MDMMotionRetargeter>();
            if (existing != null) return existing;

            Animator animator = FindTargetAnimator();
            if (animator == null) return null;
            return animator.gameObject.AddComponent<MDMMotionRetargeter>();
        }

        private Animator FindTargetAnimator()
        {
            GameObject go = GameObject.Find("PlayerArmature_Network");
            if (go == null) go = GameObject.Find("PlayerArmature");

            if (go != null)
            {
                var anim = go.GetComponent<Animator>();
                if (anim == null) anim = go.GetComponentInChildren<Animator>();
                if (anim != null && anim.avatar != null && anim.avatar.isHuman) return anim;
            }

            var all = FindObjectsByType<Animator>(FindObjectsSortMode.None);
            foreach (var anim in all)
            {
                if (anim == null || anim.avatar == null || !anim.avatar.isHuman) continue;
                if (anim.gameObject.name.IndexOf("Dog", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                return anim;
            }

            return null;
        }

        private bool ConvertNpyToJson(string npyPath, string jsonPath, out string error)
        {
            error = null;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string scriptPath = Path.Combine(projectRoot, "Backend", "tools", "convert_mdm_result_to_json.py");
            if (!File.Exists(scriptPath))
            {
                error = "converter script not found: " + scriptPath;
                return false;
            }

            string args = $"\"{scriptPath}\" --input \"{npyPath}\" --output \"{jsonPath}\" --sample-index {Mathf.Max(0, sampleIndex)} --fps {Mathf.Max(1f, fps).ToString(System.Globalization.CultureInfo.InvariantCulture)}";

            List<string> failReasons = new List<string>();
            HashSet<string> visitedCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string pythonExe in GetPythonCandidates(projectRoot))
            {
                if (string.IsNullOrWhiteSpace(pythonExe)) continue;
                if (!visitedCandidates.Add(pythonExe)) continue;

                if (!CanImportNumpy(pythonExe, out string numpyCheckError))
                {
                    failReasons.Add($"{pythonExe}: {numpyCheckError}");
                    continue;
                }

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = pythonExe,
                        Arguments = args,
                        WorkingDirectory = projectRoot,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(psi))
                    {
                        if (process == null)
                        {
                            failReasons.Add($"{pythonExe}: failed to start process");
                            continue;
                        }

                        string stdout = process.StandardOutput.ReadToEnd();
                        string stderr = process.StandardError.ReadToEnd();
                        process.WaitForExit();

                        if (process.ExitCode != 0)
                        {
                            string msg = string.IsNullOrWhiteSpace(stderr) ? $"exit {process.ExitCode}" : stderr.Trim();
                            failReasons.Add($"{pythonExe}: {msg}");
                            continue;
                        }

                        if (!File.Exists(jsonPath))
                        {
                            failReasons.Add($"{pythonExe}: json output not generated");
                            continue;
                        }

                        UnityEngine.Debug.Log("[MDM UI] Using python: " + pythonExe);
                        if (!string.IsNullOrWhiteSpace(stdout)) UnityEngine.Debug.Log("[MDM UI] " + stdout.Trim());

                        return true;
                    }
                }
                catch (Exception e)
                {
                    failReasons.Add($"{pythonExe}: {e.Message}");
                }
            }

            error = "No usable Python with numpy found. " + string.Join(" | ", failReasons);
            return false;
        }

        private IEnumerable<string> GetPythonCandidates(string projectRoot)
        {
            if (!string.IsNullOrWhiteSpace(pythonExecutableOverride)) yield return pythonExecutableOverride;

            string env = Environment.GetEnvironmentVariable("MDM_PYTHON");
            if (!string.IsNullOrWhiteSpace(env)) yield return env;

            string venvPython3 = Path.Combine(projectRoot, ".venv", "bin", "python3");
            string venvPython = Path.Combine(projectRoot, ".venv", "bin", "python");
            if (File.Exists(venvPython3)) yield return venvPython3;
            if (File.Exists(venvPython)) yield return venvPython;

            string anacondaPython = "/opt/anaconda3/bin/python3";
            if (File.Exists(anacondaPython)) yield return anacondaPython;

            string localPython = "/usr/local/bin/python3";
            if (File.Exists(localPython)) yield return localPython;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            yield return "python";
            yield return "python3";
#else
            yield return "python3";
            yield return "python";
#endif
        }

        private bool CanImportNumpy(string pythonExe, out string error)
        {
            error = null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = "-c \"import numpy; print(numpy.__version__)\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        error = "cannot start";
                        return false;
                    }

                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        error = string.IsNullOrWhiteSpace(stderr) ? "numpy check failed" : stderr.Trim();
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(stdout))
                    {
                        error = "numpy not available";
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }

            return true;
        }

        private TextMeshProUGUI CreateTextChild(GameObject parent, string name, string text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.color = Color.white;
            return tmp;
        }
    }
}
