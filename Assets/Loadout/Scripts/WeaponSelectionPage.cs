using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The weapon selection screen inside <see cref="LoadoutPauseMenu"/>: class, primary, secondary, a skin per weapon and
/// a live preview of the weapon / skin being looked at (hover a skin to preview it before picking).
/// Every click is queued and executed in Update, never inside OnGUI, so the IMGUI layout stays stable.
/// </summary>
[DisallowMultipleComponent]
public class WeaponSelectionPage : MonoBehaviour, ILoadoutMenuPage
{
    public enum FocusSlot
    {
        Primary = 0,
        Secondary = 1
    }

    [SerializeField] private WeaponLoadoutService service;

    private readonly Queue<Action> pending = new Queue<Action>(4);
    private readonly List<KeyValuePair<Rect, WeaponSkinDefinition>> skinRects = new List<KeyValuePair<Rect, WeaponSkinDefinition>>(32);
    private WeaponPreviewRig rig;
    private bool shown;
    private FocusSlot focus = FocusSlot.Primary;
    private Vector2 leftScroll;
    private Vector2 skinScroll;
    private bool hasHover;
    private WeaponSkinDefinition hoveredSkin;
    private bool hoverFound;
    private WeaponSkinDefinition hoverCandidate;

    private GUIStyle headerStyle;
    private GUIStyle toggleStyle;
    private GUIStyle labelStyle;
    private GUIStyle captionStyle;
    private bool stylesReady;

    public string PageTitle => "LOADOUT";
    public int SortOrder => 0;
    public bool IsShown => shown;
    public FocusSlot Focus => focus;
    public WeaponPreviewRig PreviewRig => rig;

    public WeaponLoadoutService Service
    {
        get
        {
            if (service == null)
                service = GetComponent<WeaponLoadoutService>();
            if (service == null)
                service = FindAnyObjectByType<WeaponLoadoutService>();
            return service;
        }
    }

    private void OnEnable()
    {
        LoadoutMenuPages.Register(this);
    }

    private void OnDisable()
    {
        LoadoutMenuPages.Unregister(this);
        OnPageHidden();
    }

    private void OnDestroy()
    {
        rig?.Dispose();
        rig = null;
    }

    private void Update()
    {
        while (pending.Count > 0)
            pending.Dequeue().Invoke();

        if (shown)
            UpdatePreview();
    }

    public void OnPageShown()
    {
        shown = true;
        hasHover = false;
        if (rig == null)
            rig = new WeaponPreviewRig();
        rig.SetActive(true);
    }

    public void OnPageHidden()
    {
        shown = false;
        hasHover = false;
        pending.Clear();
        rig?.SetActive(false);
    }

    public void SetFocus(FocusSlot slot)
    {
        focus = slot;
    }

    private string FocusedWeaponId()
    {
        WeaponLoadoutService svc = Service;
        if (svc == null)
            return string.Empty;
        LoadoutSelection selection = svc.Selection;
        return focus == FocusSlot.Primary ? selection.PrimaryId : selection.SecondaryId;
    }

    private void UpdatePreview()
    {
        WeaponLoadoutService svc = Service;
        if (svc == null || rig == null)
            return;

        LoadoutWeaponEntry entry = svc.Catalog.FindWeapon(FocusedWeaponId());
        rig.SetPrefab(entry != null ? entry.prefab : null);
        if (entry != null)
            rig.SetSkin(hasHover ? hoveredSkin : svc.GetSelectedSkin(entry.Id));
        rig.Tick(Time.unscaledDeltaTime);
    }

    public void DrawPage(float width, float height)
    {
        WeaponLoadoutService svc = Service;
        if (svc == null || svc.Catalog == null)
        {
            GUILayout.Label("Weapon loadout is unavailable in this scene (no player loadout found).");
            return;
        }

        EnsureStyles();

        WeaponLoadoutCatalog catalog = svc.Catalog;
        LoadoutSelection selection = svc.Selection;
        bool repaint = Event.current.type == EventType.Repaint;
        if (repaint)
        {
            hoverFound = false;
            hoverCandidate = null;
            skinRects.Clear();
        }

        float leftWidth = Mathf.Min(340f, width * 0.4f);
        float rightWidth = Mathf.Max(200f, width - leftWidth - 16f);

        GUILayout.BeginHorizontal(GUILayout.Height(height));

        GUILayout.BeginVertical(GUILayout.Width(leftWidth), GUILayout.Height(height));
        leftScroll = GUILayout.BeginScrollView(leftScroll, false, false, GUILayout.Width(leftWidth), GUILayout.Height(height));
        DrawClassSection(svc, catalog, selection);
        DrawPrimarySection(svc, catalog, selection);
        DrawSecondarySection(svc, catalog, selection);
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(8f);

        GUILayout.BeginVertical(GUILayout.Width(rightWidth), GUILayout.Height(height));
        DrawPreviewSection(svc, catalog, selection, rightWidth, height);
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        if (repaint)
        {
            hasHover = hoverFound;
            hoveredSkin = hoverCandidate;
        }
    }

