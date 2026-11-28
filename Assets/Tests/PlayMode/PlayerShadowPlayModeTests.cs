using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The local player's body is hidden from the first-person camera, but it must still cast a shadow
/// (and so must the gun it holds) in every map, without ever being drawn to the player's camera.
/// </summary>
public class PlayerShadowPlayModeTests
{
    private static readonly string[] AllMaps = { "TestRange", "Shipment", "Dome", "Triad" };

    private GameObject player;
    private Camera playerCamera;
    private int hideLayer;

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
    }

    private IEnumerator Load(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.7f);

        var movement = Object.FindAnyObjectByType<MovementController>();
        Assert.That(movement, Is.Not.Null, sceneName + " has a player");
        player = movement.gameObject;
        playerCamera = player.GetComponentInChildren<Camera>();
        hideLayer = LayerMask.NameToLayer("PlayerLocalHide");
        player.GetComponent<InputHandler>().SetTestInputLocked(true);
        yield return null;
    }

    private static IEnumerable<string> MapsInBuild()
    {
        foreach (string map in AllMaps)
            if (MapCatalog.IsInBuild(map))
                yield return map;
    }

    [UnityTest]
    public IEnumerator EveryMap_HiddenBodyHasShadowsOnlyTwins_NeverVisibleToThePlayerCamera()
    {
        int maps = 0;
        foreach (string map in MapsInBuild())
        {
            yield return Load(map);
            maps++;

            var proxies = player.GetComponent<LocalBodyShadowProxies>();
            Assert.That(proxies, Is.Not.Null, map + ": the local player has shadow proxies");
            proxies.Refresh();
            Assert.That(proxies.ProxyCount, Is.GreaterThan(0), map);

            Assert.That(playerCamera.cullingMask & (1 << hideLayer), Is.EqualTo(0), map + ": the hide layer stays culled");

            int twins = 0;
            int casters = 0;
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                if (LocalBodyShadowProxies.IsShadowProxy(renderer))
                {
                    twins++;
                    Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly));
                    Assert.That(renderer.gameObject.layer, Is.Not.EqualTo(hideLayer), "twins must be on a layer the shadow cull sees");
                    continue;
                }

                if (renderer.gameObject.layer == hideLayer && renderer.shadowCastingMode != ShadowCastingMode.Off
                    && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                    casters++;
            }

            Assert.That(twins, Is.EqualTo(casters), map + ": every shadow-casting hidden renderer has exactly one twin");

            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(false))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer is ParticleSystemRenderer
                    || LocalBodyShadowProxies.IsShadowProxy(renderer) || renderer.transform.IsChildOf(playerCamera.transform))
                    continue;
                Assert.That(playerCamera.cullingMask & (1 << renderer.gameObject.layer), Is.EqualTo(0),
                    map + ": " + renderer.name + " would be drawn to the player's own camera");
            }
        }

        Assert.That(maps, Is.GreaterThan(0));
    }

    [UnityTest]
    public IEnumerator EveryMap_HeldGunHasATwin_AndTheViewmodelNoLongerDoubleShadows()
    {
        foreach (string map in MapsInBuild())
        {
            yield return Load(map);
            player.GetComponent<LocalBodyShadowProxies>().Refresh();

            bool gunTwin = false;
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                if (!LocalBodyShadowProxies.IsShadowProxy(renderer))
                    continue;
                for (Transform t = renderer.transform; t != null; t = t.parent)
                    if (t.name == ThirdPersonGun.RootName)
                        gunTwin = true;
            }
            Assert.That(gunTwin, Is.True, map + ": the body's held gun casts a shadow");

            foreach (WeaponController weapon in player.GetComponentsInChildren<WeaponController>(true))
            foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer)
                    continue;
                Assert.That(renderer.shadowCastingMode, Is.Not.EqualTo(ShadowCastingMode.On),
                    map + ": viewmodel part " + renderer.name + " would shadow the gun a second time");
            }
        }
    }

    [UnityTest]
    public IEnumerator EveryMap_HasASunWithShadows_AndTheQualitySettingsAllowThem()
    {
        Assert.That(QualitySettings.shadows, Is.Not.EqualTo(UnityEngine.ShadowQuality.Disable));
        Assert.That(QualitySettings.shadowDistance, Is.GreaterThanOrEqualTo(30f));

        foreach (string map in MapsInBuild())
        {
            yield return Load(map);
            bool shadowing = false;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.enabled && light.type == LightType.Directional && light.shadows != LightShadows.None && light.intensity > 0f)
                    shadowing = true;
            Assert.That(shadowing, Is.True, map + " has a shadow-casting directional light");
        }
    }

    [UnityTest]
    public IEnumerator EveryMap_PlayerShadowIsActuallyRenderedOnTheGround()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("no GPU");

        foreach (string map in MapsInBuild())
        {
            yield return Load(map);
            var proxies = player.GetComponent<LocalBodyShadowProxies>();
            proxies.Refresh();
            yield return null;

            yield return MoveToSunlitGround();

            var cameraObject = new GameObject("ShadowProbeCamera");
            var probe = cameraObject.AddComponent<Camera>();
            probe.fieldOfView = 40f;
            probe.clearFlags = CameraClearFlags.SolidColor;
            probe.backgroundColor = Color.black;
            probe.cullingMask = ~(1 << hideLayer);
            probe.nearClipPlane = 0.1f;
            probe.farClipPlane = 40f;
            probe.enabled = false;
            Vector3 feet = player.transform.position;
            Vector3 along = Vector3.zero;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional && light.enabled && light.shadows != LightShadows.None)
                {
                    along = Vector3.ProjectOnPlane(light.transform.forward, Vector3.up);
                    break;
                }
            }
            Vector3 dir = along.sqrMagnitude > 0.01f ? along.normalized : player.transform.right;
            Vector3 lookAt = feet + dir * 0.8f;
            cameraObject.transform.position = feet - dir * 2.2f + Vector3.up * 3.2f;
            cameraObject.transform.rotation = Quaternion.LookRotation(lookAt - cameraObject.transform.position, Vector3.up);
            var rt = new RenderTexture(160, 160, 24, RenderTextureFormat.ARGB32);
            probe.targetTexture = rt;

            Renderer[] twins = System.Array.FindAll(player.GetComponentsInChildren<Renderer>(true), LocalBodyShadowProxies.IsShadowProxy);
            Assert.That(twins.Length, Is.GreaterThan(0), map);

            Color[] withShadow = Capture(probe, rt);
            foreach (Renderer twin in twins)
                twin.enabled = false;
            Color[] without = Capture(probe, rt);

            int different = 0;
            float maxDiff = 0f;
            for (int i = 0; i < withShadow.Length; i++)
            {
                maxDiff = Mathf.Max(maxDiff, Mathf.Abs(withShadow[i].grayscale - without[i].grayscale));
                if (Mathf.Abs(withShadow[i].grayscale - without[i].grayscale) > 0.002f)
                    different++;
            }

            RenderTexture.active = null;
            probe.targetTexture = null;
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(cameraObject);

            Assert.That(different, Is.GreaterThan(20), map + ": removing the body's shadow twins must visibly change the ground (" + different + " px, max " + maxDiff + ")");
        }
    }

    internal IEnumerator MoveToSunlitGround()
    {
        Light sun = null;
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional && light.enabled && light.shadows != LightShadows.None)
            {
                sun = light;
                break;
            }
        }
        Assume.That(sun, Is.Not.Null);

        Vector3 toSun = -sun.transform.forward;
        Vector3 along = Vector3.ProjectOnPlane(sun.transform.forward, Vector3.up).normalized;
        for (float radius = 0f; radius <= 60f; radius += 2f)
        {
            for (int step = 0; step < (radius == 0f ? 1 : 16); step++)
            {
                float angle = step * 22.5f * Mathf.Deg2Rad;
                Vector3 probe = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius + Vector3.up * 20f;
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit ground, 40f, ~0, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.9f)
                    continue;
                Vector3 feet = ground.point;
                if (Physics.Raycast(feet + Vector3.up * 1.2f, toSun, 90f, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                if (Physics.Raycast(feet + Vector3.up * 1.2f + Vector3.right * 1.2f, toSun, 90f, ~0, QueryTriggerInteraction.Ignore)
                    || Physics.Raycast(feet + Vector3.up * 1.2f - Vector3.right * 1.2f, toSun, 90f, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                if (Physics.Linecast(feet - along * 2.2f + Vector3.up * 3.2f, feet + Vector3.up, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                var controller = player.GetComponent<CharacterController>();
                controller.enabled = false;
                player.transform.position = feet + Vector3.up * 0.05f;
                controller.enabled = true;
                yield return null;
                yield return null;
                player.GetComponent<LocalBodyShadowProxies>().Refresh();
                yield return null;
                yield break;
            }
        }

        Assert.Inconclusive(SceneManager.GetActiveScene().name + ": no open, sunlit ground found");
    }
    private static Color[] Capture(Camera camera, RenderTexture rt)
    {
        camera.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        Color[] pixels = tex.GetPixels();
        Object.Destroy(tex);
        return pixels;
    }
}
