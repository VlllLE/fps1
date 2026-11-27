using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class CharacterLookTests
{
    private readonly List<Object> created = new List<Object>();

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

    [Test]
    public void ModelSet_ListsEveryLookWithAModelAndHeight()
    {
        SoldierModelSet set = SoldierModelSet.Load();
        Assert.That(set, Is.Not.Null);
        foreach (string id in CharacterLooks.AllIds)
        {
            CharacterLookEntry entry = set.GetLook(id);
            Assert.That(entry, Is.Not.Null, id + " is missing from the model set");
            Assert.That(entry.model, Is.Not.Null, id + " has no model");
            Assert.That(entry.height, Is.InRange(1.6f, 2f), id + " height");
        }

        Assert.That(set.GetLook("NoSuchLook"), Is.Null);
    }

    [Test]
    public void Credits_RecordSourceAuthorAndLicenseOfEveryLook()
    {
        string credits = File.ReadAllText("Assets/Characters/Looks/CREDITS.md");
        foreach (string needle in new[] { "poly.pizza", "Quaternius", "CC0", "madtroll", "CC-BY", "Ghillie", "Operator", "Soldier" })
            Assert.That(credits, Does.Contain(needle), "CREDITS.md should mention " + needle);
    }

    [Test]
    public void Looks_MapWeaponClassesToTheirCharacter()
    {
        Assert.That(CharacterLooks.ForWeaponType(WeaponType.AssaultRifle), Is.EqualTo(CharacterLooks.Soldier));
        Assert.That(CharacterLooks.ForWeaponType(WeaponType.LMG), Is.EqualTo(CharacterLooks.Soldier));
        Assert.That(CharacterLooks.ForWeaponType(WeaponType.Sniper), Is.EqualTo(CharacterLooks.Ghillie));
        Assert.That(CharacterLooks.ForWeaponType(WeaponType.SMG), Is.EqualTo(CharacterLooks.Operator));
        Assert.That(CharacterLooks.ForWeaponType(WeaponType.Shotgun), Is.EqualTo(CharacterLooks.Operator));
        Assert.That(CharacterLooks.ForWeapon(null), Is.EqualTo(CharacterLooks.Soldier));
    }

    [Test]
    public void ForBot_MixesLooksForAssaultRiflesAndKeepsClassLooksOtherwise()
    {
        WeaponConfig rifle = NewWeapon(WeaponType.AssaultRifle);
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 12; seed++)
            seen.Add(CharacterLooks.ForBot(rifle, seed));
        Assert.That(seen.Count, Is.EqualTo(CharacterLooks.AllIds.Length), "rifle bots get every look");
        Assert.That(CharacterLooks.ForBot(rifle, 5), Is.EqualTo(CharacterLooks.ForBot(rifle, 5)), "deterministic");
        Assert.That(CharacterLooks.AllIds, Does.Contain(CharacterLooks.ForBot(rifle, -7)), "negative seeds stay valid");

        Assert.That(CharacterLooks.ForBot(NewWeapon(WeaponType.Sniper), 3), Is.EqualTo(CharacterLooks.Ghillie));
        Assert.That(CharacterLooks.ForBot(NewWeapon(WeaponType.SMG), 4), Is.EqualTo(CharacterLooks.Operator));
    }

    [Test]
    public void EachLook_BuildsAnImportedSkinnedBodyOnTheGround([ValueSource(typeof(CharacterLooks), nameof(CharacterLooks.AllIds))] string id)
    {
        HumanoidBody body = BuildBody(id, 0f);

        Assert.That(body.UsesImportedModel, Is.True, id + ": " + body.LookFallbackReason);
        Assert.That(body.ActiveLookId, Is.EqualTo(id));
        Assert.That(body.LookFallbackReason, Is.Null.Or.Empty);

        Renderer[] renderers = body.BodyRoot.GetComponentsInChildren<Renderer>();
        Assert.That(renderers.Length, Is.GreaterThan(0));
        Bounds bounds = TightBounds(renderers);
        Assert.That(bounds.min.y, Is.EqualTo(body.transform.position.y).Within(0.06f), id + " stands on the ground");
        Assert.That(bounds.size.y, Is.EqualTo(SoldierModelSet.Load().bodyHeight).Within(0.2f), id + " is as tall as the classic soldier");
        Assert.That(bounds.size.x, Is.InRange(0.3f, 1.2f), id + " width");
        Assert.That(bounds.size.z, Is.InRange(0.2f, 1.2f), id + " depth");
    }

    [Test]
    public void EachLook_IsSkinnedToTheGameBonesAndKeepsItsUvs([ValueSource(typeof(CharacterLooks), nameof(CharacterLooks.AllIds))] string id)
    {
        HumanoidBody body = BuildBody(id, 0f);
        Assert.That(body.ActiveLookId, Is.EqualTo(id));

        Assert.That(body.BodyRoot.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.GreaterThan(0));
        int withUvs = 0;
        foreach (SkinnedMeshRenderer skinned in body.BodyRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Assert.That(skinned.bones.Length, Is.GreaterThan(0), skinned.name + " is bound to the game's bones");
            Assert.That(skinned.sharedMesh.vertexCount, Is.GreaterThan(500), skinned.name + " is a real mesh");
            if (skinned.sharedMesh.uv.Length == skinned.sharedMesh.vertexCount)
                withUvs++;
        }

        Assert.That(withUvs, Is.GreaterThan(0), id + " keeps the UVs of its body mesh");
    }

    [Test]
    public void UnknownLook_FallsBackToTheClassicSoldier()
    {
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("NoSuchLook"));
        HumanoidBody body = BuildBody("NoSuchLook", 0f);

        Assert.That(body.UsesImportedModel, Is.True, "the classic Blender soldier is still used");
        Assert.That(body.ActiveLookId, Is.Null.Or.Empty);
        Assert.That(body.LookFallbackReason, Is.Not.Null.And.Not.Empty);
        Assert.That(body.BodyRoot.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(0));
    }

    [Test]
    public void NoLook_KeepsTheClassicSoldier()
    {
        HumanoidBody body = BuildBody(null, 0f);
        Assert.That(body.UsesImportedModel, Is.True);
        Assert.That(body.ActiveLookId, Is.Null.Or.Empty);
        Assert.That(body.LookFallbackReason, Is.Null.Or.Empty);
    }

    [Test]
    public void SetLook_RebuildsInPlaceAndCanReturnToTheClassicSoldier()
    {
        HumanoidBody body = BuildBody(CharacterLooks.Soldier, 0f);
        Assert.That(body.ActiveLookId, Is.EqualTo(CharacterLooks.Soldier));
        Transform root = body.BodyRoot;

        body.SetLook(CharacterLooks.Ghillie);
        Assert.That(body.ActiveLookId, Is.EqualTo(CharacterLooks.Ghillie));
        Assert.That(body.BodyRoot, Is.Not.SameAs(root));

        body.SetLook(string.Empty);
        Assert.That(body.ActiveLookId, Is.Null.Or.Empty);
        Assert.That(body.UsesImportedModel, Is.True);
    }

    [Test]
    public void Look_ReceivesHideTargetsSoTheLocalPlayerNeverSeesIt()
    {
        foreach (string id in CharacterLooks.AllIds)
        {
            GameObject player = NewPlayer();
            player.AddComponent<LocalPlayerBodyVisibility>();
            HumanoidBody body = HumanoidBody.AddTo(player, new HumanoidBodySettings
            {
                palette = HumanoidPalette.Player,
                armPose = HumanoidArmPose.WeaponReady,
                look = id
            });
            body.Build();

            Assert.That(body.ActiveLookId, Is.EqualTo(id));
            List<Transform> targets = new List<Transform>(body.GetLocalHideTargets());
            Assert.That(targets, Does.Contain(body.BodyRoot), id + ": the whole look is a hide target");
        }
    }

    [Test]
    public void LookTint_PullsTheLookColoursTowardsThePalette()
    {
        var palette = HumanoidPalette.Player;
        palette.suit = Color.red;

        HumanoidBody plain = BuildBody(CharacterLooks.Soldier, 0f, palette);
        Color untinted = plain.Materials[0].color;
        HumanoidBody tinted = BuildBody(CharacterLooks.Soldier, 1f, palette);
        Color pulled = tinted.Materials[0].color;

        Assert.That(pulled.r, Is.GreaterThan(pulled.g + 0.05f), "tint 1 pulls towards the red suit");
        Assert.That(Mathf.Abs(untinted.r - untinted.g), Is.LessThan(Mathf.Abs(pulled.r - pulled.g)), "tint 0 leaves the model's own colours");
    }

    [Test]
    public void Looks_DoNotShareMaterialsWithTheClassicSoldier()
    {
        HumanoidBody classic = BuildBody(null, 0f);
        HumanoidBody look = BuildBody(CharacterLooks.Operator, 0f);
        Assert.That(look.Materials[0], Is.Not.SameAs(classic.Materials[0]));
    }

    private HumanoidBody BuildBody(string lookId, float tint, HumanoidPalette? palette = null)
    {
        GameObject player = NewPlayer();
        HumanoidBody body = HumanoidBody.AddTo(player, new HumanoidBodySettings
        {
            palette = palette ?? HumanoidPalette.Player,
            armPose = HumanoidArmPose.WeaponReady,
            look = lookId,
            lookTint = tint
        });
        body.Build();
        return body;
    }

    private GameObject NewPlayer()
    {
        var player = new GameObject("LookTestPlayer");
        created.Add(player);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 1f, 0f);
        return player;
    }

    private WeaponConfig NewWeapon(WeaponType type)
    {
        var weapon = ScriptableObject.CreateInstance<WeaponConfig>();
        weapon.weaponType = type;
        created.Add(weapon);
        return weapon;
    }

    private static Bounds TightBounds(Renderer[] renderers)
    {
        bool any = false;
        Bounds bounds = new Bounds();
        foreach (Renderer renderer in renderers)
        {
            if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                Mesh baked = new Mesh();
                skinned.BakeMesh(baked, true);
                Matrix4x4 toWorld = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
                foreach (Vector3 v in baked.vertices)
                {
                    Vector3 world = toWorld.MultiplyPoint3x4(v);
                    if (!any) { bounds = new Bounds(world, Vector3.zero); any = true; }
                    else bounds.Encapsulate(world);
                }

                Object.DestroyImmediate(baked);
            }
            else if (!any) { bounds = renderer.bounds; any = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        return bounds;
    }
}
