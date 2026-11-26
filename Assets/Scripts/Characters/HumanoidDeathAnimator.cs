using UnityEngine;

/// <summary>
/// Procedural death for the humanoid skeleton. No Animator, clips or Rigidbodies: the whole body topples about its
/// feet as an inverted pendulum (with a damped bounce when it hits the ground) while every joint is spring-driven
/// from the pose it had at the moment of death towards a per-variant limb pose, overshooting and settling like
/// loose limbs. A ground clamp keeps the lowest body point on the floor so a corpse never sinks into it.
///
/// Pure and deterministic (fixed internal sub-steps), owns no Unity objects, so it is EditMode testable. Positions are
/// in unscaled body-root space like <see cref="HumanoidAnimator"/>; <see cref="RootPosition"/> / <see cref="RootRotation"/>
/// are the extra transform to apply to the body root (rotation about the feet pivot).
/// </summary>
public sealed class HumanoidDeathAnimator
{
    private const float SubStep = 1f / 120f;
    private const float Gravity = 9.81f;
    private const float StartAngle = 0f;
    private const float Damping = 0.35f;
    private const float BounceRestitution = 0.22f;
    private const float MinSettleTime = 1.1f;
    private const float RestSpeed = 0.35f;

    private struct ProbePoint
    {
        public HumanoidBone bone;
        public Vector3 local;
        public float radius;

        public ProbePoint(HumanoidBone bone, float x, float y, float z, float radius)
        {
            this.bone = bone;
            local = new Vector3(x, y, z);
            this.radius = radius;
        }
    }

    /// <summary>Body volumes used for the ground clamp (bone-local offset plus a radius), unscaled metres.</summary>
    private static readonly ProbePoint[] Probes =
    {
        new ProbePoint(HumanoidBone.Hips, 0f, 0f, 0f, 0.16f),
        new ProbePoint(HumanoidBone.Spine, 0f, 0.04f, -0.01f, 0.15f),
        new ProbePoint(HumanoidBone.Chest, 0f, 0f, -0.03f, 0.17f),
        new ProbePoint(HumanoidBone.Chest, 0f, 0.12f, -0.03f, 0.16f),
        new ProbePoint(HumanoidBone.Neck, 0f, 0.04f, 0f, 0.06f),
        new ProbePoint(HumanoidBone.Head, 0f, 0.17f, 0.005f, 0.12f),
        new ProbePoint(HumanoidBone.UpperArmL, 0f, 0f, 0f, 0.06f),
        new ProbePoint(HumanoidBone.UpperArmR, 0f, 0f, 0f, 0.06f),
        new ProbePoint(HumanoidBone.ForearmL, 0f, 0f, 0f, 0.05f),
        new ProbePoint(HumanoidBone.ForearmR, 0f, 0f, 0f, 0.05f),
        new ProbePoint(HumanoidBone.HandL, 0f, 0f, 0f, 0.05f),
        new ProbePoint(HumanoidBone.HandR, 0f, 0f, 0f, 0.05f),
        new ProbePoint(HumanoidBone.HandL, 0f, -0.09f, 0f, 0.04f),
        new ProbePoint(HumanoidBone.HandR, 0f, -0.09f, 0f, 0.04f),
        new ProbePoint(HumanoidBone.ThighL, 0f, 0f, 0f, 0.10f),
        new ProbePoint(HumanoidBone.ThighR, 0f, 0f, 0f, 0.10f),
        new ProbePoint(HumanoidBone.ShinL, 0f, 0f, 0f, 0.08f),
        new ProbePoint(HumanoidBone.ShinR, 0f, 0f, 0f, 0.08f),
        new ProbePoint(HumanoidBone.FootL, 0f, 0f, 0f, 0.06f),
        new ProbePoint(HumanoidBone.FootR, 0f, 0f, 0f, 0.06f),
        new ProbePoint(HumanoidBone.FootL, 0f, -0.03f, 0.14f, 0.045f),
        new ProbePoint(HumanoidBone.FootR, 0f, -0.03f, 0.14f, 0.045f),
    };