    private void DrawClassSection(WeaponLoadoutService svc, WeaponLoadoutCatalog catalog, LoadoutSelection selection)
    {
        GUILayout.Label("CLASS", headerStyle);
        ClassDefinition[] classes = catalog.classes;
        if (classes == null || classes.Length == 0)
        {
            GUILayout.Label("No classes in catalog.", labelStyle);
            return;
        }

        int perRow = 2;
        int index = 0;
        while (index < classes.Length)
        {
            GUILayout.BeginHorizontal();
            for (int column = 0; column < perRow; column++, index++)
            {
                if (index >= classes.Length)
                {
                    GUILayout.FlexibleSpace();
                    continue;
                }

                ClassDefinition definition = classes[index];
                if (definition == null)
                {
                    GUILayout.FlexibleSpace();
                    continue;
                }

                bool selected = selection.ClassId == WeaponLoadoutCatalog.ClassIdOf(definition);
                if (SelectButton(definition.className, selected, 32f))
                {
                    ClassDefinition captured = definition;
                    pending.Enqueue(() => svc.SelectClass(captured));
                }
            }
            GUILayout.EndHorizontal();
        }

        ClassDefinition current = catalog.FindClass(selection.ClassId);
        if (current != null && !string.IsNullOrEmpty(current.description))
            GUILayout.Label(current.description, labelStyle);
        GUILayout.Space(8f);
    }

    private void DrawPrimarySection(WeaponLoadoutService svc, WeaponLoadoutCatalog catalog, LoadoutSelection selection)
    {
        GUILayout.Label("PRIMARY", headerStyle);
        List<LoadoutWeaponEntry> options = catalog.GetPrimaryOptions();
        for (int i = 0; i < options.Count; i++)
        {
            LoadoutWeaponEntry entry = options[i];
            bool selected = selection.PrimaryId == entry.Id;
            if (SelectButton(WeaponLabel(entry), selected, 30f))
            {
                LoadoutWeaponEntry captured = entry;
                pending.Enqueue(() =>
                {
                    svc.SelectPrimary(captured);
                    focus = FocusSlot.Primary;
                });
            }
        }
        GUILayout.Space(8f);
    }

    private void DrawSecondarySection(WeaponLoadoutService svc, WeaponLoadoutCatalog catalog, LoadoutSelection selection)
    {
        GUILayout.Label("SECONDARY", headerStyle);
        List<LoadoutWeaponEntry> options = catalog.GetSecondaryOptions(selection.PrimaryId);
        for (int i = 0; i < options.Count; i++)
        {
            LoadoutWeaponEntry entry = options[i];
            bool selected = selection.SecondaryId == entry.Id;
            if (SelectButton(WeaponLabel(entry), selected, 30f))
            {
                LoadoutWeaponEntry captured = entry;
                pending.Enqueue(() =>
                {
                    svc.SelectSecondary(captured);
                    focus = FocusSlot.Secondary;
                });
            }
        }
        GUILayout.Space(8f);
    }

    private void DrawPreviewSection(WeaponLoadoutService svc, WeaponLoadoutCatalog catalog, LoadoutSelection selection, float rightWidth, float height)
    {
        LoadoutWeaponEntry primary = catalog.FindWeapon(selection.PrimaryId);
        LoadoutWeaponEntry secondary = catalog.FindWeapon(selection.SecondaryId);

        string[] tabs =
        {
            "Primary: " + (primary != null ? primary.DisplayName : "-"),
            "Secondary: " + (secondary != null ? secondary.DisplayName : "-")
        };
        int pickedFocus = GUILayout.Toolbar((int)focus, tabs, toggleStyle, GUILayout.Height(28f));
        if (pickedFocus != (int)focus)
        {
            FocusSlot newFocus = (FocusSlot)pickedFocus;
            pending.Enqueue(() => focus = newFocus);
        }

        float previewHeight = Mathf.Clamp(height * 0.42f, 140f, 280f);
        Rect previewRect = GUILayoutUtility.GetRect(rightWidth, previewHeight, GUILayout.ExpandWidth(true));
        GUI.Box(previewRect, GUIContent.none);
        if (Event.current.type == EventType.Repaint && rig != null && rig.Texture != null && rig.PartCount > 0)
            GUI.DrawTexture(previewRect, rig.Texture, ScaleMode.ScaleToFit, false);

        LoadoutWeaponEntry focused = focus == FocusSlot.Primary ? primary : secondary;
        if (focused == null)
        {
            GUILayout.Label("No weapon selected.", labelStyle);
            return;
        }

        WeaponSkinDefinition chosen = svc.GetSelectedSkin(focused.Id);
        WeaponSkinDefinition displayed = hasHover ? hoveredSkin : chosen;
        string skinLine = hasHover
            ? $"Previewing: {WeaponLoadoutCatalog.SkinDisplayName(displayed)}"
            : $"Skin: {WeaponLoadoutCatalog.SkinDisplayName(chosen)}";
        GUILayout.Label($"{focused.DisplayName}  ·  {focused.WeaponType}", captionStyle);
        GUILayout.Label(skinLine + (displayed != null ? $"  ({displayed.rarity})" : string.Empty), labelStyle);

        GUILayout.Space(4f);
        GUILayout.Label($"SKINS — {focused.DisplayName}", headerStyle);
        DrawSkinList(svc, catalog, focused, chosen);

        GUILayout.Space(4f);
        if (GUILayout.Button("Reset loadout to defaults", toggleStyle, GUILayout.Height(28f)))
            pending.Enqueue(() => svc.ResetToDefaults());
    }

