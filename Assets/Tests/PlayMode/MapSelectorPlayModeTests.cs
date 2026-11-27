using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class MapSelectorPlayModeTests
{
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        SceneManager.LoadScene("TestRange", LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.2f);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
    }

    [UnityTest]
    public IEnumerator Selector_AutoCreated_AndActiveInTestRange()
    {
        Assert.IsNotNull(MapSelector.Instance, "Bootstrap should create the MapSelector without scene edits");
        Assert.AreEqual(0, MapSelector.CurrentMapIndex);
        Assert.IsTrue(MapSelector.IsActiveInCurrentScene);
        yield return null;
    }

    [UnityTest]
    public IEnumerator UnknownScene_IsRejected_WithoutLoading()
    {
        var selector = MapSelector.EnsureInstance();
        LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"\[MapSelector\].*not in Build Settings"));
        Assert.IsFalse(selector.TryLoadMap("DefinitelyNotAScene"));
        yield return null;
        Assert.AreEqual("TestRange", SceneManager.GetActiveScene().name);
        Assert.IsFalse(selector.IsLoading);
        Assert.IsNotNull(selector.StatusMessage);
    }

    [UnityTest]
    public IEnumerator MapNotInBuildSettings_IsGuarded()
    {
        var selector = MapSelector.EnsureInstance();
        for (int i = 0; i < MapCatalog.Count; i++)
        {
            if (selector.IsMapAvailable(i))
                continue;
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"\[MapSelector\]"));
            Assert.IsFalse(selector.TryLoadMap(i));
        }
        yield return null;
        Assert.AreEqual("TestRange", SceneManager.GetActiveScene().name);
    }

    [UnityTest]
    public IEnumerator LoadingAnotherAvailableMap_RestoresTimeScale_AndKeepsSelectorAlive()
    {
        var selector = MapSelector.EnsureInstance();
        int target = -1;
        for (int i = 0; i < MapCatalog.Count; i++)
        {
            if (i != MapSelector.CurrentMapIndex && selector.IsMapAvailable(i))
            {
                target = i;
                break;
            }
        }
        if (target < 0)
            Assert.Ignore("Only TestRange is in Build Settings; generate Shipment/Dome/Triad via Tools > Maps.");

        Time.timeScale = 0f;
        Assert.IsTrue(selector.TryLoadMap(target));
        float deadline = Time.realtimeSinceStartup + 20f;
        while (selector.IsLoading && Time.realtimeSinceStartup < deadline)
            yield return null;

        Assert.IsFalse(selector.IsLoading, "Map load should finish");
        Assert.AreEqual(MapCatalog.Maps[target].SceneName, SceneManager.GetActiveScene().name);
        Assert.AreEqual(1f, Time.timeScale);
        Assert.AreSame(selector, MapSelector.Instance);
    }

    [UnityTest]
    public IEnumerator LoadingSameMap_IsNoOp()
    {
        var selector = MapSelector.EnsureInstance();
        LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"\[MapSelector\].*Already in"));
        Assert.IsFalse(selector.TryLoadMap("TestRange"));
        yield return null;
        Assert.IsFalse(selector.IsLoading);
    }
}
