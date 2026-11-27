using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Checks the Blender-built soldier (Soldier.fbx / SoldierArmsFP.fbx) as bound by <see cref="SoldierModelBinder"/>:
/// asset wiring, skeleton compatibility, hide targets, tinting, skinning and the procedural fallback.
/// </summary>
public class SoldierModelTests
{
    private const float CapsuleRadius = 0.4f;

    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        HumanoidBody.ModelSetOverride = null;
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
                Object.DestroyImmediate(created[i]);
        }

        created.Clear();
    }

    [Test]
    public void ModelSet_ReferencesBothModelsMaterialsAndOcclusion()
    {
        SoldierModelSet set = SoldierModelSet.Load();
        Assert.That(set, Is.Not.Null, "Resources/" + SoldierModelSet.ResourcePath);
        Assert.That(set.bodyModel, Is.Not.Null);
        Assert.That(set.armsModel, Is.Not.Null);
        Assert.That(set.ambientOcclusion, Is.Not.Null);
        Assert.That(set.armsAmbientOcclusion, Is.Not.Null);
        Assert.That(set.materials.Length, Is.EqualTo(SoldierModelSet.MaterialSlotCount));
        for (int i = 0; i < set.materials.Length; i++)
            Assert.That(set.materials[i], Is.Not.Null, "material " + (SoldierMaterialSlot)i);
    }

    [Test]
    public void RestWorldPositions_FollowTheHumanoidProportions()
    {
        Assert.That(SoldierModelBinder.RestWorldPosition(HumanoidBone.Hips).y, Is.EqualTo(HumanoidProportions.HipsY).Within(1e-4f));
        Assert.That(SoldierModelBinder.RestWorldPosition(HumanoidBone.Head).y, Is.EqualTo(HumanoidProportions.HeadY).Within(1e-4f));
        Assert.That(SoldierModelBinder.RestWorldPosition(HumanoidBone.HandR).y,
            Is.EqualTo(HumanoidProportions.ShoulderY - HumanoidProportions.UpperArmLength - HumanoidProportions.ForearmLength).Within(1e-4f));
        Assert.That(SoldierModelBinder.RestWorldPosition(HumanoidBone.ShoulderL).x, Is.EqualTo(-HumanoidProportions.ShoulderHalfWidth).Within(1e-4f));
    }

    [Test]
    public void Binder_RejectsMissingModelSetsWithoutThrowing()
    {
        Assert.That(SoldierModelBinder.TryBuildBody(null, new Transform[HumanoidParts.BoneCount], null, 1f,
            HumanoidPalette.Player, 0, out SoldierBuildResult result, out string error), Is.False);
        Assert.That(result, Is.Null);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void Body_UsesTheImportedModelOnTheExistingSkeleton()
    {
        HumanoidBody body = BuildPlayerBody(out _);

        Assert.That(body.UsesImportedModel, Is.True, "import fell back: " + body.ImportFallbackReason);
        Assert.That(body.SoldierRenderers.Count, Is.EqualTo(4));
        Assert.That(body.BodyRoot.GetComponentsInChildren<MeshRenderer>().Length, Is.Zero, "no procedural parts when the model is used");

        foreach (HumanoidBone bone in HumanoidParts.AllBones)
        {
            Transform t = body.GetBone(bone);
            Assert.That(t, Is.Not.Null, bone + " missing");
            HumanoidBone? parent = HumanoidParts.GetParent(bone);
            Assert.That(t.parent, Is.SameAs(parent.HasValue ? body.GetBone(parent.Value) : body.BodyRoot), bone + " parent");
        }

        int triangles = 0;
        foreach (SkinnedMeshRenderer smr in body.SoldierRenderers)
        {
            Assert.That(smr.bones.Length, Is.EqualTo(HumanoidParts.BoneCount));
            Assert.That(smr.sharedMesh.bindposes.Length, Is.EqualTo(HumanoidParts.BoneCount));
            Assert.That(smr.sharedMesh.boneWeights.Length, Is.EqualTo(smr.sharedMesh.vertexCount));
            Assert.That(smr.sharedMaterials.Length, Is.EqualTo(smr.sharedMesh.subMeshCount));
            foreach (Material m in smr.sharedMaterials)
                Assert.That(m, Is.Not.Null);
            Assert.That(smr.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.On));
            triangles += smr.sharedMesh.triangles.Length / 3;
        }

        Assert.That(triangles, Is.InRange(20000, 35000), "triangle budget");
    }

    [Test]
    public void Body_RestMeshFitsTheControllerCapsule()
    {
        HumanoidBody body = BuildPlayerBody(out _);
        Assert.That(body.UsesImportedModel, Is.True, body.ImportFallbackReason);

        Bounds bounds = default;
        bool first = true;
        foreach (SkinnedMeshRenderer smr in body.SoldierRenderers)
        {
            if (first)
            {
                bounds = smr.sharedMesh.bounds;
                first = false;
            }
            else
            {
                bounds.Encapsulate(smr.sharedMesh.bounds);
            }
        }

        Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(-0.02f), "feet on the ground");
        Assert.That(bounds.max.y, Is.LessThanOrEqualTo(2f), "inside the 2 m controller");
        Assert.That(bounds.max.y, Is.GreaterThan(1.75f));
        Assert.That(Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)), Is.LessThanOrEqualTo(CapsuleRadius));
        Assert.That(Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)), Is.LessThanOrEqualTo(CapsuleRadius));
    }

    [Test]
    public void Body_ScalesWithTheController()
    {
        GameObject player = CreatePlayer(1.6f);
        HumanoidBody body = HumanoidBody.AddTo(player, HumanoidBodySettings.Player());
        body.AnimationEnabled = false;
        body.Build();
        Assert.That(body.UsesImportedModel, Is.True, body.ImportFallbackReason);

        float top = float.MinValue;
        foreach (SkinnedMeshRenderer smr in body.SoldierRenderers)
            top = Mathf.Max(top, smr.sharedMesh.bounds.max.y);
        Assert.That(top, Is.LessThanOrEqualTo(1.6f));
        Assert.That(top, Is.EqualTo(1.915f * 0.8f).Within(0.03f));
    }

    [Test]
    public void Body_EveryMeshHangsUnderTheLocalHideTargets()
    {
        GameObject player = CreatePlayer(2f);
        var visibility = player.AddComponent<LocalPlayerBodyVisibility>();
        HumanoidBody body = HumanoidBody.AddTo(player, HumanoidBodySettings.Player());
        body.AnimationEnabled = false;
        body.Build();
        Assert.That(body.UsesImportedModel, Is.True, body.ImportFallbackReason);

        var visible = new List<string>();
        foreach (SkinnedMeshRenderer smr in body.SoldierRenderers)
        {
            bool hidden = false;
            foreach (Transform target in visibility.HideTargets)
                hidden |= smr.transform.IsChildOf(target);
            if (!hidden)
                visible.Add(smr.name);
        }

        Assert.That(visible, Is.Empty, "no soldier mesh (torso and legs included) stays visible to the first-person camera: " + string.Join(",", visible));
    }

    [Test]
    public void Body_AnimatedBonesDeformTheSkin()
    {
        HumanoidBody body = BuildPlayerBody(out _);
        Assert.That(body.UsesImportedModel, Is.True, body.ImportFallbackReason);

        ResetPose(body);
        SkinnedMeshRenderer arm = FindRenderer(body, "ArmR");
        float restLow = LowestBakedY(arm);

        body.GetBone(HumanoidBone.UpperArmR).localRotation = Quaternion.Euler(-90f, 0f, 0f);
        float raisedLow = LowestBakedY(arm);

        Assert.That(restLow, Is.LessThan(0.9f), "right arm hangs by the side at rest");
        Assert.That(raisedLow, Is.GreaterThan(restLow + 0.35f), "raising the upper arm lifts the hand and sleeve");
    }

    [Test]
    public void Body_ElbowBendKeepsTheForearmConnectedToTheUpperArm()
    {
        HumanoidBody body = BuildPlayerBody(out _);
        Assert.That(body.UsesImportedModel, Is.True, body.ImportFallbackReason);

        ResetPose(body);
        body.GetBone(HumanoidBone.UpperArmL).localRotation = Quaternion.Euler(-60f, 0f, 0f);
        body.GetBone(HumanoidBone.ForearmL).localRotation = Quaternion.Euler(-80f, 0f, 0f);
        SkinnedMeshRenderer arm = FindRenderer(body, "ArmL");
        var baked = new Mesh();
        created.Add(baked);
        arm.BakeMesh(baked);
        Vector3[] vertices = baked.vertices;
        Vector3 elbow = body.GetBone(HumanoidBone.ForearmL).position;
        Matrix4x4 toWorld = arm.transform.localToWorldMatrix;

        // Every vertex weighted near the elbow must stay within a sleeve radius of the elbow joint: a torn or
        // collapsed skin would leave vertices far away from it.
        BoneWeight[] weights = arm.sharedMesh.boneWeights;
        int checkedVertices = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            BoneWeight w = weights[i];
            bool blended = w.weight0 > 0.05f && w.weight1 > 0.05f &&
                           ((w.boneIndex0 == (int)HumanoidBone.UpperArmL && w.boneIndex1 == (int)HumanoidBone.ForearmL) ||
                            (w.boneIndex0 == (int)HumanoidBone.ForearmL && w.boneIndex1 == (int)HumanoidBone.UpperArmL));
            if (!blended)
                continue;
            checkedVertices++;
            Assert.That(Vector3.Distance(toWorld.MultiplyPoint3x4(vertices[i]), elbow), Is.LessThan(0.18f));
        }

        Assert.That(checkedVertices, Is.GreaterThan(20), "the sleeve has elbow-blended vertices");
    }

    [Test]
    public void Body_TeamTintRecolorsClothAndKeepsTheEyesWhite()
    {
        GameObject root = new GameObject("Bot");
        created.Add(root);
        HumanoidBody body = HumanoidBody.AddTo(root, HumanoidBodySettings.Target(Color.red, 1f));
        body.Build();
        Assert.That(body.UsesImportedModel, Is.True, body.ImportFallbackReason);

        Material cloth = FindMaterial(body, "Cloth");
        Material eye = FindMaterial(body, "Eye");
        Color.RGBToHSV(Color.red, out float redHue, out _, out _);
        Color.RGBToHSV(cloth.color, out float clothHue, out _, out _);
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(clothHue * 360f, redHue * 360f)), Is.LessThan(15f));
        Assert.That(body.Materials[(int)HumanoidSlot.Suit], Is.SameAs(cloth));

        body.SetPalette(HumanoidPalette.ForTarget(Color.blue));
        Color.RGBToHSV(Color.blue, out float blueHue, out _, out _);
        Color.RGBToHSV(cloth.color, out clothHue, out _, out _);
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(clothHue * 360f, blueHue * 360f)), Is.LessThan(15f), "SetPalette re-tints the cloth");
        Assert.That(eye.color.r, Is.GreaterThan(0.95f));
        Assert.That(FindMaterial(body, "Accent").color, Is.EqualTo(HumanoidPalette.ForTarget(Color.blue).accent));
    }

    [Test]
    public void Body_FallsBackToProceduralPartsWhenTheModelSetIsEmpty()
    {
        var empty = ScriptableObject.CreateInstance<SoldierModelSet>();
        created.Add(empty);
        HumanoidBody.ModelSetOverride = empty;

        GameObject player = CreatePlayer(2f);
        HumanoidBody body = HumanoidBody.AddTo(player, HumanoidBodySettings.Player());
        body.Build();

        Assert.That(body.UsesImportedModel, Is.False);
        Assert.That(body.ImportFallbackReason, Is.Not.Empty);
        Assert.That(body.BodyRoot.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(HumanoidParts.BoneCount));
        Assert.That(body.BodyRoot.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.Zero);
        Assert.That(body.Materials.Length, Is.EqualTo(HumanoidMeshBuilder.SlotCount));
    }

    [Test]
    public void Body_CanBeForcedProcedural()
    {
        GameObject player = CreatePlayer(2f);
        HumanoidBodySettings settings = HumanoidBodySettings.Player();
        settings.forceProcedural = true;
        HumanoidBody body = HumanoidBody.AddTo(player, settings);
        body.Build();

        Assert.That(body.UsesImportedModel, Is.False);
        Assert.That(body.BodyRoot.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(HumanoidParts.BoneCount));
    }

    [Test]
    public void Body_RebuildDoesNotDuplicateTheModel()
    {
        GameObject player = CreatePlayer(2f);
        HumanoidBody body = HumanoidBody.AddTo(player, HumanoidBodySettings.Player());
        body.Build();
        body.Build();

        Assert.That(player.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.EqualTo(body.UsesImportedModel ? 4 : 0));
        int extraColliders = 0;
        foreach (Collider collider in player.GetComponentsInChildren<Collider>())
        {
            if (!(collider is CharacterController))
                extraColliders++;
        }

        Assert.That(extraColliders, Is.Zero, "a CharacterController body adds no colliders besides the controller itself");
    }

    [Test]
    public void TargetBody_KeepsDirectChildHitVolumesWithTheImportedModel()
    {
        GameObject root = new GameObject("Bot");
        created.Add(root);
        root.layer = 7;
        HumanoidBody body = HumanoidBody.AddTo(root, HumanoidBodySettings.Target(Color.red, 1f));
        body.Build();
        Assert.That(body.UsesImportedModel, Is.True, body.ImportFallbackReason);

        Assert.That(body.Hitboxes.Count, Is.EqualTo(HumanoidProportions.Hitboxes.Length));
        foreach (HumanoidHitboxSpec spec in HumanoidProportions.Hitboxes)
        {
            Transform hitbox = root.transform.Find(spec.Name);
            Assert.That(hitbox, Is.Not.Null, spec.Name);
            Assert.That(hitbox.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(hitbox.GetComponent<HitZone>().ZoneType, Is.EqualTo(spec.Zone));
            Assert.That(hitbox.gameObject.layer, Is.EqualTo(7));
        }

        foreach (SkinnedMeshRenderer smr in body.SoldierRenderers)
        {
            Assert.That(smr.gameObject.layer, Is.EqualTo(7));
            Assert.That(smr.GetComponent<Collider>(), Is.Null);
        }
    }

    [Test]
    public void FirstPersonArms_UseOneSkinnedMeshOnTheSixArmBones()
    {
        ProceduralFirstPersonArms arms = CreateArms();

        Assert.That(arms.UsesImportedModel, Is.True, "import fell back: " + arms.ImportFallbackReason);
        Assert.That(arms.SoldierRenderers.Count, Is.EqualTo(1));
        SkinnedMeshRenderer smr = arms.SoldierRenderers[0];
        Assert.That(smr.bones.Length, Is.EqualTo(6));
        Assert.That(smr.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
        Assert.That(smr.sharedMesh.triangles.Length / 3, Is.InRange(4000, 12000));
        Assert.That(arms.ArmsRoot.GetComponentsInChildren<MeshRenderer>().Length, Is.Zero);
        Assert.That(arms.ArmsRoot.GetComponentsInChildren<Collider>().Length, Is.Zero);
        Assert.That(new HashSet<Transform>(smr.bones), Is.SupersetOf(new[]
        {
            arms.RightUpperArm, arms.RightForearm, arms.RightHand, arms.LeftUpperArm, arms.LeftForearm, arms.LeftHand
        }));
    }

    [Test]
    public void FirstPersonArms_SkinFollowsTheHandsAndForearms()
    {
        ProceduralFirstPersonArms arms = CreateArms();
        Assert.That(arms.UsesImportedModel, Is.True, arms.ImportFallbackReason);

        SkinnedMeshRenderer smr = arms.SoldierRenderers[0];
        var baked = new Mesh();
        created.Add(baked);
        smr.BakeMesh(baked);
        Vector3[] vertices = baked.vertices;
        Matrix4x4 toWorld = smr.transform.localToWorldMatrix;

        AssertSkinNear(vertices, toWorld, arms.RightHand.position, 0.12f, "right hand");
        AssertSkinNear(vertices, toWorld, arms.LeftHand.position, 0.12f, "left hand");
        AssertSkinNear(vertices, toWorld, arms.RightForearm.position, 0.10f, "right elbow");
        AssertSkinNear(vertices, toWorld, arms.LeftForearm.position, 0.10f, "left elbow");
    }

    [Test]
    public void FirstPersonArms_FallBackToProceduralPartsWhenTheModelSetIsEmpty()
    {
        var empty = ScriptableObject.CreateInstance<SoldierModelSet>();
        created.Add(empty);
        HumanoidBody.ModelSetOverride = empty;

        ProceduralFirstPersonArms arms = CreateArms();

        Assert.That(arms.UsesImportedModel, Is.False);
        Assert.That(arms.ImportFallbackReason, Is.Not.Empty);
        Assert.That(arms.ArmsRoot.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(6));
        Assert.That(arms.RightHand, Is.Not.Null);
    }

    // --- Helpers ----------------------------------------------------------------------------

    private HumanoidBody BuildPlayerBody(out GameObject player)
    {
        player = CreatePlayer(2f);
        HumanoidBody body = HumanoidBody.AddTo(player, HumanoidBodySettings.Player());
        body.AnimationEnabled = false;
        body.Build();
        return body;
    }

    private static void ResetPose(HumanoidBody body)
    {
        foreach (HumanoidBone bone in HumanoidParts.AllBones)
            body.GetBone(bone).localRotation = Quaternion.identity;
    }

    private GameObject CreatePlayer(float height)
    {
        var player = new GameObject("TestPlayer");
        created.Add(player);
        var cc = player.AddComponent<CharacterController>();
        cc.height = height;
        cc.radius = CapsuleRadius;
        cc.center = new Vector3(0f, height * 0.5f, 0f);
        return player;
    }

    private ProceduralFirstPersonArms CreateArms()
    {
        var camera = new GameObject("MainCamera");
        created.Add(camera);
        camera.AddComponent<Camera>();
        var holder = new GameObject(ProceduralFirstPersonArms.WeaponHolderName);
        holder.transform.SetParent(camera.transform, false);
        holder.transform.localPosition = new Vector3(0.3f, -0.2f, 0.5f);
        var arms = camera.AddComponent<ProceduralFirstPersonArms>();
        arms.Build();
        return arms;
    }

    private static SkinnedMeshRenderer FindRenderer(HumanoidBody body, string part)
    {
        foreach (SkinnedMeshRenderer smr in body.SoldierRenderers)
        {
            if (smr.name.Contains(part))
                return smr;
        }

        Assert.Fail("no renderer named *" + part + "*");
        return null;
    }

    private static Material FindMaterial(HumanoidBody body, string slotName)
    {
        foreach (SkinnedMeshRenderer smr in body.SoldierRenderers)
        {
            foreach (Material m in smr.sharedMaterials)
            {
                if (m != null && m.name.Contains(slotName))
                    return m;
            }
        }

        Assert.Fail("no material for " + slotName);
        return null;
    }

    private float LowestBakedY(SkinnedMeshRenderer smr)
    {
        var baked = new Mesh();
        created.Add(baked);
        smr.BakeMesh(baked);
        Matrix4x4 toWorld = smr.transform.localToWorldMatrix;
        float low = float.MaxValue;
        foreach (Vector3 v in baked.vertices)
            low = Mathf.Min(low, toWorld.MultiplyPoint3x4(v).y);
        return low;
    }

    private static void AssertSkinNear(Vector3[] vertices, Matrix4x4 toWorld, Vector3 point, float radius, string label)
    {
        float best = float.MaxValue;
        for (int i = 0; i < vertices.Length; i++)
            best = Mathf.Min(best, Vector3.Distance(toWorld.MultiplyPoint3x4(vertices[i]), point));
        Assert.That(best, Is.LessThan(radius), label + " has no skin around its joint");
    }
}
