using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public readonly struct MapEntry
{
    public readonly string DisplayName;
    public readonly string SceneName;
    public readonly string ScenePath;
    public readonly KeyCode HotKey;

    public MapEntry(string displayName, string sceneName, string scenePath, KeyCode hotKey)
    {
        DisplayName = displayName;
        SceneName = sceneName;
        ScenePath = scenePath;
        HotKey = hotKey;
    }
}

/// <summary>Known playable maps plus pure helpers for Build Settings lookups and cycling.</summary>
public static class MapCatalog
{
    public static readonly MapEntry[] Maps =
    {
        new MapEntry("TestRange", "TestRange", "Assets/Scenes/TestRange.unity", KeyCode.F5),
        new MapEntry("Shipment", "Shipment", "Assets/Scenes/Shipment.unity", KeyCode.F6),
        new MapEntry("Dome", "Dome", "Assets/Scenes/Dome.unity", KeyCode.F7),
    };

    public static int Count => Maps.Length;

    public static int IndexOfScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return -1;
        for (int i = 0; i < Maps.Length; i++)
        {
            if (string.Equals(Maps[i].SceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary>True when <paramref name="sceneName"/> matches the file name of any path in <paramref name="buildScenePaths"/>.</summary>
    public static bool IsInBuild(string sceneName, IEnumerable<string> buildScenePaths)
    {
        if (string.IsNullOrEmpty(sceneName) || buildScenePaths == null)
            return false;
        foreach (string path in buildScenePaths)
        {
            if (string.IsNullOrEmpty(path))
                continue;
            if (string.Equals(Path.GetFileNameWithoutExtension(path), sceneName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static IEnumerable<string> GetBuildScenePaths()
    {
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
            yield return SceneUtility.GetScenePathByBuildIndex(i);
    }

    public static bool IsInBuild(string sceneName)
    {
        return IsInBuild(sceneName, GetBuildScenePaths());
    }

    /// <summary>
    /// Steps <paramref name="delta"/> (+1 / -1) through the catalog with wrap-around, skipping entries
    /// for which <paramref name="isAvailable"/> is false. Returns -1 when nothing else is available.
    /// </summary>
    public static int Cycle(int current, int delta, int count, Func<int, bool> isAvailable)
    {
        if (count <= 0 || delta == 0)
            return -1;
        int step = delta > 0 ? 1 : -1;
        int start = current < 0 || current >= count ? (step > 0 ? -1 : count) : current;
        int index = start;
        for (int n = 0; n < count; n++)
        {
            index = ((index + step) % count + count) % count;
            if (index == current)
                continue;
            if (isAvailable == null || isAvailable(index))
                return index;
        }
        return -1;
    }
}
