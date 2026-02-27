using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Morphis.AppFlow;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// MainScene 右上角「保存」按钮：点击后立即将当前场景数据保存到数据库。
    /// </summary>
    public class WorldSaveButton : MonoBehaviour
    {
        private const string MainSceneName = "MainScene";
        private Button _saveBtn;
        private TMP_Text _statusText;
        private bool _saving;

        private void Start()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, MainSceneName, System.StringComparison.OrdinalIgnoreCase))
                return;
            BuildSaveUI();
        }

        private void BuildSaveUI()
        {
            var canvasGO = new GameObject("WorldSaveButtonCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            var root = new GameObject("SavePanel");
            root.transform.SetParent(canvasGO.transform, false);
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.anchorMin = new Vector2(1f, 1f);
            rootRT.anchorMax = new Vector2(1f, 1f);
            rootRT.pivot = new Vector2(1f, 1f);
            rootRT.anchoredPosition = new Vector2(-20f, -20f);
            rootRT.sizeDelta = new Vector2(140f, 80f);

            var btnGO = new GameObject("SaveButton");
            btnGO.transform.SetParent(root.transform, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(1f, 1f);
            btnRT.anchorMax = new Vector2(1f, 1f);
            btnRT.pivot = new Vector2(1f, 1f);
            btnRT.anchoredPosition = Vector2.zero;
            btnRT.sizeDelta = new Vector2(120f, 44f);

            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.25f, 0.55f, 0.85f, 1f);

            _saveBtn = btnGO.AddComponent<Button>();

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "保存";
            tmp.fontSize = 22;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            var statusGO = new GameObject("Status");
            statusGO.transform.SetParent(root.transform, false);
            var statusRT = statusGO.AddComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(1f, 1f);
            statusRT.anchorMax = new Vector2(1f, 1f);
            statusRT.pivot = new Vector2(1f, 1f);
            statusRT.anchoredPosition = new Vector2(0f, -52f);
            statusRT.sizeDelta = new Vector2(200f, 24f);
            _statusText = statusGO.AddComponent<TextMeshProUGUI>();
            _statusText.text = "";
            _statusText.fontSize = 14;
            _statusText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            _statusText.alignment = TextAlignmentOptions.TopRight;
            _statusText.raycastTarget = false;

            _saveBtn.onClick.AddListener(OnSaveClicked);
        }

        private void OnSaveClicked()
        {
            if (_saving) return;

            if (!AppSession.IsLoggedIn)
            {
                SetStatus("Please log in first");
                return;
            }

            if (WorldSnapshotManager.Instance == null)
            {
                SetStatus("Manager not ready");
                return;
            }

            _saving = true;
            SetStatus("Saving...");
            _saveBtn.interactable = false;

            WorldSnapshotManager.Instance.SaveWorldServer(
                worldId: null,
                onSuccess: () =>
                {
                    SetStatus("Saved!");
                    _saving = false;
                    _saveBtn.interactable = true;
                    StartCoroutine(ClearStatusAfter(2f));
                },
                onError: err =>
                {
                    SetStatus(string.IsNullOrEmpty(err) ? "Save failed" : err);
                    _saving = false;
                    _saveBtn.interactable = true;
                    StartCoroutine(ClearStatusAfter(3f));
                });
        }

        private void SetStatus(string msg)
        {
            if (_statusText != null)
                _statusText.text = msg;
        }

        private IEnumerator ClearStatusAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_statusText != null)
                _statusText.text = "";
        }
    }
}