    private struct VariantSpec
    {
        public float gravityRate;
        public float initialSpeed;
        public float toppleDelay;
        public float restAngle;
        public float hipsDrop;
        public float bounce;
    }

    private readonly Quaternion[] startRotation = new Quaternion[HumanoidParts.BoneCount];
    private readonly Quaternion[] targetRotation = new Quaternion[HumanoidParts.BoneCount];
    private readonly Quaternion[] localRotation = new Quaternion[HumanoidParts.BoneCount];
    private readonly Vector3[] worldPosition = new Vector3[HumanoidParts.BoneCount];
    private readonly Quaternion[] worldRotation = new Quaternion[HumanoidParts.BoneCount];
    private readonly float[] boneDelay = new float[HumanoidParts.BoneCount];
    private readonly float[] boneFrequency = new float[HumanoidParts.BoneCount];
    private readonly Vector3 startHips;
    private readonly Vector3 targetHipsOffset;
    private readonly Vector3 axis;
    private readonly VariantSpec spec;
    private readonly float baseline;

    private float elapsed;
    private float accumulator;
    private float angle;
    private float angularSpeed;
    private int bounces;
    private float lift;

    public HumanoidDeathAnimator(HumanoidAnimator startPose, DeathVariant variant, float impulse = 1f)
    {
        Variant = variant;
        FallDirection = DeathVariantSelector.FallDirection(variant);
        axis = Vector3.Cross(Vector3.up, FallDirection);
        spec = GetSpec(variant);
        angularSpeed = spec.initialSpeed * Mathf.Clamp(impulse, 0.4f, 1.8f);
        angle = StartAngle;

        for (int i = 0; i < HumanoidParts.BoneCount; i++)
        {
            startRotation[i] = startPose.GetLocalRotation((HumanoidBone)i);
            localRotation[i] = startRotation[i];
            boneFrequency[i] = 11f;
        }

        startHips = startPose.HipsOffset;
        BuildTargets(variant, targetRotation);
        targetHipsOffset = new Vector3(startHips.x * 0.2f, startHips.y - spec.hipsDrop, startHips.z);
        ConfigureBones();

        HipsOffset = startHips;
        UpdateWorld();
        baseline = LowestPoint(RootRotation);
        Evaluate();
    }

    public DeathVariant Variant { get; }

    /// <summary>Horizontal direction (body space) the torso topples towards.</summary>
    public Vector3 FallDirection { get; }

    public float Time => elapsed;

    /// <summary>Topple angle in degrees: 0 standing, about 90 lying.</summary>
    public float FallAngle => angle * Mathf.Rad2Deg;

    /// <summary>True once the topple angle has stopped bouncing and every joint spring has decayed.</summary>
    public bool Settled { get; private set; }

    public Vector3 HipsOffset { get; private set; }

    public Quaternion RootRotation { get; private set; } = Quaternion.identity;

    public Vector3 RootPosition { get; private set; }

    public Quaternion GetLocalRotation(HumanoidBone bone) => localRotation[(int)bone];

    /// <summary>Position in unscaled body-root space including the death root transform.</summary>
    public Vector3 GetWorldPosition(HumanoidBone bone) => RootRotation * worldPosition[(int)bone] + RootPosition;

    public Quaternion GetWorldRotation(HumanoidBone bone) => RootRotation * worldRotation[(int)bone];

    /// <summary>Lowest point of the body volumes in unscaled body space (about 0 once the corpse rests on the floor).</summary>
    public float LowestBodyPoint => LowestPoint(RootRotation) + lift - baseline;

    public void Tick(float deltaTime)
    {
        accumulator += Mathf.Clamp(deltaTime, 0f, 0.1f);
        while (accumulator >= SubStep)
        {
            accumulator -= SubStep;
            Step(SubStep);
        }

        Evaluate();
    }

