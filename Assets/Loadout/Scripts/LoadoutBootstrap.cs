using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wires the weapon loadout into every scene that has a player, without scene or prefab edits:
/// adds <see cref="WeaponLoadoutService"/> next to the player's <see cref="ClassLoadoutApplier"/> (which re-applies
/// the persisted selection on Start) and <see cref="WeaponSelectionPage"/> next to the <see cref="LoadoutPauseMenu"/>.
/// </summary>
public static class LoadoutBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInScene();
    }

    /// <summary>
    /// Adds the loadout components to the player in the loaded scenes. Safe to call repeatedly.
    /// Returns the service, or null when there is no player with a <see cref="ClassLoadoutApplier"/>.
    /// </summary>
    public static WeaponLoadoutService EnsureInScene()
    {
        EnsureMovementInScene();

        ClassLoadoutApplier applier = Object.FindAnyObjectByType<ClassLoadoutApplier>();
        if (applier == null)
            return null;

        WeaponLoadoutService service = applier.GetComponent<WeaponLoadoutService>();
        if (service == null)
            service = applier.gameObject.AddComponent<WeaponLoadoutService>();

        LoadoutPauseMenu menu = applier.GetComponent<LoadoutPauseMenu>();
        if (menu == null)
            menu = Object.FindAnyObjectByType<LoadoutPauseMenu>();
        if (menu != null && menu.GetComponent<WeaponSelectionPage>() == null)
            menu.gameObject.AddComponent<WeaponSelectionPage>();

        return service;
    }

    /// <summary>
    /// Adds <see cref="MovementSelectionService"/> to the local player and <see cref="MovementSelectionPage"/> next to the
    /// pause menu. Returns the service, or null when the scene has no player <see cref="MovementController"/>.
    /// </summary>
    public static MovementSelectionService EnsureMovementInScene()
    {
        MovementController movement = Object.FindAnyObjectByType<MovementController>();
        if (movement == null)
            return null;

        MovementSelectionService service = movement.GetComponent<MovementSelectionService>();
        if (service == null)
            service = movement.gameObject.AddComponent<MovementSelectionService>();

        LoadoutPauseMenu menu = movement.GetComponent<LoadoutPauseMenu>();
        if (menu == null)
            menu = Object.FindAnyObjectByType<LoadoutPauseMenu>();
        if (menu != null && menu.GetComponent<MovementSelectionPage>() == null)
            menu.gameObject.AddComponent<MovementSelectionPage>();

        return service;
    }
}
