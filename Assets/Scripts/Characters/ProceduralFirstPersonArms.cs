using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// First-person arms for the local player. The visible mesh is the Blender-built SoldierArmsFP model
/// (one skinned mesh on six arm bones, see <see cref="SoldierModelBinder"/>); when it is missing or fails
/// validation the arms fall back to the procedural humanoid part recipes. Two analytic two-bone IK arms reach from shoulder anchors (just behind the
/// camera) to grip points on the weapon holder, so the hands stay on the weapon through recoil and
/// weapon-holder animation. Works with any weapon placed under <c>WeaponHolder</c>; the camera and
/// weapon attachment points are never modified.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class ProceduralFirstPersonArms : MonoBehaviour
{
    public const string RootName = "FirstPersonArms";
    public const string WeaponHolderName = "WeaponHolder";

    [SerializeField] private HumanoidPalette palette = new HumanoidPalette
    {
        suit = new Color(0.22f, 0.32f, 0.42f),
        suitDark = new Color(0.09f, 0.13f, 0.18f),
        skin = new Color(0.78f, 0.62f, 0.52f),
        accent = new Color(0.55f, 0.45f, 0.22f),
        visor = new Color(0.25f, 0.75f, 0.95f)
    };

    [Tooltip("Arm size relative to the third-person body; first-person arms read better slightly larger.")]
    [SerializeField, Min(0.1f)] private float armScale = 1.3f;
    [SerializeField] private bool buildOnAwake = true;
    [Tooltip("Always use the primitive procedural arms, even when the imported model is available.")]
    [SerializeField] private bool forceProcedural;

    [Header("Anchors (camera space)")]
    [SerializeField] private Vector3 rightShoulder = new Vector3(0.24f, -0.52f, -0.10f);
    [SerializeField] private Vector3 leftShoulder = new Vector3(-0.12f, -0.55f, 0.02f);

    [Header("Grip points (weapon holder space)")]
    [SerializeField] private Vector3 rightGrip = new Vector3(0f, -0.09f, -0.05f);
    [SerializeField] private Vector3 leftGrip = new Vector3(-0.03f, -0.08f, 0.03f);

    [Tooltip("Weapon holder the hands follow. Found by name under the camera when empty.")]
    [SerializeField] private Transform weaponHolder;

    private sealed class Arm
    {
        public Transform upper;
        public Transform lower;
        public Transform hand;
        public float side;
    }

    private readonly Arm rightArm = new Arm { side = 1f };
    private readonly Arm leftArm = new Arm { side = -1f };
    private readonly List<Object> ownedObjects = new List<Object>();
    private SoldierBuildResult soldierBuild;
    private string importFallbackReason;
    private Transform armsRoot;
    private float upperLength;
    private float lowerLength;
    private float idleTime;

    public bool IsBuilt => armsRoot != null;
    /// <summary>True when the imported Blender arm model (one skinned mesh) is displayed.</summary>
    public bool UsesImportedModel => soldierBuild != null;
    public string ImportFallbackReason => importFallbackReason;
    public bool ForceProcedural { get => forceProcedural; set => forceProcedural = value; }
    public IReadOnlyList<SkinnedMeshRenderer> SoldierRenderers
        => soldierBuild != null ? (IReadOnlyList<SkinnedMeshRenderer>)soldierBuild.renderers : System.Array.Empty<SkinnedMeshRenderer>();
    public Transform ArmsRoot => armsRoot;
    public Transform RightHand => rightArm.hand;
    public Transform LeftHand => leftArm.hand;
    public Transform RightUpperArm => rightArm.upper;
    public Transform LeftUpperArm => leftArm.upper;
    public Transform RightForearm => rightArm.lower;
    public Transform LeftForearm => leftArm.lower;
    public float UpperLength => upperLength;
    public float LowerLength => lowerLength;
    public Vector3 RightShoulderAnchor => rightShoulder;
    public Vector3 LeftShoulderAnchor => leftShoulder;

    private void Awake()
    {
        if (buildOnAwake)
            Build();
    }

    private void OnDestroy()
    {
        DestroyOwnedObjects();
    }

    private void LateUpdate()
    {
        if (armsRoot == null)
            return;

        idleTime += Time.deltaTime;
        Solve();
    }

    /// <summary>Hand position the solver aims for (camera space) for the given side.</summary>
    public Vector3 GetGripTargetLocal(bool right)
    {
        Vector3 grip = right ? rightGrip : leftGrip;
        Transform holder = ResolveWeaponHolder();
        if (holder == null)
            return grip + new Vector3(0.3f, -0.2f, 0.5f);
        return transform.InverseTransformPoint(holder.TransformPoint(grip));
    }

    [ContextMenu("Rebuild First Person Arms")]
    public void Build()
    {
        Clear();

        var rootObject = new GameObject(RootName);
        rootObject.layer = gameObject.layer;
        rootObject.hideFlags = HideFlags.DontSaveInEditor;
        armsRoot = rootObject.transform;
        armsRoot.SetParent(transform, false);
        armsRoot.localPosition = Vector3.zero;
        armsRoot.localRotation = Quaternion.identity;

        upperLength = HumanoidProportions.UpperArmLength * armScale;
        lowerLength = HumanoidProportions.ForearmLength * armScale;

        BuildArm(rightArm);
        BuildArm(leftArm);

        if (!TryAttachImportedModel())
            AttachProceduralParts();

        Solve();
    }

    public void Clear()
    {
        if (armsRoot != null)
        {
            armsRoot.gameObject.name = RootName + "_Old";
            armsRoot.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(armsRoot.gameObject);
            else
                DestroyImmediate(armsRoot.gameObject);
        }

        armsRoot = null;
        rightArm.upper = rightArm.lower = rightArm.hand = null;
        leftArm.upper = leftArm.lower = leftArm.hand = null;
        if (soldierBuild != null)
        {
            soldierBuild.Destroy();
            soldierBuild = null;
        }

        DestroyOwnedObjects();
    }

    /// <summary>Runs the IK solve now (also called every LateUpdate).</summary>
    public void Solve()
    {
        if (armsRoot == null)
            return;

        float breathe = Mathf.Sin(idleTime * Mathf.PI * 2f * 0.22f) * 0.004f;
        SolveArm(rightArm, rightShoulder + new Vector3(0f, breathe, 0f), GetGripTargetLocal(true));
        SolveArm(leftArm, leftShoulder + new Vector3(0f, breathe, 0f), GetGripTargetLocal(false));
    }

    private void BuildArm(Arm arm)
    {
        string side = arm.side > 0f ? "Right" : "Left";
        arm.upper = CreateBoneObject("FP_" + side + "UpperArm");
        arm.lower = CreateBoneObject("FP_" + side + "Forearm");
        arm.hand = CreateBoneObject("FP_" + side + "Hand");
    }

    private Transform CreateBoneObject(string partName)
    {
        var go = new GameObject(partName);
        go.layer = gameObject.layer;
        go.transform.SetParent(armsRoot, false);
        return go.transform;
    }

    private bool TryAttachImportedModel()
    {
        importFallbackReason = null;
        if (forceProcedural)
        {
            importFallbackReason = "procedural arms requested";
            return false;
        }

        try
        {
            var bones = new Transform[HumanoidParts.BoneCount];
            bones[(int)HumanoidBone.UpperArmR] = rightArm.upper;
            bones[(int)HumanoidBone.ForearmR] = rightArm.lower;
            bones[(int)HumanoidBone.HandR] = rightArm.hand;
            bones[(int)HumanoidBone.UpperArmL] = leftArm.upper;
            bones[(int)HumanoidBone.ForearmL] = leftArm.lower;
            bones[(int)HumanoidBone.HandL] = leftArm.hand;

            SoldierModelSet set = HumanoidBody.ModelSetOverride != null ? HumanoidBody.ModelSetOverride : SoldierModelSet.Load();
            if (!SoldierModelBinder.TryBuildArms(set, bones, armsRoot, armScale, palette, gameObject.layer,
                    out SoldierBuildResult build, out string error))
            {
                importFallbackReason = error ?? "unknown error";
                Debug.LogWarning("[ProceduralFirstPersonArms] Imported arm model unavailable, using procedural arms: " + importFallbackReason, this);
                return false;
            }

            soldierBuild = build;
            return true;
        }
        catch (System.Exception exception)
        {
            importFallbackReason = exception.GetType().Name + ": " + exception.Message;
            Debug.LogWarning("[ProceduralFirstPersonArms] Imported arm model failed, using procedural arms: " + importFallbackReason, this);
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
        Material[] materials = HumanoidMaterials.Create(palette);
        for (int i = 0; i < materials.Length; i++)
            ownedObjects.Add(materials[i]);

        AttachProceduralMesh(rightArm.upper, HumanoidBone.UpperArmR, materials);
        AttachProceduralMesh(rightArm.lower, HumanoidBone.ForearmR, materials);
        AttachProceduralMesh(rightArm.hand, HumanoidBone.HandR, materials);
        AttachProceduralMesh(leftArm.upper, HumanoidBone.UpperArmL, materials);
        AttachProceduralMesh(leftArm.lower, HumanoidBone.ForearmL, materials);
        AttachProceduralMesh(leftArm.hand, HumanoidBone.HandL, materials);
    }

    private void AttachProceduralMesh(Transform part, HumanoidBone bone, Material[] materials)
    {
        HumanoidMeshBuilder geometry = HumanoidParts.Build(bone, armScale);
        Mesh mesh = geometry.ToMesh(part.name, out HumanoidSlot[] usedSlots);
        ownedObjects.Add(mesh);

        part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.gameObject.AddComponent<MeshRenderer>();
        var slotMaterials = new Material[usedSlots.Length];
        for (int i = 0; i < usedSlots.Length; i++)
            slotMaterials[i] = materials[(int)usedSlots[i]];
        renderer.sharedMaterials = slotMaterials;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void SolveArm(Arm arm, Vector3 shoulderLocal, Vector3 targetLocal)
    {
        if (arm.upper == null || arm.lower == null || arm.hand == null)
            return;

        Vector3 pole = new Vector3(arm.side * 0.35f, -1f, -0.2f);
        HumanoidIK.SolveTwoBone(shoulderLocal, targetLocal, upperLength, lowerLength, pole, out Vector3 elbow, out Vector3 wrist);

        Vector3 upperDir = Direction(elbow - shoulderLocal, Vector3.forward);
        Vector3 lowerDir = Direction(wrist - elbow, Vector3.forward);
        Quaternion upperRotation = AimDown(upperDir, Vector3.forward);
        Quaternion lowerRotation = AimDown(lowerDir, Vector3.forward);

        arm.upper.localPosition = shoulderLocal;
        arm.upper.localRotation = upperRotation;
        arm.lower.localPosition = elbow;
        arm.lower.localRotation = lowerRotation;
        arm.hand.localPosition = wrist;

        Vector3 handForward = (targetLocal - wrist).sqrMagnitude > 1e-6f ? lowerDir : Vector3.forward;
        arm.hand.localRotation = AimDown(handForward, new Vector3(-arm.side, 0f, 0f));
    }

    private Transform ResolveWeaponHolder()
    {
        if (weaponHolder == null)
            weaponHolder = transform.Find(WeaponHolderName);
        return weaponHolder;
    }

    private static Vector3 Direction(Vector3 v, Vector3 fallback)
    {
        float lengthSq = v.sqrMagnitude;
        return lengthSq < 1e-10f ? fallback : v / Mathf.Sqrt(lengthSq);
    }

    /// <summary>Rotation whose local -Y follows <paramref name="direction"/>, with local +Z as close to <paramref name="rollReference"/> as possible.</summary>
    public static Quaternion AimDown(Vector3 direction, Vector3 rollReference)
    {
        Vector3 forward = rollReference - direction * Vector3.Dot(rollReference, direction);
        if (forward.sqrMagnitude < 1e-4f)
            forward = Vector3.up - direction * Vector3.Dot(Vector3.up, direction);
        if (forward.sqrMagnitude < 1e-6f)
            forward = Vector3.Cross(direction, Vector3.right);
        forward = forward / Mathf.Sqrt(forward.sqrMagnitude);
        return Quaternion.LookRotation(forward, -direction);
    }

    private void DestroyOwnedObjects()
    {
        for (int i = 0; i < ownedObjects.Count; i++)
        {
            if (ownedObjects[i] == null)
                continue;
            if (Application.isPlaying)
                Destroy(ownedObjects[i]);
            else
                DestroyImmediate(ownedObjects[i]);
        }

        ownedObjects.Clear();
    }
}
