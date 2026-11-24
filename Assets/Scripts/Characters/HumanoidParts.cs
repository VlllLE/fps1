using UnityEngine;

public enum HumanoidBone
{
    Hips = 0,
    Spine,
    Chest,
    Neck,
    Head,
    ShoulderL,
    ShoulderR,
    UpperArmL,
    UpperArmR,
    ForearmL,
    ForearmR,
    HandL,
    HandR,
    ThighL,
    ThighR,
    ShinL,
    ShinR,
    FootL,
    FootR
}

/// <summary>
/// Hand-authored procedural geometry for each bone of the stylised humanoid. All parts are
/// modelled in bone space (bone origin at the joint, limbs hanging towards -Y, forward +Z) so
/// the body can be animated by rotating bones without any skinning.
/// </summary>
public static class HumanoidParts
{
    public const int BoneCount = 19;

    public static readonly HumanoidBone[] AllBones =
    {
        HumanoidBone.Hips, HumanoidBone.Spine, HumanoidBone.Chest, HumanoidBone.Neck, HumanoidBone.Head,
        HumanoidBone.ShoulderL, HumanoidBone.ShoulderR, HumanoidBone.UpperArmL, HumanoidBone.UpperArmR,
        HumanoidBone.ForearmL, HumanoidBone.ForearmR, HumanoidBone.HandL, HumanoidBone.HandR,
        HumanoidBone.ThighL, HumanoidBone.ThighR, HumanoidBone.ShinL, HumanoidBone.ShinR,
        HumanoidBone.FootL, HumanoidBone.FootR
    };

    /// <summary>Parent bone, or null when the bone hangs directly under the body root.</summary>
    public static HumanoidBone? GetParent(HumanoidBone bone)
    {
        switch (bone)
        {
            case HumanoidBone.Hips: return null;
            case HumanoidBone.Spine: return HumanoidBone.Hips;
            case HumanoidBone.Chest: return HumanoidBone.Spine;
            case HumanoidBone.Neck: return HumanoidBone.Chest;
            case HumanoidBone.Head: return HumanoidBone.Neck;
            case HumanoidBone.ShoulderL:
            case HumanoidBone.ShoulderR: return HumanoidBone.Chest;
            case HumanoidBone.UpperArmL: return HumanoidBone.ShoulderL;
            case HumanoidBone.UpperArmR: return HumanoidBone.ShoulderR;
            case HumanoidBone.ForearmL: return HumanoidBone.UpperArmL;
            case HumanoidBone.ForearmR: return HumanoidBone.UpperArmR;
            case HumanoidBone.HandL: return HumanoidBone.ForearmL;
            case HumanoidBone.HandR: return HumanoidBone.ForearmR;
            case HumanoidBone.ThighL:
            case HumanoidBone.ThighR: return HumanoidBone.Hips;
            case HumanoidBone.ShinL: return HumanoidBone.ThighL;
            case HumanoidBone.ShinR: return HumanoidBone.ThighR;
            case HumanoidBone.FootL: return HumanoidBone.ShinL;
            case HumanoidBone.FootR: return HumanoidBone.ShinR;
            default: return null;
        }
    }

    /// <summary>Unscaled rest position relative to the parent bone (or body root).</summary>
    public static Vector3 GetRestLocalPosition(HumanoidBone bone)
    {
        switch (bone)
        {
            case HumanoidBone.Hips: return new Vector3(0f, HumanoidProportions.HipsY, 0f);
            case HumanoidBone.Spine: return new Vector3(0f, HumanoidProportions.WaistY - HumanoidProportions.HipsY, 0f);
            case HumanoidBone.Chest: return new Vector3(0f, HumanoidProportions.ChestY - HumanoidProportions.WaistY, 0f);
            case HumanoidBone.Neck: return new Vector3(0f, HumanoidProportions.NeckY - HumanoidProportions.ChestY, 0f);
            case HumanoidBone.Head: return new Vector3(0f, HumanoidProportions.HeadY - HumanoidProportions.NeckY, 0f);
            case HumanoidBone.ShoulderL:
                return new Vector3(-HumanoidProportions.ShoulderHalfWidth, HumanoidProportions.ShoulderY - HumanoidProportions.ChestY, 0f);
            case HumanoidBone.ShoulderR:
                return new Vector3(HumanoidProportions.ShoulderHalfWidth, HumanoidProportions.ShoulderY - HumanoidProportions.ChestY, 0f);
            case HumanoidBone.ForearmL:
            case HumanoidBone.ForearmR: return new Vector3(0f, -HumanoidProportions.UpperArmLength, 0f);
            case HumanoidBone.HandL:
            case HumanoidBone.HandR: return new Vector3(0f, -HumanoidProportions.ForearmLength, 0f);
            case HumanoidBone.ThighL:
                return new Vector3(-HumanoidProportions.HipHalfWidth, HumanoidProportions.HipJointY - HumanoidProportions.HipsY, 0f);
            case HumanoidBone.ThighR:
                return new Vector3(HumanoidProportions.HipHalfWidth, HumanoidProportions.HipJointY - HumanoidProportions.HipsY, 0f);
            case HumanoidBone.ShinL:
            case HumanoidBone.ShinR: return new Vector3(0f, -HumanoidProportions.ThighLength, 0f);
            case HumanoidBone.FootL:
            case HumanoidBone.FootR: return new Vector3(0f, -HumanoidProportions.ShinLength, 0f);
            default: return Vector3.zero;
        }
    }

