using System;
using UnityEngine;

/// <summary>
/// The visible gun a soldier body carries. It is a visual-only copy of the weapon's imported model (no gameplay
/// components, no colliders), placed every frame at the pose <see cref="HumanoidAnimator"/> computed so both hands
/// stay on it. When the weapon prefab has no model a small primitive gun is built from the hold profile instead.
///
/// Optional attachment transforms (children of the model named per <see cref="GunHoldCatalog.OverrideNames"/>)
/// replace the measured grip points of the profile. On death the gun leaves the hand and falls with
/// <see cref="DroppedGunPhysics"/>.
/// </summary>
public sealed class ThirdPersonGun
{
    public const string RootName = "ThirdPersonGun";
    private const float PrimitiveHalfThickness = 0.045f;

    private readonly Transform root;
    private readonly GunHoldProfile profile;
    private readonly Transform magazine;
    private readonly Transform bolt;
    private readonly Transform muzzle;
    private readonly Vector3 magazineRest;
    private readonly Vector3 boltRest;
    private readonly bool usesModel;
    private readonly Material ownedMaterial;
    private DroppedGunPhysics drop;
    private Transform owner;

    private ThirdPersonGun(Transform root, GunHoldProfile profile, Transform magazine, Transform bolt, Transform muzzle, bool usesModel, Material ownedMaterial)
    {
        this.root = root;
        this.profile = profile;
        this.magazine = magazine;
        this.bolt = bolt;
        this.muzzle = muzzle;
        this.usesModel = usesModel;
        this.ownedMaterial = ownedMaterial;
        magazineRest = magazine != null ? magazine.localPosition : Vector3.zero;
        boltRest = bolt != null ? bolt.localPosition : Vector3.zero;
        owner = root.parent;
    }

    public Transform Root => root;

    public GunHoldProfile Profile => profile;

    /// <summary>Barrel tip (tracer origin). Never null.</summary>
    public Transform Muzzle => muzzle;

    public bool UsesImportedModel => usesModel;

    public bool IsDropped => drop != null;

    public bool DropAtRest => drop != null && drop.AtRest;

