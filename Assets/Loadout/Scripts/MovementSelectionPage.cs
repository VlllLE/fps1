using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The MOVEMENT tab of <see cref="LoadoutPauseMenu"/>: lists the movement archetypes with a clear selected state, shows the
/// key stats of the one you hover (or the selected one, when nothing is hovered) next to the class speed multiplier, and
/// applies a click to the local player at once. Keys 1-5 pick an archetype too. Every choice is queued and executed in
/// Update, never inside OnGUI, like the weapon page.
/// </summary>
[DisallowMultipleComponent]
public class MovementSelectionPage : MonoBehaviour, ILoadoutMenuPage
{
    [SerializeField] private MovementSelectionService service;

    private readonly Queue<Action> pending = new Queue<Action>(4);
    private readonly List<KeyValuePair<Rect, MovementArchetypeEntry>> rowRects = new List<KeyValuePair<Rect, MovementArchetypeEntry>>(8);
    private bool shown;
    private bool hasHover;
    private MovementArchetypeEntry hovered;
    private float currentLeftWidth = 320f;
    private bool hoverFound;
    private MovementArchetypeEntry hoverCandidate;
    private Vector2 detailScroll;

    private GUIStyle headerStyle;
    private GUIStyle toggleStyle;
    private GUIStyle labelStyle;
    private GUIStyle captionStyle;
    private GUIStyle statStyle;
    private GUIStyle statValueStyle;
    private bool stylesReady;

    public string PageTitle => "MOVEMENT";
    public int SortOrder => 1;
    public bool IsShown => shown;

    /// <summary>The archetype whose stats are on screen: the hovered one, else the selected one.</summary>
    public MovementArchetypeEntry ShownEntry => hasHover && hovered != null ? hovered : Service != null ? Service.SelectedEntry : null;

    public MovementSelectionService Service
    {
        get
        {
            if (service == null)
                service = GetComponent<MovementSelectionService>();
            if (service == null)
                service = FindAnyObjectByType<MovementSelectionService>();
            return service;
        }
    }

    private void OnEnable()
    {
        LoadoutMenuPages.RegisterMovement(this);
    }

    private void OnDisable()
    {
        LoadoutMenuPages.UnregisterMovement(this);
        OnPageHidden();
    }

    private void Update()
    {
        while (pending.Count > 0)
            pending.Dequeue().Invoke();

        if (shown)
            HandleNumberKeys();
    }

    public void OnPageShown()
    {
        shown = true;
        hasHover = false;
    }

    public void OnPageHidden()
    {
        shown = false;
        hasHover = false;
        pending.Clear();
    }

    private void HandleNumberKeys()
    {
        MovementSelectionService svc = Service;
        if (svc == null || svc.Catalog == null)
            return;

        int count = Mathf.Min(svc.Catalog.Count, 9);
        for (int i = 0; i < count; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i))
                continue;

