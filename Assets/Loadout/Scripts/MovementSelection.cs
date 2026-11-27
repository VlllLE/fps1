using System;
using UnityEngine;

/// <summary>
/// The player's chosen movement archetype as a plain id (Default, Scout, Heavy, Acrobat, Marksman). Stored as one JSON
/// string through the same <see cref="ILoadoutStore"/> backends the weapon loadout uses, under its own PlayerPrefs key.
/// The class speed multiplier is not part of it: that always follows the equipped class.
/// </summary>
[Serializable]
public sealed class MovementSelection
{
    public const string StoreKey = "FPS.Movement.v1";

    [SerializeField] private string archetypeId = string.Empty;

    public string ArchetypeId
    {
        get => archetypeId ?? string.Empty;
        set => archetypeId = value ?? string.Empty;
    }

    public MovementSelection Clone()
    {
        return new MovementSelection { archetypeId = archetypeId };
    }

    public string ToJson()
    {
        return JsonUtility.ToJson(this);
    }

    public static bool TryFromJson(string json, out MovementSelection selection)
    {
        selection = null;
        if (string.IsNullOrEmpty(json))
            return false;

        try
        {
            var parsed = new MovementSelection();
            JsonUtility.FromJsonOverwrite(json, parsed);
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
    public static bool TryLoad(ILoadoutStore store, out MovementSelection selection)
    {
        selection = null;
        if (store == null || !store.TryRead(out string json))
            return false;

        return TryFromJson(json, out selection) && !string.IsNullOrEmpty(selection.ArchetypeId);
    }
}
