using UnityEngine;

public enum HumanoidLocomotion
{
    Grounded = 0,
    Sliding,
    Airborne,
    Diving
}

public enum HumanoidArmPose
{
    Relaxed = 0,
    WeaponReady
}

/// <summary>Plain-data description of what the body should be doing this frame.</summary>
public struct HumanoidAnimationInput
{
    /// <summary>Planar speed along the body's facing direction (m/s, negative when backing up).</summary>
    public float forwardSpeed;
    /// <summary>Planar speed along the body's right direction (m/s).</summary>
    public float rightSpeed;
    public float verticalSpeed;
    public HumanoidLocomotion locomotion;
    /// <summary>0 = standing, 1 = fully crouched.</summary>
    public float crouch01;
    /// <summary>Look pitch in degrees; positive looks down (Unity convention).</summary>
    public float lookPitchDegrees;
    public HumanoidArmPose armPose;
}

/// <summary>
/// Deterministic procedural animator for the humanoid skeleton. It owns no Unity objects: feed it
/// <see cref="Tick"/> and read the resulting bone rotations / hips offset. All positions are in
/// unscaled body-root space (feet at y = 0). Legs and arms are placed with analytic two-bone IK so
/// crouching, sliding and airborne poses keep feet and hands consistent without keyframed clips.
/// </summary>
public sealed class HumanoidAnimator
{
    private const float TwoPi = Mathf.PI * 2f;
    private const float MoveThreshold = 0.25f;
    private const float CrouchHipsDrop = 0.52f;

    private readonly Vector3[] restLocal = new Vector3[HumanoidParts.BoneCount];
    private readonly Quaternion[] localRot = new Quaternion[HumanoidParts.BoneCount];
    private readonly Vector3[] worldPos = new Vector3[HumanoidParts.BoneCount];
    private readonly Quaternion[] worldRot = new Quaternion[HumanoidParts.BoneCount];

    private float phase;
    private float idleTime;
    private float moveWeight;
    private float runWeight;
    private float moveDirX;
    private float moveDirZ = 1f;
    private float crouchWeight;
    private float slideWeight;
    private float airWeight;
    private float diveWeight;
    private float readyWeight;
    private float lookPitch;
    private float verticalSpeed;
    private float flinchPitch;
    private float flinchRoll;

    public HumanoidAnimator()
    {
        for (int i = 0; i < HumanoidParts.BoneCount; i++)
        {
            restLocal[i] = HumanoidParts.GetRestLocalPosition((HumanoidBone)i);
            localRot[i] = Quaternion.identity;
            worldRot[i] = Quaternion.identity;
        }

        Evaluate();
    }

    /// <summary>Offset added to the hips rest position (bob, crouch, slide).</summary>
    public Vector3 HipsOffset { get; private set; }

    /// <summary>Gait cycle position in [0, 1).</summary>
    public float GaitPhase => phase;

    public float CrouchWeight => crouchWeight;

    public Quaternion GetLocalRotation(HumanoidBone bone)
    {
        return localRot[(int)bone];
    }

    /// <summary>Unscaled position relative to the parent bone (or body root), including the hips offset.</summary>
    public Vector3 GetLocalPosition(HumanoidBone bone)
    {
        Vector3 position = restLocal[(int)bone];
        if (bone == HumanoidBone.Hips)
            position += HipsOffset;
        return position;
    }

    public Vector3 GetWorldPosition(HumanoidBone bone)
    {
        return worldPos[(int)bone];
    }

    public Quaternion GetWorldRotation(HumanoidBone bone)
    {
        return worldRot[(int)bone];
    }

    /// <summary>Adds a short hit reaction. <paramref name="localDirection"/> is the shot travel direction in body space.</summary>
    public void AddFlinch(float intensity, Vector3 localDirection)
    {
        Vector3 dir = localDirection.sqrMagnitude > 1e-6f
            ? localDirection / Mathf.Sqrt(localDirection.sqrMagnitude)
            : Vector3.back;
        intensity = Mathf.Clamp(intensity, 0f, 2f);
        flinchPitch = Mathf.Clamp(flinchPitch + intensity * 9f * dir.z, -22f, 22f);
        flinchRoll = Mathf.Clamp(flinchRoll - intensity * 7f * dir.x, -18f, 18f);
    }