            MovementArchetypeEntry entry = svc.Catalog.archetypes[i];
            if (entry != null)
                svc.Select(entry);
            return;
        }
    }

    public void DrawPage(float width, float height)
    {
        MovementSelectionService svc = Service;
        if (svc == null || svc.Catalog == null || svc.Catalog.Count == 0 || svc.Movement == null)
        {
            GUILayout.Label("Movement archetypes are unavailable in this scene (no player movement found).");
            return;
        }

        EnsureStyles();

        bool repaint = Event.current.type == EventType.Repaint;
        if (repaint)
        {
            hoverFound = false;
            hoverCandidate = null;
            rowRects.Clear();
        }

        MovementArchetypeCatalog catalog = svc.Catalog;
        MovementArchetypeEntry selected = svc.SelectedEntry;
        float leftWidth = Mathf.Min(330f, width * 0.36f);
        float rightWidth = Mathf.Max(260f, width - leftWidth - 16f);
        currentLeftWidth = leftWidth;

        GUILayout.BeginHorizontal(GUILayout.Height(height));

        GUILayout.BeginVertical(GUILayout.Width(leftWidth), GUILayout.Height(height));
        DrawArchetypeList(svc, catalog, selected);
        GUILayout.EndVertical();

        GUILayout.Space(14f);

        GUILayout.BeginVertical(GUILayout.Width(rightWidth - 6f), GUILayout.Height(height));
        MovementArchetypeEntry shownEntry = hasHover && hovered != null ? hovered : selected;
        DrawDetails(svc, catalog, shownEntry, selected, rightWidth, height);
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        if (repaint)
        {
            hasHover = hoverFound;
            hovered = hoverCandidate;
        }
    }

    // ---------------------------------------------------------------- Left: the list

    private void DrawArchetypeList(MovementSelectionService svc, MovementArchetypeCatalog catalog, MovementArchetypeEntry selected)
    {
        GUILayout.Label("ARCHETYPE", headerStyle);
        for (int i = 0; i < catalog.archetypes.Length; i++)
        {
            MovementArchetypeEntry entry = catalog.archetypes[i];
            if (entry == null)
                continue;

            bool isSelected = selected != null && selected.Id == entry.Id;
            string label = $"{i + 1}   {entry.displayName}" + (isSelected ? "     SELECTED" : string.Empty);
            Color previous = GUI.contentColor;
            if (isSelected)
                GUI.contentColor = new Color(1f, 0.85f, 0.4f);
            bool now = GUILayout.Toggle(isSelected, label, toggleStyle, GUILayout.Height(40f));
            GUI.contentColor = previous;

            if (Event.current.type == EventType.Repaint)
                rowRects.Add(new KeyValuePair<Rect, MovementArchetypeEntry>(GUILayoutUtility.GetLastRect(), entry));

            if (now && !isSelected)
            {
                MovementArchetypeEntry captured = entry;
                pending.Enqueue(() => svc.Select(captured));
            }
        }

        if (Event.current.type == EventType.Repaint)
        {
            Vector2 mouse = Event.current.mousePosition;
            for (int i = 0; i < rowRects.Count; i++)
            {
                if (rowRects[i].Key.Contains(mouse))
                {
                    hoverFound = true;
                    hoverCandidate = rowRects[i].Value;
                    break;
                }
            }
        }

        GUILayout.Space(10f);
        GUILayout.Label("Hover an archetype to compare it with the selected one. Click or press 1-5 to switch; it applies right away and is remembered.", labelStyle, GUILayout.Width(currentLeftWidth - 12f));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset movement to default", toggleStyle, GUILayout.Height(28f)))
            pending.Enqueue(() => svc.ResetToDefault());
    }

    // ---------------------------------------------------------------- Right: stats

    private void DrawDetails(MovementSelectionService svc, MovementArchetypeCatalog catalog, MovementArchetypeEntry shownEntry,
        MovementArchetypeEntry selected, float rightWidth, float height)
    {
        if (shownEntry == null || shownEntry.config == null)
        {
            GUILayout.Label("No archetype selected.", labelStyle);
            return;
        }

        float multiplier = svc.ClassMultiplier;
        bool comparing = selected != null && selected.Id != shownEntry.Id && selected.config != null;
        var stats = new MovementStatSheet(shownEntry.config, multiplier);
        MovementStatSheet? baseline = comparing ? new MovementStatSheet(selected.config, multiplier) : (MovementStatSheet?)null;
        var maxima = CatalogMaxima(catalog, multiplier);

        GUILayout.Label(shownEntry.displayName.ToUpperInvariant(), captionStyle);
        string state = comparing ? $"Previewing (selected: {selected.displayName})" : "Selected";
        GUILayout.Label($"{state}  ·  {shownEntry.description}", labelStyle);
        GUILayout.Space(4f);

        detailScroll = GUILayout.BeginScrollView(detailScroll, false, false, GUILayout.ExpandHeight(true));

        GUILayout.Label("KEY STATS", headerStyle);
        float barWidth = Mathf.Max(120f, rightWidth - 330f);
        DrawStat("Walk speed", stats.EffectiveWalkSpeed, baseline?.EffectiveWalkSpeed, maxima.walk, "{0:0.0} m/s", barWidth);
        DrawStat("Sprint speed", stats.EffectiveSprintSpeed, baseline?.EffectiveSprintSpeed, maxima.sprint, "{0:0.0} m/s", barWidth);
        DrawStat("Jump height", stats.JumpHeight, baseline?.JumpHeight, maxima.jump, "{0:0.00} m", barWidth);
        DrawStat("Air control", stats.AirControl, baseline?.AirControl, maxima.airControl, "{0:0}%", barWidth, 100f);
        DrawStat("Air acceleration", stats.AirAcceleration, baseline?.AirAcceleration, maxima.airAccel, "{0:0.0}", barWidth);
        DrawStat("Slide boost", stats.SlideBoostSpeed, baseline?.SlideBoostSpeed, maxima.slide, "{0:0.0} m/s", barWidth);
        DrawStat("Slide length", stats.SlideDuration, baseline?.SlideDuration, maxima.slideTime, "{0:0.00} s", barWidth);
        DrawStat("Bunny-hop cap", stats.EffectiveBhopCap, baseline?.EffectiveBhopCap, maxima.bhop, "{0:0.0} m/s", barWidth);
        DrawStat("Bunny-hop speed kept", stats.BhopRetention, baseline?.BhopRetention, 1f, "{0:0}%", barWidth, 100f);
        DrawStat("Dive speed", stats.DiveSpeed, baseline?.DiveSpeed, maxima.dive, "{0:0.0} m/s", barWidth);
        DrawStat("Wall bounce", stats.WallBounceForce, baseline?.WallBounceForce, maxima.wallBounce, "{0:0.0}", barWidth);

        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        DrawTrait("Wall run", stats.WallRun);
        DrawTrait("Wall bounce", stats.WallBounce);
        DrawTrait("Mantle", stats.Mantle);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);
        DrawClassMultiplier(svc, multiplier, shownEntry.config);

        GUILayout.EndScrollView();
    }

    private void DrawClassMultiplier(MovementSelectionService svc, float multiplier, MovementConfig config)
    {
        GUILayout.Label("CLASS SPEED MULTIPLIER", headerStyle);
        string className = svc.ClassName;
        GUILayout.Label(
            $"x{multiplier:0.00}" + (string.IsNullOrEmpty(className) ? string.Empty : $"  ({className})") +
            $"  ·  walk {config.walkSpeed:0.0} x {multiplier:0.00} = {config.walkSpeed * multiplier:0.0} m/s, applied on top of every archetype. " +
            "It comes from your class on the LOADOUT tab.", labelStyle);

        WeaponLoadoutService weapons = FindAnyObjectByType<WeaponLoadoutService>();
        ClassDefinition[] classes = weapons != null && weapons.Catalog != null ? weapons.Catalog.classes : null;
        if (classes == null || classes.Length == 0)
            return;

        GUILayout.BeginHorizontal();
        for (int i = 0; i < classes.Length; i++)
        {
            ClassDefinition definition = classes[i];
            if (definition == null)
                continue;

            bool current = definition.className == className;
            Color previous = GUI.contentColor;
            if (current)
                GUI.contentColor = new Color(1f, 0.85f, 0.4f);
            GUILayout.Label($"{definition.className} x{definition.moveSpeedMultiplier:0.00}", current ? statValueStyle : statStyle, GUILayout.Width(150f));
            GUI.contentColor = previous;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void DrawTrait(string label, bool on)
    {
        Color previous = GUI.contentColor;
        GUI.contentColor = on ? new Color(0.55f, 1f, 0.55f) : new Color(0.65f, 0.65f, 0.65f);
        GUILayout.Label((on ? "[x] " : "[ ] ") + label, statValueStyle, GUILayout.Width(120f));
        GUI.contentColor = previous;
    }

    private void DrawStat(string label, float value, float? baseline, float max, string format, float barWidth, float displayScale = 1f)
    {
        GUILayout.BeginHorizontal(GUILayout.Height(20f));
        GUILayout.Label(label, statStyle, GUILayout.Width(150f));

        Rect bar = GUILayoutUtility.GetRect(barWidth, 14f, GUILayout.Width(barWidth), GUILayout.Height(14f));
        if (Event.current.type == EventType.Repaint)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            float fill = Mathf.Clamp01(max > 0.0001f ? value / max : 0f);
            GUI.color = new Color(0.95f, 0.75f, 0.25f, 1f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fill, bar.height), Texture2D.whiteTexture);
            if (baseline.HasValue)
            {
                float marker = Mathf.Clamp01(max > 0.0001f ? baseline.Value / max : 0f);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(bar.x + bar.width * marker - 1f, bar.y - 2f, 2f, bar.height + 4f), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        GUILayout.Label(string.Format(format, value * displayScale), statValueStyle, GUILayout.Width(80f));

        if (baseline.HasValue && Mathf.Abs(baseline.Value) > 0.0001f)
        {
            float delta = (value - baseline.Value) / baseline.Value;
            Color previous = GUI.contentColor;
            GUI.contentColor = delta > 0.005f ? new Color(0.55f, 1f, 0.55f) : delta < -0.005f ? new Color(1f, 0.55f, 0.5f) : Color.white;
            GUILayout.Label(Mathf.Abs(delta) < 0.005f ? "same" : $"{delta * 100f:+0;-0}%", statValueStyle, GUILayout.Width(60f));
            GUI.contentColor = previous;
        }
        else
        {
            GUILayout.Space(60f);
        }

        GUILayout.EndHorizontal();
    }

    private struct Maxima
    {
        public float walk, sprint, jump, airControl, airAccel, slide, slideTime, bhop, dive, wallBounce;
    }

    private static Maxima CatalogMaxima(MovementArchetypeCatalog catalog, float multiplier)
    {
        var m = new Maxima { airControl = 1f };
        for (int i = 0; i < catalog.archetypes.Length; i++)
        {
            MovementArchetypeEntry entry = catalog.archetypes[i];
            if (entry == null || entry.config == null)
                continue;

            var s = new MovementStatSheet(entry.config, multiplier);
            m.walk = Mathf.Max(m.walk, s.EffectiveWalkSpeed);
            m.sprint = Mathf.Max(m.sprint, s.EffectiveSprintSpeed);
            m.jump = Mathf.Max(m.jump, s.JumpHeight);
            m.airAccel = Mathf.Max(m.airAccel, s.AirAcceleration);
            m.slide = Mathf.Max(m.slide, s.SlideBoostSpeed);
            m.slideTime = Mathf.Max(m.slideTime, s.SlideDuration);
            m.bhop = Mathf.Max(m.bhop, s.EffectiveBhopCap);
            m.dive = Mathf.Max(m.dive, s.DiveSpeed);
            m.wallBounce = Mathf.Max(m.wallBounce, s.WallBounceForce);
        }

        return m;
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        headerStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
        toggleStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
        toggleStyle.padding = new RectOffset(12, 6, 2, 2);
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        captionStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        captionStyle.normal.textColor = Color.white;
        statStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        statStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        statValueStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        statValueStyle.normal.textColor = Color.white;
        stylesReady = true;
    }
}
