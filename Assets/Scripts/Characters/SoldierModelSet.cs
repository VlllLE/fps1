using UnityEngine;

/// <summary>Material slots of the imported soldier models (names match the Blender generator).</summary>
public enum SoldierMaterialSlot
{
    Armor = 0,
    Cloth,
    Skin,
    Gear,
    Accent,
    Lens,
    Eye
}

/// <summary>
/// References the Blender-built soldier assets (<c>Tools/Blender/build_soldier.py</c>): the skinned
/// third-person body, the first-person arms, the template materials and the baked ambient occlusion
/// map. Loaded from <c>Resources/Characters/SoldierModelSet</c>; when the asset or a model is missing
/// the humanoid builder falls back to the procedural body.
/// </summary>
[CreateAssetMenu(fileName = "SoldierModelSet", menuName = "Characters/Soldier Model Set")]
public sealed class SoldierModelSet : ScriptableObject
{
    public const string ResourcePath = "Characters/SoldierModelSet";
    public const int MaterialSlotCount = 7;

    [Tooltip("Imported Soldier.fbx (4 skinned meshes: Body, Head, ArmL, ArmR on the 19 humanoid bones).")]
    public GameObject bodyModel;
    [Tooltip("Imported SoldierArmsFP.fbx (one skinned mesh on UpperArm/Forearm/Hand L+R).")]
    public GameObject armsModel;
    [Tooltip("Template materials indexed by SoldierMaterialSlot (Armor, Cloth, Skin, Gear, Accent, Lens, Eye).")]
    public Material[] materials = new Material[MaterialSlotCount];
    [Tooltip("Baked ambient occlusion of the third-person body (UV0).")]
    public Texture2D ambientOcclusion;
    [Tooltip("Baked ambient occlusion of the first-person arms (UV0).")]
    public Texture2D armsAmbientOcclusion;
    [Tooltip("Height of the body model in metres (feet to the antenna tip), used to validate and normalise the import scale.")]
    public float bodyHeight = 1.915f;
    [Tooltip("Height of the first-person arm model in metres.")]
    public float armsHeight = 0.737f;

    private static SoldierModelSet cached;

    /// <summary>Loads the model set from Resources, or null when it is not present.</summary>
    public static SoldierModelSet Load()
    {
        if (cached == null)
            cached = Resources.Load<SoldierModelSet>(ResourcePath);
        return cached;
    }

    public Material GetTemplate(SoldierMaterialSlot slot)
    {
        int index = (int)slot;
        return materials != null && index >= 0 && index < materials.Length ? materials[index] : null;
    }
}
