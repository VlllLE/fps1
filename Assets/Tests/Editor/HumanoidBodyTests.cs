using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class HumanoidBodyTests
{
    private const float CapsuleRadius = 0.4f;
    private const float CapsuleHeight = 2f;

    private readonly List<Object> created = new List<Object>();
    private static HumanoidMeshBuilder[] partCache;

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
                Object.DestroyImmediate(created[i]);
        }

        created.Clear();
    }

    // --- Geometry ---------------------------------------------------------------------------

    [Test]
    public void Parts_AreValidMeshesWithOutwardWinding()
    {
        foreach (HumanoidBone bone in HumanoidParts.AllBones)
        {
            HumanoidMeshBuilder b = HumanoidParts.Build(bone, 1f);
            Assert.That(b.VertexCount, Is.GreaterThan(0), bone + " has no vertices");
            Assert.That(b.TriangleCount, Is.GreaterThan(0), bone + " has no triangles");
            Assert.That(b.Normals.Count, Is.EqualTo(b.VertexCount), bone + " normal count");
            Assert.That(b.Uvs.Count, Is.EqualTo(b.VertexCount), bone + " uv count");

            for (int i = 0; i < b.VertexCount; i++)
            {
                Vector3 v = b.Vertices[i];
                Assert.That(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), Is.False, bone + " NaN vertex");
                Assert.That(b.Normals[i].magnitude, Is.EqualTo(1f).Within(0.02f), bone + " normal length");
            }

            int wrong = 0;
            for (int slot = 0; slot < HumanoidMeshBuilder.SlotCount; slot++)
            {
                IReadOnlyList<int> tris = b.GetTriangles((HumanoidSlot)slot);
                Assert.That(tris.Count % 3, Is.Zero);
                for (int t = 0; t < tris.Count; t += 3)
                {
                    int a = tris[t], c = tris[t + 1], d = tris[t + 2];
                    Assert.That(a, Is.InRange(0, b.VertexCount - 1));
                    Assert.That(c, Is.InRange(0, b.VertexCount - 1));
                    Assert.That(d, Is.InRange(0, b.VertexCount - 1));
                    Vector3 faceNormal = Vector3.Cross(b.Vertices[c] - b.Vertices[a], b.Vertices[d] - b.Vertices[a]);
                    Vector3 vertexNormal = b.Normals[a] + b.Normals[c] + b.Normals[d];
                    if (Vector3.Dot(faceNormal, vertexNormal) < 0f)
                        wrong++;
                }
            }

            Assert.That(wrong, Is.Zero, bone + " has triangles wound against their normals");
        }
    }

    [Test]
    public void Parts_ScaleUniformlyWithBuildScale()
    {
        Bounds full = HumanoidParts.Build(HumanoidBone.Head, 1f).CalculateBounds();
        Bounds half = HumanoidParts.Build(HumanoidBone.Head, 0.5f).CalculateBounds();
        Assert.That(half.size.y, Is.EqualTo(full.size.y * 0.5f).Within(0.002f));
        Assert.That(half.size.x, Is.EqualTo(full.size.x * 0.5f).Within(0.002f));
    }

    [Test]
    public void Skeleton_HasUniqueBonesWithParentsBeforeChildren()
    {
        Assert.That(HumanoidParts.AllBones.Length, Is.EqualTo(HumanoidParts.BoneCount));
        var seen = new HashSet<HumanoidBone>();
        foreach (HumanoidBone bone in HumanoidParts.AllBones)
        {
            HumanoidBone? parent = HumanoidParts.GetParent(bone);
            if (parent.HasValue)
                Assert.That(seen.Contains(parent.Value), Is.True, bone + " listed before its parent");
            Assert.That(seen.Add(bone), Is.True, bone + " listed twice");
        }
    }

    [Test]
    public void RestPose_HasHumanProportionsAndFitsControllerCapsule()
    {
        var animator = new HumanoidAnimator();
        GetExtents(animator, out Vector3 min, out Vector3 max, out float maxRadius);

        Assert.That(min.y, Is.GreaterThanOrEqualTo(-0.01f), "feet below the ground");
        Assert.That(max.y, Is.LessThanOrEqualTo(CapsuleHeight), "head above the capsule");
        Assert.That(max.y, Is.EqualTo(HumanoidProportions.TopOfHeadY).Within(0.02f));
        Assert.That(max.y, Is.GreaterThan(1.7f), "body should read as a full-height character");
        Assert.That(maxRadius, Is.LessThanOrEqualTo(CapsuleRadius), "body wider than the capsule");

        Vector3 head = animator.GetWorldPosition(HumanoidBone.Head);
        Vector3 hips = animator.GetWorldPosition(HumanoidBone.Hips);
        Vector3 foot = animator.GetWorldPosition(HumanoidBone.FootL);
        Vector3 hand = animator.GetWorldPosition(HumanoidBone.HandL);
        Assert.That(head.y, Is.GreaterThan(hips.y + 0.4f), "head above hips");
        Assert.That(foot.y, Is.EqualTo(HumanoidProportions.AnkleY).Within(0.005f));
        Assert.That(hand.y, Is.LessThan(hips.y + 0.1f).And.GreaterThan(hips.y - 0.3f), "relaxed hands hang near the hips");
        Assert.That(animator.GetWorldPosition(HumanoidBone.ShoulderL).x, Is.LessThan(0f));
        Assert.That(animator.GetWorldPosition(HumanoidBone.ShoulderR).x, Is.GreaterThan(0f));
    }

    [Test]
    public void EyeHeight_SitsInsideTheHead()
    {
        var animator = new HumanoidAnimator();
        Vector3 head = animator.GetWorldPosition(HumanoidBone.Head);
        Assert.That(HumanoidProportions.EyeHeight, Is.InRange(head.y - 0.05f, head.y + 0.3f));
    }

    // --- Animation --------------------------------------------------------------------------

    [Test]
    public void Animation_PosesStayInsideControllerHeightAndNeverSinkIntoTheGround()
    {
        var cases = new Dictionary<string, HumanoidAnimationInput>
        {
            { "idle", new HumanoidAnimationInput() },
            { "ready", new HumanoidAnimationInput { armPose = HumanoidArmPose.WeaponReady } },
            { "walk", new HumanoidAnimationInput { forwardSpeed = 6f } },
            { "sprint", new HumanoidAnimationInput { forwardSpeed = 9.6f, armPose = HumanoidArmPose.WeaponReady } },
            { "backpedal", new HumanoidAnimationInput { forwardSpeed = -4f } },
            { "strafe", new HumanoidAnimationInput { rightSpeed = 5f } },
            { "crouch", new HumanoidAnimationInput { crouch01 = 1f } },
            { "slide", new HumanoidAnimationInput { crouch01 = 1f, forwardSpeed = 9f, locomotion = HumanoidLocomotion.Sliding } },
            { "air", new HumanoidAnimationInput { forwardSpeed = 6f, verticalSpeed = 5f, locomotion = HumanoidLocomotion.Airborne } },
            { "dive", new HumanoidAnimationInput { forwardSpeed = 14f, locomotion = HumanoidLocomotion.Diving } },
            { "lookDown", new HumanoidAnimationInput { lookPitchDegrees = 60f } },
            { "lookUp", new HumanoidAnimationInput { lookPitchDegrees = -60f } }
        };

        foreach (KeyValuePair<string, HumanoidAnimationInput> pair in cases)
        {
            var animator = new HumanoidAnimator();
            for (int frame = 0; frame < 137; frame++)
            {
                animator.Tick(1f / 60f, pair.Value);
                if (frame % 17 != 0)
                    continue;

                GetExtents(animator, out Vector3 min, out Vector3 max, out _);
                Assert.That(float.IsNaN(min.y) || float.IsNaN(max.y), Is.False, pair.Key + " produced NaN");
                Assert.That(min.y, Is.GreaterThanOrEqualTo(-0.02f), pair.Key + " sinks into the ground");
                Assert.That(max.y, Is.LessThanOrEqualTo(CapsuleHeight), pair.Key + " pokes out of the capsule top");
            }
        }
    }

    [Test]
    public void Crouch_LowersTheBodyAndKeepsFeetPlanted()
    {
        var animator = new HumanoidAnimator();
        float standingHips = animator.GetWorldPosition(HumanoidBone.Hips).y;
        var input = new HumanoidAnimationInput { crouch01 = 1f };
        for (int i = 0; i < 120; i++)
            animator.Tick(1f / 60f, input);

        Assert.That(animator.GetWorldPosition(HumanoidBone.Hips).y, Is.LessThan(standingHips - 0.4f));
        Assert.That(animator.GetWorldPosition(HumanoidBone.FootL).y, Is.EqualTo(HumanoidProportions.AnkleY).Within(0.02f));
        Assert.That(animator.GetWorldPosition(HumanoidBone.FootR).y, Is.EqualTo(HumanoidProportions.AnkleY).Within(0.02f));
        GetExtents(animator, out _, out Vector3 max, out _);
        Assert.That(max.y, Is.LessThan(1.4f), "crouched body should fit the 1.2 m crouch capsule closely");

        var knee = animator.GetWorldPosition(HumanoidBone.ShinL);
        var hip = animator.GetWorldPosition(HumanoidBone.ThighL);
        var ankle = animator.GetWorldPosition(HumanoidBone.FootL);
        Assert.That(knee.z, Is.GreaterThan(Mathf.Lerp(hip.z, ankle.z, 0.5f)), "knees bend forward");
    }

    [Test]
    public void Walk_AlternatesLegsKeepsBoneLengthsAndSwingsArmsOpposite()
    {
        var animator = new HumanoidAnimator();
        var input = new HumanoidAnimationInput { forwardSpeed = 6f };
        float maxLeftAhead = float.MinValue;
        float maxRightAhead = float.MinValue;
        float armPhaseProduct = 0f;

        for (int i = 0; i < 180; i++)
        {
            animator.Tick(1f / 60f, input);
            if (i < 30)
                continue;

            float leftFoot = animator.GetWorldPosition(HumanoidBone.FootL).z;
            float rightFoot = animator.GetWorldPosition(HumanoidBone.FootR).z;
            maxLeftAhead = Mathf.Max(maxLeftAhead, leftFoot - rightFoot);
            maxRightAhead = Mathf.Max(maxRightAhead, rightFoot - leftFoot);

            float leftHand = animator.GetWorldPosition(HumanoidBone.HandL).z;
            armPhaseProduct += (leftFoot - rightFoot) * (leftHand - animator.GetWorldPosition(HumanoidBone.HandR).z);

            AssertLength(animator, HumanoidBone.ThighL, HumanoidBone.ShinL, HumanoidProportions.ThighLength);
            AssertLength(animator, HumanoidBone.ShinR, HumanoidBone.FootR, HumanoidProportions.ShinLength);
            AssertLength(animator, HumanoidBone.UpperArmL, HumanoidBone.ForearmL, HumanoidProportions.UpperArmLength);
            AssertLength(animator, HumanoidBone.ForearmR, HumanoidBone.HandR, HumanoidProportions.ForearmLength);
        }

        Assert.That(maxLeftAhead, Is.GreaterThan(0.25f), "left foot never steps ahead of the right");
        Assert.That(maxRightAhead, Is.GreaterThan(0.25f), "right foot never steps ahead of the left");
        Assert.That(armPhaseProduct, Is.LessThan(0f), "arms should swing opposite to the legs");
    }

    [Test]
    public void Idle_BreathesWithoutMovingFeet()
    {
        var animator = new HumanoidAnimator();
        var input = new HumanoidAnimationInput();
        for (int i = 0; i < 10; i++)
            animator.Tick(1f / 60f, input);
        Quaternion chestA = animator.GetLocalRotation(HumanoidBone.Chest);
        Vector3 footA = animator.GetWorldPosition(HumanoidBone.FootL);

        for (int i = 0; i < 70; i++)
            animator.Tick(1f / 60f, input);

        Assert.That(Quaternion.Angle(chestA, animator.GetLocalRotation(HumanoidBone.Chest)), Is.GreaterThan(0.2f));
        Assert.That(Vector3.Distance(footA, animator.GetWorldPosition(HumanoidBone.FootL)), Is.LessThan(0.01f));
    }

    [Test]
    public void Tick_IsDeterministic()
    {
        var a = new HumanoidAnimator();
        var b = new HumanoidAnimator();
        var input = new HumanoidAnimationInput { forwardSpeed = 5f, rightSpeed = 1f, crouch01 = 0.3f, lookPitchDegrees = 12f };
        for (int i = 0; i < 90; i++)
        {
            a.Tick(1f / 60f, input);
            b.Tick(1f / 60f, input);
        }

        foreach (HumanoidBone bone in HumanoidParts.AllBones)
            Assert.That(Vector3.Distance(a.GetWorldPosition(bone), b.GetWorldPosition(bone)), Is.LessThan(1e-5f));
    }

    [Test]
    public void LookPitch_TiltsHeadWithoutMovingFeet()
    {
        var animator = new HumanoidAnimator();
        Vector3 foot = animator.GetWorldPosition(HumanoidBone.FootR);
        var input = new HumanoidAnimationInput { lookPitchDegrees = 50f };
        for (int i = 0; i < 90; i++)
            animator.Tick(1f / 60f, input);

        Vector3 headForward = animator.GetWorldRotation(HumanoidBone.Head) * Vector3.forward;
        Assert.That(headForward.y, Is.LessThan(-0.2f), "head should pitch down when looking down");
        Assert.That(Vector3.Distance(foot, animator.GetWorldPosition(HumanoidBone.FootR)), Is.LessThan(0.01f));
    }

    [Test]
    public void Flinch_KicksTheChestThenSettles()
    {
        var animator = new HumanoidAnimator();
        Quaternion rest = animator.GetLocalRotation(HumanoidBone.Chest);
        animator.AddFlinch(1f, Vector3.back);
        animator.Tick(0.016f, new HumanoidAnimationInput());
        float kicked = Quaternion.Angle(rest, animator.GetLocalRotation(HumanoidBone.Chest));
        for (int i = 0; i < 120; i++)
            animator.Tick(1f / 60f, new HumanoidAnimationInput());
        float settled = Quaternion.Angle(rest, animator.GetLocalRotation(HumanoidBone.Chest));

        Assert.That(kicked, Is.GreaterThan(4f));
        Assert.That(settled, Is.LessThan(kicked * 0.5f));
    }

    // --- IK ---------------------------------------------------------------------------------

    [Test]
    public void TwoBoneIK_ReachesReachableTargetsWithFixedBoneLengths()
    {
        Vector3 root = new Vector3(0f, 1f, 0f);
        Vector3 target = new Vector3(0.2f, 0.45f, 0.3f);
        bool reached = HumanoidIK.SolveTwoBone(root, target, 0.42f, 0.42f, Vector3.forward, out Vector3 mid, out Vector3 tip);

        Assert.That(reached, Is.True);
        Assert.That(Vector3.Distance(tip, target), Is.LessThan(0.002f));
        Assert.That(Vector3.Distance(root, mid), Is.EqualTo(0.42f).Within(0.002f));
        Assert.That(Vector3.Distance(mid, tip), Is.EqualTo(0.42f).Within(0.002f));
    }

    [Test]
    public void TwoBoneIK_BendsTowardThePole()
    {
        Vector3 root = new Vector3(0f, 1f, 0f);
        Vector3 target = new Vector3(0f, 0.3f, 0f);
        HumanoidIK.SolveTwoBone(root, target, 0.42f, 0.42f, Vector3.forward, out Vector3 forwardMid, out _);
        HumanoidIK.SolveTwoBone(root, target, 0.42f, 0.42f, Vector3.back, out Vector3 backMid, out _);

        Assert.That(forwardMid.z, Is.GreaterThan(0.05f));
        Assert.That(backMid.z, Is.LessThan(-0.05f));
    }

    [Test]
    public void TwoBoneIK_ClampsUnreachableAndDegenerateTargets()
    {
        Vector3 root = Vector3.zero;
        bool reached = HumanoidIK.SolveTwoBone(root, new Vector3(0f, -5f, 0f), 0.4f, 0.4f, Vector3.forward, out Vector3 mid, out Vector3 tip);
        Assert.That(reached, Is.False);
        Assert.That(tip.magnitude, Is.EqualTo(0.8f).Within(0.01f));
        Assert.That(float.IsNaN(mid.x) || float.IsNaN(mid.y) || float.IsNaN(mid.z), Is.False);

        HumanoidIK.SolveTwoBone(root, root, 0.4f, 0.4f, Vector3.down, out mid, out tip);
        Assert.That(float.IsNaN(mid.x) || float.IsNaN(mid.y) || float.IsNaN(mid.z), Is.False);

        HumanoidIK.SolveTwoBone(root, new Vector3(0f, -0.5f, 0f), 0.4f, 0.4f, Vector3.down, out mid, out tip);
        Assert.That(float.IsNaN(mid.x) || float.IsNaN(mid.y) || float.IsNaN(mid.z), Is.False, "pole parallel to the chain");
    }

    // --- Scene objects ----------------------------------------------------------------------

    [Test]
    public void Build_CreatesFullBodySizedToTheControllerWithoutColliders()
    {
        GameObject player = CreatePlayerObject(2f);
        HumanoidBody body = HumanoidBody.AddTo(player, ProceduralSettings());
        body.Build();

        Assert.That(body.IsBuilt, Is.True);
        Assert.That(body.BuiltScale, Is.EqualTo(1f).Within(1e-4f));
        Assert.That(player.transform.Find(HumanoidBody.BodyRootName), Is.Not.Null);

        foreach (HumanoidBone bone in HumanoidParts.AllBones)
        {
            Transform t = body.GetBone(bone);
            Assert.That(t, Is.Not.Null, bone + " missing");
            HumanoidBone? parent = HumanoidParts.GetParent(bone);
            Transform expectedParent = parent.HasValue ? body.GetBone(parent.Value) : body.BodyRoot;
            Assert.That(t.parent, Is.SameAs(expectedParent), bone + " parent");

            var filter = t.GetComponent<MeshFilter>();
            var renderer = t.GetComponent<MeshRenderer>();
            Assert.That(filter != null && filter.sharedMesh != null, Is.True, bone + " mesh");
            Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(filter.sharedMesh.subMeshCount), bone + " materials per submesh");
            foreach (Material m in renderer.sharedMaterials)
                Assert.That(m, Is.Not.Null);
        }

        Assert.That(player.GetComponentsInChildren<Collider>().Length, Is.Zero, "a CharacterController body must not add colliders");

        Bounds bounds = CombinedRendererBounds(body.BodyRoot);
        Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(-0.02f));
        Assert.That(bounds.max.y, Is.LessThanOrEqualTo(CapsuleHeight));
        Assert.That(Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)), Is.LessThanOrEqualTo(CapsuleRadius));
        Assert.That(Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)), Is.LessThanOrEqualTo(CapsuleRadius));
    }

    [Test]
    public void Build_ScalesWithControllerHeight()
    {
        GameObject player = CreatePlayerObject(1.6f);
        HumanoidBody body = HumanoidBody.AddTo(player, ProceduralSettings());
        body.Build();

        Assert.That(body.BuiltScale, Is.EqualTo(0.8f).Within(1e-4f));
        Bounds bounds = CombinedRendererBounds(body.BodyRoot);
        Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(-0.02f));
        Assert.That(bounds.max.y, Is.LessThanOrEqualTo(1.6f));
        Assert.That(bounds.max.y, Is.EqualTo(HumanoidProportions.TopOfHeadY * 0.8f).Within(0.03f));
    }

    [Test]
    public void Build_IsRepeatableWithoutDuplicatingTheBody()
    {
        GameObject player = CreatePlayerObject(2f);
        HumanoidBody body = HumanoidBody.AddTo(player, ProceduralSettings());
        body.Build();
        body.Build();

        int bodyRoots = 0;
        for (int i = 0; i < player.transform.childCount; i++)
        {
            if (player.transform.GetChild(i).name == HumanoidBody.BodyRootName)
                bodyRoots++;
        }

        Assert.That(bodyRoots, Is.EqualTo(1));
        Assert.That(player.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(HumanoidParts.BoneCount));
    }

    [Test]
    public void Build_RegistersTheWholeBodyAndHeadNeckAndArmsAsLocalHideTargets()
    {
        GameObject player = CreatePlayerObject(2f);
        var visibility = player.AddComponent<LocalPlayerBodyVisibility>();
        HumanoidBody body = HumanoidBody.AddTo(player, HumanoidBodySettings.Player());
        body.Build();

        var names = new HashSet<string>();
        foreach (Transform t in visibility.HideTargets)
            names.Add(t.name);

        Assert.That(visibility.HideTargets.Length, Is.EqualTo(5));
        Assert.That(names, Is.SupersetOf(new[] { HumanoidBody.BodyRootName, "Head", "Neck", "ShoulderL", "ShoulderR" }));
        Assert.That(body.GetLocalHideTargets()[0], Is.Not.Null);
    }

    [Test]
    public void Step_AppliesAnimatorPoseToTransforms()
    {
        GameObject player = CreatePlayerObject(2f);
        HumanoidBody body = HumanoidBody.AddTo(player, HumanoidBodySettings.Player());
        body.Build();
        float standingHips = body.GetBone(HumanoidBone.Hips).localPosition.y;

        var crouch = new HumanoidAnimationInput { crouch01 = 1f };
        for (int i = 0; i < 90; i++)
            body.Step(1f / 60f, crouch);

        Assert.That(body.GetBone(HumanoidBone.Hips).localPosition.y, Is.LessThan(standingHips - 0.4f));
        Assert.That(body.GetBone(HumanoidBone.Hips).localRotation, Is.Not.EqualTo(Quaternion.identity));
    }

    [Test]
    public void Hitboxes_LineUpWithTheBodyAndStayInsideTheCapsule()
    {
        foreach (HumanoidHitboxSpec spec in HumanoidProportions.Hitboxes)
        {
            Vector3 half;
            switch (spec.Shape)
            {
                case HumanoidHitboxShape.Sphere: half = Vector3.one * spec.Size.x; break;
                case HumanoidHitboxShape.Box: half = spec.Size * 0.5f; break;
                default: half = new Vector3(spec.Size.x, spec.Size.y * 0.5f, spec.Size.x); break;
            }

            Assert.That(spec.Center.y - half.y, Is.GreaterThanOrEqualTo(-0.001f), spec.Name + " below feet");
            Assert.That(spec.Center.y + half.y, Is.LessThanOrEqualTo(CapsuleHeight), spec.Name + " above capsule");
            Assert.That(Mathf.Abs(spec.Center.x) + half.x, Is.LessThanOrEqualTo(CapsuleRadius), spec.Name + " outside capsule (x)");
            Assert.That(Mathf.Abs(spec.Center.z) + half.z, Is.LessThanOrEqualTo(CapsuleRadius), spec.Name + " outside capsule (z)");
        }

        var animator = new HumanoidAnimator();
        Vector3 headCentre = animator.GetWorldPosition(HumanoidBone.Head) + HumanoidProportions.HeadCenterLocal;
        HumanoidHitboxSpec headSpec = HumanoidProportions.Hitboxes[0];
        Assert.That(headSpec.Name, Is.EqualTo("Head"));
        Assert.That(Vector3.Distance(headCentre, headSpec.Center), Is.LessThan(0.03f), "head hitbox should wrap the head mesh");
    }

    [Test]
    public void TargetBody_BuildsDirectChildHitZonesThatResolveAsHeadBodyAndLimb()
    {
        GameObject targetRoot = CreateTargetObject(new Vector3(0f, 1f, 10f), 1f);
        var receiver = targetRoot.AddComponent<TestHitReceiver>();
        var resolver = new GameObject("Resolver").AddComponent<HitDetection>();
        created.Add(resolver.gameObject);

        Transform head = targetRoot.transform.Find("Head");
        Transform bodyBox = targetRoot.transform.Find("Body");
        Transform legBox = targetRoot.transform.Find("Leg_L");
        Assert.That(head, Is.Not.Null, "direct-child Head hit volume (used by smoke tests)");
        Assert.That(bodyBox, Is.Not.Null);
        Assert.That(legBox, Is.Not.Null);
        Assert.That(head.GetComponent<HitZone>().ZoneType, Is.EqualTo(HitZoneType.Head));
        Assert.That(head.GetComponent<MeshFilter>(), Is.Null, "hit volumes carry no legacy primitive mesh");
        Physics.SyncTransforms();

        AssertShotResolves(resolver, receiver, head.position, HitZoneType.Head, "Head");
        AssertShotResolves(resolver, receiver, bodyBox.position, HitZoneType.Body, "Body");
        AssertShotResolves(resolver, receiver, legBox.position, HitZoneType.Limb, "Leg_L");
    }

    [Test]
    public void TargetBody_StandsOnTheGroundBelowThePivot()
    {
        GameObject targetRoot = CreateTargetObject(new Vector3(0f, 1.5f, 0f), 1.5f);
        HumanoidBody body = targetRoot.GetComponent<HumanoidBody>();
        Bounds bounds = CombinedRendererBounds(body.BodyRoot);

        Assert.That(body.BuiltScale, Is.EqualTo(1.5f).Within(1e-4f));
        Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.03f), "feet should touch the ground the legacy pivot implied");
        float topY = body.UsesImportedModel ? SoldierModelSet.Load().bodyHeight : HumanoidProportions.TopOfHeadY;
        Assert.That(bounds.max.y, Is.EqualTo(topY * 1.5f).Within(0.05f));
    }

    [Test]
    public void LegacyTarget_IsUpgradedKeepingHeadAndBodyHitVolumes()
    {
        GameObject root = new GameObject("Target_Legacy");
        created.Add(root);
        root.transform.position = new Vector3(0f, 1f, 8f);

        GameObject legacyBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        legacyBody.name = "Body";
        legacyBody.transform.SetParent(root.transform, false);
        legacyBody.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        legacyBody.AddComponent<HitZone>().Configure(HitZoneType.Body);
        var tinted = new Material(Shader.Find("Standard")) { color = Color.green };
        created.Add(tinted);
        legacyBody.GetComponent<Renderer>().sharedMaterial = tinted;

        GameObject legacyHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        legacyHead.name = "Head";
        legacyHead.transform.SetParent(root.transform, false);
        legacyHead.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        legacyHead.transform.localScale = Vector3.one * 0.5f;
        legacyHead.AddComponent<HitZone>().Configure(HitZoneType.Head);

        var target = root.AddComponent<Target>();

        Assert.That(HumanoidBodyBootstrap.UpgradeTarget(target), Is.True);
        HumanoidBody body = root.GetComponent<HumanoidBody>();
        Assert.That(body, Is.Not.Null);
        body.Build();

        Transform head = root.transform.Find("Head");
        Assert.That(head, Is.Not.Null);
        Assert.That(head.GetComponent<MeshFilter>(), Is.Null, "legacy sphere head was replaced");
        Assert.That(head.GetComponent<SphereCollider>(), Is.Not.Null);
        Assert.That(head.GetComponent<HitZone>().ZoneType, Is.EqualTo(HitZoneType.Head));
        Assert.That(root.transform.Find("Body").GetComponent<MeshFilter>(), Is.Null, "legacy cylinder body was replaced");
        Assert.That(body.BodyRoot.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(body.UsesImportedModel ? 4 : HumanoidParts.BoneCount));

        Color.RGBToHSV(Color.green, out float legacyHue, out _, out _);
        Color.RGBToHSV(body.Materials[(int)HumanoidSlot.Suit].color, out float suitHue, out _, out _);
        Assert.That(suitHue, Is.EqualTo(legacyHue).Within(0.03f), "legacy target colour tints the suit");

        Assert.That(HumanoidBodyBootstrap.UpgradeTarget(target), Is.False, "upgrade is idempotent");
    }

    [Test]
    public void LegacyPlayer_IsUpgradedToProceduralBodyAndArms()
    {
        GameObject player = CreatePlayerObject(2f);
        player.AddComponent<LocalPlayerBodyVisibility>();

        var legacyBodyRoot = new GameObject("BodyRoot");
        legacyBodyRoot.transform.SetParent(player.transform, false);
        var hips = new GameObject("Hips");
        hips.transform.SetParent(legacyBodyRoot.transform, false);

        var cameraObject = new GameObject("MainCamera");
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.AddComponent<Camera>();
        var legacyArms = new GameObject("FirstPersonArms");
        legacyArms.transform.SetParent(cameraObject.transform, false);

        Assert.That(HumanoidBodyBootstrap.UpgradePlayer(player), Is.True);
        HumanoidBody body = player.GetComponent<HumanoidBody>();
        var arms = cameraObject.GetComponent<ProceduralFirstPersonArms>();
        Assert.That(body, Is.Not.Null);
        Assert.That(arms, Is.Not.Null);
        body.Build();
        arms.Build();

        Assert.That(player.transform.Find("BodyRoot/Hips/Spine/Chest/Neck/Head"), Is.Not.Null);
        Assert.That(body.GetBone(HumanoidBone.Hips).childCount, Is.GreaterThan(0));
        int armsRoots = 0;
        for (int i = 0; i < cameraObject.transform.childCount; i++)
        {
            if (cameraObject.transform.GetChild(i).name == ProceduralFirstPersonArms.RootName)
                armsRoots++;
        }

        Assert.That(armsRoots, Is.EqualTo(1), "legacy first-person arms were replaced");
        Assert.That(HumanoidBodyBootstrap.UpgradePlayer(player), Is.False, "upgrade is idempotent");
    }

    // --- First-person arms ------------------------------------------------------------------

    [Test]
    public void FirstPersonArms_HandsGripTheWeaponHolderAndKeepBoneLengths()
    {
        ProceduralFirstPersonArms arms = CreateFirstPersonArms(out Transform holder);
        Vector3 holderPosition = holder.localPosition;

        AssertHandsOnGrips(arms);

        Assert.That(arms.RightHand.localPosition.z, Is.GreaterThan(0.2f), "hands appear in front of the camera");
        Assert.That(arms.RightForearm.localPosition.y, Is.LessThan(arms.RightHand.localPosition.y), "elbow hangs below the wrist");
        Assert.That(arms.LeftForearm.localPosition.y, Is.LessThan(arms.LeftHand.localPosition.y));
        Assert.That(Vector3.Distance(arms.RightForearm.localPosition, arms.RightShoulderAnchor), Is.EqualTo(arms.UpperLength).Within(0.003f));
        Assert.That(Vector3.Distance(arms.RightHand.localPosition, arms.RightForearm.localPosition), Is.EqualTo(arms.LowerLength).Within(0.003f));
        Assert.That(holder.localPosition, Is.EqualTo(holderPosition), "arms never move the weapon attachment point");
    }

    [Test]
    public void FirstPersonArms_FollowTheWeaponHolderThroughRecoil()
    {
        ProceduralFirstPersonArms arms = CreateFirstPersonArms(out Transform holder);
        holder.localPosition += new Vector3(0f, 0.03f, -0.06f);
        arms.Solve();

        AssertHandsOnGrips(arms);
    }

    [Test]
    public void FirstPersonArms_DoNotCastShadowsOrAddColliders()
    {
        ProceduralFirstPersonArms arms = CreateFirstPersonArms(out _);
        Assert.That(arms.ArmsRoot.GetComponentsInChildren<Collider>().Length, Is.Zero);
        foreach (Renderer renderer in arms.ArmsRoot.GetComponentsInChildren<Renderer>())
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
        Assert.That(arms.ArmsRoot.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(arms.UsesImportedModel ? 1 : 6));
    }

    // --- Helpers ----------------------------------------------------------------------------

    private static HumanoidBodySettings ProceduralSettings()
    {
        HumanoidBodySettings settings = HumanoidBodySettings.Player();
        settings.forceProcedural = true;
        return settings;
    }

    private ProceduralFirstPersonArms CreateFirstPersonArms(out Transform holder)
    {
        var camera = new GameObject("MainCamera");
        created.Add(camera);
        camera.AddComponent<Camera>();
        var holderObject = new GameObject(ProceduralFirstPersonArms.WeaponHolderName);
        holderObject.transform.SetParent(camera.transform, false);
        holderObject.transform.localPosition = new Vector3(0.3f, -0.2f, 0.5f);
        holder = holderObject.transform;
        var arms = camera.AddComponent<ProceduralFirstPersonArms>();
        arms.Build();
        return arms;
    }

    private static void AssertHandsOnGrips(ProceduralFirstPersonArms arms)
    {
        Assert.That(Vector3.Distance(arms.RightHand.localPosition, arms.GetGripTargetLocal(true)), Is.LessThan(0.02f), "right hand off the grip");
        Assert.That(Vector3.Distance(arms.LeftHand.localPosition, arms.GetGripTargetLocal(false)), Is.LessThan(0.02f), "left hand off the support point");
    }

    private GameObject CreatePlayerObject(float height)
    {
        var player = new GameObject("TestPlayer");
        created.Add(player);
        var cc = player.AddComponent<CharacterController>();
        cc.height = height;
        cc.radius = CapsuleRadius;
        cc.center = new Vector3(0f, height * 0.5f, 0f);
        return player;
    }

    private GameObject CreateTargetObject(Vector3 position, float scale)
    {
        var root = new GameObject("TestTarget");
        created.Add(root);
        root.transform.position = position;
        HumanoidBody body = HumanoidBody.AddTo(root, HumanoidBodySettings.Target(Color.red, scale));
        body.Build();
        return root;
    }

    private static void AssertShotResolves(HitDetection resolver, TestHitReceiver receiver, Vector3 aimPoint, HitZoneType expectedZone, string expectedCollider)
    {
        int before = receiver.HitCount;
        Vector3 origin = aimPoint + new Vector3(0f, 0f, -6f);
        HitResult result = resolver.Resolve(HitRequest.Create(origin, Vector3.forward, 20f, 10f));

        Assert.That(result.DidHit, Is.True, expectedCollider + " shot missed");
        Assert.That(result.Impacts[0].Zone, Is.EqualTo(expectedZone), expectedCollider + " zone");
        Assert.That(result.Impacts[0].RaycastHit.collider.name, Is.EqualTo(expectedCollider));
        Assert.That(receiver.HitCount, Is.EqualTo(before + 1));
    }

    private static void AssertLength(HumanoidAnimator animator, HumanoidBone from, HumanoidBone to, float expected)
    {
        float length = Vector3.Distance(animator.GetWorldPosition(from), animator.GetWorldPosition(to));
        Assert.That(length, Is.EqualTo(expected).Within(0.003f), from + "->" + to);
    }

    private static HumanoidMeshBuilder GetPart(HumanoidBone bone)
    {
        if (partCache == null)
        {
            partCache = new HumanoidMeshBuilder[HumanoidParts.BoneCount];
            foreach (HumanoidBone b in HumanoidParts.AllBones)
                partCache[(int)b] = HumanoidParts.Build(b, 1f);
        }

        return partCache[(int)bone];
    }

    private static void GetExtents(HumanoidAnimator animator, out Vector3 min, out Vector3 max, out float maxRadius)
    {
        min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        maxRadius = 0f;
        foreach (HumanoidBone bone in HumanoidParts.AllBones)
        {
            Vector3 position = animator.GetWorldPosition(bone);
            Quaternion rotation = animator.GetWorldRotation(bone);
            IReadOnlyList<Vector3> vertices = GetPart(bone).Vertices;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = position + rotation * vertices[i];
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
                maxRadius = Mathf.Max(maxRadius, Mathf.Sqrt(p.x * p.x + p.z * p.z));
            }
        }
    }

    private static Bounds CombinedRendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = TightBounds(renderers[0]);
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(TightBounds(renderers[i]));
        return bounds;
    }

    // SkinnedMeshRenderer.bounds is the padded culling volume, so measure the baked vertices instead.
    private static Bounds TightBounds(Renderer renderer)
    {
        if (!(renderer is SkinnedMeshRenderer skinned) || skinned.sharedMesh == null)
            return renderer.bounds;

        var baked = new Mesh();
        skinned.BakeMesh(baked, true);
        Matrix4x4 toWorld = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
        Vector3[] vertices = baked.vertices;
        Bounds bounds = new Bounds(toWorld.MultiplyPoint3x4(vertices[0]), Vector3.zero);
        for (int i = 1; i < vertices.Length; i++)
            bounds.Encapsulate(toWorld.MultiplyPoint3x4(vertices[i]));
        Object.DestroyImmediate(baked);
        return bounds;
    }

    private sealed class TestHitReceiver : MonoBehaviour, IHitDamageReceiver
    {
        public int HitCount { get; private set; }

        public void ApplyHitDamage(HitDamageInfo damage)
        {
            HitCount++;
        }
    }
}
