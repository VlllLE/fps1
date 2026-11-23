using UnityEngine;
using UnityEngine.UI;

namespace CodMovementShooter.UI
{
    /// <summary>
    /// Brief center flash confirming a successful hit.
    /// </summary>
    public sealed class HitMarkerView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Graphic[] tintTargets;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color headshotColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private float showDuration = 0.08f;
        [SerializeField] private float fadeDuration = 0.18f;

        private float _visibleUntil;
        private float _fadeOutAt;
        private bool _active;

        public void Bind(CanvasGroup canvasGroup, params Graphic[] graphics)
        {
            group = canvasGroup;
            tintTargets = graphics;
            HideImmediate();
        }

        public void Show(bool headshot = false)
        {
            Color color = headshot ? headshotColor : normalColor;
            if (tintTargets != null)
            {
                for (int i = 0; i < tintTargets.Length; i++)
                {
                    if (tintTargets[i] != null)
                        tintTargets[i].color = color;
                }
            }

            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            _visibleUntil = Time.unscaledTime + showDuration;
            _fadeOutAt = _visibleUntil + fadeDuration;
            _active = true;
        }

        private void Update()
        {
            if (!_active || group == null)
                return;

            float now = Time.unscaledTime;
            if (now <= _visibleUntil)
            {
                group.alpha = 1f;
                return;
            }

            if (now >= _fadeOutAt)
            {
                HideImmediate();
                return;
            }

            float t = (now - _visibleUntil) / Mathf.Max(0.0001f, fadeDuration);
            group.alpha = 1f - Mathf.Clamp01(t);
        }

        private void HideImmediate()
        {
            _active = false;
            if (group != null)
                group.alpha = 0f;
        }
    }
}