    public void Tick(float deltaTime, HumanoidAnimationInput input)
    {
        float dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
        float speed = Mathf.Sqrt(input.forwardSpeed * input.forwardSpeed + input.rightSpeed * input.rightSpeed);
        bool grounded = input.locomotion == HumanoidLocomotion.Grounded;

        float moving = grounded && speed > MoveThreshold ? 1f : 0f;
        moveWeight = Approach(moveWeight, moving, dt, 10f);
        runWeight = Approach(runWeight, Mathf.Clamp01(Mathf.InverseLerp(2.5f, 9f, speed)), dt, 6f);

        if (speed > MoveThreshold)
        {
            moveDirX = Approach(moveDirX, input.rightSpeed / speed, dt, 8f);
            moveDirZ = Approach(moveDirZ, input.forwardSpeed / speed, dt, 8f);
            float length = Mathf.Sqrt(moveDirX * moveDirX + moveDirZ * moveDirZ);
            if (length > 1e-3f)
            {
                moveDirX /= length;
                moveDirZ /= length;
            }
            else
            {
                moveDirX = 0f;
                moveDirZ = 1f;
            }
        }

        float cycleLength = Mathf.Lerp(1.9f, 4f, runWeight);
        if (grounded)
            phase = Mathf.Repeat(phase + speed * dt / cycleLength, 1f);

        idleTime += dt;
        crouchWeight = Approach(crouchWeight, Mathf.Clamp01(input.crouch01), dt, 12f);
        slideWeight = Approach(slideWeight, input.locomotion == HumanoidLocomotion.Sliding ? 1f : 0f, dt, 14f);
        airWeight = Approach(airWeight, input.locomotion == HumanoidLocomotion.Airborne ? 1f : 0f, dt, 10f);
        diveWeight = Approach(diveWeight, input.locomotion == HumanoidLocomotion.Diving ? 1f : 0f, dt, 12f);
        readyWeight = Approach(readyWeight, input.armPose == HumanoidArmPose.WeaponReady ? 1f : 0f, dt, 8f);
        lookPitch = Approach(lookPitch, Mathf.Clamp(input.lookPitchDegrees, -70f, 70f), dt, 14f);
        verticalSpeed = Approach(verticalSpeed, input.verticalSpeed, dt, 10f);

        float decay = Mathf.Exp(-10f * dt);
        flinchPitch *= decay;
        flinchRoll *= decay;

        Evaluate();
    }

    private static float Approach(float current, float target, float dt, float rate)
    {
        return current + (target - current) * (1f - Mathf.Exp(-rate * dt));
    }

    private void Evaluate()
    {
        float breathe = Mathf.Sin(idleTime * TwoPi * 0.22f);
        float sway = Mathf.Sin(idleTime * TwoPi * 0.11f);
        float idleWeight = 1f - moveWeight * 0.85f;
        float gaitAngle = phase * TwoPi;

        float crouch = crouchWeight;
        float twist = Mathf.Lerp(3f, 7f, runWeight) * moveWeight * Mathf.Sin(gaitAngle);

        float hipsPitch = (Mathf.Lerp(0f, 9f, runWeight) * moveWeight + 7f * crouch) * 0.5f;
        hipsPitch = Mathf.Lerp(hipsPitch, -24f, slideWeight);
        hipsPitch = Mathf.Lerp(hipsPitch, 6f, airWeight);
        hipsPitch = Mathf.Lerp(hipsPitch, 78f, diveWeight);
        float hipsYaw = twist * (1f - slideWeight) * (1f - diveWeight);
        float hipsRoll = sway * 0.8f * idleWeight;

        float bob = Mathf.Lerp(0.02f, 0.045f, runWeight) * moveWeight * (0.5f + 0.5f * Mathf.Cos(2f * gaitAngle));
        float hipsY = -bob - CrouchHipsDrop * crouch + 0.004f * breathe * idleWeight;
        hipsY = Mathf.Lerp(hipsY, -0.42f, slideWeight);
        hipsY = Mathf.Lerp(hipsY, -0.04f, airWeight);
        hipsY = Mathf.Lerp(hipsY, -0.10f, diveWeight);
        HipsOffset = new Vector3(0.012f * sway * idleWeight, hipsY, 0f);

        float spinePitch = 7f * crouch + 0.6f * breathe;
        float chestPitch = 0.9f * breathe + flinchPitch;
        float leanSum = hipsPitch + 7f * crouch;

        localRot[(int)HumanoidBone.Hips] = Quaternion.Euler(hipsPitch, hipsYaw, hipsRoll);
        localRot[(int)HumanoidBone.Spine] = Quaternion.Euler(spinePitch, -hipsYaw * 0.9f, 0f);
        localRot[(int)HumanoidBone.Chest] = Quaternion.Euler(chestPitch + lookPitch * 0.2f, -hipsYaw * 0.5f, flinchRoll);
        localRot[(int)HumanoidBone.Neck] = Quaternion.Euler(lookPitch * 0.25f - 0.25f * leanSum, 0f, 0f);
        localRot[(int)HumanoidBone.Head] = Quaternion.Euler(lookPitch * 0.4f - 0.35f * leanSum, 0f, 0f);
        localRot[(int)HumanoidBone.ShoulderL] = Quaternion.identity;
        localRot[(int)HumanoidBone.ShoulderR] = Quaternion.identity;

        for (int i = 0; i <= (int)HumanoidBone.ShoulderR; i++)
            UpdateWorld((HumanoidBone)i);

        SolveLeg(-1f);
        SolveLeg(1f);
        SolveArm(-1f, breathe);
        SolveArm(1f, breathe);

        for (int i = 0; i < HumanoidParts.BoneCount; i++)
            UpdateWorld((HumanoidBone)i);
    }

