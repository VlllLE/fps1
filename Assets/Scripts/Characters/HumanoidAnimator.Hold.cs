using UnityEngine;

/// <summary>
/// Weapon-holding half of <see cref="HumanoidAnimator"/>: distributes aim pitch/yaw over the spine, places the weapon
/// from a <see cref="GunHoldProfile"/> and stance, and drives both hands onto the weapon with two-bone IK
/// (right hand on the grip, left hand on the handguard, magwell or shell port during reloads). Everything is
/// additive: with no profile the animator behaves exactly as before.
/// </summary>
public sealed partial class HumanoidAnimator
{
    /// <summary>Distance from the wrist (hand bone) to the palm centre along the fingers.</summary>
    public const float PalmOffset = 0.055f;

    private const int LeftIndex = 0;
    private const int RightIndex = 1;
    private const float ArmReach = (HumanoidProportions.UpperArmLength + HumanoidProportions.ForearmLength) * 0.985f;

    private GunHoldProfile profile;
    private float holdWeight;
    private float stanceT;
    private float aimYaw;
    private float recoil;
    private float recoilVelocity;
    private bool reloading;
    private float reloadProgress;
    private int reloadCycles = 1;

    private Vector3 gunPosition;
    private Quaternion gunRotation = Quaternion.identity;
    private Vector3 magazineOffset;
    private Vector3 boltOffset;
    private float guardSlide;
    private readonly Vector3[] holdWrist = new Vector3[2];
    private readonly Quaternion[] holdHand = { Quaternion.identity, Quaternion.identity };
    private readonly float[] holdArmWeight = new float[2];

    /// <summary>0 = no weapon held, 1 = fully holding. Blends in and out when a weapon is equipped or dropped.</summary>
    public float HoldWeight => holdWeight;

    public bool IsHolding => holdWeight > 0.02f && profile != null;

    public GunHoldProfile HoldProfile => profile;

    /// <summary>Smoothed stance parameter: 0 = lowered, 1 = ready, 2 = aim.</summary>
    public float StanceValue => stanceT;

    /// <summary>Weapon origin in unscaled body-root space.</summary>
    public Vector3 GunPosition => gunPosition;

    public Quaternion GunRotation => gunRotation;

    /// <summary>How far the "Magazine" part has been pulled from its rest position (weapon space).</summary>
    public Vector3 MagazineOffset => magazineOffset;

    /// <summary>Offset of the bolt handle while the right hand cycles it (weapon space).</summary>
    public Vector3 BoltOffset => boltOffset;

    /// <summary>0..1 position along the segment from the handguard grip to <see cref="GunHoldProfile.leftGripRear"/>.</summary>
    public float GuardSlide => guardSlide;

    public float Recoil => recoil;

    /// <summary>Kicks the weapon (and the hands holding it) for one shot. Strength 1 is a rifle round.</summary>
    public void NotifyShot(float strength = 1f)
    {
        float scale = profile != null ? profile.recoilKick : 1f;
        recoilVelocity = Mathf.Min(recoilVelocity + RecoilImpulse * Mathf.Clamp(strength, 0f, 2f) * scale, RecoilImpulse * 3f);
    }

    private const float RecoilImpulse = 40f;
    private const float RecoilOmega = 21f;
    private const float RecoilDamping = 0.55f;

