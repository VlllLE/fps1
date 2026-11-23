using UnityEngine;

namespace CodMovementShooter.UI
{
    /// <summary>
    /// Four-arm crosshair that expands while firing or moving quickly.
    /// </summary>
    public sealed class CrosshairView : MonoBehaviour
    {
        [Header("Arms")]
        [SerializeField] private RectTransform top;
        [SerializeField] private RectTransform bottom;
        [SerializeField] private RectTransform left;
        [SerializeField] private RectTransform right;

        [Header("Layout")]
        [SerializeField] private float baseGap = 6f;
        [SerializeField] private float maxExtraGap = 18f;
        [SerializeField] private float armLength = 14f;
        [SerializeField] private float armThickness = 2f;

        [Header("Dynamics")]
        [SerializeField] private float fireImpulse = 0.55f;
        [SerializeField] private float movementSpeedForFullExpand = 12f;
        [SerializeField] private float expandLerp = 14f;
        [SerializeField] private float recoverLerp = 8f;

        private float _fireExpand;
        private float _movementExpand;
        private float _currentExtra;

        public void BindArms(RectTransform topArm, RectTransform bottomArm, RectTransform leftArm, RectTransform rightArm)
        {
            top = topArm;
            bottom = bottomArm;
            left = leftArm;
            right = rightArm;
            ApplyGap(baseGap);
        }

        public void NotifyFired()
        {
            _fireExpand = Mathf.Clamp01(_fireExpand + fireImpulse);
        }

        public void SetMovementSpeed(float horizontalSpeed)
        {
            float denom = Mathf.Max(0.01f, movementSpeedForFullExpand);
            _movementExpand = Mathf.Clamp01(horizontalSpeed / denom);
        }

        private void LateUpdate()
        {
            float target = Mathf.Clamp01(Mathf.Max(_fireExpand, _movementExpand * 0.85f));
            float lerp = target > _currentExtra ? expandLerp : recoverLerp;
            _currentExtra = Mathf.MoveTowards(_currentExtra, target, lerp * Time.deltaTime);
            _fireExpand = Mathf.MoveTowards(_fireExpand, 0f, recoverLerp * Time.deltaTime);

            ApplyGap(baseGap + _currentExtra * maxExtraGap);
        }

        private void ApplyGap(float gap)
        {
            if (top != null)
            {
                top.sizeDelta = new Vector2(armThickness, armLength);
                top.anchoredPosition = new Vector2(0f, gap + armLength * 0.5f);
            }

            if (bottom != null)
            {
                bottom.sizeDelta = new Vector2(armThickness, armLength);
                bottom.anchoredPosition = new Vector2(0f, -(gap + armLength * 0.5f));
            }

            if (left != null)
            {
                left.sizeDelta = new Vector2(armLength, armThickness);
                left.anchoredPosition = new Vector2(-(gap + armLength * 0.5f), 0f);
            }

            if (right != null)
            {
                right.sizeDelta = new Vector2(armLength, armThickness);
                right.anchoredPosition = new Vector2(gap + armLength * 0.5f, 0f);
            }
        }
    }
}
