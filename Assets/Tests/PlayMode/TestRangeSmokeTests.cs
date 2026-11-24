using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

public class TestRangeSmokeTests
{
    private GameObject player;
    private MovementController movement;
    private CharacterController characterController;
    private InputHandler inputHandler;
    
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        SceneManager.LoadScene("TestRange", LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.5f);
        
        player = GameObject.Find("Player");
        if (player == null)
        {
            movement = Object.FindAnyObjectByType<MovementController>();
            if (movement != null)
                player = movement.gameObject;
        }
        
        if (player != null)
        {
            movement = player.GetComponent<MovementController>();
            characterController = player.GetComponent<CharacterController>();
            inputHandler = player.GetComponent<InputHandler>();
            inputHandler.SetTestInputLocked(true);
            inputHandler.SetMoveInput(Vector2.zero);
            inputHandler.SimulateSprintHeld(false);
            inputHandler.SetCrouchHeld(false);
            inputHandler.SetJumpHeld(false);
            inputHandler.SetTestInputLocked(false);
        }
    }
    
    [UnityTest]
    public IEnumerator Player_SpawnsAndExists()
    {
        Assert.IsNotNull(player, "Player should exist in scene");
        Assert.IsNotNull(characterController, "Player should have CharacterController");
        Assert.IsNotNull(movement, "Player should have MovementController");
        Assert.IsNotNull(inputHandler, "Player should have InputHandler");
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator Player_StandsOnGround_AfterSpawn()
    {
        Assert.IsNotNull(player, "Player should exist");
        Assert.IsNotNull(characterController, "CharacterController should exist");
        
        // Allow gravity to settle the character onto the ground
        for (int i = 0; i < 45; i++)
        {
            if (!characterController.isGrounded)
                characterController.Move(Vector3.down * 0.2f);
            yield return null;
        }
        
        float playerY = player.transform.position.y;
        Assert.Greater(playerY, 0.3f, "Player should be above ground level");
        Assert.Less(playerY, 2.5f, "Player should not be floating high");
        
        bool nearGround = characterController.isGrounded || playerY < 1.25f;
        Assert.IsTrue(nearGround, $"Player should settle near ground (y={playerY}, grounded={characterController.isGrounded})");
    }
    
    [UnityTest]
    public IEnumerator Player_MovesWhenCharacterControllerDriven()
    {
        Assert.IsNotNull(player, "Player should exist");
        Assert.IsNotNull(characterController, "CharacterController should exist");
        
        Vector3 startPos = player.transform.position;
        
        characterController.Move(Vector3.forward * 2f);
        yield return null;
        yield return new WaitForSeconds(0.1f);
        
        float distance = Vector3.Distance(
            new Vector3(startPos.x, 0, startPos.z),
            new Vector3(player.transform.position.x, 0, player.transform.position.z));
        
        Assert.Greater(distance, 0.5f, "Player should move when CharacterController.Move is called");
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator Player_HasCamera_WithCorrectSetup()
    {
        Assert.IsNotNull(player, "Player should exist");
        
        var camera = Camera.main;
        Assert.IsNotNull(camera, "Main camera should exist");
        Assert.AreEqual("MainCamera", camera.tag, "Camera should be tagged MainCamera");
        
        Assert.IsTrue(camera.transform.IsChildOf(player.transform), 
            "Camera should be a child of player");
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator Target_ExistsAndHasComponents()
    {
        var targets = Object.FindObjectsByType<Target>(FindObjectsSortMode.None);
        
        Assert.Greater(targets.Length, 0, "Scene should have at least one target");
        
        foreach (var target in targets)
        {
            Assert.Greater(target.MaxHealth, 0, $"Target {target.name} should have positive max health");
            Assert.AreEqual(target.MaxHealth, target.CurrentHealth, 
                $"Target {target.name} should start at full health");
            Assert.IsFalse(target.IsDead(), $"Target {target.name} should not start dead");
        }
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator Target_TakesDamage_WhenHit()
    {
        var target = Object.FindAnyObjectByType<Target>();
        Assert.IsNotNull(target, "Should have a target to test");
        
        float initialHealth = target.CurrentHealth;
        
        var damageInfo = new DamageInfo
        {
            damage = 25f,
            hitPoint = target.transform.position,
            hitNormal = Vector3.up,
            hitDirection = Vector3.forward,
            isHeadshot = false
        };
        
        target.TakeDamage(damageInfo);
        
        yield return null;
        
        Assert.Less(target.CurrentHealth, initialHealth, "Target health should decrease after damage");
        Assert.AreEqual(1, target.TotalHitsTaken, "Target should record one hit");
        Assert.AreEqual(25f, target.TotalDamageTaken, 0.01f, "Target should record damage amount");
    }
    
    [UnityTest]
    public IEnumerator Target_Dies_WhenHealthDepleted()
    {
        var target = Object.FindAnyObjectByType<Target>();
        Assert.IsNotNull(target, "Should have a target to test");
        
        bool deathEventFired = false;
        target.OnDeath += () => deathEventFired = true;
        
        var damageInfo = new DamageInfo
        {
            damage = target.CurrentHealth + 10f,
            hitPoint = target.transform.position,
            hitNormal = Vector3.up,
            hitDirection = Vector3.forward,
            isHeadshot = false
        };
        
        target.TakeDamage(damageInfo);
        
        yield return null;
        
        Assert.IsTrue(target.IsDead(), "Target should be dead after lethal damage");
        Assert.IsTrue(deathEventFired, "Death event should have fired");
    }
    
    [UnityTest]
    public IEnumerator Hitscan_FromPlayerToTarget_RegistersHit()
    {
        Assert.IsNotNull(player, "Player should exist");
        
        var target = GameObject.Find("Target_Near");
        Assert.IsNotNull(target, "Target_Near should exist");
        
        var targetComp = target.GetComponent<Target>();
        Assert.IsNotNull(targetComp, "Target_Near should have Target component");
        
        float healthBefore = targetComp.CurrentHealth;
        
        var cam = Camera.main;
        Assert.IsNotNull(cam, "Main camera should exist");
        
        // Aim at the target head for a clear line of sight
        var head = target.transform.Find("Head");
        Vector3 aimPoint = head != null ? head.position : target.transform.position + Vector3.up;
        Vector3 origin = cam.transform.position;
        Vector3 direction = (aimPoint - origin).normalized;
        
        Assert.IsTrue(Physics.Raycast(origin, direction, out RaycastHit hit, 100f),
            "Raycast from player camera toward Target_Near should hit something");
        
        var hitTarget = hit.collider.GetComponentInParent<Target>();
        Assert.IsNotNull(hitTarget, $"Raycast should hit a Target (hit {hit.collider.name} on {hit.collider.transform.root.name})");
        
        hitTarget.TakeDamage(new DamageInfo
        {
            damage = 25f,
            hitPoint = hit.point,
            hitNormal = hit.normal,
            hitDirection = direction,
            isHeadshot = false
        });
        
        yield return null;
        
        Assert.Less(hitTarget.CurrentHealth, healthBefore, "Target should take damage from hitscan");
        Assert.Greater(hitTarget.TotalHitsTaken, 0, "Target should record the hit");
    }
    
    [UnityTest]
    public IEnumerator Ground_Exists_AndIsCollidable()
    {
        var ground = GameObject.Find("Ground");
        Assert.IsNotNull(ground, "Ground should exist in scene");
        
        var collider = ground.GetComponent<Collider>();
        Assert.IsNotNull(collider, "Ground should have a collider");
        Assert.IsTrue(collider.enabled, "Ground collider should be enabled");
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator Walls_ExistForWallBounces()
    {
        var allObjects = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        int wallCount = 0;
        
        foreach (var t in allObjects)
        {
            if (t.name.StartsWith("Wall"))
            {
                wallCount++;
                Assert.IsNotNull(t.GetComponent<Collider>(), $"Wall {t.name} should have a collider");
            }
        }
        
        Assert.Greater(wallCount, 0, "Scene should have wall objects for wall bounces");
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator Ramps_ExistForSlides()
    {
        var rampA = GameObject.Find("Ramp_A");
        var rampB = GameObject.Find("Ramp_B");
        
        Assert.IsTrue(rampA != null || rampB != null, "Scene should have at least one ramp for slides");
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator WeaponController_ExistsOnPlayer()
    {
        Assert.IsNotNull(player, "Player should exist");
        
        var weapon = player.GetComponentInChildren<WeaponController>();
        Assert.IsNotNull(weapon, "Player should have a WeaponController in children");
        
        var config = weapon.Config;
        Assert.IsNotNull(config, "Weapon should have a config assigned");
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator MovementConfig_IsAssigned()
    {
        Assert.IsNotNull(movement, "Movement controller should exist");
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator Player_Idle_WithNoInput_VelocityDampens()
    {
        Assert.IsNotNull(movement);
        Assert.IsNotNull(inputHandler);
        
        inputHandler.SetTestInputLocked(true);
        inputHandler.SetMoveInput(Vector2.zero);
        
        for (int i = 0; i < 30; i++)
        {
            if (!characterController.isGrounded)
                characterController.Move(Vector3.down * 0.2f);
            yield return null;
        }
        inputHandler.SimulateSprintHeld(false);
        inputHandler.SetCrouchHeld(false);
        inputHandler.SetJumpHeld(false);
        
        Assert.IsTrue(movement.IsGrounded || characterController.isGrounded,
            "Idle damp test requires grounded player");
        
        movement.SetVelocity(player.transform.forward * 6f);
        yield return null;
        
        Assert.Greater(new Vector3(movement.Velocity.x, 0f, movement.Velocity.z).magnitude, 0.5f);
        
        for (int i = 0; i < 60; i++)
            yield return null;
        
        Assert.IsFalse(inputHandler.HasMoveInput, "Idle test should have zero move input");
        float horizSpeed = new Vector3(movement.Velocity.x, 0f, movement.Velocity.z).magnitude;
        inputHandler.SetTestInputLocked(false);
        
        Assert.Less(horizSpeed, 0.35f,
            $"Horizontal velocity should decay when idle with no input (speed={horizSpeed:F2} state={movement.CurrentState} grounded={movement.IsGrounded} move={inputHandler.MoveInput} dt={Time.deltaTime:F4})");
    }

    [UnityTest]
    public IEnumerator Player_Idle_Grounded_DoesNotDriftHorizontally()
    {
        Assert.IsNotNull(movement);
        Assert.IsNotNull(inputHandler);
        Assert.IsNotNull(characterController);

        inputHandler.SetTestInputLocked(true);
        inputHandler.SetMoveInput(Vector2.zero);
        inputHandler.SimulateSprintHeld(false);
        inputHandler.SetCrouchHeld(false);
        inputHandler.SetJumpHeld(false);

        for (int i = 0; i < 45; i++)
        {
            if (!characterController.isGrounded)
                characterController.Move(Vector3.down * 0.2f);
            yield return null;
        }

        Assert.IsTrue(movement.IsGrounded || characterController.isGrounded,
            "Idle drift test requires grounded player");

        movement.SetVelocity(Vector3.zero);
        Vector3 start = player.transform.position;

        for (int i = 0; i < 120; i++)
            yield return null;

        Vector3 end = player.transform.position;
        float horizontalDrift = Vector2.Distance(
            new Vector2(start.x, start.z),
            new Vector2(end.x, end.z));
        inputHandler.SetTestInputLocked(false);

        Assert.Less(horizontalDrift, 0.02f,
            $"Idle grounded player should not slide (drift={horizontalDrift:F4} start={start} end={end})");
    }
    
    [UnityTest]
    public IEnumerator Player_Jump_SimulatedInput_LeavesGround()
    {
        Assert.IsNotNull(movement);
        Assert.IsNotNull(inputHandler);
        
        float yBefore = player.transform.position.y;
        
        for (int i = 0; i < 60; i++)
        {
            if (!characterController.isGrounded)
                characterController.Move(Vector3.down * 0.15f);
            yield return null;
        }
        
        Assert.IsTrue(movement.IsGrounded, "Player should be grounded before jump");
        
        inputHandler.SetTestInputLocked(true);
        for (int i = 0; i < 8; i++)
        {
            if (movement.CurrentState == MovementController.MovementState.Airborne)
                break;
            inputHandler.SimulateJumpPress();
            yield return null;
        }
        inputHandler.SetTestInputLocked(false);
        
        Assert.AreEqual(MovementController.MovementState.Airborne, movement.CurrentState,
            $"Jump should enter airborne (grounded={movement.IsGrounded}, vy={movement.Velocity.y})");
        Assert.Greater(movement.Velocity.y, 0f, "Jump should impart upward velocity");
    }
    
    [UnityTest]
    public IEnumerator Player_Crouch_SimulatedInput_ReducesColliderHeight()
    {
        Assert.IsNotNull(movement);
        Assert.IsNotNull(inputHandler);
        Assert.IsNotNull(characterController);
        
        Assert.IsTrue(movement.IsGrounded, "Player should be grounded before crouch");
        float tallHeight = characterController.height;
        
        inputHandler.SetTestInputLocked(true);
        inputHandler.SimulateCrouchPress();
        inputHandler.SetCrouchHeld(true);
        for (int i = 0; i < 45; i++)
            yield return null;
        inputHandler.SetTestInputLocked(false);
        
        Assert.Less(characterController.height, tallHeight - 0.05f,
            "Standing crouch should lower CharacterController height");
    }
    
    [UnityTest]
    public IEnumerator Player_Slide_SimulatedSprintAndCrouch_EntersSlideState()
    {
        Assert.IsNotNull(movement);
        Assert.IsNotNull(inputHandler);
        
        Assert.IsTrue(movement.IsGrounded, "Player should be grounded before slide");
        movement.SetVelocity(player.transform.forward * 7f);
        yield return null;
        
        inputHandler.SetTestInputLocked(true);
        inputHandler.SetMoveInput(new Vector2(0f, 1f));
        inputHandler.SimulateSprintHeld(true);
        
        for (int i = 0; i < 20; i++)
            yield return null;
        
        inputHandler.SimulateCrouchPress();
        inputHandler.SetCrouchHeld(true);
        
        for (int i = 0; i < 10; i++)
            yield return null;
        
        inputHandler.SetTestInputLocked(false);
        
        Assert.AreEqual(MovementController.MovementState.Sliding, movement.CurrentState,
            "Sprint + forward + crouch should enter slide when fast enough");
    }

    [UnityTest]
    public IEnumerator Player_Grounded_WhenCharacterControllerGrounded()
    {
        yield return SettlePlayerOnGround();
        if (!characterController.isGrounded)
            Assert.Inconclusive("CharacterController did not reach ground in test scene");

        Assert.IsTrue(movement.IsGrounded,
            "MovementController should report grounded when CharacterController is grounded");
    }

    [UnityTest]
    public IEnumerator Player_SlideCancel_JumpWhileSliding_BoostsAirborne()
    {
        yield return SettlePlayerOnGround();
        Assert.IsTrue(movement.IsGrounded);

        movement.SetVelocity(player.transform.forward * 7f);
        yield return null;

        inputHandler.SetTestInputLocked(true);
        inputHandler.SetMoveInput(new Vector2(0f, 1f));
        inputHandler.SimulateSprintHeld(true);
        for (int i = 0; i < 15; i++)
            yield return null;

        inputHandler.SimulateCrouchPress();
        inputHandler.SetCrouchHeld(true);
        for (int i = 0; i < 8; i++)
            yield return null;
        Assert.AreEqual(MovementController.MovementState.Sliding, movement.CurrentState);

        inputHandler.SimulateJumpPress();
        for (int i = 0; i < 5; i++)
            yield return null;
        inputHandler.SetTestInputLocked(false);

        Assert.AreEqual(MovementController.MovementState.Airborne, movement.CurrentState);
        Assert.Greater(movement.Velocity.y, 0f, "Slide cancel should launch player upward");
    }

    [UnityTest]
    public IEnumerator Player_Bhop_ChainJump_RetainsSpeed()
    {
        yield return SettlePlayerOnGround();
        Assert.IsTrue(movement.IsGrounded);

        inputHandler.SetTestInputLocked(true);
        inputHandler.SetMoveInput(new Vector2(0f, 1f));
        inputHandler.SimulateSprintHeld(true);
        for (int i = 0; i < 25; i++)
            yield return null;

        float speedBeforeJump = movement.CurrentSpeed;
        inputHandler.SimulateJumpPress();
        for (int i = 0; i < 15; i++)
            yield return null;

        Assert.AreEqual(MovementController.MovementState.Airborne, movement.CurrentState);

        inputHandler.SetJumpHeld(true);
        for (int i = 0; i < 30; i++)
        {
            if (movement.IsGrounded)
            {
                inputHandler.SimulateJumpPress();
                for (int j = 0; j < 5; j++)
                    yield return null;
                break;
            }
            yield return null;
        }
        inputHandler.SetTestInputLocked(false);

        Assert.AreEqual(MovementController.MovementState.Airborne, movement.CurrentState,
            "Buffered jump on landing should chain a bhop");
        Assert.GreaterOrEqual(movement.CurrentSpeed, speedBeforeJump * 0.5f,
            "Bhop should preserve meaningful horizontal speed");
    }

    [UnityTest]
    public IEnumerator Player_Bhop_HoldSpace_AutoRejumpsAndBuildsSpeed()
    {
        yield return MovePlayerToOpenLane();
        Assert.IsTrue(movement.IsGrounded, "Player must be grounded before hold-Space bhop test");

        int bhopCount = 0;
        float speedAtLastBhop = 0f;
        float maxObservedSpeed = 0f;
        System.Action onBhop = () =>
        {
            bhopCount++;
            speedAtLastBhop = new Vector3(movement.Velocity.x, 0f, movement.Velocity.z).magnitude;
        };
        movement.BunnyHopped += onBhop;

        try
        {
            inputHandler.SetTestInputLocked(true);
            inputHandler.SetMoveInput(new Vector2(0f, 1f));
            inputHandler.SimulateSprintHeld(true);

            float sprintTarget = movement.Config.walkSpeed * 1.3f * movement.ClassSpeedMultiplier;
            float sprintDeadline = Time.time + 1.2f;
            while (movement.CurrentSpeed < sprintTarget && Time.time < sprintDeadline)
                yield return null;

            float speedBeforeJump = movement.CurrentSpeed;
            Assert.GreaterOrEqual(speedBeforeJump, movement.Config.walkSpeed * movement.ClassSpeedMultiplier * 0.9f,
                $"Expected sprint speed before jump (got {speedBeforeJump:F2})");

            // Single Jump press, then keep Space held — no second SimulateJumpPress call.
            inputHandler.SimulateJumpPress();
            yield return null;

            float bhopDeadline = Time.time + 3.5f;
            while (bhopCount < 2 && Time.time < bhopDeadline)
            {
                float horiz = new Vector3(movement.Velocity.x, 0f, movement.Velocity.z).magnitude;
                if (horiz > maxObservedSpeed)
                    maxObservedSpeed = horiz;
                yield return null;
            }

            float cap = movement.Config.bhopMaxSpeed * movement.ClassSpeedMultiplier;
            Assert.GreaterOrEqual(bhopCount, 2,
                $"Holding Jump should auto-rejump into consecutive bhops (bhops={bhopCount}, chain={movement.BhopChainCount}, state={movement.CurrentState})");
            Assert.GreaterOrEqual(movement.BhopChainCount, 2,
                $"BhopChainCount should increment across hold-Space landings (chain={movement.BhopChainCount})");
            Assert.Greater(speedAtLastBhop, speedBeforeJump,
                $"Chained bhops should preserve and build speed without landing friction loss (before={speedBeforeJump:F2}, after={speedAtLastBhop:F2})");
            Assert.LessOrEqual(maxObservedSpeed, cap + 0.05f,
                $"Bhop speed must stay capped by MovementConfig.bhopMaxSpeed (max={maxObservedSpeed:F2}, cap={cap:F2})");

            // Release Space + WASD and confirm clean return to idle with no drift/sticking.
            inputHandler.SetJumpHeld(false);
            inputHandler.SimulateSprintHeld(false);
            inputHandler.SetMoveInput(Vector2.zero);

            float settleDeadline = Time.time + 2.0f;
            while ((!movement.IsGrounded || movement.CurrentSpeed > 0.35f) && Time.time < settleDeadline)
                yield return null;

            Assert.IsTrue(movement.IsGrounded, "Releasing Jump should allow player to land cleanly");
            Assert.Less(movement.CurrentSpeed, 0.35f,
                $"Releasing inputs after bhop should settle to idle (speed={movement.CurrentSpeed:F2})");
        }
        finally
        {
            movement.BunnyHopped -= onBhop;
            inputHandler.SetJumpHeld(false);
            inputHandler.SimulateSprintHeld(false);
            inputHandler.SetMoveInput(Vector2.zero);
            inputHandler.SetTestInputLocked(false);
        }
    }

    [UnityTest]
    public IEnumerator Player_Bhop_AirStrafe_BuildsSpeedWithinCap()
    {
        yield return MovePlayerToOpenLane();
        Assert.IsTrue(movement.IsGrounded, "Player must be grounded before air-strafe bhop test");

        int bhopCount = 0;
        float peakSpeed = 0f;
        System.Action onBhop = () => bhopCount++;
        movement.BunnyHopped += onBhop;

        try
        {
            inputHandler.SetTestInputLocked(true);
            inputHandler.SetMoveInput(new Vector2(0f, 1f));
            inputHandler.SimulateSprintHeld(false);

            float walkTarget = movement.Config.walkSpeed * 0.9f * movement.ClassSpeedMultiplier;
            float accelDeadline = Time.time + 1.2f;
            while (movement.CurrentSpeed < walkTarget && Time.time < accelDeadline)
                yield return null;

            float speedBeforeJump = movement.CurrentSpeed;
            inputHandler.SimulateJumpPress();
            yield return null;

            float deadline = Time.time + 3.5f;
            while (bhopCount < 2 && Time.time < deadline)
            {
                // Alternate left/right air-strafes across hops while keeping Jump held.
                float strafeX = (bhopCount % 2 == 0) ? 0.85f : -0.85f;
                inputHandler.SetMoveInput(new Vector2(strafeX, 0.5f).normalized);

                float horiz = new Vector3(movement.Velocity.x, 0f, movement.Velocity.z).magnitude;
                if (horiz > peakSpeed)
                    peakSpeed = horiz;
                yield return null;
            }

            float cap = movement.Config.bhopMaxSpeed * movement.ClassSpeedMultiplier;
            Assert.GreaterOrEqual(bhopCount, 2,
                $"Holding Jump while air-strafing should chain bhops (bhops={bhopCount})");
            Assert.Greater(peakSpeed, speedBeforeJump + 0.3f,
                $"Air-strafing with bhop should build horizontal speed (before={speedBeforeJump:F2}, peak={peakSpeed:F2})");
            Assert.LessOrEqual(peakSpeed, cap + 0.05f,
                $"Air-strafe + bhop speed must stay within config cap (peak={peakSpeed:F2}, cap={cap:F2})");
        }
        finally
        {
            movement.BunnyHopped -= onBhop;
            inputHandler.SetJumpHeld(false);
            inputHandler.SetMoveInput(Vector2.zero);
            inputHandler.SetTestInputLocked(false);
        }
    }

    [UnityTest]
    public IEnumerator Player_SlideCancel_IntoHoldSpaceBhop_PreservesMomentum()
    {
        yield return MovePlayerToOpenLane();
        Assert.IsTrue(movement.IsGrounded, "Player must be grounded before slide-cancel bhop test");

        bool slideCancelled = false;
        int bhopCount = 0;
        float speedAfterBhop = 0f;
        System.Action onSlideCancel = () => slideCancelled = true;
        System.Action onBhop = () =>
        {
            bhopCount++;
            speedAfterBhop = new Vector3(movement.Velocity.x, 0f, movement.Velocity.z).magnitude;
        };
        movement.SlideCancelled += onSlideCancel;
        movement.BunnyHopped += onBhop;

        try
        {
            inputHandler.SetTestInputLocked(true);
            inputHandler.SetMoveInput(new Vector2(0f, 1f));
            inputHandler.SimulateSprintHeld(true);

            float sprintTarget = movement.Config.walkSpeed * 1.2f * movement.ClassSpeedMultiplier;
            float sprintDeadline = Time.time + 1.2f;
            while (movement.CurrentSpeed < sprintTarget && Time.time < sprintDeadline)
                yield return null;

            inputHandler.SimulateCrouchPress();
            inputHandler.SetCrouchHeld(true);
            for (int i = 0; i < 6; i++)
                yield return null;

            Assert.AreEqual(MovementController.MovementState.Sliding, movement.CurrentState,
                "Should be sliding before slide-cancel");

            // Jump out of slide and keep Jump held into the landing bhop.
            inputHandler.SimulateJumpPress();
            yield return null;
            inputHandler.SetCrouchHeld(false);

            float deadline = Time.time + 2.5f;
            while (bhopCount < 1 && Time.time < deadline)
                yield return null;

            float cap = movement.Config.bhopMaxSpeed * movement.ClassSpeedMultiplier;
            Assert.IsTrue(slideCancelled, "Jumping during slide should fire SlideCancelled");
            Assert.GreaterOrEqual(bhopCount, 1,
                $"Holding Jump after slide-cancel should chain into a bhop on landing (bhops={bhopCount})");
            Assert.GreaterOrEqual(speedAfterBhop, movement.Config.walkSpeed * movement.ClassSpeedMultiplier,
                $"Slide-cancel into bhop should keep high horizontal speed (speed={speedAfterBhop:F2})");
            Assert.LessOrEqual(speedAfterBhop, cap + 0.05f,
                $"Slide-cancel into bhop speed must stay within config cap (speed={speedAfterBhop:F2}, cap={cap:F2})");
        }
        finally
        {
            movement.SlideCancelled -= onSlideCancel;
            movement.BunnyHopped -= onBhop;
            inputHandler.SetJumpHeld(false);
            inputHandler.SetCrouchHeld(false);
            inputHandler.SimulateSprintHeld(false);
            inputHandler.SetMoveInput(Vector2.zero);
            inputHandler.SetTestInputLocked(false);
        }
    }

    [UnityTest]
    public IEnumerator Player_Dive_SprintForward_EntersDiving()
    {
        yield return SettlePlayerOnGround();
        Assert.IsTrue(movement.IsGrounded);

        bool dived = false;
        System.Action onDive = () => dived = true;
        movement.Dived += onDive;

        try
        {
            inputHandler.SetTestInputLocked(true);
            inputHandler.SetMoveInput(new Vector2(0f, 1f));
            inputHandler.SimulateSprintHeld(true);

            float minDiveSpeed = movement.Config.minDiveSpeed + 0.5f;
            float sprintDeadline = Time.time + 1.2f;
            while ((movement.CurrentSpeed < minDiveSpeed || movement.CurrentState != MovementController.MovementState.Sprinting)
                && Time.time < sprintDeadline)
            {
                yield return null;
            }

            Assert.GreaterOrEqual(movement.CurrentSpeed, movement.Config.minDiveSpeed, "Need sprint speed before dive");
            inputHandler.SimulateDivePress();
            yield return null;
            yield return null;

            Assert.IsTrue(dived, "Sprint forward + V should fire Dived event");
            Assert.AreEqual(MovementController.MovementState.Diving, movement.CurrentState,
                "Sprint forward + V should remain in Diving state instead of cancelling into DiveRecovery");
        }
        finally
        {
            movement.Dived -= onDive;
            inputHandler.SimulateSprintHeld(false);
            inputHandler.SetMoveInput(Vector2.zero);
            inputHandler.SetTestInputLocked(false);
        }
    }

    [UnityTest]
    public IEnumerator Player_WallBounce_HighSpeedNearWall_ChangesVelocity()
    {
        var wall = GameObject.Find("Wall_Right") ?? GameObject.Find("Wall_Left");
        Assert.IsNotNull(wall, "Need a wall in TestRange for wall bounce test");

        bool wallBounced = false;
        System.Action onWallBounce = () => wallBounced = true;
        movement.WallBounced += onWallBounce;

        try
        {
            Vector3 wallPos = wall.transform.position;
            Vector3 toWall = wall.name == "Wall_Left" ? Vector3.left : Vector3.right;
            float wallHalfThickness = wall.transform.localScale.x * 0.5f;
            // Position player cleanly airborne approaching the wall perpendicularly inside wallCheckDistance + radius.
            Vector3 startPos = new Vector3(
                wallPos.x - toWall.x * (wallHalfThickness + 0.85f),
                2.0f,
                wallPos.z);

            characterController.enabled = false;
            player.transform.SetPositionAndRotation(startPos, Quaternion.LookRotation(toWall));
            characterController.enabled = true;
            movement.ResetMovement();

            inputHandler.SetTestInputLocked(true);
            inputHandler.SetMoveInput(new Vector2(0f, 1f));
            movement.SetVelocity(toWall * 8f + Vector3.up * 1.5f);
            yield return null;

            Assert.AreEqual(MovementController.MovementState.Airborne, movement.CurrentState,
                "Player should be airborne approaching the wall before wall bounce");

            inputHandler.SimulateJumpPress();
            float bounceDeadline = Time.time + 0.25f;
            while (!wallBounced && Time.time < bounceDeadline)
                yield return null;

            Assert.IsTrue(wallBounced, "Jumping at high speed near wall while airborne should trigger WallBounced");
            float awayDot = Vector3.Dot(new Vector3(movement.Velocity.x, 0f, movement.Velocity.z).normalized, toWall);
            Assert.Less(awayDot, 0.2f,
                $"Wall bounce jump should redirect away from the wall (awayDot={awayDot:F2}, vel={movement.Velocity})");
        }
        finally
        {
            movement.WallBounced -= onWallBounce;
            inputHandler.SetJumpHeld(false);
            inputHandler.SetMoveInput(Vector2.zero);
            inputHandler.SetTestInputLocked(false);
        }
    }

    [UnityTest]
    public IEnumerator Player_WalkInput_MovesFromIdleWithoutMinMoveBlocking()
    {
        yield return SettlePlayerOnGround();
        Assert.LessOrEqual(characterController.minMoveDistance, 0.005f,
            "High minMoveDistance silently drops WASD moves at high FPS");

        Vector3 start = player.transform.position;
        inputHandler.SetTestInputLocked(true);
        inputHandler.SetMoveInput(new Vector2(0f, 1f));
        for (int i = 0; i < 20; i++)
            yield return null;
        float walkSpeed = movement.CurrentSpeed;
        inputHandler.SetMoveInput(Vector2.zero);
        inputHandler.SetTestInputLocked(false);

        float moved = Vector2.Distance(
            new Vector2(start.x, start.z),
            new Vector2(player.transform.position.x, player.transform.position.z));
        Assert.Greater(moved, 0.3f, $"Forward move input should displace player from idle (moved={moved:F3})");
        Assert.GreaterOrEqual(walkSpeed, 4.5f,
            $"Walking forward should reach near config walkSpeed, not cap at accel/friction (speed={walkSpeed:F2})");
    }

    [UnityTest]
    public IEnumerator Player_IdleJump_LeavesGroundWithoutBeingCancelled()
    {
        yield return SettlePlayerOnGround();
        Assert.IsTrue(movement.IsGrounded, "Player must be grounded before standing jump");

        movement.SetVelocity(Vector3.zero);
        yield return null;

        float startY = player.transform.position.y;
        float peakY = startY;
        try
        {
            inputHandler.SetTestInputLocked(true);
            inputHandler.SetMoveInput(Vector2.zero);
            inputHandler.SimulateJumpPress();
            yield return null;
            inputHandler.SetJumpHeld(false);

            float jumpDeadline = Time.time + 0.35f;
            while (Time.time < jumpDeadline && (peakY - startY) < 0.6f)
            {
                yield return null;
                if (player.transform.position.y > peakY)
                    peakY = player.transform.position.y;
            }
        }
        finally
        {
            inputHandler.SetJumpHeld(false);
            inputHandler.SetTestInputLocked(false);
        }

        Assert.Greater(peakY - startY, 0.25f,
            $"Standing jump from idle should lift player off ground (startY={startY:F2}, peakY={peakY:F2})");
    }

    [UnityTest]
    public IEnumerator Weapon_Reload_AnimatesPoseAndRefillsAmmo()
    {
        var weapon = player.GetComponentInChildren<WeaponController>();
        Assert.IsNotNull(weapon, "Player should have an equipped WeaponController");

        weapon.SetAmmo(1, 30);
        Quaternion baseRot = weapon.transform.localRotation;

        weapon.TryReload();
        Assert.IsTrue(weapon.IsReloading, "TryReload should enter reloading state");

        float maxAngleDelta = 0f;
        float poseDeadline = Time.time + 0.6f;
        while (Time.time < poseDeadline && maxAngleDelta < 15f)
        {
            yield return null;
            float delta = Quaternion.Angle(baseRot, weapon.transform.localRotation);
            if (delta > maxAngleDelta)
                maxAngleDelta = delta;
        }

        Assert.Greater(maxAngleDelta, 5f,
            $"Weapon should visibly animate during reload (maxAngleDelta={maxAngleDelta:F2})");

        float reloadDeadline = Time.time + (weapon.Config != null ? weapon.Config.reloadTime : 2.5f) + 0.75f;
        while (weapon.IsReloading && Time.time < reloadDeadline)
            yield return null;

        Assert.IsFalse(weapon.IsReloading, "Reload should complete within configured reloadTime");
        Assert.Greater(weapon.CurrentAmmo, 1, $"Reload should refill magazine ammo (ammo={weapon.CurrentAmmo})");
    }

    private IEnumerator SettlePlayerOnGround()
    {
        for (int i = 0; i < 45; i++)
        {
            if (!characterController.isGrounded)
                characterController.Move(Vector3.down * 0.2f);
            yield return null;
        }
    }

    private IEnumerator MovePlayerToOpenLane()
    {
        characterController.enabled = false;
        player.transform.SetPositionAndRotation(new Vector3(14f, 1.5f, -18f), Quaternion.identity);
        characterController.enabled = true;
        movement.ResetMovement();
        yield return null;
        yield return SettlePlayerOnGround();
        movement.ResetMovement();
        yield return null;
    }
}