    /// <summary>Under-damped spring: a shot snaps the weapon back and up, then it settles with a small overshoot.</summary>
    private void TickRecoil(float dt)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.004f));
        float h = dt / steps;
        for (int i = 0; i < steps; i++)
        {
            float accel = -RecoilOmega * RecoilOmega * recoil - 2f * RecoilDamping * RecoilOmega * recoilVelocity;
            recoilVelocity += accel * h;
            recoil += recoilVelocity * h;
        }

        recoil = Mathf.Clamp(recoil, -0.3f, 2.2f);
    }

    /// <summary>World-space (body-root space) palm centre of the animated hand.</summary>
    public Vector3 GetPalmPosition(bool right)
    {
        int hand = (int)(right ? HumanoidBone.HandR : HumanoidBone.HandL);
        return worldPos[hand] + worldRot[hand] * new Vector3(0f, -PalmOffset, 0f);
    }

    /// <summary>Where the weapon says the palm should be (body-root space). Only meaningful while holding.</summary>
    public Vector3 GetPalmTarget(bool right)
    {
        int index = right ? RightIndex : LeftIndex;
        return holdWrist[index] + holdHand[index] * new Vector3(0f, -PalmOffset, 0f);
    }

    /// <summary>Weight with which the given hand follows the weapon (0 for a free support hand).</summary>
    public float GetHandHoldWeight(bool right)
    {
        return holdArmWeight[right ? RightIndex : LeftIndex];
    }

    /// <summary>Knee bend a braced (bipod) weapon asks for while aiming.</summary>
    private static float HoldCrouchTarget(HumanoidAnimationInput input)
    {
        GunHoldProfile p = input.holdProfile;
        if (p == null || p.aimCrouch <= 0f)
            return 0f;
        return p.aimCrouch * Mathf.Clamp01((int)input.holdStance - 1f);
    }

    private void TickHold(float dt, HumanoidAnimationInput input)
    {
        if (input.holdProfile != null)
        {
            if (profile != input.holdProfile && holdWeight < 0.05f)
                stanceT = (float)input.holdStance;
            profile = input.holdProfile;
        }

        holdWeight = Approach(holdWeight, input.holdProfile != null ? 1f : 0f, dt, 8f);
        if (input.holdProfile == null && holdWeight < 0.002f)
            holdWeight = 0f;
        stanceT = Approach(stanceT, (float)input.holdStance, dt, 7f);
        aimYaw = Approach(aimYaw, Mathf.Clamp(input.aimYawDegrees, -120f, 120f), dt, 14f);
        TickRecoil(dt);
        reloading = input.reloading;
        reloadProgress = Mathf.Clamp01(input.reloadProgress);
        reloadCycles = Mathf.Max(1, input.reloadCycles);
    }

    private bool HoldActive => profile != null && holdWeight > 1e-3f;

    private void ApplyHoldTorso(float spinePitch, float chestPitch, float hipsYaw, float leanSum)
    {
        if (!HoldActive)
            return;

        HoldStancePose pose = profile.Evaluate(stanceT);
        float w = holdWeight;
        float aimWeight = Mathf.Clamp01(stanceT - 1f);
        float lean = profile.bipodStance ? 6f * Mathf.Clamp01(stanceT) : 0f;
        lean += profile.aimLean * aimWeight - 2.4f * recoil;

        float spineYaw = -hipsYaw * 0.9f + w * (pose.blade * 0.5f + aimYaw * 0.25f);
        float chestYaw = -hipsYaw * 0.5f + w * (pose.blade * 0.5f + aimYaw * 0.35f);
        localRot[(int)HumanoidBone.Spine] = Quaternion.Euler(spinePitch + w * (lookPitch * 0.10f + lean), spineYaw, 0f);
        localRot[(int)HumanoidBone.Chest] = Quaternion.Euler(chestPitch + lookPitch * (0.2f + 0.1f * w) - 1.6f * recoil * w, chestYaw, flinchRoll);

        UpdateWorld(HumanoidBone.Hips);
        UpdateWorld(HumanoidBone.Spine);
        UpdateWorld(HumanoidBone.Chest);

        Quaternion chest = worldRot[(int)HumanoidBone.Chest];
        Quaternion headBase = chest * localRot[(int)HumanoidBone.Neck] * localRot[(int)HumanoidBone.Head];
        Quaternion lookRotation = Quaternion.Euler(lookPitch, aimYaw, 0f);
        Quaternion headTarget = Quaternion.Slerp(headBase, lookRotation, w);
        Quaternion residual = Quaternion.Inverse(chest) * headTarget;
        residual *= Quaternion.Euler((pose.headNod - 3f * recoil) * w, 0f, pose.headRoll * w);
        Quaternion neck = Quaternion.Slerp(Quaternion.identity, residual, 0.4f);
        localRot[(int)HumanoidBone.Neck] = neck;
        localRot[(int)HumanoidBone.Head] = Quaternion.Inverse(neck) * residual;
    }

    /// <summary>Support-hand weight: 0 when the profile frees it (knife, lowered pistol).</summary>
    private float SupportWeight()
    {
        if (profile == null || !profile.twoHanded)
            return 0f;
        if (profile.supportHandFreeWhenLowered)
            return Mathf.Clamp01(stanceT * 2f);
        return 1f;
    }

    private float HoldArmWeight(float side)
    {
        if (!HoldActive)
            return 0f;
        return holdArmWeight[side < 0f ? LeftIndex : RightIndex];
    }

    /// <summary>Elbow pole direction (chest space) for the held-weapon arm: tucked in for the shooting arm, under the gun for the support arm.</summary>
    private Vector3 HoldElbowPole(float side)
    {
        HoldStancePose pose = profile.Evaluate(stanceT);
        Vector2 elbow = side < 0f ? pose.leftElbow : pose.rightElbow;
        return new Vector3(side * elbow.x, -1f, -elbow.y);
    }

    private Vector3 HoldWristTarget(float side)
    {
        return holdWrist[side < 0f ? LeftIndex : RightIndex];
    }

    private Quaternion HoldHandRotation(float side)
    {
        return holdHand[side < 0f ? LeftIndex : RightIndex];
    }

    private static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float Segment(float t, float t0, float t1)
    {
        return t1 <= t0 ? (t >= t1 ? 1f : 0f) : Ease((t - t0) / (t1 - t0));
    }

    private void ComputeHold()
    {
        holdArmWeight[LeftIndex] = 0f;
        holdArmWeight[RightIndex] = 0f;
        magazineOffset = Vector3.zero;
        boltOffset = Vector3.zero;
        if (!HoldActive)
            return;

        Quaternion chestRotation = worldRot[(int)HumanoidBone.Chest];
        Vector3 chestPosition = worldPos[(int)HumanoidBone.Chest];
        HoldStancePose pose = profile.Evaluate(stanceT);

        float pitch = pose.pitchBase + lookPitch * pose.pitchLook - 7.5f * recoil;
        gunRotation = Quaternion.Euler(pitch, aimYaw + pose.yawOffset, 0f);
        Vector3 reference = chestPosition + chestRotation * pose.anchor;
        reference += gunRotation * new Vector3(0f, 0.008f * recoil, -0.065f * recoil);
        gunPosition = reference - gunRotation * profile.ReferencePoint;

        Quaternion leftFrame = GunHoldCatalog.HandRotation(false, profile.leftFingers, profile.leftPalmNormal);
        float supportWeight = SupportWeight();

        UpdateWorld(HumanoidBone.UpperArmL);
        UpdateWorld(HumanoidBone.UpperArmR);

        guardSlide = 0f;
        if (supportWeight > 0.01f)
        {
            Vector3 leftShoulder = worldPos[(int)HumanoidBone.UpperArmL];
            Vector3 wristOffset = gunRotation * leftFrame * new Vector3(0f, PalmOffset, 0f);
            Vector3 WristAt(float t)
            {
                return gunPosition + gunRotation * Vector3.Lerp(profile.leftGrip, profile.leftGripRear, t) + wristOffset;
            }

            if ((WristAt(0f) - leftShoulder).magnitude > ArmReach)
            {
                float lo = 0f;
                float hi = 1f;
                if ((WristAt(1f) - leftShoulder).magnitude > ArmReach)
                {
                    lo = 1f;
                }
                else
                {
                    for (int i = 0; i < 10; i++)
                    {
                        float mid = 0.5f * (lo + hi);
                        if ((WristAt(mid) - leftShoulder).magnitude > ArmReach)
                            lo = mid;
                        else
                            hi = mid;
                    }

                    lo = hi;
                }

                guardSlide = lo;
                if (lo >= 1f)
                {
                    Vector3 wrist = WristAt(1f);
                    Vector3 toShoulder = leftShoulder - wrist;
                    float shortfall = toShoulder.magnitude - ArmReach;
                    if (shortfall > 0f && toShoulder.sqrMagnitude > 1e-8f)
                        gunPosition += toShoulder.normalized * Mathf.Min(shortfall + 0.002f, 0.2f);
                }
            }
        }

        Vector3 guardPalm = Vector3.Lerp(profile.leftGrip, profile.leftGripRear, guardSlide);
        Vector3 leftPalm = guardPalm;
        Quaternion leftFrameGun = leftFrame;
        Vector3 rightPalm = profile.rightGrip;
        Quaternion rightFrameGun = GunHoldCatalog.HandRotation(true, profile.rightFingers, profile.rightPalmNormal);

        if (reloading && supportWeight > 0.01f)
        {
            EvaluateReload(reloadProgress, guardPalm, out leftPalm, out float magBlend, out magazineOffset, out float boltWeight, out float boltPull);
            Quaternion magFrame = GunHoldCatalog.HandRotation(false, profile.magFingers, profile.magPalmNormal);
            leftFrameGun = Quaternion.Slerp(leftFrame, magFrame, magBlend);
            if (profile.boltRack && boltWeight > 0f)
            {
                boltOffset = new Vector3(0f, 0f, -0.07f * boltPull);
                rightPalm = Vector3.Lerp(profile.rightGrip, profile.boltHandle + boltOffset, boltWeight);
            }
        }

        SetHoldTarget(RightIndex, rightPalm, rightFrameGun, 1f);
        SetHoldTarget(LeftIndex, leftPalm, leftFrameGun, supportWeight);
    }

    private void SetHoldTarget(int index, Vector3 palmGun, Quaternion handGun, float weight)
    {
        Quaternion world = gunRotation * handGun;
        Vector3 palm = gunPosition + gunRotation * palmGun;
        holdHand[index] = world;
        holdWrist[index] = palm - world * new Vector3(0f, -PalmOffset, 0f);
        holdArmWeight[index] = holdWeight * Mathf.Clamp01(weight);
    }

    /// <summary>
    /// Support-hand path for a reload (weapon space). Magazine guns: guard, magwell, pull the magazine, to the pouch,
    /// fresh magazine back up into the well, rack, back to the guard. Shell guns repeat pouch to port.
    /// </summary>
    private void EvaluateReload(
        float t,
        Vector3 guard,
        out Vector3 palm,
        out float magBlend,
        out Vector3 magOffset,
        out float boltWeight,
        out float boltPull)
    {
        Vector3 well = profile.magwell;
        Vector3 pulled = well + new Vector3(0f, -0.20f, 0f);
        Vector3 pouch = well + new Vector3(-0.11f, -0.30f, -0.10f);
        magOffset = Vector3.zero;
        boltWeight = 0f;
        boltPull = 0f;

        if (profile.shellReload)
        {
            Vector3 shell = well + new Vector3(-0.10f, -0.18f, -0.05f);
            if (t < 0.10f)
            {
                float k = Segment(t, 0f, 0.10f);
                palm = Vector3.Lerp(guard, shell, k);
                magBlend = k;
            }
            else if (t > 0.92f)
            {
                float k = Segment(t, 0.92f, 1f);
                palm = Vector3.Lerp(well, guard, k);
                magBlend = 1f - k;
            }
            else
            {
                float cycle = (t - 0.10f) / 0.82f * reloadCycles;
                float local = cycle - Mathf.Floor(cycle);
                palm = local < 0.5f
                    ? Vector3.Lerp(shell, well, Segment(local, 0f, 0.5f))
                    : Vector3.Lerp(well, shell, Segment(local, 0.5f, 1f));
                magBlend = 1f;
            }

            return;
        }

        if (t < 0.12f)
        {
            float k = Segment(t, 0f, 0.12f);
            palm = Vector3.Lerp(guard, well, k);
            magBlend = k;
        }
        else if (t < 0.30f)
        {
            palm = Vector3.Lerp(well, pulled, Segment(t, 0.12f, 0.30f));
            magBlend = 1f;
            magOffset = palm - well;
        }
        else if (t < 0.48f)
        {
            palm = Vector3.Lerp(pulled, pouch, Segment(t, 0.30f, 0.48f));
            magBlend = 1f;
            magOffset = palm - well;
        }
        else if (t < 0.58f)
        {
            palm = pouch;
            magBlend = 1f;
            magOffset = palm - well;
        }
        else if (t < 0.74f)
        {
            palm = Vector3.Lerp(pouch, pulled, Segment(t, 0.58f, 0.74f));
            magBlend = 1f;
            magOffset = palm - well;
        }
        else if (t < 0.86f)
        {
            palm = Vector3.Lerp(pulled, well, Segment(t, 0.74f, 0.86f));
            magBlend = 1f;
            magOffset = palm - well;
        }
        else
        {
            float k = Segment(t, 0.86f, 1f);
            palm = Vector3.Lerp(well, guard, k);
            magBlend = 1f - k;
        }

        if (profile.boltRack && t > 0.80f)
        {
            boltWeight = Segment(t, 0.80f, 0.86f) * (1f - Segment(t, 0.97f, 1f));
            boltPull = t < 0.93f ? Segment(t, 0.86f, 0.93f) : 1f - Segment(t, 0.93f, 0.97f);
        }
    }
}