    private void SolveLeg(float side)
    {
        HumanoidBone thigh = side < 0f ? HumanoidBone.ThighL : HumanoidBone.ThighR;
        HumanoidBone shin = side < 0f ? HumanoidBone.ShinL : HumanoidBone.ShinR;
        HumanoidBone foot = side < 0f ? HumanoidBone.FootL : HumanoidBone.FootR;

        Quaternion hipsRotation = worldRot[(int)HumanoidBone.Hips];
        Vector3 hipsPosition = worldPos[(int)HumanoidBone.Hips];

        float legPhase = Mathf.Repeat(phase + (side < 0f ? 0f : 0.5f), 1f);
        float s = Mathf.Sin(legPhase * TwoPi);
        float c = Mathf.Cos(legPhase * TwoPi);
        float amplitude = Mathf.Lerp(0.16f, 0.34f, runWeight) * moveWeight;
        float lift = Mathf.Lerp(0.07f, 0.20f, runWeight) * moveWeight * Mathf.Max(0f, s);
        float along = -amplitude * c;

        float stance = HumanoidProportions.HipHalfWidth + 0.04f;
        Vector3 ground = new Vector3(
            side * stance + moveDirX * along,
            HumanoidProportions.AnkleY + lift,
            moveDirZ * along - side * 0.05f * crouchWeight);
        Quaternion groundRotation = Quaternion.Euler(Mathf.Min(lift * 80f, 28f), side * 6f, 0f);

        bool lead = side > 0f;
        Vector3 slide = lead ? new Vector3(0.12f, 0.22f, 0.58f) : new Vector3(-0.13f, 0.20f, -0.30f);
        Quaternion slideRotation = Quaternion.Euler(-10f, side * 6f, 0f);

        Vector3 air = lead ? new Vector3(0.12f, 0.40f, -0.10f) : new Vector3(-0.12f, 0.34f, 0.14f);
        air.y -= Mathf.Clamp(verticalSpeed * 0.01f, -0.06f, 0.06f);
        Quaternion airRotation = Quaternion.Euler(35f, side * 8f, 0f);

        Vector3 dive = hipsPosition + hipsRotation * new Vector3(side * 0.10f, -0.80f, 0.05f);
        Quaternion diveRotation = hipsRotation * Quaternion.Euler(40f, side * 6f, 0f);

        Vector3 target = Vector3.Lerp(ground, slide, slideWeight);
        target = Vector3.Lerp(target, air, airWeight);
        target = Vector3.Lerp(target, dive, diveWeight);

        Quaternion footRotation = Quaternion.Slerp(groundRotation, slideRotation, slideWeight);
        footRotation = Quaternion.Slerp(footRotation, airRotation, airWeight);
        footRotation = Quaternion.Slerp(footRotation, diveRotation, diveWeight);

        Vector3 pole = hipsRotation * new Vector3(side * 0.15f, 0f, 1f);
        Vector3 rollReference = hipsRotation * Vector3.forward;
        SolveLimb(thigh, shin, HumanoidProportions.ThighLength, HumanoidProportions.ShinLength,
            target, pole, rollReference, hipsRotation * Vector3.up);

        localRot[(int)foot] = Quaternion.Inverse(worldRot[(int)shin]) * footRotation;
    }

