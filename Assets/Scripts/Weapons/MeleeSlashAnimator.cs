using UnityEngine;

/// <summary>
/// Procedural first-person slash for melee weapons. Listens to <see cref="WeaponController.OnMeleeSwing"/>
/// and moves a model child through wind-up, strike and recovery. Successive swings alternate
/// sides (forehand / backhand). Poses are offsets from the model's rest transform.
/// </summary>
[DisallowMultipleComponent]
public class MeleeSlashAnimator : MonoBehaviour
{
    [SerializeField] private WeaponController weapon;
    [SerializeField] private Transform model;
    [SerializeField] private float duration = 0.32f;

    [Header("Wind-up pose")]
    [SerializeField] private Vector3 windupPosition = new Vector3(0.06f, 0.04f, -0.04f);
    [SerializeField] private Vector3 windupEuler = new Vector3(-15f, 30f, -40f);

    [Header("Strike pose")]
    [SerializeField] private Vector3 strikePosition = new Vector3(-0.14f, -0.06f, 0.15f);
    [SerializeField] private Vector3 strikeEuler = new Vector3(25f, -55f, 50f);

    [SerializeField, Range(0.05f, 0.5f)] private float windupEnd = 0.2f;
    [SerializeField, Range(0.2f, 0.8f)] private float strikeEnd = 0.5f;

    private Vector3 restPosition;
    private Quaternion restRotation = Quaternion.identity;
    private bool restCaptured;
    private bool swinging;
    private bool mirror;
    private float elapsed;

    public bool IsSwinging => swinging;
    public float Duration => duration;

    private void Awake()
    {
        ResolveReferences();
        CaptureRest();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureRest();
        if (weapon != null)
            weapon.OnMeleeSwing += HandleSwing;
    }

    private void OnDisable()
    {
        if (weapon != null)
            weapon.OnMeleeSwing -= HandleSwing;
        swinging = false;
        ApplyRest();
    }

    private void Update()
    {
        if (!swinging || model == null)
            return;

        elapsed += Time.deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
        Evaluate(t, mirror, out Vector3 pos, out Quaternion rot);
        model.localPosition = restPosition + pos;
        model.localRotation = restRotation * rot;

        if (t >= 1f)
        {
            swinging = false;
            ApplyRest();
        }
    }

    /// <summary>Starts a slash. Alternates sides each call.</summary>
    public void Play()
    {
        HandleSwing();
    }

    /// <summary>
    /// Samples the slash at normalised time <paramref name="t"/>. Returns zero offsets at t = 0 and t = 1.
    /// </summary>
    public void Evaluate(float t, bool mirrored, out Vector3 positionOffset, out Quaternion rotationOffset)
    {
        t = Mathf.Clamp01(t);
        float sign = mirrored ? -1f : 1f;

        Vector3 wp = MirrorPosition(windupPosition, sign);
        Vector3 sp = MirrorPosition(strikePosition, sign);
        Quaternion wr = Quaternion.Euler(MirrorEuler(windupEuler, sign));
        Quaternion sr = Quaternion.Euler(MirrorEuler(strikeEuler, sign));

        float wEnd = Mathf.Clamp(windupEnd, 0.01f, 0.95f);
        float sEnd = Mathf.Clamp(strikeEnd, wEnd + 0.01f, 0.99f);

        if (t < wEnd)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / wEnd);
            positionOffset = Vector3.Lerp(Vector3.zero, wp, k);
            rotationOffset = Quaternion.Slerp(Quaternion.identity, wr, k);
        }
        else if (t < sEnd)
        {
            float u = (t - wEnd) / (sEnd - wEnd);
            float k = 1f - (1f - u) * (1f - u) * (1f - u);
            positionOffset = Vector3.Lerp(wp, sp, k);
            rotationOffset = Quaternion.Slerp(wr, sr, k);
        }
        else
        {
            float k = Mathf.SmoothStep(0f, 1f, (t - sEnd) / (1f - sEnd));
            positionOffset = Vector3.Lerp(sp, Vector3.zero, k);
            rotationOffset = Quaternion.Slerp(sr, Quaternion.identity, k);
        }
    }

    private static Vector3 MirrorPosition(Vector3 p, float sign)
    {
        return new Vector3(p.x * sign, p.y, p.z);
    }

    private static Vector3 MirrorEuler(Vector3 e, float sign)
    {
        return new Vector3(e.x, e.y * sign, e.z * sign);
    }

    private void HandleSwing()
    {
        if (weapon != null && weapon.Config != null && weapon.Config.meleeSwingDuration > 0f)
            duration = weapon.Config.meleeSwingDuration;

        CaptureRest();
        mirror = !mirror;
        elapsed = 0f;
        swinging = true;
    }

    private void ResolveReferences()
    {
        if (weapon == null)
            weapon = GetComponentInParent<WeaponController>();
        if (model == null)
        {
            Transform child = transform.Find("Model");
            model = child != null ? child : null;
        }
    }

    private void CaptureRest()
    {
        if (model == null || (restCaptured && swinging))
            return;

        restPosition = model.localPosition;
        restRotation = model.localRotation;
        restCaptured = true;
    }

    private void ApplyRest()
    {
        if (model == null || !restCaptured)
            return;

        model.localPosition = restPosition;
        model.localRotation = restRotation;
    }
}