    public static bool IsLeft(HumanoidBone bone)
    {
        switch (bone)
        {
            case HumanoidBone.ShoulderL:
            case HumanoidBone.UpperArmL:
            case HumanoidBone.ForearmL:
            case HumanoidBone.HandL:
            case HumanoidBone.ThighL:
            case HumanoidBone.ShinL:
            case HumanoidBone.FootL:
                return true;
            default:
                return false;
        }
    }

    /// <summary>Builds the geometry attached to a bone, uniformly scaled by <paramref name="scale"/>.</summary>
    public static HumanoidMeshBuilder Build(HumanoidBone bone, float scale)
    {
        var b = new HumanoidMeshBuilder { Root = Matrix4x4.Scale(Vector3.one * scale) };
        float side = IsLeft(bone) ? -1f : 1f;

        switch (bone)
        {
            case HumanoidBone.Hips: BuildHips(b); break;
            case HumanoidBone.Spine: BuildSpine(b); break;
            case HumanoidBone.Chest: BuildChest(b); break;
            case HumanoidBone.Neck: BuildNeck(b); break;
            case HumanoidBone.Head: BuildHead(b); break;
            case HumanoidBone.ShoulderL:
            case HumanoidBone.ShoulderR: BuildShoulder(b, side); break;
            case HumanoidBone.UpperArmL:
            case HumanoidBone.UpperArmR: BuildUpperArm(b); break;
            case HumanoidBone.ForearmL:
            case HumanoidBone.ForearmR: BuildForearm(b); break;
            case HumanoidBone.HandL:
            case HumanoidBone.HandR: BuildHand(b, side); break;
            case HumanoidBone.ThighL:
            case HumanoidBone.ThighR: BuildThigh(b, side); break;
            case HumanoidBone.ShinL:
            case HumanoidBone.ShinR: BuildShin(b); break;
            case HumanoidBone.FootL:
            case HumanoidBone.FootR: BuildFoot(b); break;
        }

        return b;
    }

    private static void BuildHips(HumanoidMeshBuilder b)
    {
        b.AddLoft(HumanoidSlot.SuitDark, new[]
        {
            new LoftRing(-0.14f, 0.10f, 0.085f),
            new LoftRing(-0.10f, 0.16f, 0.105f),
            new LoftRing(-0.04f, 0.175f, 0.11f),
            new LoftRing(0.03f, 0.168f, 0.108f),
            new LoftRing(0.06f, 0.158f, 0.103f),
        }, 20, true, false);

        b.AddLoft(HumanoidSlot.Accent, new[]
        {
            new LoftRing(0.0f, 0.178f, 0.117f),
            new LoftRing(0.015f, 0.18f, 0.12f),
            new LoftRing(0.055f, 0.18f, 0.12f),
            new LoftRing(0.06f, 0.176f, 0.116f),
        }, 20, true, true);
        b.AddBox(HumanoidSlot.Accent, new Vector3(0f, 0.03f, 0.12f), new Vector3(0.045f, 0.04f, 0.02f));
    }

    private static void BuildSpine(HumanoidMeshBuilder b)
    {
        b.AddLoft(HumanoidSlot.Suit, new[]
        {
            new LoftRing(0.0f, 0.158f, 0.103f),
            new LoftRing(0.07f, 0.155f, 0.10f),
            new LoftRing(0.13f, 0.168f, 0.108f),
            new LoftRing(0.18f, 0.18f, 0.115f),
        }, 20);
    }

