using UnityEngine;

/// <summary>
/// Slides one named part of a gun model back and forth in response to WeaponController events:
/// the pistol slide on every shot (held open on an empty magazine) and the shotgun pump after each shot and
/// at the end of a reload. Purely cosmetic; if the part is missing (fallback primitives) nothing happens.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class WeaponPartAnimator : MonoBehaviour
{
    [SerializeField] private string partName = "Slide";
    [Tooltip("Local-space displacement of the part at full travel.")]
    [SerializeField] private Vector3 travel = new Vector3(0f, 0f, -0.024f);
    [SerializeField] private bool animateOnFire = true;
    [SerializeField] private bool animateOnReloadComplete;
    [Tooltip("Seconds between the shot and the start of the stroke (the shotgun cycles its pump after the shot).")]
    [SerializeField] private float delay;
    [SerializeField] private float backTime = 0.03f;
    [SerializeField] private float returnTime = 0.08f;
    [Tooltip("Keep the part at holdOpenFraction of its travel while the magazine is empty (slide lock).")]
    [SerializeField] private bool holdOpenWhenEmpty;
    [Range(0f, 1f)]
    [SerializeField] private float holdOpenFraction = 0.75f;

    private WeaponController controller;
    private Transform part;
    private Vector3 restLocalPosition;
    private float strokeStart = -1f;
    private int currentAmmo = -1;
    private bool subscribed;

    public Transform Part => part;
    public bool IsAnimating => strokeStart >= 0f;

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ResetPart();
    }

    private void Subscribe()
    {
        if (subscribed)
            return;
        controller = GetComponent<WeaponController>();
        if (controller == null)
            return;

        controller.OnWeaponFired += HandleFired;
        controller.OnReloadComplete += HandleReloadComplete;
        controller.OnAmmoChanged += HandleAmmoChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || controller == null)
            return;
        controller.OnWeaponFired -= HandleFired;
        controller.OnReloadComplete -= HandleReloadComplete;
        controller.OnAmmoChanged -= HandleAmmoChanged;
        subscribed = false;
    }

    private void HandleFired()
    {
        if (animateOnFire)
            BeginStroke();
    }

    private void HandleReloadComplete()
    {
        if (animateOnReloadComplete)
            BeginStroke();
    }

    private void HandleAmmoChanged(int magazine, int reserve)
    {
        currentAmmo = magazine;
        if (holdOpenWhenEmpty)
            ResolvePart();
    }

    /// <summary>
    /// Starts one stroke now (after the configured delay). Public so tests and cinematics can drive it.
    /// </summary>
    public void BeginStroke()
    {
        if (!ResolvePart())
            return;
        strokeStart = Time.time + delay;
    }

    private bool ResolvePart()
    {
        if (part != null)
            return true;

        part = FindDescendantByName(transform, partName);
        if (part == null)
            return false;
        restLocalPosition = part.localPosition;
        return true;
    }

    private void Update()
    {
        if (part == null)
            return;

        float offset = 0f;
        if (strokeStart >= 0f)
        {
            float t = Time.time - strokeStart;
            if (t < 0f)
                offset = 0f;
            else if (t < backTime)
                offset = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.0001f, backTime));
            else if (t < backTime + returnTime)
                offset = 1f - Mathf.SmoothStep(0f, 1f, (t - backTime) / Mathf.Max(0.0001f, returnTime));
            else
                strokeStart = -1f;
        }

        if (holdOpenWhenEmpty && currentAmmo == 0)
            offset = Mathf.Max(offset, holdOpenFraction);

        part.localPosition = restLocalPosition + travel * offset;
    }

    private void ResetPart()
    {
        strokeStart = -1f;
        if (part != null)
            part.localPosition = restLocalPosition;
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        foreach (Transform child in root)
        {
            if (child.name == targetName)
                return child;
            Transform nested = FindDescendantByName(child, targetName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
