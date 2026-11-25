using System.Collections.Generic;
using UnityEngine;

/// <summary>Construction-time options for a <see cref="HumanoidBody"/> added from code.</summary>
public struct HumanoidBodySettings
{
    public HumanoidPalette palette;
    /// <summary>Uniform body scale (1 = fits a 2 m tall controller). Values &lt;= 0 derive it from the CharacterController.</summary>
    public float scale;
    /// <summary>Feet position relative to the owning transform. Ignored unless <see cref="useFeetOffset"/> is set.</summary>
    public Vector3 feetOffset;
    public bool useFeetOffset;
    public bool createHitboxes;
    public HumanoidArmPose armPose;
    /// <summary>Skip the imported Blender soldier and build the primitive procedural body instead.</summary>
    public bool forceProcedural;

    public static HumanoidBodySettings Player()
    {
        return new HumanoidBodySettings
        {
            palette = HumanoidPalette.Player,
            armPose = HumanoidArmPose.WeaponReady
        };
    }

    /// <summary>
    /// Settings for a stationary training target whose pivot sits at the centre of a 2 m * <paramref name="scale"/>
    /// tall body (the layout of the legacy primitive targets).
    /// </summary>
    public static HumanoidBodySettings Target(Color tint, float scale)
    {
        return new HumanoidBodySettings
        {
            palette = HumanoidPalette.ForTarget(tint),
            scale = scale,
            feetOffset = new Vector3(0f, -scale, 0f),
            useFeetOffset = true,
            createHitboxes = true,
            armPose = HumanoidArmPose.Relaxed
        };
    }
}

