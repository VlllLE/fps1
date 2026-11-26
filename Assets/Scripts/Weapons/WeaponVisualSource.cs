using UnityEngine;

/// <summary>
/// Finds the imported model of a weapon prefab and the material remap its model loader would apply, without
/// instantiating the (behaviour-heavy) gameplay prefab. Used for third-person guns on soldiers.
/// </summary>
public static class WeaponVisualSource
{
    /// <summary>Returns the model asset to instantiate, or null when the prefab has none (the caller then builds a primitive gun).</summary>
    public static GameObject FindModel(GameObject weaponPrefab, out System.Action<GameObject> applyMaterials)
    {
        applyMaterials = null;
        if (weaponPrefab == null)
            return null;

        var imported = weaponPrefab.GetComponentInChildren<ImportedGunModel>(true);
        if (imported != null && imported.ImportedModelPrefab != null)
        {
            applyMaterials = imported.ApplyMaterials;
            return imported.ImportedModelPrefab;
        }

        var assault = weaponPrefab.GetComponentInChildren<AssaultWeaponModel>(true);
        if (assault != null && assault.ImportedModelPrefab != null)
        {
            applyMaterials = assault.ApplyMaterials;
            return assault.ImportedModelPrefab;
        }

        var loader = weaponPrefab.GetComponentInChildren<WeaponModelLoader>(true);
        if (loader != null && loader.ModelPrefab != null)
        {
            applyMaterials = loader.ApplyMaterials;
            return loader.ModelPrefab;
        }

        var karambit = weaponPrefab.GetComponentInChildren<KarambitModel>(true);
        if (karambit != null && karambit.ImportedModelPrefab != null)
        {
            applyMaterials = karambit.ApplyMaterials;
            return karambit.ImportedModelPrefab;
        }

        return null;
    }
}
