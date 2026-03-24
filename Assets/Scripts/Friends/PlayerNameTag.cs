using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StarterAssets;

namespace Morphis.Friends
{
    /// <summary>
    /// World-space player name tag that always faces the active camera.
    /// </summary>
    public class PlayerNameTag : MonoBehaviour
    {
        private static readonly Vector3 WorldOffset = new Vector3(0f, 0.6f, 0f);

        private NetworkPlayerSetup _owner;
        private Transform _anchor;
        private RectTransform _canvasRect;
        private TextMeshProUGUI _label;

        public void Bind(NetworkPlayerSetup owner)
        {
            _owner = owner;
            _anchor = owner != null ? owner.GetNameTagAnchor() : transform;
            EnsureVisuals();
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (_label == null)
            {
                EnsureVisuals();
            }

            if (_label == null)
            {
                return;
            }

            _label.text = _owner != null ? _owner.DisplayName : "玩家";
            _label.color = _owner != null && _owner.isLocalPlayer
                ? new Color(1f, 0.95f, 0.75f, 1f)
                : Color.white;
        }

        private void LateUpdate()
        {
            if (_canvasRect == null)
            {
                return;
            }

            if (_owner == null)
            {
                Destroy(this);
                return;
            }

            if (_anchor == null)
            {
                _anchor = _owner.GetNameTagAnchor();
            }

            var anchor = _anchor != null ? _anchor : transform;
            _canvasRect.position = anchor.position + WorldOffset;

            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            var lookDirection = _canvasRect.position - cam.transform.position;
            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _canvasRect.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        private void EnsureVisuals()
        {
            if (_canvasRect != null && _label != null)
            {
                return;
            }

            var existing = transform.Find("PlayerNameTagCanvas");
            if (existing != null)
            {
                _canvasRect = existing as RectTransform;
                _label = existing.GetComponentInChildren<TextMeshProUGUI>(true);
                return;
            }

            var canvasObj = new GameObject("PlayerNameTagCanvas");
            canvasObj.transform.SetParent(transform, false);

            _canvasRect = canvasObj.AddComponent<RectTransform>();
            _canvasRect.sizeDelta = new Vector2(240f, 56f);
            _canvasRect.localScale = Vector3.one * 0.01f;

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 60;

            canvasObj.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 16f;
            canvasObj.AddComponent<GraphicRaycaster>().enabled = false;

            var backgroundObj = new GameObject("Background");
            backgroundObj.transform.SetParent(canvasObj.transform, false);
            var bgRect = backgroundObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var background = backgroundObj.AddComponent<Image>();
            background.color = new Color(0.05f, 0.07f, 0.11f, 0.72f);

            var textObj = new GameObject("Label");
            textObj.transform.SetParent(backgroundObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 6f);
            textRect.offsetMax = new Vector2(-12f, -6f);

            _label = textObj.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 26f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.enableWordWrapping = false;
            _label.color = Color.white;
            _label.text = "玩家";
        }
    }
}
