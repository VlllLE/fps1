using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One selectable movement archetype: a stable id, menu text and the <see cref="MovementConfig"/> it applies.</summary>
[Serializable]
public sealed class MovementArchetypeEntry
{
    public const string DefaultId = "Default";

    [Tooltip("Stable id stored in PlayerPrefs. Defaults to the display name.")]
    public string id;
    public string displayName;
    [TextArea(1, 3)] public string description;
    public MovementConfig config;

    public string Id => string.IsNullOrEmpty(id) ? displayName ?? string.Empty : id;
    public bool IsDefault => Id == DefaultId;
}

/// <summary>
/// The movement archetypes the player can choose between (Default, Scout, Heavy, Acrobat, Marksman), loaded from
/// <c>Resources/MovementArchetypeCatalog</c>. Rebuild with <c>Tools &gt; Movement &gt; Rebuild Movement Catalog</c>.
/// </summary>
[CreateAssetMenu(fileName = "MovementArchetypeCatalog", menuName = "FPS/Movement Archetype Catalog", order = 2)]
public class MovementArchetypeCatalog : ScriptableObject
{
    public const string ResourceName = "MovementArchetypeCatalog";

    public MovementArchetypeEntry[] archetypes = Array.Empty<MovementArchetypeEntry>();

    public int Count => archetypes != null ? archetypes.Length : 0;

    public static MovementArchetypeCatalog LoadFromResources()
    {
        return Resources.Load<MovementArchetypeCatalog>(ResourceName);
    }

    /// <summary>Builds an in-memory catalog (tests, tools, scenes that ship without the asset).</summary>
    public static MovementArchetypeCatalog CreateRuntime(IEnumerable<MovementArchetypeEntry> entries)
    {
        var catalog = CreateInstance<MovementArchetypeCatalog>();
        catalog.hideFlags = HideFlags.HideAndDontSave;
        var list = new List<MovementArchetypeEntry>();
        if (entries != null)
        {
            foreach (MovementArchetypeEntry entry in entries)
            {
                if (entry != null && entry.config != null)
                    list.Add(entry);
            }
        }
        catalog.archetypes = list.ToArray();
        return catalog;
    }

    public MovementArchetypeEntry Find(string id)
    {
        if (archetypes == null || string.IsNullOrEmpty(id))
            return null;

        for (int i = 0; i < archetypes.Length; i++)
        {
            if (archetypes[i] != null && archetypes[i].Id == id)
                return archetypes[i];
        }

        return null;
    }

    public MovementArchetypeEntry FindByConfig(MovementConfig config)
    {
        if (archetypes == null || config == null)
            return null;

        for (int i = 0; i < archetypes.Length; i++)
        {
            if (archetypes[i] != null && archetypes[i].config == config)
                return archetypes[i];
        }

        return null;
    }

    /// <summary>The entry used when nothing is chosen: the one named Default, else the first.</summary>
    public MovementArchetypeEntry DefaultEntry
    {
        get
        {
            MovementArchetypeEntry entry = Find(MovementArchetypeEntry.DefaultId);
            if (entry != null)
                return entry;
            return archetypes != null && archetypes.Length > 0 ? archetypes[0] : null;
        }
    }
}

/// <summary>
/// The numbers the movement menu shows for one <see cref="MovementConfig"/>, derived straight from the config so the
/// menu can never disagree with what the controller does. Speeds with the class multiplier applied are "effective".
/// </summary>
public readonly struct MovementStatSheet
{
    public readonly float ClassMultiplier;
    public readonly float WalkSpeed;
    public readonly float SprintSpeed;
    public readonly float EffectiveWalkSpeed;
    public readonly float EffectiveSprintSpeed;
    public readonly float JumpForce;
    public readonly float JumpHeight;
    public readonly float AirControl;
    public readonly float AirAcceleration;
    public readonly float SlideBoostSpeed;
    public readonly float SlideDuration;
    public readonly float SlideFriction;
    public readonly float BhopCap;
    public readonly float EffectiveBhopCap;
    public readonly float BhopRetention;
    public readonly float DiveSpeed;
    public readonly float WallBounceForce;
    public readonly bool WallBounce;
    public readonly bool WallRun;
    public readonly bool Mantle;

    public MovementStatSheet(MovementConfig config, float classMultiplier = 1f)
    {
        float mult = Mathf.Max(0.1f, classMultiplier);
        ClassMultiplier = mult;
        WalkSpeed = config.walkSpeed;
        SprintSpeed = config.walkSpeed * config.sprintMultiplier;
        EffectiveWalkSpeed = WalkSpeed * mult;
        EffectiveSprintSpeed = SprintSpeed * mult;
        JumpForce = config.jumpForce;
        float gravity = Physics.gravity.magnitude * Mathf.Max(0.01f, config.gravityMultiplier);
        JumpHeight = config.jumpForce * config.jumpForce / (2f * gravity);
        AirControl = config.airControl;
        AirAcceleration = config.airAcceleration;
        SlideBoostSpeed = config.slideBoostSpeed;
        SlideDuration = config.slideDuration;
        SlideFriction = config.slideFriction;
        BhopCap = config.bhopMaxSpeed;
        EffectiveBhopCap = BhopCap * mult;
        BhopRetention = config.bhopSpeedRetention;
        DiveSpeed = config.diveForwardSpeed;
        WallBounceForce = config.wallBounceForce;
        WallBounce = config.enableWallBounce;
        WallRun = config.enableWallRun;
        Mantle = config.enableMantle;
    }
}
