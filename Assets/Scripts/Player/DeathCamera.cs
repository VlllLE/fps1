using UnityEngine;

/// <summary>
/// Third-person camera shown while the local player is dead: it lifts out of the eyes, pulls back and looks at the
/// falling body from the side, then drifts slowly around it. The first-person arms/weapon are hidden and the body
/// layers hidden from the first-person camera are shown again. Everything is restored by <see cref="End"/>.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class DeathCamera : MonoBehaviour
{
    public const float BlendDuration = 0.9f;
    public const float OrbitDegreesPerSecond = 7f;
    public const float Distance = 2.4f;
    public const float Height = 1.5f;

    private Camera cam;
    private Transform subjectRoot;
    private Transform focusBone;
    private Vector3 fallDirection = Vector3.back;
    private Vector3 focus;
    private float elapsed;
    private bool active;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 savedLocalPosition;
    private Quaternion savedLocalRotation;
    private int savedMask;
    private float sideSign = 1f;
    private Renderer[] hiddenRenderers;
    private bool[] hiddenStates;

    public bool IsActive => active;

    public static Vector3 ComputeEndPosition(Vector3 focusPoint, Vector3 fallDirectionWorld, float sideSign, float orbitDegrees, float distance, float height)
    {
        Vector3 flat = new Vector3(fallDirectionWorld.x, 0f, fallDirectionWorld.z);
        if (flat.sqrMagnitude < 1e-4f)
            flat = Vector3.back;
        flat.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, flat) * sideSign;
        side = Quaternion.AngleAxis(orbitDegrees, Vector3.up) * side;
        return focusPoint + side * distance + Vector3.up * height;
    }

    /// <summary>Takes over the camera. <paramref name="focusTransform"/> (usually the hips bone) is followed; the subject is ignored by the collision probe.</summary>
    public void Begin(Camera camera, Transform subject, Transform focusTransform, Vector3 fallDirectionWorld, int hiddenLayer)
    {
        if (active)
            End();
        cam = camera != null ? camera : GetComponentInChildren<Camera>();
        if (cam == null)
            return;

        subjectRoot = subject != null ? subject : transform;
        focusBone = focusTransform;
        fallDirection = fallDirectionWorld;
        savedLocalPosition = cam.transform.localPosition;
        savedLocalRotation = cam.transform.localRotation;
        startPosition = cam.transform.position;
        startRotation = cam.transform.rotation;
        savedMask = cam.cullingMask;
        if (hiddenLayer >= 0)
            cam.cullingMask |= 1 << hiddenLayer;

        Vector3 toStartRight = cam.transform.right;
        Vector3 flat = new Vector3(fallDirection.x, 0f, fallDirection.z);
        Vector3 side = Vector3.Cross(Vector3.up, flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.back);
        sideSign = Vector3.Dot(side, toStartRight) >= 0f ? 1f : -1f;

        HideFirstPersonRenderers();
        focus = ResolveFocus();
        elapsed = 0f;
        active = true;
    }

    public void End()
    {
        if (!active)
            return;
        active = false;
        if (cam != null)
        {
            cam.cullingMask = savedMask;
            cam.transform.localPosition = savedLocalPosition;
            cam.transform.localRotation = savedLocalRotation;
        }

        if (hiddenRenderers != null)
        {
            for (int i = 0; i < hiddenRenderers.Length; i++)
            {
                if (hiddenRenderers[i] != null)
                    hiddenRenderers[i].enabled = hiddenStates[i];
            }
        }

        hiddenRenderers = null;
        hiddenStates = null;
    }

    private void OnDisable()
    {
        End();
    }

    private void LateUpdate()
    {
        if (!active || cam == null)
            return;

        elapsed += Time.deltaTime;
        Vector3 target = ResolveFocus();
        focus = Vector3.Lerp(focus, target, 1f - Mathf.Exp(-8f * Time.deltaTime));

        float t = Mathf.Clamp01(elapsed / BlendDuration);
        float ease = t * t * (3f - 2f * t);
        float orbit = Mathf.Max(0f, elapsed - BlendDuration) * OrbitDegreesPerSecond;
        Vector3 end = ComputeEndPosition(focus, fallDirection, sideSign, orbit, Distance, Height);
        end = PullInFromWalls(focus, end);

        Vector3 position = Vector3.Lerp(startPosition, end, ease);
        Quaternion look = Quaternion.LookRotation((focus - position).normalized, Vector3.up);
        Quaternion rotation = Quaternion.Slerp(startRotation, look, Mathf.Clamp01(ease * 1.4f));
        cam.transform.SetPositionAndRotation(position, rotation);
    }

    private Vector3 ResolveFocus()
    {
        if (focusBone != null)
            return focusBone.position + Vector3.up * 0.1f;
        return subjectRoot != null ? subjectRoot.position + Vector3.up * 0.5f : Vector3.zero;
    }

    private Vector3 PullInFromWalls(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.01f)
            return to;

        RaycastHit[] hits = Physics.RaycastAll(from, delta / length, length, ~0, QueryTriggerInteraction.Ignore);
        float nearest = length;
        for (int i = 0; i < hits.Length; i++)
        {
            if (subjectRoot != null && hits[i].transform.IsChildOf(subjectRoot))
                continue;
            if (hits[i].distance < nearest)
                nearest = hits[i].distance;
        }

        return from + delta / length * Mathf.Max(0.3f, nearest - 0.25f);
    }

    private void HideFirstPersonRenderers()
    {
        Renderer[] all = cam.GetComponentsInChildren<Renderer>(true);
        hiddenRenderers = all;
        hiddenStates = new bool[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            hiddenStates[i] = all[i].enabled;
            all[i].enabled = false;
        }
    }
}