    private void DrawSkinList(WeaponLoadoutService svc, WeaponLoadoutCatalog catalog, LoadoutWeaponEntry weapon, WeaponSkinDefinition chosen)
    {
        bool repaint = Event.current.type == EventType.Repaint;
        Vector2 mouseOuter = Event.current.mousePosition;

        skinScroll = GUILayout.BeginScrollView(skinScroll, false, false, GUILayout.ExpandHeight(true));

        int count = (catalog.skins != null ? catalog.skins.Length : 0) + 1;
        int perRow = 2;
        for (int index = 0; index < count;)
        {
            GUILayout.BeginHorizontal();
            for (int column = 0; column < perRow; column++, index++)
            {
                if (index >= count)
                {
                    GUILayout.FlexibleSpace();
                    continue;
                }

                WeaponSkinDefinition skin = index == 0 ? null : catalog.skins[index - 1];
                if (index != 0 && skin == null)
                {
                    GUILayout.FlexibleSpace();
                    continue;
                }

                bool selected = skin == chosen;
                Color previous = GUI.contentColor;
                GUI.contentColor = RarityColor(skin);
                bool clicked = SelectButton(WeaponLoadoutCatalog.SkinDisplayName(skin), selected, 28f);
                GUI.contentColor = previous;

                if (repaint)
                    skinRects.Add(new KeyValuePair<Rect, WeaponSkinDefinition>(GUILayoutUtility.GetLastRect(), skin));

                if (clicked)
                {
                    WeaponSkinDefinition captured = skin;
                    string weaponId = weapon.Id;
                    pending.Enqueue(() => svc.SelectSkin(weaponId, captured));
                }
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        if (repaint)
        {
            Rect viewRect = GUILayoutUtility.GetLastRect();
            if (viewRect.Contains(mouseOuter))
            {
                Vector2 inner = mouseOuter - viewRect.position + skinScroll;
                for (int i = 0; i < skinRects.Count; i++)
                {
                    if (skinRects[i].Key.Contains(inner))
                    {
                        hoverFound = true;
                        hoverCandidate = skinRects[i].Value;
                        break;
                    }
                }
            }
        }
    }

    private bool SelectButton(string label, bool selected, float height)
    {
        bool now = GUILayout.Toggle(selected, label, toggleStyle, GUILayout.Height(height));
        return now && !selected;
    }

    private static string WeaponLabel(LoadoutWeaponEntry entry)
    {
        return entry.IsMelee ? $"{entry.DisplayName}  (melee)" : $"{entry.DisplayName}  ({entry.WeaponType})";
    }

    private static Color RarityColor(WeaponSkinDefinition skin)
    {
        if (skin == null)
            return Color.white;

        switch (skin.rarity)
        {
            case WeaponSkinDefinition.SkinRarity.Uncommon:
                return new Color(0.55f, 1f, 0.55f);
            case WeaponSkinDefinition.SkinRarity.Rare:
                return new Color(0.5f, 0.75f, 1f);
            case WeaponSkinDefinition.SkinRarity.Epic:
                return new Color(0.85f, 0.6f, 1f);
            case WeaponSkinDefinition.SkinRarity.Legendary:
                return new Color(1f, 0.75f, 0.3f);
            default:
                return Color.white;
        }
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        headerStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
        toggleStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
        toggleStyle.padding = new RectOffset(10, 6, 2, 2);
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        captionStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        captionStyle.normal.textColor = Color.white;
        stylesReady = true;
    }
}
