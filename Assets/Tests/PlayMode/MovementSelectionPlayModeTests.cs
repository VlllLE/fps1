using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>The MOVEMENT tab: choosing an archetype swaps the live config, persists, and the menu hands control back cleanly.</summary>
public class MovementSelectionPlayModeTests
{
    private GameObject player;
    private MovementController movement;
    private CharacterController characterController;
    private InputHandler inputHandler;
    private MovementSelectionService service;
    private LoadoutPauseMenu menu;

    private static void ClearPrefs()
    {
        PlayerPrefs.DeleteKey(MovementSelection.StoreKey);
        PlayerPrefs.DeleteKey(PlayerPrefsLoadoutStore.DefaultKey);
        PlayerPrefs.Save();
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        ClearPrefs();
        yield return LoadTestRange();
    }

    private IEnumerator LoadTestRange()
    {
        SceneManager.LoadScene("TestRange", LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.5f);

        movement = Object.FindAnyObjectByType<MovementController>();
        Assert.That(movement, Is.Not.Null, "TestRange needs its player");
        player = movement.gameObject;
        characterController = player.GetComponent<CharacterController>();
        inputHandler = player.GetComponent<InputHandler>();
        service = player.GetComponent<MovementSelectionService>();
        menu = Object.FindAnyObjectByType<LoadoutPauseMenu>();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        if (inputHandler != null)
        {
            inputHandler.SetMoveInput(Vector2.zero);
            inputHandler.SimulateSprintHeld(false);
            inputHandler.SetTestInputLocked(false);
        }

        if (menu != null && menu.IsOpen)
            menu.SetOpen(false);

        ClearPrefs();
    }

    private MovementConfig ConfigOf(string id) => service.Catalog.Find(id).config;

    private IEnumerator SprintForward(float seconds, System.Action<float> peakSpeed)
    {
        characterController.enabled = false;
        player.transform.SetPositionAndRotation(new Vector3(14f, 1.5f, -18f), Quaternion.identity);
        characterController.enabled = true;
        movement.ResetMovement();
        for (int i = 0; i < 45; i++)
        {
            if (!characterController.isGrounded)
                characterController.Move(Vector3.down * 0.2f);
            yield return null;
        }

        movement.ResetMovement();
        yield return null;

        inputHandler.SetTestInputLocked(true);
        inputHandler.SetMoveInput(new Vector2(0f, 1f));
        inputHandler.SimulateSprintHeld(true);

        float peak = 0f;
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            peak = Mathf.Max(peak, movement.CurrentSpeed);
            yield return null;
        }

