using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Upgrades legacy primitive characters (capsule/box player bodies, cylinder-and-sphere training
/// targets) that were baked into scenes and prefabs before the procedural humanoid existed. Runs
/// automatically after every scene load; objects that already have a <see cref="HumanoidBody"/> are
/// left alone, so freshly built prefabs are unaffected.
/// </summary>
public static class HumanoidBodyBootstrap
{
    private const string LegacyPlayerBodyName = "BodyRoot";
    private const string LegacyArmsName = "FirstPersonArms";
    private const string PlayerSpawnName = "PlayerSpawn";

    private static bool installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!installed)
        {
            installed = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        UpgradeAll();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpgradeAll();
    }

    /// <summary>Upgrades every legacy player body and target currently loaded. Returns how many objects changed.</summary>
    public static int UpgradeAll()
    {
        int changed = 0;

        var players = Object.FindObjectsByType<LocalPlayerBodyVisibility>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (UpgradePlayer(players[i].gameObject))
                changed++;
        }

        Target[] targets = Object.FindObjectsByType<Target>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Array.Sort(targets, (a, b) => string.CompareOrdinal(a.name, b.name));
        GameObject spawn = GameObject.Find(PlayerSpawnName);
        for (int i = 0; i < targets.Length; i++)
        {
            string look = CharacterLooks.ForDummy(i);
            if (UpgradeTarget(targets[i], look))
                changed++;
            if (ApplyDummyLook(targets[i], look))
                changed++;
            if (spawn != null)
                targets[i].FaceTowards(spawn.transform.position);
        }

        return changed;
    }

    /// <summary>
    /// Dresses a training dummy that still wears the classic soldier in a character look. Dummies that already name
    /// a look (set by a map or a test) are left alone, so this is idempotent. The dummy's colour stays as a subtle tint.
    /// </summary>
    public static bool ApplyDummyLook(Target target, string look)
    {
        if (target == null || string.IsNullOrEmpty(look))
            return false;

        HumanoidBody body = target.GetComponent<HumanoidBody>();
        if (body == null || !string.IsNullOrEmpty(body.LookId))
            return false;

        body.SetLook(look, CharacterLooks.DummyLookTint);
        return true;
    }

    /// <summary>
    /// Replaces a primitive player body (BodyRoot/Hips...) and primitive first-person arms with the
    /// procedural versions. Returns false when the player already uses them or has no legacy body.
    /// </summary>
    public static bool UpgradePlayer(GameObject player)
    {
        if (player == null || player.GetComponent<HumanoidBody>() != null)
            return false;

        Transform legacyBody = player.transform.Find(LegacyPlayerBodyName);
        if (legacyBody == null || legacyBody.Find("Hips") == null)
            return false;

        RetireLegacyObject(legacyBody.gameObject);

        Camera cam = player.GetComponentInChildren<Camera>(true);
        if (cam != null && cam.GetComponent<ProceduralFirstPersonArms>() == null)
        {
            Transform legacyArms = FindDescendant(cam.transform, LegacyArmsName);
            if (legacyArms != null && legacyArms.GetComponent<MeshFilter>() == null)
                RetireLegacyObject(legacyArms.gameObject);
            cam.gameObject.AddComponent<ProceduralFirstPersonArms>();
        }

        HumanoidBody.AddTo(player, HumanoidBodySettings.Player());
        return true;
    }

    /// <summary>
    /// Replaces the legacy cylinder "Body" and sphere "Head" of a target with the humanoid, keeping direct
    /// "Head"/"Body" hit volumes. The legacy body colour tints the new suit.
    /// </summary>
    public static bool UpgradeTarget(Target target, string look = null)
    {
        if (target == null || target.GetComponent<HumanoidBody>() != null)
            return false;

        Transform root = target.transform;
        Transform legacyBody = root.Find("Body");
        Transform legacyHead = root.Find("Head");
        bool bodyIsPrimitive = legacyBody != null && legacyBody.GetComponent<MeshFilter>() != null;
        bool headIsPrimitive = legacyHead != null && legacyHead.GetComponent<MeshFilter>() != null;
        if (!bodyIsPrimitive && !headIsPrimitive)
            return false;

        float scale = 1f;
        Color tint = new Color(0.8f, 0.2f, 0.2f);
        if (bodyIsPrimitive)
        {
            scale = Mathf.Max(0.1f, legacyBody.localScale.y);
            var renderer = legacyBody.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                tint = renderer.sharedMaterial.color;
        }
        else if (headIsPrimitive)
        {
            scale = Mathf.Max(0.1f, legacyHead.localScale.x / 0.5f);
        }

        if (bodyIsPrimitive)
            RetireLegacyObject(legacyBody.gameObject);
        if (headIsPrimitive)
            RetireLegacyObject(legacyHead.gameObject);

        HumanoidBodySettings settings = HumanoidBodySettings.Target(tint, scale);
        if (!string.IsNullOrEmpty(look))
        {
            settings.look = look;
            settings.lookTint = CharacterLooks.DummyLookTint;
        }

        HumanoidBody.AddTo(target.gameObject, settings);
        return true;
    }

    private static void RetireLegacyObject(GameObject go)
    {
        go.name = go.name + "_Legacy";
        go.SetActive(false);
        if (Application.isPlaying)
            Object.Destroy(go);
        else
            Object.DestroyImmediate(go);
    }

    private static Transform FindDescendant(Transform root, string childName)
    {
        var stack = new Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            Transform t = stack.Pop();
            for (int i = 0; i < t.childCount; i++)
            {
                Transform child = t.GetChild(i);
                if (child.name == childName)
                    return child;
                stack.Push(child);
            }
        }

        return null;
    }
}
