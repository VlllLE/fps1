using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The player's gun must only fire for a button press that gameplay itself saw go down: never without input,
/// and never because a button was already held when input came back (menu closed, respawn, map load,
/// weapon swap, focus or cursor lock regained).
/// </summary>
public class FireInputGuardPlayModeTests
{
    private GameObject player;
    private InputHandler input;
    private PlayerWeaponBridge bridge;
    private WeaponSwitcher switcher;
    private LoadoutPauseMenu menu;
    private bool fireHeld;
    private int shots;
    private readonly List<WeaponController> subscribed = new List<WeaponController>();
    private readonly List<GameObject> spawned = new List<GameObject>();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return LoadPlayerScene("TestRange");
    }

    [TearDown]
    public void TearDown()
    {
        UnsubscribeAll();
        if (menu != null && menu.IsOpen)
            menu.SetOpen(false);
        if (input != null)
        {
            input.SimulateSprintHeld(false);
            input.SetMoveInput(Vector2.zero);
            input.SetCrouchHeld(false);
            input.SetTestInputLocked(false);
        }
        if (bridge != null)
        {
            bridge.SetInputSources(null, null, null);
            bridge.RequireLiveInput = true;
        }
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        foreach (GameObject go in spawned)
            if (go != null)
                Object.Destroy(go);
        spawned.Clear();
    }

    private IEnumerator LoadPlayerScene(string sceneName)
    {
        UnsubscribeAll();
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.5f);

        var movement = Object.FindAnyObjectByType<MovementController>();
        Assert.That(movement, Is.Not.Null, sceneName + " has a player");
        player = movement.gameObject;
        input = player.GetComponent<InputHandler>();
        bridge = player.GetComponent<PlayerWeaponBridge>();
        switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        menu = Object.FindAnyObjectByType<LoadoutPauseMenu>();
        Assert.That(bridge, Is.Not.Null, sceneName + " player has a weapon bridge");

        input.SetTestInputLocked(true);
        input.SetMoveInput(Vector2.zero);
        fireHeld = false;
        shots = 0;
        bridge.RequireLiveInput = false;
        bridge.SetInputSources(() => fireHeld, () => false, () => false);

        foreach (WeaponController weapon in player.GetComponentsInChildren<WeaponController>(true))
        {
            weapon.OnWeaponFired += CountShot;
            subscribed.Add(weapon);
        }
        yield return null;
    }

    private void CountShot() => shots++;

    private void UnsubscribeAll()
    {
        foreach (WeaponController weapon in subscribed)
            if (weapon != null)
                weapon.OnWeaponFired -= CountShot;
        subscribed.Clear();
    }

    private static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++)
            yield return null;
    }

    private IEnumerator WaitForSwitch()
    {
        float deadline = Time.realtimeSinceStartup + 4f;
        yield return null;
        while (switcher.IsSwitching && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return Frames(5);
    }

    private IEnumerator PressFire(int heldFrames = 3)
    {
        fireHeld = false;
        yield return Frames(3);
        fireHeld = true;
        yield return Frames(heldFrames);
        fireHeld = false;
        yield return Frames(2);
    }

    [UnityTest]
    public IEnumerator Sanity_AFreshPressStillFires()
    {
        yield return Frames(10);
        yield return PressFire();
        Assert.That(shots, Is.GreaterThan(0), "a real press must still fire");
    }

    [UnityTest]
    public IEnumerator NoInput_NeverFires_AcrossMovementStates()
    {
        yield return Frames(10);
        input.SetMoveInput(Vector2.up);
        yield return Frames(40);

        input.SimulateSprintHeld(true);
        yield return Frames(40);

        input.SimulateCrouchPress();
        yield return Frames(40);

        input.SimulateSprintHeld(false);
        input.SetCrouchHeld(true);
        yield return Frames(40);
        input.SetCrouchHeld(false);

        input.SimulateJumpPress();
        yield return Frames(40);

        input.SetMoveInput(new Vector2(1f, 0f));
        yield return Frames(30);

        Assert.That(shots, Is.Zero, "the gun fired without any fire input while moving");
    }

    [UnityTest]
    public IEnumerator NoInput_NeverFires_WithTheRealMouseAndKeyboardReaders()
    {
        bridge.SetInputSources(null, null, null);
        bridge.RequireLiveInput = true;
        input.SetMoveInput(Vector2.up);
        input.SimulateSprintHeld(true);
        yield return Frames(60);
        Assert.That(shots, Is.Zero);
    }

    [UnityTest]
    public IEnumerator MenuOpenAndClose_NeverFires()
    {
        Assume.That(menu, Is.Not.Null, "scene has no loadout menu");
        yield return Frames(10);

        for (int i = 0; i < 3; i++)
        {
            menu.SetOpen(true);
            yield return Frames(10);
            menu.SetOpen(false);
            yield return Frames(10);
        }

        Assert.That(shots, Is.Zero);
    }

    [UnityTest]
    public IEnumerator ButtonHeldWhileTheMenuCloses_DoesNotFireUntilReleasedAndPressedAgain()
    {
        Assume.That(menu, Is.Not.Null, "scene has no loadout menu");
        yield return Frames(10);
        menu.SetOpen(true);
        yield return Frames(5);

        fireHeld = true;
        menu.SetOpen(false);
        yield return Frames(20);
        Assert.That(shots, Is.Zero, "a button already down when the menu closed must not shoot");

        fireHeld = false;
        yield return Frames(3);
        yield return PressFire();
        Assert.That(shots, Is.GreaterThan(0), "the next real press fires again");
    }

    [UnityTest]
    public IEnumerator ButtonHeldWhileTheBridgeIsDisabled_DoesNotFireOnRespawn()
    {
        yield return Frames(10);
        fireHeld = true;
        yield return Frames(3);
        int before = shots;

        bridge.enabled = false;
        yield return Frames(5);
        bridge.enabled = true;
        yield return Frames(20);
        Assert.That(shots, Is.EqualTo(before), "re-enabling the bridge with the button held must not start a new shot");

        fireHeld = false;
        yield return Frames(3);
        yield return PressFire();
        Assert.That(shots, Is.GreaterThan(before));
    }

    [UnityTest]
    public IEnumerator WeaponSwitch_WithButtonHeld_DoesNotFireTheNewWeapon()
    {
        Assume.That(switcher, Is.Not.Null);
        yield return Frames(10);
        WeaponController first = switcher.CurrentWeapon;
        switcher.SwitchToSlot(1);
        yield return WaitForSwitch();
        if (switcher.CurrentWeapon == first)
        {
            switcher.SwitchToSlot(0);
            yield return WaitForSwitch();
        }
        Assume.That(switcher.CurrentWeapon, Is.Not.SameAs(first), "loadout has a second weapon");

        fireHeld = true;
        yield return Frames(3);
        int before = shots;

        switcher.SwitchToLastWeapon();
        yield return WaitForSwitch();
        yield return Frames(20);
        Assume.That(switcher.CurrentWeapon, Is.SameAs(first), "swapped back");
        Assert.That(shots, Is.EqualTo(before), "the weapon swapped in must not fire from a button held across the swap");
    }

    [UnityTest]
    public IEnumerator SwitchingWeapons_WithoutInput_NeverFires()
    {
        Assume.That(switcher, Is.Not.Null);
        yield return Frames(10);
        for (int i = 0; i < 6; i++)
        {
            switcher.SwitchToSlot(i % 2);
            yield return WaitForSwitch();
        }
        Assert.That(shots, Is.Zero);
    }

    [UnityTest]
    public IEnumerator CursorUnlocked_IgnoresTheButton_AndRelockingWithItHeldDoesNotFire()
    {
        bridge.RequireLiveInput = true;
        Cursor.lockState = CursorLockMode.None;
        yield return Frames(5);

        fireHeld = true;
        yield return Frames(10);
        Assert.That(shots, Is.Zero, "no shooting while the cursor is free (clicking back into the window)");

        Cursor.lockState = CursorLockMode.Locked;
        yield return Frames(20);
        Assert.That(shots, Is.Zero, "the click that relocks the cursor must not shoot");

        fireHeld = false;
        yield return Frames(3);
        yield return PressFire();
        if (!Application.isFocused)
            Assert.Ignore("Editor window is unfocused, so live input is correctly withheld");
        Assert.That(shots, Is.GreaterThan(0));
    }

    [UnityTest]
    public IEnumerator EveryMap_StartsAndRunsWithoutFiring()
    {
        int checkedMaps = 0;
        for (int i = 0; i < MapCatalog.Count; i++)
        {
            string scene = MapCatalog.Maps[i].SceneName;
            if (!MapCatalog.IsInBuild(scene))
                continue;

            yield return LoadPlayerScene(scene);
            input.SetMoveInput(Vector2.up);
            input.SimulateSprintHeld(true);
            yield return Frames(45);
            input.SimulateSprintHeld(false);
            input.SetMoveInput(Vector2.zero);
            Assert.That(shots, Is.Zero, scene + ": the gun fired by itself");
            checkedMaps++;
        }

        Assert.That(checkedMaps, Is.GreaterThan(0));
    }

    [UnityTest]
    public IEnumerator MapChange_WithButtonHeld_DoesNotFireInTheNewMap()
    {
        int target = -1;
        for (int i = 0; i < MapCatalog.Count; i++)
        {
            string scene = MapCatalog.Maps[i].SceneName;
            if (scene != SceneManager.GetActiveScene().name && MapCatalog.IsInBuild(scene))
            {
                target = i;
                break;
            }
        }
        Assume.That(target, Is.GreaterThanOrEqualTo(0), "only one map in Build Settings");

        fireHeld = true;
        UnsubscribeAll();
        string next = MapCatalog.Maps[target].SceneName;
        void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            shots = 0;
            var movement = Object.FindAnyObjectByType<MovementController>();
            bridge = movement.GetComponent<PlayerWeaponBridge>();
            input = movement.GetComponent<InputHandler>();
            bridge.RequireLiveInput = false;
            bridge.SetInputSources(() => fireHeld, () => false, () => false);
            foreach (WeaponController weapon in movement.GetComponentsInChildren<WeaponController>(true))
            {
                weapon.OnWeaponFired += CountShot;
                subscribed.Add(weapon);
            }
        }

        SceneManager.sceneLoaded += OnLoaded;
        SceneManager.LoadScene(next, LoadSceneMode.Single);
        yield return null;
        SceneManager.sceneLoaded -= OnLoaded;
        input.SetTestInputLocked(true);
        yield return new WaitForSeconds(0.5f);
        yield return Frames(30);
        Assert.That(shots, Is.Zero, "a button held through the map change must not shoot in " + next);
    }

    private WeaponController CreateBurstWeapon(out WeaponConfig config)
    {
        var root = new GameObject("BurstTestRoot");
        spawned.Add(root);
        if (Object.FindAnyObjectByType<HitDetection>() == null)
            root.AddComponent<HitDetection>();
        var cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(root.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;

        config = ScriptableObject.CreateInstance<WeaponConfig>();
        config.fireRate = 600f;
        config.availableFireModes = new[] { FireMode.Burst };
        config.burstCount = 3;
        config.burstDelay = 0.05f;
        config.magazineSize = 30;

        var weaponObject = new GameObject("BurstGun");
        weaponObject.transform.SetParent(root.transform, false);
        WeaponController weapon = weaponObject.AddComponent<WeaponController>();
        weapon.SetPlayerCamera(camera);
        weapon.Initialize(config);
        weapon.Equip();
        return weapon;
    }

    [UnityTest]
    public IEnumerator Burst_HolsteredMidBurst_StopsFiring()
    {
        WeaponController weapon = CreateBurstWeapon(out WeaponConfig config);
        int fired = 0;
        weapon.OnWeaponFired += () => fired++;
        yield return new WaitForSeconds(0.3f);

        weapon.StartFiring();
        weapon.StopFiring();
        int afterFirst = fired;
        Assert.That(afterFirst, Is.GreaterThanOrEqualTo(1));
        weapon.Holster();
        yield return new WaitForSeconds(0.6f);

        Assert.That(fired, Is.EqualTo(afterFirst), "a holstered weapon must not finish its burst");
        Object.Destroy(config);
    }

    [UnityTest]
    public IEnumerator Burst_InterruptedByDeactivation_CanFireAgainWhenEquipped()
    {
        WeaponController weapon = CreateBurstWeapon(out WeaponConfig config);
        int fired = 0;
        weapon.OnWeaponFired += () => fired++;
        yield return new WaitForSeconds(0.3f);

        weapon.StartFiring();
        weapon.StopFiring();
        weapon.gameObject.SetActive(false);
        yield return null;
        weapon.Equip();
        yield return new WaitForSeconds(0.6f);

        int before = fired;
        weapon.StartFiring();
        weapon.StopFiring();
        yield return new WaitForSeconds(0.6f);

        Assert.That(fired, Is.GreaterThan(before), "a burst cut short by a weapon swap must not leave the weapon unable to fire");
        Object.Destroy(config);
    }
}