    private static void BuildChest(HumanoidMeshBuilder b)
    {
        b.AddLoft(HumanoidSlot.Suit, new[]
        {
            new LoftRing(0.0f, 0.18f, 0.115f),
            new LoftRing(0.07f, 0.195f, 0.125f),
            new LoftRing(0.14f, 0.20f, 0.122f),
            new LoftRing(0.19f, 0.185f, 0.11f),
            new LoftRing(0.225f, 0.12f, 0.085f),
            new LoftRing(0.245f, 0.07f, 0.065f),
            new LoftRing(0.262f, 0.004f, 0.004f),
        }, 22);

        b.AddLoft(HumanoidSlot.SuitDark, new[]
        {
            new LoftRing(0.02f, 0.188f, 0.12f),
            new LoftRing(0.07f, 0.20f, 0.13f),
            new LoftRing(0.14f, 0.205f, 0.127f),
            new LoftRing(0.17f, 0.20f, 0.12f),
            new LoftRing(0.185f, 0.19f, 0.113f),
        }, 22, true, true);

        b.AddBox(HumanoidSlot.Accent, new Vector3(0f, 0.09f, 0.135f), new Vector3(0.15f, 0.07f, 0.035f));
        b.AddBox(HumanoidSlot.SuitDark, new Vector3(0f, 0.07f, -0.16f), new Vector3(0.26f, 0.30f, 0.10f));
    }

    private static void BuildNeck(HumanoidMeshBuilder b)
    {
        b.AddLoft(HumanoidSlot.Skin, new[]
        {
            new LoftRing(-0.04f, 0.052f),
            new LoftRing(0.0f, 0.05f),
            new LoftRing(0.06f, 0.046f),
            new LoftRing(0.10f, 0.046f),
        }, 14, true, true);
    }

    private static void BuildHead(HumanoidMeshBuilder b)
    {
        b.AddEllipsoid(HumanoidSlot.Skin, new Vector3(0f, 0.165f, 0f), new Vector3(0.105f, 0.125f, 0.115f), 10, 20);
        b.AddEllipsoid(HumanoidSlot.Skin, new Vector3(0f, 0.085f, 0.03f), new Vector3(0.078f, 0.065f, 0.08f), 6, 16);
        b.AddEllipsoid(HumanoidSlot.Skin, new Vector3(0f, 0.125f, 0.118f), new Vector3(0.014f, 0.022f, 0.022f), 4, 8);
        b.AddEllipsoid(HumanoidSlot.Skin, new Vector3(-0.105f, 0.15f, 0f), new Vector3(0.014f, 0.028f, 0.02f), 4, 8);
        b.AddEllipsoid(HumanoidSlot.Skin, new Vector3(0.105f, 0.15f, 0f), new Vector3(0.014f, 0.028f, 0.02f), 4, 8);
        b.AddEllipsoid(HumanoidSlot.Visor, new Vector3(0f, 0.175f, 0.07f), new Vector3(0.09f, 0.028f, 0.062f), 6, 16);
        b.AddEllipsoid(HumanoidSlot.Suit, new Vector3(0f, 0.175f, -0.005f), new Vector3(0.112f, 0.13f, 0.122f), 6, 20, 62f, 0f);
        b.AddEllipsoid(HumanoidSlot.SuitDark, new Vector3(0f, 0.236f, 0.075f), new Vector3(0.075f, 0.01f, 0.055f), 3, 12);
    }

    private static void BuildShoulder(HumanoidMeshBuilder b, float side)
    {
        b.AddEllipsoid(HumanoidSlot.Suit, new Vector3(side * 0.005f, 0.012f, 0f), new Vector3(0.07f, 0.062f, 0.068f), 8, 16);
        b.AddEllipsoid(HumanoidSlot.SuitDark, new Vector3(side * 0.015f, 0.01f, 0f), new Vector3(0.078f, 0.07f, 0.075f), 5, 16, 75f, 0f);
    }

    private static void BuildUpperArm(HumanoidMeshBuilder b)
    {
        b.AddLoft(HumanoidSlot.Suit, new[]
        {
            new LoftRing(-0.29f, 0.043f),
            new LoftRing(-0.25f, 0.045f),
            new LoftRing(-0.14f, 0.05f),
            new LoftRing(-0.04f, 0.056f),
            new LoftRing(0.0f, 0.056f),
            new LoftRing(0.03f, 0.004f),
        }, 14, true, false);
    }