    private void Step(float dt)
    {
        elapsed += dt;
        float ramp = Mathf.Clamp01((elapsed - spec.toppleDelay) / 0.25f);
        ramp = ramp * ramp * (3f - 2f * ramp);

        float acceleration = spec.gravityRate * Mathf.Sin(angle) * ramp - Damping * angularSpeed;
        if (elapsed < spec.toppleDelay)
            acceleration = 0f;

        angularSpeed += acceleration * dt;
        angle += angularSpeed * dt;

        float rest = spec.restAngle * Mathf.Deg2Rad;
        if (angle >= rest)
        {
            angle = rest;
            if (angularSpeed > RestSpeed && bounces < 3)
            {
                angularSpeed = -angularSpeed * spec.bounce;
                bounces++;
            }
            else
            {
                angularSpeed = 0f;
            }
        }
        else if (angle < StartAngle && angularSpeed < 0f)
        {
            angle = StartAngle;
            angularSpeed = 0f;
        }

        Settled = elapsed >= spec.toppleDelay + MinSettleTime && angle >= rest - 1e-4f && angularSpeed == 0f;
    }

    private void Evaluate()
    {
        for (int i = 0; i < HumanoidParts.BoneCount; i++)
        {
            float s = SpringResponse(elapsed - boneDelay[i], boneFrequency[i]);
            localRotation[i] = startRotation[i] * ScaleRotation(Quaternion.Inverse(startRotation[i]) * targetRotation[i], s);
        }

        float hipsBlend = SpringResponse(elapsed - 0.02f, 9f);
        HipsOffset = startHips + (targetHipsOffset - startHips) * hipsBlend;

        UpdateWorld();

        RootRotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axis);
        lift = baseline - LowestPoint(RootRotation);
        RootPosition = new Vector3(0f, lift, 0f);
    }

    private void UpdateWorld()
    {
        for (int i = 0; i < HumanoidParts.BoneCount; i++)
        {
            var bone = (HumanoidBone)i;
            Vector3 local = HumanoidParts.GetRestLocalPosition(bone);
            if (bone == HumanoidBone.Hips)
                local += HipsOffset;

            HumanoidBone? parent = HumanoidParts.GetParent(bone);
            if (parent.HasValue)
            {
                int p = (int)parent.Value;
                worldRotation[i] = worldRotation[p] * localRotation[i];
                worldPosition[i] = worldPosition[p] + worldRotation[p] * local;
            }
            else
            {
                worldRotation[i] = localRotation[i];
                worldPosition[i] = local;
            }
        }
    }

    private float LowestPoint(Quaternion root)
    {
        float lowest = float.MaxValue;
        for (int i = 0; i < Probes.Length; i++)
        {
            int b = (int)Probes[i].bone;
            Vector3 p = root * (worldPosition[b] + worldRotation[b] * Probes[i].local);
            lowest = Mathf.Min(lowest, p.y - Probes[i].radius);
        }

        return lowest;
    }

    /// <summary>Unit step response of an under-damped spring: overshoots and rings before settling at 1.</summary>
    internal static float SpringResponse(float t, float frequency)
    {
        if (t <= 0f)
            return 0f;
        const float zeta = 0.42f;
        float wd = frequency * Mathf.Sqrt(1f - zeta * zeta);
        float decay = Mathf.Exp(-zeta * frequency * t);
        return 1f - decay * (Mathf.Cos(wd * t) + zeta / Mathf.Sqrt(1f - zeta * zeta) * Mathf.Sin(wd * t));
    }

    /// <summary>Rotation about the same axis as <paramref name="q"/> by <paramref name="s"/> times its angle (s may exceed 1).</summary>
    internal static Quaternion ScaleRotation(Quaternion q, float s)
    {
        if (q.w < 0f)
            q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
        float w = Mathf.Clamp(q.w, -1f, 1f);
        float sinHalf = Mathf.Sqrt(Mathf.Max(0f, 1f - w * w));
        if (sinHalf < 1e-5f)
            return Quaternion.identity;
        Vector3 rotationAxis = new Vector3(q.x, q.y, q.z) / sinHalf;
        float degrees = 2f * Mathf.Acos(w) * Mathf.Rad2Deg;
        return Quaternion.AngleAxis(degrees * s, rotationAxis);
    }

    private void ConfigureBones()
    {
        for (int i = 0; i < HumanoidParts.BoneCount; i++)
        {
            switch ((HumanoidBone)i)
            {
                case HumanoidBone.Spine: boneFrequency[i] = 13f; boneDelay[i] = 0f; break;
                case HumanoidBone.Chest: boneFrequency[i] = 12f; boneDelay[i] = 0.02f; break;
                case HumanoidBone.Neck: boneFrequency[i] = 10f; boneDelay[i] = 0.04f; break;
                case HumanoidBone.Head: boneFrequency[i] = 9f; boneDelay[i] = 0.06f; break;
                case HumanoidBone.UpperArmL:
                case HumanoidBone.UpperArmR: boneFrequency[i] = 10f; boneDelay[i] = 0.05f; break;
                case HumanoidBone.ForearmL:
                case HumanoidBone.ForearmR: boneFrequency[i] = 8.5f; boneDelay[i] = 0.09f; break;
                case HumanoidBone.HandL:
                case HumanoidBone.HandR: boneFrequency[i] = 8f; boneDelay[i] = 0.12f; break;
                case HumanoidBone.ThighL:
                case HumanoidBone.ThighR: boneFrequency[i] = 11f; boneDelay[i] = 0.03f; break;
                case HumanoidBone.ShinL:
                case HumanoidBone.ShinR: boneFrequency[i] = 9.5f; boneDelay[i] = 0.07f; break;
                case HumanoidBone.FootL:
                case HumanoidBone.FootR: boneFrequency[i] = 9f; boneDelay[i] = 0.1f; break;
                default: boneFrequency[i] = 12f; boneDelay[i] = 0f; break;
            }
        }
    }

    private static VariantSpec GetSpec(DeathVariant variant)
    {
        switch (variant)
        {
            case DeathVariant.HeadshotSnapBack:
                return new VariantSpec { gravityRate = 18f, initialSpeed = 3.2f, toppleDelay = 0f, restAngle = 90f, hipsDrop = 0.04f, bounce = 0.3f };
            case DeathVariant.ForwardCollapse:
                return new VariantSpec { gravityRate = 15f, initialSpeed = 1.7f, toppleDelay = 0f, restAngle = 89f, hipsDrop = 0.1f, bounce = 0.2f };
            case DeathVariant.SideFallLeft:
            case DeathVariant.SideFallRight:
                return new VariantSpec { gravityRate = 16f, initialSpeed = 1.5f, toppleDelay = 0f, restAngle = 88f, hipsDrop = 0.08f, bounce = 0.2f };
            case DeathVariant.KneelCollapse:
                return new VariantSpec { gravityRate = 14f, initialSpeed = 0.15f, toppleDelay = 0.42f, restAngle = 89f, hipsDrop = 0.5f, bounce = 0.18f };
            default:
                return new VariantSpec { gravityRate = 16f, initialSpeed = 1.6f, toppleDelay = 0f, restAngle = 90f, hipsDrop = 0.06f, bounce = 0.25f };
        }
    }

    // ---- Limb pose tables (bone-local Euler degrees; limbs hang along -Y, +X pitch swings a hanging limb backwards,
    // +Z swings it towards the soldier's right). Left/right tables for the side falls are mirrored from the right fall.

    private static void BuildTargets(DeathVariant variant, Quaternion[] result)
    {
        var e = new Vector3[HumanoidParts.BoneCount];
        bool mirror = false;

        switch (variant)
        {
            case DeathVariant.ForwardCollapse:
                e[(int)HumanoidBone.Spine] = V(12, 0, 0);
                e[(int)HumanoidBone.Chest] = V(10, 0, 0);
                e[(int)HumanoidBone.Neck] = V(14, 24, 0);
                e[(int)HumanoidBone.Head] = V(10, 28, 6);
                e[(int)HumanoidBone.UpperArmL] = V(-145, 0, -18);
                e[(int)HumanoidBone.UpperArmR] = V(-125, 0, 22);
                e[(int)HumanoidBone.ForearmL] = V(-15, 0, 0);
                e[(int)HumanoidBone.ForearmR] = V(-30, 0, 0);
                e[(int)HumanoidBone.ThighL] = V(-8, 0, -8);
                e[(int)HumanoidBone.ThighR] = V(-30, 0, 8);
                e[(int)HumanoidBone.ShinL] = V(14, 0, 0);
                e[(int)HumanoidBone.ShinR] = V(46, 0, 0);
                e[(int)HumanoidBone.FootL] = V(20, 0, 0);
                e[(int)HumanoidBone.FootR] = V(30, 0, 0);
                break;

            case DeathVariant.HeadshotSnapBack:
                e[(int)HumanoidBone.Spine] = V(-10, 0, 0);
                e[(int)HumanoidBone.Chest] = V(-20, 0, 0);
                e[(int)HumanoidBone.Neck] = V(-36, 0, 0);
                e[(int)HumanoidBone.Head] = V(-28, 0, 0);
                e[(int)HumanoidBone.UpperArmL] = V(-80, 0, -35);
                e[(int)HumanoidBone.UpperArmR] = V(-95, 0, 35);
                e[(int)HumanoidBone.ForearmL] = V(-35, 0, 0);
                e[(int)HumanoidBone.ForearmR] = V(-50, 0, 0);
                e[(int)HumanoidBone.ThighL] = V(-20, 0, -18);
                e[(int)HumanoidBone.ThighR] = V(-8, 0, 16);
                e[(int)HumanoidBone.ShinL] = V(30, 0, 0);
                e[(int)HumanoidBone.ShinR] = V(12, 0, 0);
                e[(int)HumanoidBone.FootL] = V(14, 0, 0);
                e[(int)HumanoidBone.FootR] = V(-8, 0, 0);
                break;

            case DeathVariant.SideFallRight:
            case DeathVariant.SideFallLeft:
                mirror = variant == DeathVariant.SideFallLeft;
                e[(int)HumanoidBone.Spine] = V(6, 0, -6);
                e[(int)HumanoidBone.Chest] = V(4, 0, -10);
                e[(int)HumanoidBone.Neck] = V(4, 0, -12);
                e[(int)HumanoidBone.Head] = V(0, 0, -8);
                e[(int)HumanoidBone.UpperArmL] = V(-30, 0, 50);
                e[(int)HumanoidBone.UpperArmR] = V(10, 0, 20);
                e[(int)HumanoidBone.ForearmL] = V(-40, 0, 0);
                e[(int)HumanoidBone.ForearmR] = V(-25, 0, 0);
                e[(int)HumanoidBone.ThighL] = V(-35, 0, 6);
                e[(int)HumanoidBone.ThighR] = V(-12, 0, 0);
                e[(int)HumanoidBone.ShinL] = V(50, 0, 0);
                e[(int)HumanoidBone.ShinR] = V(20, 0, 0);
                e[(int)HumanoidBone.FootL] = V(18, 0, 0);
                e[(int)HumanoidBone.FootR] = V(8, 0, 0);
                break;

            case DeathVariant.KneelCollapse:
                e[(int)HumanoidBone.Hips] = V(0, 0, 0);
                e[(int)HumanoidBone.Spine] = V(25, 0, 0);
                e[(int)HumanoidBone.Chest] = V(15, 0, 0);
                e[(int)HumanoidBone.Neck] = V(10, 6, 0);
                e[(int)HumanoidBone.Head] = V(15, 8, 0);
                e[(int)HumanoidBone.UpperArmL] = V(-15, 0, -15);
                e[(int)HumanoidBone.UpperArmR] = V(-20, 0, 15);
                e[(int)HumanoidBone.ForearmL] = V(-20, 0, 0);
                e[(int)HumanoidBone.ForearmR] = V(-30, 0, 0);
                e[(int)HumanoidBone.ThighL] = V(-60, 0, -6);
                e[(int)HumanoidBone.ThighR] = V(-52, 0, 6);
                e[(int)HumanoidBone.ShinL] = V(120, 0, 0);
                e[(int)HumanoidBone.ShinR] = V(110, 0, 0);
                e[(int)HumanoidBone.FootL] = V(30, 0, 0);
                e[(int)HumanoidBone.FootR] = V(26, 0, 0);
                break;

            default:
                e[(int)HumanoidBone.Spine] = V(-5, 0, 0);
                e[(int)HumanoidBone.Chest] = V(-8, 0, 0);
                e[(int)HumanoidBone.Neck] = V(-12, 8, 0);
                e[(int)HumanoidBone.Head] = V(-10, 12, 0);
                e[(int)HumanoidBone.UpperArmL] = V(-20, 0, -40);
                e[(int)HumanoidBone.UpperArmR] = V(-35, 0, 55);
                e[(int)HumanoidBone.ForearmL] = V(-30, 0, 0);
                e[(int)HumanoidBone.ForearmR] = V(-15, 0, 0);
                e[(int)HumanoidBone.HandL] = V(0, 0, -10);
                e[(int)HumanoidBone.HandR] = V(0, 0, 10);
                e[(int)HumanoidBone.ThighL] = V(-10, 0, -14);
                e[(int)HumanoidBone.ThighR] = V(-4, 0, 12);
                e[(int)HumanoidBone.ShinL] = V(22, 0, 0);
                e[(int)HumanoidBone.ShinR] = V(8, 0, 0);
                e[(int)HumanoidBone.FootL] = V(15, 0, 0);
                e[(int)HumanoidBone.FootR] = V(-10, 0, 0);
                break;
        }

        for (int i = 0; i < HumanoidParts.BoneCount; i++)
        {
            int source = mirror ? (int)Mirrored((HumanoidBone)i) : i;
            Vector3 euler = e[source];
            if (mirror)
                euler = new Vector3(euler.x, -euler.y, -euler.z);
            result[i] = Quaternion.Euler(euler.x, euler.y, euler.z);
        }
    }

    private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    internal static HumanoidBone Mirrored(HumanoidBone bone)
    {
        switch (bone)
        {
            case HumanoidBone.ShoulderL: return HumanoidBone.ShoulderR;
            case HumanoidBone.ShoulderR: return HumanoidBone.ShoulderL;
            case HumanoidBone.UpperArmL: return HumanoidBone.UpperArmR;
            case HumanoidBone.UpperArmR: return HumanoidBone.UpperArmL;
            case HumanoidBone.ForearmL: return HumanoidBone.ForearmR;
            case HumanoidBone.ForearmR: return HumanoidBone.ForearmL;
            case HumanoidBone.HandL: return HumanoidBone.HandR;
            case HumanoidBone.HandR: return HumanoidBone.HandL;
            case HumanoidBone.ThighL: return HumanoidBone.ThighR;
            case HumanoidBone.ThighR: return HumanoidBone.ThighL;
            case HumanoidBone.ShinL: return HumanoidBone.ShinR;
            case HumanoidBone.ShinR: return HumanoidBone.ShinL;
            case HumanoidBone.FootL: return HumanoidBone.FootR;
            case HumanoidBone.FootR: return HumanoidBone.FootL;
            default: return bone;
        }
    }
}