    /// <summary>
    /// Builds the gun for <paramref name="config"/>. <paramref name="weaponPrefab"/> may be null (the config's own prefab is used).
    /// The gun is parented to <paramref name="parent"/> and every renderer is put on <paramref name="layer"/>.
    /// </summary>
    public static ThirdPersonGun Create(Transform parent, WeaponConfig config, GameObject weaponPrefab, int layer)
    {
        if (weaponPrefab == null && config != null)
            weaponPrefab = config.weaponPrefab;

        GunHoldProfile profile = config != null ? GunHoldCatalog.Get(config) : GunHoldCatalog.Build(GunHoldCatalog.M4A1);
        if (weaponPrefab != null && config != null)
            profile = GunHoldCatalog.Get(config.weaponType, weaponPrefab.name);

        var rootObject = new GameObject(RootName);
        rootObject.layer = layer;
        rootObject.transform.SetParent(parent, false);

        GameObject model = null;
        bool usesModel = false;
        Material material = null;
        try
        {
            Action<GameObject> applyMaterials;
            GameObject source = WeaponVisualSource.FindModel(weaponPrefab, out applyMaterials);
            if (source != null)
            {
                model = UnityEngine.Object.Instantiate(source, rootObject.transform);
                model.name = "Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                if (HasMesh(model))
                {
                    applyMaterials?.Invoke(model);
                    usesModel = true;
                }
                else
                {
                    DestroyObject(model);
                    model = null;
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[ThirdPersonGun] Weapon model unavailable, using a primitive gun: " + exception.Message);
            if (model != null)
                DestroyObject(model);
            model = null;
            usesModel = false;
        }

        if (!usesModel)
        {
            material = CreateMaterial(new Color(0.09f, 0.09f, 0.1f));
            model = BuildPrimitiveGun(rootObject.transform, profile, material);
        }

        StripPhysics(rootObject);
        Transform magazine = FindDescendant(model.transform, "Magazine");
        Transform bolt = FindDescendant(model.transform, "Bolt");
        if (bolt == null)
            bolt = FindDescendant(model.transform, "Slide") ?? FindDescendant(model.transform, "Pump");
        ApplyOverrides(rootObject.transform, model.transform, profile);

        Transform muzzle = FindDescendant(model.transform, "MuzzlePoint");
        if (muzzle == null)
        {
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model.transform, false);
            muzzle.localPosition = profile.muzzle;
            muzzle.localRotation = Quaternion.identity;
        }

        SetLayer(rootObject, layer);
        return new ThirdPersonGun(rootObject.transform, profile, magazine, bolt, muzzle, usesModel, material);
    }

    /// <summary>Places the gun where the animator wants it (in the parent's space, scaled with the body).</summary>
    public void Follow(HumanoidAnimator animator, float scale)
    {
        if (root == null || drop != null)
            return;

        bool show = animator.IsHolding;
        if (root.gameObject.activeSelf != show)
            root.gameObject.SetActive(show);
        if (!show)
            return;

        root.localPosition = animator.GunPosition * scale;
        root.localRotation = animator.GunRotation;
        root.localScale = Vector3.one * scale;
        MovePart(magazine, magazineRest, animator.MagazineOffset);
        MovePart(bolt, boltRest, animator.BoltOffset);
    }

    private void MovePart(Transform part, Vector3 rest, Vector3 weaponSpaceOffset)
    {
        if (part == null)
            return;
        Transform parent = part.parent;
        Vector3 offset = parent != null ? parent.InverseTransformVector(root.TransformVector(weaponSpaceOffset)) : weaponSpaceOffset;
        part.localPosition = rest + offset;
    }

    /// <summary>Lets go of the gun. <paramref name="groundHeight"/> returns the floor height under a world position.</summary>
    public void BeginDrop(Vector3 worldVelocity, Vector3 spinDegreesPerSecond, Func<Vector3, float> groundHeight)
    {
        if (root == null || drop != null)
            return;
        root.gameObject.SetActive(true);
        MovePart(magazine, magazineRest, Vector3.zero);
        MovePart(bolt, boltRest, Vector3.zero);
        owner = root.parent;
        drop = new DroppedGunPhysics(root.position, root.rotation, worldVelocity, spinDegreesPerSecond, groundHeight);
    }

    public void TickDrop(float deltaTime)
    {
        if (drop == null || root == null)
            return;
        drop.Tick(deltaTime);
        root.SetPositionAndRotation(drop.Position, drop.Rotation);
    }

    /// <summary>Puts the gun back in the hands (respawn).</summary>
    public void Regrip(Transform parent)
    {
        if (root == null)
            return;
        drop = null;
        owner = parent;
        root.SetParent(parent, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
    }

    public void Destroy()
    {
        if (root != null)
            DestroyObject(root.gameObject);
        if (ownedMaterial != null)
            DestroyObject(ownedMaterial);
    }

    private static void ApplyOverrides(Transform gunRoot, Transform model, GunHoldProfile profile)
    {
        Transform right = FindDescendant(model, GunHoldCatalog.OverrideNames[0]);
        Transform left = FindDescendant(model, GunHoldCatalog.OverrideNames[1]);
        Transform stock = FindDescendant(model, GunHoldCatalog.OverrideNames[2]);
        if (right != null)
            profile.rightGrip = gunRoot.InverseTransformPoint(right.position);
        if (left != null)
        {
            Vector3 point = gunRoot.InverseTransformPoint(left.position);
            Vector3 shift = point - profile.leftGrip;
            profile.leftGrip = point;
            profile.leftGripRear += shift;
        }

        if (stock != null)
            profile.stock = gunRoot.InverseTransformPoint(stock.position);
    }

    private static GameObject BuildPrimitiveGun(Transform parent, GunHoldProfile p, Material material)
    {
        var model = new GameObject("Model");
        model.transform.SetParent(parent, false);

        float muzzleZ = p.muzzle.z;
        float backZ = p.stocked ? p.stock.z : p.rightGrip.z - 0.06f;
        float length = muzzleZ - backZ;
        float receiverBack = p.rightGrip.z - 0.05f;
        float receiverFront = p.stocked ? Mathf.Min(muzzleZ - 0.1f, p.leftGrip.z + 0.1f) : muzzleZ - 0.02f;

        AddBox(model.transform, "Receiver", new Vector3(0.05f, 0.085f, receiverFront - receiverBack), new Vector3(0f, p.muzzle.y - 0.015f, (receiverBack + receiverFront) * 0.5f), material);
        if (p.stocked)
        {
            AddBox(model.transform, "Barrel", new Vector3(0.032f, 0.032f, muzzleZ - receiverFront + 0.02f), new Vector3(0f, p.muzzle.y, (muzzleZ + receiverFront) * 0.5f), material);
            AddBox(model.transform, "Stock", new Vector3(0.04f, 0.11f, receiverBack - backZ), new Vector3(0f, p.muzzle.y - 0.02f, (receiverBack + backZ) * 0.5f), material);
        }

        AddBox(model.transform, "Grip", new Vector3(0.035f, 0.11f, 0.04f), new Vector3(p.rightGrip.x, p.rightGrip.y - 0.015f, p.rightGrip.z - 0.005f), material);
        GameObject mag = AddBox(model.transform, "Magazine", new Vector3(0.03f, 0.13f, 0.05f), new Vector3(0f, p.magwell.y - 0.01f, p.magwell.z), material);
        mag.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
        if (length < 0.1f)
            AddBox(model.transform, "Blade", new Vector3(0.02f, 0.02f, 0.12f), new Vector3(0f, 0f, 0.07f), material);
        return model;
    }

    private static GameObject AddBox(Transform parent, string name, Vector3 size, Vector3 position, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        var collider = box.GetComponent<Collider>();
        if (collider != null)
            DestroyObject(collider);
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = size;
        var renderer = box.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        return box;
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        var material = new Material(shader) { color = color };
        material.name = "ThirdPersonGunPrimitive";
        return material;
    }

    private static void StripPhysics(GameObject rootObject)
    {
        Collider[] colliders = rootObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            DestroyObject(colliders[i]);
        Rigidbody[] bodies = rootObject.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
            DestroyObject(bodies[i]);
    }

    private static void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        Transform t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayer(t.GetChild(i).gameObject, layer);
    }

    private static bool HasMesh(GameObject instance)
    {
        MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].sharedMesh != null)
                return true;
        }

        return instance.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
    }

    private static Transform FindDescendant(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;
            Transform nested = FindDescendant(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static void DestroyObject(UnityEngine.Object obj)
    {
        if (obj == null)
            return;
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(obj);
        else
            UnityEngine.Object.DestroyImmediate(obj);
    }
}