    private static void BuildForearm(HumanoidMeshBuilder b)
    {
        b.AddEllipsoid(HumanoidSlot.SuitDark, Vector3.zero, new Vector3(0.05f, 0.05f, 0.05f), 6, 14);
        b.AddLoft(HumanoidSlot.Suit, new[]
        {
            new LoftRing(-0.27f, 0.032f),
            new LoftRing(-0.24f, 0.034f),
            new LoftRing(-0.12f, 0.042f),
            new LoftRing(-0.03f, 0.046f),
            new LoftRing(0.0f, 0.045f),
            new LoftRing(0.03f, 0.004f),
        }, 14, true, false);
        b.AddLoft(HumanoidSlot.Accent, new[]
        {
            new LoftRing(-0.245f, 0.036f),
            new LoftRing(-0.24f, 0.039f),
            new LoftRing(-0.19f, 0.043f),
            new LoftRing(-0.185f, 0.040f),
        }, 14, true, true);
    }

    private static void BuildHand(HumanoidMeshBuilder b, float side)
    {
        b.AddEllipsoid(HumanoidSlot.Skin, new Vector3(0f, -0.045f, 0f), new Vector3(0.034f, 0.05f, 0.02f), 6, 12);
        b.AddEllipsoid(HumanoidSlot.Skin, new Vector3(0f, -0.095f, 0.004f), new Vector3(0.03f, 0.03f, 0.016f), 5, 12);
        b.AddEllipsoid(HumanoidSlot.Skin, new Vector3(-side * 0.03f, -0.05f, 0.018f), new Vector3(0.012f, 0.03f, 0.012f), 4, 8);
    }

    private static void BuildThigh(HumanoidMeshBuilder b, float side)
    {
        b.AddLoft(HumanoidSlot.SuitDark, new[]
        {
            new LoftRing(-0.42f, 0.060f),
            new LoftRing(-0.36f, 0.062f),
            new LoftRing(-0.25f, 0.073f),
            new LoftRing(-0.10f, 0.088f),
            new LoftRing(0.0f, 0.09f),
            new LoftRing(0.03f, 0.08f),
            new LoftRing(0.05f, 0.004f),
        }, 16, true, false);
        b.AddEllipsoid(HumanoidSlot.SuitDark, new Vector3(0f, -0.42f, 0f), new Vector3(0.062f, 0.06f, 0.062f), 6, 14);
        b.AddEllipsoid(HumanoidSlot.Accent, new Vector3(0f, -0.42f, 0.045f), new Vector3(0.048f, 0.05f, 0.028f), 5, 12);
        b.AddBox(HumanoidSlot.Accent, new Vector3(side * 0.09f, -0.2f, 0f), new Vector3(0.035f, 0.12f, 0.09f));
    }

    private static void BuildShin(HumanoidMeshBuilder b)
    {
        b.AddLoft(HumanoidSlot.SuitDark, new[]
        {
            new LoftRing(-0.30f, 0.050f),
            new LoftRing(-0.20f, 0.050f),
            new LoftRing(-0.10f, 0.056f),
            new LoftRing(0.0f, 0.062f),
            new LoftRing(0.02f, 0.004f),
        }, 14, true, false);
        b.AddLoft(HumanoidSlot.Accent, new[]
        {
            new LoftRing(-0.42f, 0.052f),
            new LoftRing(-0.40f, 0.056f),
            new LoftRing(-0.30f, 0.058f),
            new LoftRing(-0.24f, 0.060f),
            new LoftRing(-0.225f, 0.056f),
        }, 14, true, true);
    }

    private static void BuildFoot(HumanoidMeshBuilder b)
    {
        // Lofted along +Y then rotated so the loft axis points forward (+Z). Ring centres are
        // chosen so the sole is flat 0.10 below the ankle.
        const float soleDrop = 0.10f;
        float[] t = { -0.075f, -0.06f, -0.03f, 0.03f, 0.09f, 0.15f, 0.19f, 0.205f };
        float[] rx = { 0.018f, 0.04f, 0.05f, 0.052f, 0.05f, 0.045f, 0.03f, 0.004f };
        float[] rz = { 0.02f, 0.04f, 0.05f, 0.05f, 0.036f, 0.028f, 0.02f, 0.004f };
        var rings = new LoftRing[t.Length];
        for (int i = 0; i < t.Length; i++)
            rings[i] = new LoftRing(t[i], rx[i], rz[i], 0f, soleDrop - rz[i]);

        // Columns: loft X -> X, loft Y -> Z (forward), loft Z -> -Y (a +90 degree turn about X).
        var alongForward = new Matrix4x4(
            new Vector4(1f, 0f, 0f, 0f),
            new Vector4(0f, 0f, 1f, 0f),
            new Vector4(0f, -1f, 0f, 0f),
            new Vector4(0f, 0f, 0f, 1f));
        b.AddLoft(HumanoidSlot.Accent, rings, 14, alongForward, false, false);
    }
}
