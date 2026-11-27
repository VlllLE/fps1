using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A full-size page hosted inside <see cref="LoadoutPauseMenu"/>. Lives behind an interface so assemblies that
/// depend on more than the core Scripts assembly (e.g. weapon skins) can plug into the menu.
/// </summary>
public interface ILoadoutMenuPage
{
    string PageTitle { get; }

    /// <summary>Lower values are listed first when several pages are registered.</summary>
    int SortOrder { get; }

    /// <summary>Called when the menu opens on this page or the player switches to it.</summary>
    void OnPageShown();

    /// <summary>Called when the menu closes or the player switches to another tab.</summary>
    void OnPageHidden();

    /// <summary>Draws the page with IMGUI layout calls inside the given size.</summary>
    void DrawPage(float width, float height);
}

/// <summary>
/// Registry of <see cref="ILoadoutMenuPage"/> implementations. Pages register while enabled.
/// </summary>
public static class LoadoutMenuPages
{
    private static readonly List<ILoadoutMenuPage> pages = new List<ILoadoutMenuPage>(2);

    public static ILoadoutMenuPage Primary
    {
        get
        {
            Prune();
            return pages.Count > 0 ? pages[0] : null;
        }
    }

    private static ILoadoutMenuPage movementPage;

    /// <summary>The page shown on the menu's MOVEMENT tab, or null when none is registered.</summary>
    public static ILoadoutMenuPage MovementPage
    {
        get
        {
            if (movementPage is Object unityObject && unityObject == null)
                movementPage = null;
            return movementPage;
        }
    }

    public static void RegisterMovement(ILoadoutMenuPage page)
    {
        if (page != null)
            movementPage = page;
    }

    public static void UnregisterMovement(ILoadoutMenuPage page)
    {
        if (ReferenceEquals(movementPage, page))
            movementPage = null;
    }

    public static int Count
    {
        get
        {
            Prune();
            return pages.Count;
        }
    }

    public static void Register(ILoadoutMenuPage page)
    {
        if (page == null || pages.Contains(page))
            return;

        pages.Add(page);
        pages.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
    }

    public static void Unregister(ILoadoutMenuPage page)
    {
        if (page != null)
            pages.Remove(page);
    }

    private static void Prune()
    {
        for (int i = pages.Count - 1; i >= 0; i--)
        {
            if (pages[i] is Object unityObject && unityObject == null)
                pages.RemoveAt(i);
        }
    }
}
