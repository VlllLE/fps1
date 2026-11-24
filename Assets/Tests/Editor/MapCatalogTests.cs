using NUnit.Framework;
using UnityEngine;

public class MapCatalogTests
{
    private static readonly string[] BuildPaths =
    {
        "Assets/Scenes/TestRange.unity",
        "Assets/Scenes/Dome.unity",
    };

    [Test]
    public void Catalog_ListsTestRangeShipmentDome_WithDistinctHotkeys()
    {
        Assert.AreEqual(3, MapCatalog.Count);
        Assert.AreEqual("TestRange", MapCatalog.Maps[0].SceneName);
        Assert.AreEqual("Shipment", MapCatalog.Maps[1].SceneName);
        Assert.AreEqual("Dome", MapCatalog.Maps[2].SceneName);
        Assert.AreEqual(KeyCode.F5, MapCatalog.Maps[0].HotKey);
        Assert.AreEqual(KeyCode.F6, MapCatalog.Maps[1].HotKey);
        Assert.AreEqual(KeyCode.F7, MapCatalog.Maps[2].HotKey);
    }

    [Test]
    public void Catalog_ScenePathsMatchSceneNames()
    {
        foreach (var map in MapCatalog.Maps)
            Assert.AreEqual($"Assets/Scenes/{map.SceneName}.unity", map.ScenePath);
    }

    [Test]
    public void IndexOfScene_FindsKnownMapsCaseInsensitive_AndRejectsUnknown()
    {
        Assert.AreEqual(0, MapCatalog.IndexOfScene("TestRange"));
        Assert.AreEqual(2, MapCatalog.IndexOfScene("dome"));
        Assert.AreEqual(-1, MapCatalog.IndexOfScene("Nope"));
        Assert.AreEqual(-1, MapCatalog.IndexOfScene(null));
        Assert.AreEqual(-1, MapCatalog.IndexOfScene(""));
    }

    [Test]
    public void IsInBuild_MatchesByFileName()
    {
        Assert.IsTrue(MapCatalog.IsInBuild("TestRange", BuildPaths));
        Assert.IsTrue(MapCatalog.IsInBuild("Dome", BuildPaths));
        Assert.IsFalse(MapCatalog.IsInBuild("Shipment", BuildPaths));
        Assert.IsFalse(MapCatalog.IsInBuild("Shipment", null));
        Assert.IsFalse(MapCatalog.IsInBuild(null, BuildPaths));
        Assert.IsFalse(MapCatalog.IsInBuild("Range", BuildPaths));
    }

    [Test]
    public void Cycle_WrapsForwardAndBackward()
    {
        Assert.AreEqual(1, MapCatalog.Cycle(0, 1, 3, null));
        Assert.AreEqual(0, MapCatalog.Cycle(2, 1, 3, null));
        Assert.AreEqual(2, MapCatalog.Cycle(0, -1, 3, null));
        Assert.AreEqual(1, MapCatalog.Cycle(2, -1, 3, null));
    }

    [Test]
    public void Cycle_SkipsUnavailableMaps()
    {
        System.Func<int, bool> noShipment = i => i != 1;
        Assert.AreEqual(2, MapCatalog.Cycle(0, 1, 3, noShipment));
        Assert.AreEqual(0, MapCatalog.Cycle(2, 1, 3, noShipment));
        Assert.AreEqual(2, MapCatalog.Cycle(0, -1, 3, noShipment));
    }

    [Test]
    public void Cycle_ReturnsMinusOne_WhenNothingElseAvailable()
    {
        System.Func<int, bool> onlyCurrent = i => i == 0;
        Assert.AreEqual(-1, MapCatalog.Cycle(0, 1, 3, onlyCurrent));
        Assert.AreEqual(-1, MapCatalog.Cycle(0, 0, 3, null));
        Assert.AreEqual(-1, MapCatalog.Cycle(0, 1, 0, null));
    }

    [Test]
    public void Cycle_FromUnknownCurrent_StartsAtEnds()
    {
        Assert.AreEqual(0, MapCatalog.Cycle(-1, 1, 3, null));
        Assert.AreEqual(2, MapCatalog.Cycle(-1, -1, 3, null));
    }
}