/// <summary>
/// Builds and animates the humanoid used for the first-person player's shadow / third-person body and
/// for training targets. The 19-bone hierarchy is generated at runtime so prefabs and scenes only store
/// this small component. The visible mesh is the Blender-built soldier (skinned to those bones, see
/// <see cref="SoldierModelBinder"/>); if the imported model is missing or fails validation the body falls
/// back to the primitive procedural parts (rigid meshes on the bones). It is sized from the
/// CharacterController and never touches movement, input or camera code.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class HumanoidBody : MonoBehaviour
{
    public const string BodyRootName = "BodyRoot";

    private static HumanoidBodySettings? pendingSettings;

    /// <summary>Test hook: use this model set instead of the one in Resources (an empty set exercises the fallback).</summary>
    public static SoldierModelSet ModelSetOverride;

    [Header("Look")]
    [SerializeField] private HumanoidPalette palette = new HumanoidPalette
    {
        suit = new Color(0.18f, 0.28f, 0.38f),
        suitDark = new Color(0.09f, 0.13f, 0.18f),
        skin = new Color(0.78f, 0.62f, 0.52f),
        accent = new Color(0.55f, 0.45f, 0.22f),
        visor = new Color(0.25f, 0.75f, 0.95f)
    };

    [Header("Sizing")]
    [Tooltip("Uniform body scale. 0 derives it from the CharacterController height (height / 2).")]
    [SerializeField, Min(0f)] private float scaleOverride;
    [SerializeField] private bool useFeetOffsetOverride;
    [Tooltip("Feet position relative to this transform when no CharacterController defines it.")]
    [SerializeField] private Vector3 feetOffsetOverride;

    [Header("Behaviour")]
    [SerializeField] private bool buildOnAwake = true;
    [Tooltip("Create static Head/Body/limb hit volumes (for targets and remote pawns, not for the CharacterController player).")]
    [SerializeField] private bool createHitboxes;
    [SerializeField] private bool animate = true;
    [Tooltip("Always use the primitive procedural body, even when the imported soldier model is available.")]
    [SerializeField] private bool forceProcedural;
    [SerializeField] private HumanoidArmPose armPose = HumanoidArmPose.WeaponReady;
    [Tooltip("Optional look direction source (usually the player camera). Found automatically if empty.")]
    [SerializeField] private Transform lookSource;

    private readonly Transform[] bones = new Transform[HumanoidParts.BoneCount];
    private readonly List<GameObject> hitboxes = new List<GameObject>();
    private readonly List<Object> ownedObjects = new List<Object>();
    private Transform bodyRoot;
    private Material[] materials;
    private SoldierBuildResult soldierBuild;
    private string importFallbackReason;
    private HumanoidAnimator animator;
    private CharacterController controller;
    private MovementController movement;
    private Target target;
    private float builtScale = 1f;
    private float standingControllerHeight;
    private Vector3 lastPosition;
    private bool hasLastPosition;

    /// <summary>
    /// Adds a body with explicit settings. In play mode the body is built during Awake with those settings;
    /// in edit mode only the settings are stored and the body is generated when the scene runs.
    /// </summary>
    public static HumanoidBody AddTo(GameObject host, HumanoidBodySettings settings)
    {
        pendingSettings = settings;
        try
        {
            HumanoidBody body = host.AddComponent<HumanoidBody>();
            body.ApplySettings(settings);
            return body;
        }
        finally
        {
            pendingSettings = null;
        }
    }

    public Transform BodyRoot => bodyRoot;
    public HumanoidAnimator Animator => animator;
    public float BuiltScale => builtScale;
    public bool IsBuilt => bodyRoot != null;
    public IReadOnlyList<GameObject> Hitboxes => hitboxes;
    public Material[] Materials => materials;
    /// <summary>True when the imported Blender soldier (skinned meshes) is displayed instead of the procedural parts.</summary>
    public bool UsesImportedModel => soldierBuild != null;
    /// <summary>Why the imported model was not used (null when it is in use or was not attempted).</summary>
    public string ImportFallbackReason => importFallbackReason;
    public IReadOnlyList<SkinnedMeshRenderer> SoldierRenderers
        => soldierBuild != null ? (IReadOnlyList<SkinnedMeshRenderer>)soldierBuild.renderers : System.Array.Empty<SkinnedMeshRenderer>();
    public HumanoidArmPose ArmPose { get => armPose; set => armPose = value; }
    public bool AnimationEnabled { get => animate; set => animate = value; }

    public Transform GetBone(HumanoidBone bone)
    {
        return bones[(int)bone];
    }

    /// <summary>Parts that must not render in the local first-person camera (head, neck, torso arms).</summary>
    public Transform[] GetLocalHideTargets()
    {
        var list = new List<Transform>(4);
        AddIfNotNull(list, bones[(int)HumanoidBone.Neck]);
        AddIfNotNull(list, bones[(int)HumanoidBone.Head]);
        AddIfNotNull(list, bones[(int)HumanoidBone.ShoulderL]);
        AddIfNotNull(list, bones[(int)HumanoidBone.ShoulderR]);
        return list.ToArray();
    }

    private void Awake()
    {
        if (pendingSettings.HasValue)
        {
            ApplySettings(pendingSettings.Value);
            pendingSettings = null;
        }

        if (buildOnAwake)
            Build();
    }

    private void OnEnable()
    {
        if (target == null)
            target = GetComponent<Target>();
        if (target != null)
            target.OnDamageTaken += HandleDamageTaken;
    }

    private void OnDisable()
    {
        if (target != null)
            target.OnDamageTaken -= HandleDamageTaken;
        hasLastPosition = false;
    }

    private void OnDestroy()
    {
        DestroyOwnedObjects();
    }

    private void LateUpdate()
    {
        if (!animate || animator == null)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        animator.Tick(dt, GatherInput(dt));
        ApplyPose();
    }

    public void ApplySettings(HumanoidBodySettings settings)
    {
        palette = settings.palette;
        scaleOverride = Mathf.Max(0f, settings.scale);
        useFeetOffsetOverride = settings.useFeetOffset;
        feetOffsetOverride = settings.feetOffset;
        createHitboxes = settings.createHitboxes;
        armPose = settings.armPose;
        forceProcedural = settings.forceProcedural;
    }

    /// <summary>(Re)builds the body hierarchy, meshes, materials and optional hit volumes.</summary>
    [ContextMenu("Rebuild Humanoid Body")]
    public void Build()
    {
        Clear();

        controller = GetComponent<CharacterController>();
        movement = GetComponent<MovementController>();
        standingControllerHeight = controller != null ? controller.height : HumanoidProportions.NominalControllerHeight;
        builtScale = ResolveScale();
        Vector3 feet = ResolveFeetOffset();

        var rootObject = new GameObject(BodyRootName);
        rootObject.layer = gameObject.layer;
        rootObject.hideFlags = HideFlags.DontSaveInEditor;
        bodyRoot = rootObject.transform;
        bodyRoot.SetParent(transform, false);
        bodyRoot.localPosition = feet;
        bodyRoot.localRotation = Quaternion.identity;
        bodyRoot.localScale = Vector3.one;

        animator = new HumanoidAnimator();

        foreach (HumanoidBone bone in HumanoidParts.AllBones)
            CreateBone(bone);

        ApplyPose();

        if (!TryAttachImportedModel())
            AttachProceduralParts();

        if (createHitboxes)
            BuildHitboxes(feet);

        LinkLocalVisibility();
        lookSource = lookSource != null ? lookSource : FindLookSource();
        hasLastPosition = false;
    }

    /// <summary>Destroys the generated body (not the component).</summary>
    public void Clear()
    {
        for (int i = 0; i < hitboxes.Count; i++)
        {
            if (hitboxes[i] == null)
                continue;
            hitboxes[i].name = "Hitbox_Old";
            hitboxes[i].SetActive(false);
            DestroyUnityObject(hitboxes[i]);
        }

        hitboxes.Clear();

        if (bodyRoot != null)
        {
            bodyRoot.gameObject.name = "BodyRoot_Old";
            bodyRoot.gameObject.SetActive(false);
            DestroyUnityObject(bodyRoot.gameObject);
        }

        bodyRoot = null;
        for (int i = 0; i < bones.Length; i++)
            bones[i] = null;

        if (soldierBuild != null)
        {
            soldierBuild.Destroy();
            soldierBuild = null;
        }

        DestroyOwnedObjects();
        animator = null;
    }

    /// <summary>Applies one animation step without waiting for LateUpdate (used by tests and tools).</summary>
    public void Step(float deltaTime, HumanoidAnimationInput input)
    {
        if (animator == null)
            return;
        animator.Tick(deltaTime, input);
        ApplyPose();
    }

    /// <summary>Re-tints the generated materials (e.g. team colours).</summary>
    public void SetPalette(HumanoidPalette newPalette)
    {
        palette = newPalette;
        if (materials == null)
            return;
        if (soldierBuild != null)
        {
            SoldierMaterials.ApplyPalette(soldierBuild.soldierMaterials, newPalette);
            return;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null)
                materials[i].color = newPalette.Get((HumanoidSlot)i);
        }
    }

    private float ResolveScale()
    {
        if (scaleOverride > 0f)
            return scaleOverride;
        if (controller != null)
            return HumanoidProportions.ScaleForControllerHeight(controller.height);
        return 1f;
    }

    private Vector3 ResolveFeetOffset()
    {
        if (useFeetOffsetOverride)
            return feetOffsetOverride;
        if (controller != null)
            return new Vector3(0f, controller.center.y - controller.height * 0.5f, 0f);
        return Vector3.zero;
    }

    private void CreateBone(HumanoidBone bone)
    {
        var go = new GameObject(bone.ToString());
        go.layer = gameObject.layer;
        HumanoidBone? parent = HumanoidParts.GetParent(bone);
        Transform parentTransform = parent.HasValue ? bones[(int)parent.Value] : bodyRoot;
        go.transform.SetParent(parentTransform, false);
        go.transform.localPosition = animator.GetLocalPosition(bone) * builtScale;
        go.transform.localRotation = animator.GetLocalRotation(bone);
        bones[(int)bone] = go.transform;
    }

    private bool TryAttachImportedModel()
    {
        importFallbackReason = null;
        if (forceProcedural)
        {
            importFallbackReason = "procedural body requested";
            return false;
        }

        try
        {
            SoldierModelSet set = ModelSetOverride != null ? ModelSetOverride : SoldierModelSet.Load();
            if (!SoldierModelBinder.TryBuildBody(set, bones, bodyRoot, builtScale, palette, gameObject.layer,
                    out SoldierBuildResult build, out string error))
            {
                importFallbackReason = error ?? "unknown error";
                Debug.LogWarning("[HumanoidBody] Imported soldier model unavailable, using the procedural body: " + importFallbackReason, this);
                return false;
            }

            soldierBuild = build;
            materials = new Material[HumanoidMeshBuilder.SlotCount];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = build.soldierMaterials[(int)SoldierMaterials.ForHumanoidSlot((HumanoidSlot)i)];
            return true;
        }
        catch (System.Exception exception)
        {
            importFallbackReason = exception.GetType().Name + ": " + exception.Message;
            Debug.LogWarning("[HumanoidBody] Imported soldier model failed, using the procedural body: " + importFallbackReason, this);
            if (soldierBuild != null)
            {
                soldierBuild.Destroy();
                soldierBuild = null;
            }

            return false;
        }
    }

    private void AttachProceduralParts()
    {
        materials = HumanoidMaterials.Create(palette);
        for (int i = 0; i < materials.Length; i++)
            ownedObjects.Add(materials[i]);

        foreach (HumanoidBone bone in HumanoidParts.AllBones)
        {
            Transform t = bones[(int)bone];
            if (t == null)
                continue;

            HumanoidMeshBuilder geometry = HumanoidParts.Build(bone, builtScale);
            if (geometry.VertexCount == 0)
                continue;

            Mesh mesh = geometry.ToMesh("Humanoid_" + bone, out HumanoidSlot[] usedSlots);
            ownedObjects.Add(mesh);

            var filter = t.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = t.gameObject.AddComponent<MeshRenderer>();
            var slotMaterials = new Material[usedSlots.Length];
            for (int i = 0; i < usedSlots.Length; i++)
                slotMaterials[i] = materials[(int)usedSlots[i]];
            renderer.sharedMaterials = slotMaterials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private void ApplyPose()
    {
        if (animator == null)
            return;

        foreach (HumanoidBone bone in HumanoidParts.AllBones)
        {
            Transform t = bones[(int)bone];
            if (t == null)
                continue;
            t.localRotation = animator.GetLocalRotation(bone);
        }

        Transform hips = bones[(int)HumanoidBone.Hips];
        if (hips != null)
            hips.localPosition = animator.GetLocalPosition(HumanoidBone.Hips) * builtScale;
    }

    private HumanoidAnimationInput GatherInput(float dt)
    {
        var input = new HumanoidAnimationInput { armPose = armPose };

        Vector3 worldVelocity;
        if (movement != null)
        {
            worldVelocity = movement.Velocity;
        }
        else
        {
            worldVelocity = hasLastPosition ? (transform.position - lastPosition) / dt : Vector3.zero;
        }

        lastPosition = transform.position;
        hasLastPosition = true;

        Vector3 local = transform.InverseTransformDirection(worldVelocity);
        input.forwardSpeed = local.z;
        input.rightSpeed = local.x;
        input.verticalSpeed = worldVelocity.y;

        input.locomotion = HumanoidLocomotion.Grounded;
        if (movement != null)
        {
            switch (movement.CurrentState)
            {
                case MovementController.MovementState.Sliding:
                    input.locomotion = HumanoidLocomotion.Sliding;
                    break;
                case MovementController.MovementState.Diving:
                    input.locomotion = HumanoidLocomotion.Diving;
                    break;
                case MovementController.MovementState.Airborne:
                case MovementController.MovementState.Mantling:
                    input.locomotion = HumanoidLocomotion.Airborne;
                    break;
            }

            if (!movement.IsGrounded && input.locomotion == HumanoidLocomotion.Grounded)
                input.locomotion = HumanoidLocomotion.Airborne;
        }

        if (controller != null && standingControllerHeight > 0.01f)
        {
            float reduction = standingControllerHeight - controller.height;
            input.crouch01 = Mathf.Clamp01(reduction / (standingControllerHeight * 0.4f));
        }

        if (lookSource != null)
        {
            float y = Mathf.Clamp(lookSource.forward.y, -1f, 1f);
            input.lookPitchDegrees = -Mathf.Asin(y) * Mathf.Rad2Deg;
        }

        return input;
    }

    private Transform FindLookSource()
    {
        Camera cam = GetComponentInChildren<Camera>();
        return cam != null ? cam.transform : null;
    }

    private void BuildHitboxes(Vector3 feet)
    {
        foreach (HumanoidHitboxSpec spec in HumanoidProportions.Hitboxes)
        {
            if (transform.Find(spec.Name) != null)
                continue;

            var go = new GameObject(spec.Name);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = feet + spec.Center * builtScale;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            switch (spec.Shape)
            {
                case HumanoidHitboxShape.Sphere:
                    var sphere = go.AddComponent<SphereCollider>();
                    sphere.radius = spec.Size.x * builtScale;
                    break;
                case HumanoidHitboxShape.Box:
                    var box = go.AddComponent<BoxCollider>();
                    box.size = spec.Size * builtScale;
                    break;
                default:
                    var capsule = go.AddComponent<CapsuleCollider>();
                    capsule.direction = 1;
                    capsule.radius = spec.Size.x * builtScale;
                    capsule.height = spec.Size.y * builtScale;
                    break;
            }

            go.AddComponent<HitZone>().Configure(spec.Zone);
            hitboxes.Add(go);
        }
    }

    private void LinkLocalVisibility()
    {
        var visibility = GetComponent<LocalPlayerBodyVisibility>();
        if (visibility != null)
            visibility.SetHideTargets(GetLocalHideTargets());
    }

    private void HandleDamageTaken(DamageInfo info)
    {
        if (animator == null)
            return;

        float intensity = 0.35f + Mathf.Clamp01(info.damage / 50f) * 0.65f;
        if (info.isHeadshot)
            intensity += 0.3f;
        animator.AddFlinch(intensity, transform.InverseTransformDirection(info.hitDirection));
    }

    private void DestroyOwnedObjects()
    {
        for (int i = 0; i < ownedObjects.Count; i++)
        {
            if (ownedObjects[i] != null)
                DestroyUnityObject(ownedObjects[i]);
        }

        ownedObjects.Clear();
        materials = null;
    }

    private static void DestroyUnityObject(Object obj)
    {
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    private static void AddIfNotNull(List<Transform> list, Transform t)
    {
        if (t != null)
            list.Add(t);
    }
}