    private void SolveArm(float side, float breathe)
    {
        HumanoidBone upper = side < 0f ? HumanoidBone.UpperArmL : HumanoidBone.UpperArmR;
        HumanoidBone lower = side < 0f ? HumanoidBone.ForearmL : HumanoidBone.ForearmR;
        HumanoidBone hand = side < 0f ? HumanoidBone.HandL : HumanoidBone.HandR;

        Quaternion chestRotation = worldRot[(int)HumanoidBone.Chest];
        Vector3 chestPosition = worldPos[(int)HumanoidBone.Chest];
        Vector3 shoulderLocal = new Vector3(
            side * HumanoidProportions.ShoulderHalfWidth,
            HumanoidProportions.ShoulderY - HumanoidProportions.ChestY,
            0f);

        float cLeft = Mathf.Cos(phase * TwoPi);
        float swing = Mathf.Lerp(0.10f, 0.30f, runWeight) * moveWeight * moveDirZ * (side < 0f ? cLeft : -cLeft);
        float runBlend = runWeight * moveWeight;
        Vector3 relaxedRel = new Vector3(
            side * Mathf.Lerp(0.045f, 0.09f, runBlend),
            Mathf.Lerp(-0.50f, -0.34f, runBlend),
            swing);
        Vector3 relaxed = shoulderLocal + relaxedRel;

        Vector3 ready = side > 0f ? new Vector3(0.10f, 0.04f, 0.42f) : new Vector3(-0.01f, 0.05f, 0.46f);
        ready.z += 0.01f * breathe;
        Vector3 pitchPivot = new Vector3(0f, 0.15f, 0f);
        ready = pitchPivot + Quaternion.AngleAxis(lookPitch * 0.6f, Vector3.right) * (ready - pitchPivot);

        Vector3 slide = shoulderLocal + new Vector3(side * 0.12f, -0.35f, 0.10f);
        Vector3 air = shoulderLocal + new Vector3(side * 0.10f, -0.30f, 0.15f);
        Vector3 dive = shoulderLocal + new Vector3(-side * 0.11f, 0.01f, 0.52f);

        Vector3 localTarget = Vector3.Lerp(relaxed, ready, readyWeight);
        localTarget = Vector3.Lerp(localTarget, slide, slideWeight);
        localTarget = Vector3.Lerp(localTarget, air, airWeight);
        localTarget = Vector3.Lerp(localTarget, dive, diveWeight);

        Vector3 target = chestPosition + chestRotation * localTarget;
        Vector3 pole = chestRotation * new Vector3(side * 0.2f, -0.8f, -0.5f);
        Vector3 rollReference = chestRotation * Vector3.forward;

        SolveLimb(upper, lower, HumanoidProportions.UpperArmLength, HumanoidProportions.ForearmLength,
            target, pole, rollReference, chestRotation * Vector3.up);
        localRot[(int)hand] = Quaternion.identity;
    }

    private void SolveLimb(
        HumanoidBone upper,
        HumanoidBone lower,
        float upperLength,
        float lowerLength,
        Vector3 target,
        Vector3 pole,
        Vector3 rollReference,
        Vector3 rollFallback)
    {
        UpdateWorld(upper);
        Vector3 root = worldPos[(int)upper];
        HumanoidIK.SolveTwoBone(root, target, upperLength, lowerLength, pole, out Vector3 mid, out Vector3 tip);

        Vector3 upperDir = SafeDirection(mid - root);
        Vector3 lowerDir = SafeDirection(tip - mid);
        HumanoidBone? parent = HumanoidParts.GetParent(upper);
        Quaternion parentRotation = parent.HasValue ? worldRot[(int)parent.Value] : Quaternion.identity;

        localRot[(int)upper] = Quaternion.Inverse(parentRotation) * AimDown(upperDir, rollReference, rollFallback);
        UpdateWorld(upper);
        localRot[(int)lower] = Quaternion.Inverse(worldRot[(int)upper]) * AimDown(lowerDir, rollReference, rollFallback);
        UpdateWorld(lower);
    }

    private static Vector3 SafeDirection(Vector3 v)
    {
        float lengthSq = v.sqrMagnitude;
        return lengthSq < 1e-10f ? Vector3.down : v / Mathf.Sqrt(lengthSq);
    }

    /// <summary>Rotation whose local -Y points along <paramref name="direction"/> with local +Z as close to the roll reference as possible.</summary>
    private static Quaternion AimDown(Vector3 direction, Vector3 rollReference, Vector3 fallback)
    {
        Vector3 forward = rollReference - direction * Vector3.Dot(rollReference, direction);
        if (forward.sqrMagnitude < 1e-4f)
            forward = fallback - direction * Vector3.Dot(fallback, direction);
        if (forward.sqrMagnitude < 1e-6f)
            forward = Vector3.Cross(direction, Vector3.right);
        forward = forward / Mathf.Sqrt(forward.sqrMagnitude);
        return Quaternion.LookRotation(forward, -direction);
    }

    private void UpdateWorld(HumanoidBone bone)
    {
        int index = (int)bone;
        Vector3 position = restLocal[index];
        if (bone == HumanoidBone.Hips)
            position += HipsOffset;

        HumanoidBone? parent = HumanoidParts.GetParent(bone);
        if (parent.HasValue)
        {
            int parentIndex = (int)parent.Value;
            worldRot[index] = worldRot[parentIndex] * localRot[index];
            worldPos[index] = worldPos[parentIndex] + worldRot[parentIndex] * position;
        }
        else
        {
            worldRot[index] = localRot[index];
            worldPos[index] = position;
        }
    }
}
