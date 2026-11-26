using System.Collections.Generic;
using UnityEngine;

/// <summary>Angle helpers for the damage-direction indicator (kept separate so they are unit-testable).</summary>
public static class DamageIndicatorMath
{
    /// <summary>
    /// Signed angle in degrees from the viewer's forward direction to <paramref name="source"/> on the ground plane:
    /// 0 = straight ahead, 90 = to the right, -90 = to the left, +-180 = behind.
    /// </summary>
    public static float SourceAngle(Vector3 viewerPosition, Vector3 viewerForward, Vector3 source)
    {
        Vector3 forward = viewerForward;
        forward.y = 0f;
        Vector3 to = source - viewerPosition;
        to.y = 0f;
        if (forward.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f)
            return 0f;
        return Vector3.SignedAngle(forward, to, Vector3.up);
    }

    /// <summary>Opacity of an indicator <paramref name="age"/> seconds after the hit.</summary>
    public static float Fade(float age, float lifetime)
    {
        if (lifetime <= 0f || age >= lifetime)
            return 0f;
        return Mathf.Clamp01(1f - age / lifetime);
    }
}

/// <summary>
/// Minimal immediate-mode HUD for the player's health: a health bar, red wedges pointing at whoever shot you, a damage
/// vignette and a death message. Added at runtime next to <see cref="PlayerHealth"/> by <see cref="BotManager"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDamageIndicator : MonoBehaviour
{
    private struct Marker
    {
        public Vector3 sourcePosition;
        public float time;
    }

    private const float MarkerLifetime = 2.5f;
    private const int MaxMarkers = 8;

    private readonly List<Marker> markers = new List<Marker>();
    private PlayerHealth health;
    private float lastHitTime = -99f;
    private float lastHitStrength;
    private GUIStyle deathStyle;

    public int ActiveMarkerCount => markers.Count;

    private void OnEnable()
    {
        health = GetComponent<PlayerHealth>();
        if (health != null)
            health.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDamaged -= HandleDamaged;
    }

    private void HandleDamaged(float amount, Vector3 sourcePosition, bool fatal)
    {
        if (markers.Count >= MaxMarkers)
            markers.RemoveAt(0);
        markers.Add(new Marker { sourcePosition = sourcePosition, time = Time.unscaledTime });
        lastHitTime = Time.unscaledTime;
        lastHitStrength = Mathf.Clamp01(amount / 40f);
    }

    private void OnGUI()
    {
        if (health == null || Event.current.type != EventType.Repaint)
            return;

        float scale = Mathf.Max(0.5f, Screen.height / 1080f);
        Matrix4x4 saved = GUI.matrix;
        Color savedColor = GUI.color;

        DrawVignette();
        DrawMarkers(scale);
        DrawHealthBar(scale);
        if (health.IsDead)
            DrawDeathText(scale);

        GUI.matrix = saved;
        GUI.color = savedColor;
    }

    private void DrawVignette()
    {
        float since = Time.unscaledTime - lastHitTime;
        float alpha = DamageIndicatorMath.Fade(since, 0.8f) * (0.25f + 0.35f * lastHitStrength);
        float low = Mathf.Clamp01((0.4f - health.Health01) / 0.4f) * 0.25f;
        alpha = Mathf.Max(alpha, health.IsDead ? 0.5f : low);
        if (alpha <= 0.01f)
            return;

        float edge = Screen.height * 0.16f;
        GUI.color = new Color(0.75f, 0f, 0f, alpha);
        Texture2D white = Texture2D.whiteTexture;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, edge), white);
        GUI.DrawTexture(new Rect(0f, Screen.height - edge, Screen.width, edge), white);
        GUI.DrawTexture(new Rect(0f, edge, edge, Screen.height - 2f * edge), white);
        GUI.DrawTexture(new Rect(Screen.width - edge, edge, edge, Screen.height - 2f * edge), white);
    }

    private void DrawMarkers(float scale)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        float now = Time.unscaledTime;
        markers.RemoveAll(m => now - m.time >= MarkerLifetime);

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float radius = 170f * scale;
        Vector2 size = new Vector2(120f * scale, 26f * scale);
        Texture2D white = Texture2D.whiteTexture;

        for (int i = 0; i < markers.Count; i++)
        {
            Marker marker = markers[i];
            float angle = DamageIndicatorMath.SourceAngle(cam.transform.position, cam.transform.forward, marker.sourcePosition);
            float alpha = DamageIndicatorMath.Fade(now - marker.time, MarkerLifetime);

            GUI.matrix = Matrix4x4.identity;
            GUIUtility.RotateAroundPivot(angle, center);
            GUI.color = new Color(1f, 0.1f, 0.05f, 0.85f * alpha);
            GUI.DrawTexture(new Rect(center.x - size.x * 0.5f, center.y - radius - size.y, size.x, size.y), white);
        }

        GUI.matrix = Matrix4x4.identity;
    }

    private void DrawHealthBar(float scale)
    {
        float width = 260f * scale;
        float height = 16f * scale;
        float x = 24f * scale;
        float y = Screen.height - 40f * scale;
        Texture2D white = Texture2D.whiteTexture;

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x - 3f, y - 3f, width + 6f, height + 6f), white);

        float fraction = health.Health01;
        GUI.color = Color.Lerp(new Color(0.85f, 0.1f, 0.1f), new Color(0.2f, 0.85f, 0.3f), fraction);
        GUI.DrawTexture(new Rect(x, y, width * fraction, height), white);
    }

    private void DrawDeathText(float scale)
    {
        if (deathStyle == null)
        {
            deathStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }

        deathStyle.fontSize = Mathf.RoundToInt(42f * scale);
        GUI.color = new Color(1f, 0.85f, 0.85f, 1f);
        GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 80f * scale), "YOU DIED", deathStyle);
    }
}
