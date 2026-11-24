using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class WeaponLoadoutPlayModeTests
{
    private WeaponLoadoutService service;
    private LoadoutPauseMenu menu;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsLoadoutStore.DefaultKey);
        yield return LoadTestRange();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        PlayerPrefs.DeleteKey(PlayerPrefsLoadoutStore.DefaultKey);
    }

    private IEnumerator LoadTestRange()
    {
        SceneManager.LoadScene("TestRange", LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.5f);

        service = Object.FindAnyObjectByType<WeaponLoadoutService>();
        menu = Object.FindAnyObjectByType<LoadoutPauseMenu>();
    }

    private static void SetPauseTimeScale(LoadoutPauseMenu target, bool value)
    {
        FieldInfo field = typeof(LoadoutPauseMenu).GetField("pauseTimeScale", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "LoadoutPauseMenu.pauseTimeScale field not found");
        field.SetValue(target, value);
    }

    [UnityTest]
    public IEnumerator Bootstrap_AddsServiceAndPage_WithoutSceneEdits()
    {
        Assert.IsNotNull(service, "WeaponLoadoutService should be added to the player automatically");
        Assert.IsNotNull(menu, "TestRange player should have the pause menu");
        Assert.IsNotNull(menu.GetComponent<WeaponSelectionPage>());
        Assert.IsNotNull(LoadoutMenuPages.Primary);
        Assert.IsNotNull(service.Catalog);
        Assert.GreaterOrEqual(service.Catalog.weapons.Length, 2);
        Assert.IsNotNull(service.GetEquippedWeapon(SlotType.Primary));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SelectPrimary_SpawnsThatWeapon_AndEquipsPrimarySlot()
    {
        LoadoutWeaponEntry dmr = service.Catalog.FindWeapon("DMRSniper");
        Assert.IsNotNull(dmr);

        Assert.IsTrue(service.SelectPrimary(dmr));
        yield return null;

        WeaponController primary = service.GetEquippedWeapon(SlotType.Primary);
        Assert.IsNotNull(primary);
        Assert.AreSame(dmr.config, primary.Config);
        Assert.AreEqual(0, service.WeaponSwitcher.CurrentSlotIndex);
        Assert.AreEqual("DMRSniper", service.Selection.PrimaryId);
        Assert.AreNotEqual("DMRSniper", service.Selection.SecondaryId);
    }

    [UnityTest]
    public IEnumerator SelectSecondary_Karambit_SpawnsMelee_AsSecondarySlot()
    {
        LoadoutWeaponEntry karambit = service.Catalog.FindWeapon("Karambit");
        Assert.IsNotNull(karambit);

        Assert.IsTrue(service.SelectSecondary(karambit));
        yield return null;

        WeaponController secondary = service.GetEquippedWeapon(SlotType.Secondary);
        Assert.IsNotNull(secondary);
        Assert.IsTrue(secondary.Config != null && secondary.Config.IsMelee, "Karambit should be the secondary weapon");
        Assert.AreEqual(1, service.WeaponSwitcher.CurrentSlotIndex);
        Assert.AreEqual("Karambit", service.Selection.SecondaryId);
    }

    [UnityTest]
    public IEnumerator SelectingTheSecondaryAsPrimary_SwapsSlots()
    {
        LoadoutWeaponEntry primaryBefore = service.Catalog.FindWeapon(service.Selection.PrimaryId);
        LoadoutWeaponEntry secondaryBefore = service.Catalog.FindWeapon(service.Selection.SecondaryId);
        Assume.That(secondaryBefore != null && secondaryBefore.allowPrimary, "Class secondary cannot be primary");

        Assert.IsTrue(service.SelectPrimary(secondaryBefore));
        yield return null;

        Assert.AreEqual(secondaryBefore.Id, service.Selection.PrimaryId);
        Assert.AreEqual(primaryBefore.Id, service.Selection.SecondaryId);
        Assert.AreSame(secondaryBefore.config, service.GetEquippedWeapon(SlotType.Primary).Config);
        Assert.AreSame(primaryBefore.config, service.GetEquippedWeapon(SlotType.Secondary).Config);
    }

    [UnityTest]
    public IEnumerator MeleeCannotBePrimary()
    {
        LoadoutWeaponEntry karambit = service.Catalog.FindWeapon("Karambit");
        string primaryBefore = service.Selection.PrimaryId;

        Assert.IsFalse(service.SelectPrimary(karambit));
        yield return null;

        Assert.AreEqual(primaryBefore, service.Selection.PrimaryId);
    }

    [UnityTest]
    public IEnumerator SelectSkin_AppliesToEquippedWeapon_AndCanBeReset()
    {
        string primaryId = service.Selection.PrimaryId;
        WeaponSkinDefinition skin = service.Catalog.FindSkin("Skin_Damascus");
        Assert.IsNotNull(skin);

        Assert.IsTrue(service.SelectSkin(primaryId, skin));
        yield return null;

        WeaponController primary = service.GetEquippedWeapon(SlotType.Primary);
        WeaponSkinApplier applier = primary.GetComponentInChildren<WeaponSkinApplier>(true);
        Assert.IsNotNull(applier);
        Assert.AreSame(skin, applier.CurrentSkin);
        Assert.AreSame(skin, service.GetSelectedSkin(primaryId));

        Assert.IsTrue(service.SelectSkin(primaryId, null));
        yield return null;

        Assert.IsNull(service.GetSelectedSkin(primaryId));
        Assert.AreNotSame(skin, applier.CurrentSkin);
    }

    [UnityTest]
    public IEnumerator SkinChoice_SurvivesChangingWeaponsAndBack()
    {
        LoadoutWeaponEntry m4 = service.Catalog.FindWeapon("M4A1");
        LoadoutWeaponEntry dmr = service.Catalog.FindWeapon("DMRSniper");
        WeaponSkinDefinition skin = service.Catalog.FindSkin("Skin_Prism");

        service.SelectPrimary(m4);
        service.SelectSkin("M4A1", skin);
        service.SelectPrimary(dmr);
        yield return null;
        service.SelectPrimary(m4);
        yield return null;

        WeaponSkinApplier applier = service.GetEquippedWeapon(SlotType.Primary).GetComponentInChildren<WeaponSkinApplier>(true);
        Assert.IsNotNull(applier);
        Assert.AreSame(skin, applier.CurrentSkin);
    }

    [UnityTest]
    public IEnumerator Selection_PersistsAcrossSceneReload()
    {
        LoadoutWeaponEntry dmr = service.Catalog.FindWeapon("DMRSniper");
        LoadoutWeaponEntry karambit = service.Catalog.FindWeapon("Karambit");
        WeaponSkinDefinition skin = service.Catalog.FindSkin("Skin_NightViper");

        service.SelectPrimary(dmr);
        service.SelectSecondary(karambit);
        service.SelectSkin("DMRSniper", skin);
        Assert.IsTrue(PlayerPrefs.HasKey(PlayerPrefsLoadoutStore.DefaultKey));

        yield return LoadTestRange();

        Assert.IsNotNull(service);
        Assert.IsTrue(service.HasSavedSelection);
        Assert.AreEqual("DMRSniper", service.Selection.PrimaryId);
        Assert.AreEqual("Karambit", service.Selection.SecondaryId);

        WeaponController primary = service.GetEquippedWeapon(SlotType.Primary);
        Assert.AreSame(dmr.config, primary.Config);
        Assert.IsTrue(service.GetEquippedWeapon(SlotType.Secondary).Config.IsMelee);
        Assert.AreSame(skin, primary.GetComponentInChildren<WeaponSkinApplier>(true).CurrentSkin);
    }

    [UnityTest]
    public IEnumerator SelectClass_AdoptsClassWeapons_AndPersists()
    {
        ClassDefinition sniper = service.Catalog.FindClass("Sniper");
        Assert.IsNotNull(sniper);

        Assert.IsTrue(service.SelectClass(sniper));
        yield return null;

        Assert.AreEqual("Sniper", service.Selection.ClassId);
        Assert.AreEqual(sniper.GetPrimaryPrefab().name, service.Selection.PrimaryId);
        Assert.AreEqual(sniper.GetSecondaryPrefab().name, service.Selection.SecondaryId);
        Assert.IsTrue(LoadoutSelection.TryLoad(new PlayerPrefsLoadoutStore(), out LoadoutSelection saved));
        Assert.AreEqual("Sniper", saved.ClassId);
    }

    [UnityTest]
    public IEnumerator ClassPickedElsewhere_IsAdoptedAndPersisted()
    {
        ClassDefinition smg = service.Catalog.FindClass("SMG");
        Assert.IsNotNull(smg);

        var selector = Object.FindAnyObjectByType<TestRangeClassSelector>();
        Assert.IsNotNull(selector);
        Assert.IsTrue(selector.ApplyClass(smg));
        yield return null;

        Assert.AreEqual("SMG", service.Selection.ClassId);
        Assert.IsTrue(LoadoutSelection.TryLoad(new PlayerPrefsLoadoutStore(), out LoadoutSelection saved));
        Assert.AreEqual("SMG", saved.ClassId);
    }

    [UnityTest]
    public IEnumerator ResetToDefaults_ClearsSavedSelection()
    {
        service.SelectPrimary(service.Catalog.FindWeapon("DMRSniper"));
        yield return null;
        Assert.IsTrue(PlayerPrefs.HasKey(PlayerPrefsLoadoutStore.DefaultKey));

        Assert.IsTrue(service.ResetToDefaults());
        yield return null;

        Assert.IsFalse(PlayerPrefs.HasKey(PlayerPrefsLoadoutStore.DefaultKey));
        Assert.IsFalse(service.HasSavedSelection);
    }

    [UnityTest]
    public IEnumerator Menu_OpenAndClose_RestoresCursorAndTimeScale()
    {
        menu.OpenTab(LoadoutPauseMenu.MenuTab.Loadout);
        yield return null;

        Assert.IsTrue(menu.IsOpen);
        Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
        Assert.IsTrue(Cursor.visible);
        WeaponSelectionPage page = menu.GetComponent<WeaponSelectionPage>();
        Assert.IsTrue(page.IsShown);
        Assert.IsNotNull(page.PreviewRig);

        menu.SetOpen(false);
        yield return null;

        Assert.IsFalse(menu.IsOpen);
        Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);
        Assert.IsFalse(Cursor.visible);
        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsFalse(page.IsShown);
    }

    [UnityTest]
    public IEnumerator Menu_WithPauseTimeScale_RestoresPreviousTimeScale()
    {
        SetPauseTimeScale(menu, true);
        Time.timeScale = 1f;

        menu.OpenTab(LoadoutPauseMenu.MenuTab.Loadout);
        yield return null;
        Assert.AreEqual(0f, Time.timeScale);

        menu.SetOpen(false);
        yield return null;
        Assert.AreEqual(1f, Time.timeScale);
        Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);
    }

    [UnityTest]
    public IEnumerator Menu_ToggleLoadoutTab_OpensThenClosesOnSameTab_AndSwitchesToMap()
    {
        menu.ToggleTab(LoadoutPauseMenu.MenuTab.Loadout);
        yield return null;
        Assert.IsTrue(menu.IsOpen);
        Assert.AreEqual(LoadoutPauseMenu.MenuTab.Loadout, menu.CurrentTab);

        menu.ToggleTab(LoadoutPauseMenu.MenuTab.Map);
        yield return null;
        Assert.IsTrue(menu.IsOpen);
        Assert.AreEqual(LoadoutPauseMenu.MenuTab.Map, menu.CurrentTab);
        Assert.IsFalse(menu.GetComponent<WeaponSelectionPage>().IsShown);

        menu.ToggleTab(LoadoutPauseMenu.MenuTab.Map);
        yield return null;
        Assert.IsFalse(menu.IsOpen);
        Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);
    }

    [UnityTest]
    public IEnumerator Menu_SelectionChangedWhileOpen_StillAppliesAfterClose()
    {
        menu.OpenTab(LoadoutPauseMenu.MenuTab.Loadout);
        yield return null;

        Assert.IsTrue(service.SelectPrimary(service.Catalog.FindWeapon("BoltActionSniper")));
        menu.SetOpen(false);
        yield return null;

        Assert.AreSame(service.Catalog.FindWeapon("BoltActionSniper").config, service.GetEquippedWeapon(SlotType.Primary).Config);
    }

    [UnityTest]
    public IEnumerator PreviewRig_BuildsMeshesForGunAndKarambit_AndAppliesSkin()
    {
        using (var rig = new WeaponPreviewRig(160, 90))
        {
            LoadoutWeaponEntry gun = service.Catalog.FindWeapon("M4A1");
            rig.SetPrefab(gun.prefab);
            Assert.Greater(rig.PartCount, 0, "Gun preview should contain renderers");
            Assert.AreSame(gun.prefab, rig.CurrentPrefab);

            WeaponSkinDefinition skin = service.Catalog.FindSkin("Skin_CarbonFiber");
            rig.SetSkin(skin);
            Assert.AreSame(skin, rig.ShownSkin);

            rig.SetActive(true);
            rig.Tick(0.1f);
            yield return null;
            Assert.IsTrue(rig.IsActive);
            Assert.IsNotNull(rig.Texture);

            LoadoutWeaponEntry knife = service.Catalog.FindWeapon("Karambit");
            rig.SetPrefab(knife.prefab);
            Assert.Greater(rig.PartCount, 0, "Karambit preview should contain generated meshes");
            Assert.AreSame(knife.prefab, rig.CurrentPrefab);
        }

        yield return null;
        Assert.IsNull(GameObject.Find("WeaponPreviewRig"), "Disposing the rig must remove its scene objects");
    }

    [UnityTest]
    public IEnumerator PreviewRig_CameraIsOffWhenMenuCloses()
    {
        menu.OpenTab(LoadoutPauseMenu.MenuTab.Loadout);
        yield return null;
        yield return null;

        WeaponSelectionPage page = menu.GetComponent<WeaponSelectionPage>();
        Assert.IsNotNull(page.PreviewRig);
        Assert.IsTrue(page.PreviewRig.IsActive);

        menu.SetOpen(false);
        yield return null;

        Assert.IsNotNull(page.PreviewRig);
        Assert.IsFalse(page.PreviewRig.IsActive, "Preview camera must be off when the menu is closed");
    }
}
