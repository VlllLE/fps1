using UnityEngine;

/// <summary>
/// Persistence backend for <see cref="LoadoutSelection"/>. The selection is stored as one JSON string.
/// </summary>
public interface ILoadoutStore
{
    bool TryRead(out string json);
    void Write(string json);
    void Clear();
}

/// <summary>
/// Stores the selection in <see cref="PlayerPrefs"/> so it survives scene loads and game restarts.
/// </summary>
public sealed class PlayerPrefsLoadoutStore : ILoadoutStore
{
    public const string DefaultKey = "FPS.Loadout.v1";

    private readonly string key;

    public PlayerPrefsLoadoutStore(string key = DefaultKey)
    {
        this.key = string.IsNullOrEmpty(key) ? DefaultKey : key;
    }

    public string Key => key;

    public bool TryRead(out string json)
    {
        json = null;
        if (!PlayerPrefs.HasKey(key))
            return false;

        json = PlayerPrefs.GetString(key, string.Empty);
        return !string.IsNullOrEmpty(json);
    }

    public void Write(string json)
    {
        PlayerPrefs.SetString(key, json ?? string.Empty);
        PlayerPrefs.Save();
    }

    public void Clear()
    {
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
}

/// <summary>
/// In-memory store for tests and tools.
/// </summary>
public sealed class MemoryLoadoutStore : ILoadoutStore
{
    private string json;

    public bool TryRead(out string value)
    {
        value = json;
        return !string.IsNullOrEmpty(json);
    }

    public void Write(string value)
    {
        json = value;
    }

    public void Clear()
    {
        json = null;
    }
}
