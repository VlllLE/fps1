using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's chosen loadout as plain ids: class, primary weapon, secondary weapon and one skin per weapon.
/// Ids are stable asset names (class name, weapon prefab name, skin asset name) so they survive reordering.
/// </summary>
[Serializable]
public sealed class LoadoutSelection
{
    [Serializable]
    public sealed class SkinPick
    {
        public string weaponId;
        public string skinId;
    }

    [SerializeField] private string classId = string.Empty;
    [SerializeField] private string primaryId = string.Empty;
    [SerializeField] private string secondaryId = string.Empty;
    [SerializeField] private List<SkinPick> skins = new List<SkinPick>();

    public string ClassId
    {
        get => classId ?? string.Empty;
        set => classId = value ?? string.Empty;
    }

    public string PrimaryId
    {
        get => primaryId ?? string.Empty;
        set => primaryId = value ?? string.Empty;
    }

    public string SecondaryId
    {
        get => secondaryId ?? string.Empty;
        set => secondaryId = value ?? string.Empty;
    }

    public int SkinCount => skins != null ? skins.Count : 0;

    /// <summary>Skin id picked for <paramref name="weaponId"/>, or empty for the weapon's default skin.</summary>
    public string GetSkinId(string weaponId)
    {
        if (skins == null || string.IsNullOrEmpty(weaponId))
            return string.Empty;

        for (int i = 0; i < skins.Count; i++)
        {
            if (skins[i] != null && skins[i].weaponId == weaponId)
                return skins[i].skinId ?? string.Empty;
        }

        return string.Empty;
    }

    /// <summary>Sets the skin for a weapon. Empty / null restores the weapon's default skin.</summary>
    public void SetSkinId(string weaponId, string skinId)
    {
        if (string.IsNullOrEmpty(weaponId))
            return;
        if (skins == null)
            skins = new List<SkinPick>();

        for (int i = skins.Count - 1; i >= 0; i--)
        {
            if (skins[i] == null || skins[i].weaponId == weaponId)
                skins.RemoveAt(i);
        }

        if (!string.IsNullOrEmpty(skinId))
            skins.Add(new SkinPick { weaponId = weaponId, skinId = skinId });
    }

    public void ClearSkins()
    {
        skins?.Clear();
    }

    /// <summary>Removes skin picks for which <paramref name="remove"/> (weaponId, skinId) returns true.</summary>
    public void RemoveSkinsWhere(Func<string, string, bool> remove)
    {
        if (skins == null || remove == null)
            return;

        for (int i = skins.Count - 1; i >= 0; i--)
        {
            SkinPick pick = skins[i];
            if (pick == null || remove(pick.weaponId, pick.skinId))
                skins.RemoveAt(i);
        }
    }

    public LoadoutSelection Clone()
    {
        var copy = new LoadoutSelection
        {
            classId = classId,
            primaryId = primaryId,
            secondaryId = secondaryId
        };
        if (skins != null)
        {
            for (int i = 0; i < skins.Count; i++)
            {
                if (skins[i] != null)
                    copy.skins.Add(new SkinPick { weaponId = skins[i].weaponId, skinId = skins[i].skinId });
            }
        }

        return copy;
    }

    public string ToJson()
    {
        return JsonUtility.ToJson(this);
    }

    public static bool TryFromJson(string json, out LoadoutSelection selection)
    {
        selection = null;
        if (string.IsNullOrEmpty(json))
            return false;

        try
        {
            var parsed = new LoadoutSelection();
            JsonUtility.FromJsonOverwrite(json, parsed);
            if (parsed.skins == null)
                parsed.skins = new List<SkinPick>();
            selection = parsed;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public void Save(ILoadoutStore store)
    {
        store?.Write(ToJson());
    }

    /// <summary>Loads the persisted selection. Returns false when nothing (valid) is stored.</summary>
    public static bool TryLoad(ILoadoutStore store, out LoadoutSelection selection)
    {
        selection = null;
        if (store == null || !store.TryRead(out string json))
            return false;

        return TryFromJson(json, out selection);
    }
}
