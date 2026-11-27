using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class LocalBodyHiddenPlayModeTests
{
    private GameObject player;
    private InputHandler input;
    private MovementController movement;
    private FirstPersonCamera look;
    private Camera playerCamera;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        SceneManager.LoadScene("TestRange", LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.5f);

        movement = Object.FindAnyObjectByType<MovementController>();
        Assert.That(movement, Is.Not.Null, "the TestRange scene has a player");
        player = movement.gameObject;
        input = player.GetComponent<InputHandler>();
        look = player.GetComponentInChildren<FirstPersonCamera>();
        playerCamera = player.GetComponentInChildren<Camera>();
        input.SetTestInputLocked(true);
        input.SetMoveInput(Vector2.zero);
        input.SetCrouchHeld(false);
        yield return new WaitForSeconds(0.5f);
    }

    [TearDown]
    public void TearDown()
    {
        if (input != null)
        {
            input.SimulateSprintHeld(false);
            input.SetMoveInput(Vector2.zero);
            input.SetCrouchHeld(false);
            input.SetTestInputLocked(false);
        }
    }

    [UnityTest]
    public IEnumerator Standing_NoBodyPartIsVisibleToTheFirstPersonCamera()
    {
        yield return Settle(20);
        AssertNoOwnBodyVisible("standing");
    }

    [UnityTest]
    public IEnumerator Crouched_NoBodyPartIsVisibleToTheFirstPersonCamera()
    {
        input.SetCrouchHeld(true);
        yield return Settle(60);

        Assert.That(player.GetComponent<CharacterController>().height, Is.LessThan(1.5f), "the player is actually crouched");
        AssertNoOwnBodyVisible("crouched");
    }

    [UnityTest]
    public IEnumerator Crouched_LookingDownOrAhead_NoBodyPartIsVisible()
    {
        input.SetCrouchHeld(true);
        yield return Settle(40);

        foreach (float pitch in new[] { 85f, 45f, 0f, -60f })
        {
            look.SetLook(player.transform.eulerAngles.y, pitch);
            yield return Settle(5);
            AssertNoOwnBodyVisible("crouched, pitch " + pitch);
        }
    }

    [UnityTest]
    public IEnumerator CrouchWalking_NoBodyPartIsVisible()
    {
        input.SetCrouchHeld(true);
        input.SetMoveInput(Vector2.up);
        yield return Settle(40);

        AssertNoOwnBodyVisible("crouch walking");
    }

    [UnityTest]
    public IEnumerator SprintAndSlide_NoBodyPartIsVisible()
    {
        input.SetMoveInput(Vector2.up);
        input.SimulateSprintHeld(true);
        yield return Settle(30);
        AssertNoOwnBodyVisible("sprinting");

        input.SimulateCrouchPress();
        for (int i = 0; i < 12; i++)
        {
            yield return null;
            AssertNoOwnBodyVisible("sliding frame " + i + " (" + movement.CurrentState + ")");
        }
    }

    [UnityTest]
    public IEnumerator Airborne_NoBodyPartIsVisible()
    {
        input.SimulateJumpPress();
        for (int i = 0; i < 10; i++)
        {
            yield return null;
            AssertNoOwnBodyVisible("jumping frame " + i);
        }
    }

    [UnityTest]
    public IEnumerator SwitchingWeapons_KeepsTheHeldThirdPersonGunHidden()
    {
        HumanoidBody body = player.GetComponent<HumanoidBody>();
        Assert.That(body, Is.Not.Null);
        WeaponSwitcher switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        yield return Settle(10);

        if (switcher != null)
        {
            switcher.SwitchToSlot(1);
            yield return Settle(10);
            switcher.SwitchToSlot(0);
            yield return Settle(10);
        }

        input.SetCrouchHeld(true);
        yield return Settle(30);
        AssertNoOwnBodyVisible("crouched after switching weapons");
    }

    [UnityTest]
    public IEnumerator BodyMeshesStillCastShadowsAndExistForOtherCameras()
    {
        yield return Settle(10);
        HumanoidBody body = player.GetComponent<HumanoidBody>();
        Assert.That(body.BodyRoot.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(0));
        foreach (Renderer renderer in body.BodyRoot.GetComponentsInChildren<Renderer>())
        {
            Assert.That(renderer.enabled, Is.True, renderer.name + " stays enabled so shadows and third-person views still work");
            Assert.That(renderer.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("PlayerLocalHide")), renderer.name);
        }
    }

    private static IEnumerator Settle(int frames)
    {
        for (int i = 0; i < frames; i++)
            yield return null;
    }

    private void AssertNoOwnBodyVisible(string context)
    {
        var visible = new List<string>();
        int mask = playerCamera.cullingMask;
        foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(false))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;
            if (renderer is ParticleSystemRenderer)
                continue;
            if (renderer.transform.IsChildOf(playerCamera.transform))
                continue;
            if ((mask & (1 << renderer.gameObject.layer)) != 0)
                visible.Add(renderer.transform.name + " (layer " + LayerMask.LayerToName(renderer.gameObject.layer) + ")");
        }

        Assert.That(visible, Is.Empty, "[" + context + "] the first-person camera renders the player's own body: " + string.Join(", ", visible));
    }
}