        peakSpeed(peak);
        inputHandler.SetMoveInput(Vector2.zero);
        inputHandler.SimulateSprintHeld(false);
        yield return new WaitForSeconds(0.5f);
        inputHandler.SetTestInputLocked(false);
    }

    [UnityTest]
    public IEnumerator Bootstrap_AddsTheServiceToThePlayer_AndThePageToTheMenu()
    {
        Assert.That(service, Is.Not.Null, "the player gets a MovementSelectionService");
        Assert.That(menu, Is.Not.Null);
        var page = menu.GetComponent<MovementSelectionPage>();
        Assert.That(page, Is.Not.Null, "the pause menu hosts the movement page");
        Assert.That(LoadoutMenuPages.MovementPage, Is.SameAs(page));
        Assert.That(service.Catalog.Count, Is.EqualTo(5));
        yield return null;
    }

    [UnityTest]
    public IEnumerator Default_NothingChosen_KeepsTheScenesDefaultConfig_AndSavesNothing()
    {
        Assert.That(service.HasSavedSelection, Is.False);
        Assert.That(service.SelectedId, Is.EqualTo("Default"));
        Assert.That(movement.Config, Is.SameAs(ConfigOf("Default")));
        Assert.That(PlayerPrefs.HasKey(MovementSelection.StoreKey), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Selecting_AppliesTheConfigToTheLivePlayer_AndSavesIt()
    {
        int changes = 0;
        service.Changed += () => changes++;
        Vector3 before = player.transform.position;

        Assert.That(service.Select("Scout"), Is.True);

        Assert.That(movement.Config, Is.SameAs(ConfigOf("Scout")), "applied live, no restart");
        Assert.That(service.SelectedId, Is.EqualTo("Scout"));
        Assert.That(service.HasSavedSelection, Is.True);
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(PlayerPrefs.GetString(MovementSelection.StoreKey), Does.Contain("Scout"));
        Assert.That(player.transform.position, Is.EqualTo(before), "switching must not move the player");
        Assert.That(service.Select("NoSuchArchetype"), Is.False);
        Assert.That(movement.Config, Is.SameAs(ConfigOf("Scout")), "an unknown id changes nothing");
        yield return null;
    }

    [UnityTest]
    public IEnumerator EveryArchetype_CanBeSelected_AndTheControllerFollows()
    {
        foreach (MovementArchetypeEntry entry in service.Catalog.archetypes)
        {
            Assert.That(service.Select(entry), Is.True, entry.Id);
            Assert.That(movement.Config, Is.SameAs(entry.config), entry.Id);
            Assert.That(service.SelectedEntry, Is.SameAs(entry), entry.Id);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator Selecting_ChangesHowTheMovementActuallyFeels_BeforeAndAfter()
    {
        float defaultPeak = 0f;
        yield return SprintForward(1.6f, p => defaultPeak = p);

        service.Select("Heavy");
        float heavyPeak = 0f;
        yield return SprintForward(1.6f, p => heavyPeak = p);

        service.Select("Scout");
        float scoutPeak = 0f;
        yield return SprintForward(1.6f, p => scoutPeak = p);

        float mult = movement.ClassSpeedMultiplier;
        Assert.That(heavyPeak, Is.LessThan(defaultPeak - 0.5f), $"Heavy sprints slower (default {defaultPeak:0.00}, heavy {heavyPeak:0.00})");
        Assert.That(scoutPeak, Is.GreaterThan(defaultPeak + 0.5f), $"Scout sprints faster (default {defaultPeak:0.00}, scout {scoutPeak:0.00})");
        Assert.That(heavyPeak, Is.EqualTo(new MovementStatSheet(ConfigOf("Heavy"), mult).EffectiveSprintSpeed).Within(0.6f));
        Assert.That(scoutPeak, Is.EqualTo(new MovementStatSheet(ConfigOf("Scout"), mult).EffectiveSprintSpeed).Within(0.6f));
    }

    [UnityTest]
    public IEnumerator Choice_SurvivesALoadOfTheSameSceneThroughPlayerPrefs()
    {
        service.Select("Acrobat");
        Assert.That(PlayerPrefs.HasKey(MovementSelection.StoreKey), Is.True);

        yield return LoadTestRange();
        yield return new WaitForSeconds(0.3f);

        Assert.That(service, Is.Not.Null);
        Assert.That(service.HasSavedSelection, Is.True);
        Assert.That(movement.Config, Is.SameAs(ConfigOf("Acrobat")), "the saved archetype is re-applied when the scene starts");
        Assert.That(service.SelectedId, Is.EqualTo("Acrobat"));
    }

    [UnityTest]
    public IEnumerator Choice_IsAlsoReappliedOnAnotherMap()
    {
        service.Select("Marksman");
        MapSelector selector = MapSelector.EnsureInstance();
        int other = -1;
        for (int i = 0; i < MapCatalog.Count; i++)
        {
            if (i != MapSelector.CurrentMapIndex && selector.IsMapAvailable(i))
            {
                other = i;
                break;
            }
        }

        if (other < 0)
            Assert.Ignore("Only TestRange is in Build Settings; generate Shipment/Dome/Triad via Tools > Maps.");

        Assert.That(selector.TryLoadMap(other), Is.True);
        float deadline = Time.realtimeSinceStartup + 20f;
        while (selector.IsLoading && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return new WaitForSeconds(1f);

        var newMovement = Object.FindAnyObjectByType<MovementController>();
        if (newMovement == null)
            Assert.Ignore("The other map has no player to apply the archetype to.");
        Assert.That(newMovement.Config.name, Is.EqualTo("MovementConfig_Marksman"));
    }

    [UnityTest]
    public IEnumerator Reset_ForgetsTheChoice_AndReturnsToTheDefaultConfig()
    {
        service.Select("Heavy");
        Assert.That(service.ResetToDefault(), Is.True);

        Assert.That(movement.Config, Is.SameAs(ConfigOf("Default")));
        Assert.That(service.SelectedId, Is.EqualTo("Default"));
        Assert.That(service.HasSavedSelection, Is.False);
        Assert.That(PlayerPrefs.HasKey(MovementSelection.StoreKey), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator UnknownSavedId_IsIgnored_InsteadOfBreakingTheScene()
    {
        PlayerPrefs.SetString(MovementSelection.StoreKey, "{\"archetypeId\":\"Teleporter\"}");
        PlayerPrefs.Save();

        yield return LoadTestRange();
        yield return new WaitForSeconds(0.3f);

        Assert.That(service.HasSavedSelection, Is.False);
        Assert.That(movement.Config, Is.SameAs(ConfigOf("Default")));
    }

    [UnityTest]
    public IEnumerator ClassSpeedMultiplier_StaysStackedOnTopOfWhicheverArchetype()
    {
        movement.ClassSpeedMultiplier = 1.08f;
        service.Select("Scout");
        Assert.That(movement.ClassSpeedMultiplier, Is.EqualTo(1.08f), "picking an archetype never changes the class multiplier");
        Assert.That(service.ClassMultiplier, Is.EqualTo(1.08f));

        service.Select("Heavy");
        Assert.That(movement.ClassSpeedMultiplier, Is.EqualTo(1.08f));
        Assert.That(new MovementStatSheet(movement.Config, service.ClassMultiplier).EffectiveWalkSpeed,
            Is.EqualTo(movement.Config.walkSpeed * 1.08f).Within(1e-4f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator RealClassChange_KeepsTheArchetype_AndTakesItsOwnMultiplier()
    {
        var weapons = player.GetComponent<WeaponLoadoutService>();
        Assert.That(weapons, Is.Not.Null);
        weapons.Configure(weapons.Catalog, new MemoryLoadoutStore());

        ClassDefinition smg = System.Array.Find(weapons.Catalog.classes, c => c != null && c.className == "SMG");
        ClassDefinition sniper = System.Array.Find(weapons.Catalog.classes, c => c != null && c.className == "Sniper");
        Assert.That(smg, Is.Not.Null);
        Assert.That(sniper, Is.Not.Null);

        service.Select("Acrobat");
        Assert.That(weapons.SelectClass(smg), Is.True);
        Assert.That(movement.Config, Is.SameAs(ConfigOf("Acrobat")), "changing class does not reset the archetype");
        Assert.That(movement.ClassSpeedMultiplier, Is.EqualTo(smg.moveSpeedMultiplier));
        Assert.That(service.ClassName, Is.EqualTo("SMG"));

        Assert.That(weapons.SelectClass(sniper), Is.True);
        Assert.That(movement.Config, Is.SameAs(ConfigOf("Acrobat")));
        Assert.That(movement.ClassSpeedMultiplier, Is.EqualTo(sniper.moveSpeedMultiplier));
        yield return null;
    }

    [UnityTest]
    public IEnumerator Menu_MovementTab_UnlocksTheCursor_ShowsThePage_AndResumesCleanly()
    {
        Assert.That(menu.CurrentTab, Is.EqualTo(LoadoutPauseMenu.MenuTab.Loadout));
        var page = menu.GetComponent<MovementSelectionPage>();

        menu.OpenTab(LoadoutPauseMenu.MenuTab.Movement);
        yield return null;

        Assert.That(menu.IsOpen, Is.True);
        Assert.That(menu.CurrentTab, Is.EqualTo(LoadoutPauseMenu.MenuTab.Movement));
        Assert.That(page.IsShown, Is.True);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        Assert.That(Cursor.visible, Is.True);

        service.Select("Scout");
        yield return null;
        Assert.That(movement.Config, Is.SameAs(ConfigOf("Scout")), "a choice made while the cursor is unlocked applies at once");
        Assert.That(page.ShownEntry.Id, Is.EqualTo("Scout"));

        menu.SetOpen(false);
        yield return null;

        Assert.That(menu.IsOpen, Is.False);
        Assert.That(page.IsShown, Is.False);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
        Assert.That(Cursor.visible, Is.False);

        float peak = 0f;
        yield return SprintForward(1.2f, p => peak = p);
        Assert.That(peak, Is.GreaterThan(ConfigOf("Scout").walkSpeed), "the player moves normally again after closing the menu");
    }

    [UnityTest]
    public IEnumerator Menu_TogglingTheMovementTab_ClosesItAgain_AndSwitchingTabsHidesThePage()
    {
        var page = menu.GetComponent<MovementSelectionPage>();

        menu.ToggleTab(LoadoutPauseMenu.MenuTab.Movement);
        yield return null;
        Assert.That(menu.IsOpen && page.IsShown, Is.True);

        menu.OpenTab(LoadoutPauseMenu.MenuTab.Loadout);
        yield return null;
        Assert.That(page.IsShown, Is.False, "leaving the tab hides the page");
        Assert.That(menu.IsOpen, Is.True);

        menu.OpenTab(LoadoutPauseMenu.MenuTab.Movement);
        menu.ToggleTab(LoadoutPauseMenu.MenuTab.Movement);
        yield return null;
        Assert.That(menu.IsOpen, Is.False);
        Assert.That(page.IsShown, Is.False);
    }
}
