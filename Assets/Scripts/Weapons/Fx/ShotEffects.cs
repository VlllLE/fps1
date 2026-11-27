using UnityEngine;

/// <summary>
/// Pooled, purely visual bullet feedback shared by the player's first-person weapon and by every third-person gun
/// (bots and the local player's own body): tracer streaks, impact bursts and bullet holes, muzzle flashes with a
/// light and ejected casings. Nothing here owns colliders, rigidbodies, damage or hit decisions; callers hand over
/// the finished <see cref="HitResult"/> so hit detection and hit markers behave exactly as before.
/// </summary>
public static class ShotEffects
{
    /// <summary>Tracers never travel further than this (a long range sniper shot still shows a streak that ends in a moment).</summary>
    public const float MaxTracerDistance = 140f;

    public const int TracerCapacity = 48;
    public const int FlashCapacity = 10;
    public const int CasingCapacity = 48;
    public const int ImpactCapacity = 28;
    public const int DecalCapacity = 64;

    private const string RootName = "ShotEffects";

    private static GameObject root;
    private static FxPool<TracerFx> tracers;
    private static FxPool<MuzzleFlashFx> flashes;
    private static FxPool<ShellCasingFx> casings;
    private static FxPool<ImpactFx> impacts;
    private static FxPool<BulletDecalFx> decals;

    /// <summary>Master switch (tests and menus may turn the effects off).</summary>
    public static bool Enabled { get; set; } = true;

    public static int TracerObjectsCreated => tracers != null ? tracers.Created : 0;
    public static int ActiveTracers => tracers != null ? tracers.ActiveCount : 0;
    public static int FlashObjectsCreated => flashes != null ? flashes.Created : 0;
    public static int ActiveFlashes => flashes != null ? flashes.ActiveCount : 0;
    public static int CasingObjectsCreated => casings != null ? casings.Created : 0;
    public static int ActiveCasings => casings != null ? casings.ActiveCount : 0;
    public static int ImpactObjectsCreated => impacts != null ? impacts.Created : 0;
    public static int ActiveImpacts => impacts != null ? impacts.ActiveCount : 0;
    public static int DecalObjectsCreated => decals != null ? decals.Created : 0;
    public static int ActiveDecals => decals != null ? decals.ActiveCount : 0;

    /// <summary>Total tracers requested since the last <see cref="ResetStats"/> (including ones recycled).</summary>
    public static int TracersPlayed { get; private set; }
    public static int FlashesPlayed { get; private set; }
    public static int CasingsPlayed { get; private set; }
    public static int ImpactsPlayed { get; private set; }
    public static int DecalsPlayed { get; private set; }

    public static void ResetStats()
    {
        TracersPlayed = 0;
        FlashesPlayed = 0;
        CasingsPlayed = 0;
        ImpactsPlayed = 0;
        DecalsPlayed = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnDomainReloadDisabled()
    {
        root = null;
        tracers = null;
        flashes = null;
        casings = null;
        impacts = null;
        decals = null;
        Enabled = true;
        ResetStats();
    }

    /// <summary>Destroys the pooled objects (they are rebuilt on demand). Used by tests and scene teardown.</summary>
    public static void Clear()
    {
        if (root != null)
        {
            if (Application.isPlaying)
                Object.Destroy(root);
            else
                Object.DestroyImmediate(root);
        }

        root = null;
        tracers = null;
        flashes = null;
        casings = null;
        impacts = null;
        decals = null;
        ResetStats();
    }

    /// <summary>Everything a hitscan shot should show: the tracer from the muzzle plus impacts along the path.</summary>
    public static void PlayHitscan(Vector3 muzzle, HitResult result, WeaponConfig config)
    {
        if (!Enabled || result == null || !Application.isPlaying)
            return;

        bool pellet = config != null && config.pelletCount > 1;
        PlayTracer(muzzle, ShotEndPoint(result, config), config, pellet);
        PlayImpacts(result);
    }

    /// <summary>Where the visible streak ends: the last thing the shot hit, or the maximum range.</summary>
    public static Vector3 ShotEndPoint(HitResult result, WeaponConfig config)
    {
        if (result.Impacts.Count > 0)
            return result.Impacts[result.Impacts.Count - 1].RaycastHit.point;

        float range = config != null ? config.range : MaxTracerDistance;
        return result.Origin + result.Direction * Mathf.Min(range, MaxTracerDistance);
    }

    public static TracerFx PlayTracer(Vector3 from, Vector3 to, WeaponConfig config, bool pellet = false)
    {
        if (!Enabled || !Application.isPlaying)
            return null;

        EnsureRoot();
        TracersPlayed++;
        TracerFx tracer = tracers.Get(true);
        if (tracer == null)
            return null;

        WeaponFxProfile profile = WeaponFxProfile.For(config);
        float width = pellet ? Mathf.Min(profile.tracerWidth, 0.02f) : profile.tracerWidth;
        tracer.gameObject.SetActive(true);
        tracer.Begin(from, to, width, profile.tracerLength, profile.tracerSpeed, profile.tracerColor);
        return tracer;
    }

    public static void PlayImpacts(HitResult result)
    {
        if (!Enabled || result == null || !Application.isPlaying)
            return;

        for (int i = 0; i < result.Impacts.Count; i++)
            PlayImpact(result.Impacts[i]);
    }

    public static void PlayImpact(HitImpact impact)
    {
        RaycastHit hit = impact.RaycastHit;
        if (hit.collider == null)
            return;

        PlayImpact(hit.point, hit.normal, hit.collider);
    }

    public static void PlayImpact(Vector3 point, Vector3 normal, Collider collider)
    {
        if (!Enabled || !Application.isPlaying)
            return;

        EnsureRoot();
        ImpactsPlayed++;
        bool onTarget = IsTarget(collider);
        ImpactFx fx = impacts.Get(true);
        if (fx != null)
        {
            fx.gameObject.SetActive(true);
            if (onTarget)
                fx.BeginTarget(point, normal);
            else
                fx.BeginWorld(point, normal, SurfaceDustColor(collider), !(collider is TerrainCollider));
        }

        if (!onTarget && IsWorldCollider(collider))
        {
            BulletDecalFx decal = decals.Get(true);
            if (decal != null)
            {
                DecalsPlayed++;
                decal.gameObject.SetActive(true);
                decal.Begin(point, normal, Random.Range(0.07f, 0.1f));
            }
        }
    }

    /// <summary>A muzzle flash that follows <paramref name="muzzle"/> for its short life. The flash lives on the muzzle's layer, so a flash on the local body's hidden gun stays out of the first-person camera.</summary>
    public static MuzzleFlashFx PlayMuzzleFlash(Transform muzzle, WeaponConfig config, bool withLight)
    {
        if (!Enabled || !Application.isPlaying || muzzle == null)
            return null;

        WeaponFxProfile profile = WeaponFxProfile.For(config);
        if (profile.flashScale <= 0f)
            return null;

        EnsureRoot();
        FlashesPlayed++;
        MuzzleFlashFx flash = flashes.Get(true);
        if (flash == null)
            return null;

        flash.gameObject.SetActive(true);
        flash.Begin(muzzle, muzzle.position, muzzle.rotation, profile.flashScale, profile.flashLightIntensity, muzzle.gameObject.layer, withLight);
        return flash;
    }

    /// <summary>Ejects a casing from <paramref name="ejectPoint"/> (its +X side, a little up and back), after the profile's delay.</summary>
    public static ShellCasingFx PlayCasing(Transform ejectPoint, WeaponConfig config)
    {
        if (!Enabled || !Application.isPlaying || ejectPoint == null)
            return null;

        WeaponFxProfile profile = WeaponFxProfile.For(config);
        if (!profile.ejectsCasings)
            return null;

        EnsureRoot();
        CasingsPlayed++;
        ShellCasingFx casing = casings.Get(true);
        if (casing == null)
            return null;

        Vector3 velocity = ejectPoint.right * Random.Range(2.2f, 3.4f)
            + ejectPoint.up * Random.Range(1.4f, 2.4f)
            - ejectPoint.forward * Random.Range(0.1f, 0.6f);
        casing.gameObject.SetActive(true);
        casing.Begin(ejectPoint.position, ejectPoint.rotation, velocity, profile.casingScale, profile.casingColor, ejectPoint.gameObject.layer, profile.casingDelay);
        return casing;
    }

    /// <summary>True for static scenery a bullet hole or a bouncing casing can sit on (not players, bots, targets or moving bodies).</summary>
    public static bool IsWorldCollider(Collider collider)
    {
        if (collider == null || collider.isTrigger)
            return false;
        if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic)
            return false;
        if (collider.GetComponentInParent<CharacterController>() != null)
            return false;
        if (IsTarget(collider))
            return false;
        return collider.gameObject.layer != LayerMask.NameToLayer("PlayerLocalHide");
    }

    /// <summary>True when the collider belongs to something that takes damage (a hit zone, bot, player or practice target).</summary>
    public static bool IsTarget(Collider collider)
    {
        if (collider == null)
            return false;
        if (collider.GetComponent<HitZone>() != null)
            return true;
        return collider.GetComponentInParent<IHitDamageReceiver>() != null;
    }

    private static Color SurfaceDustColor(Collider collider)
    {
        Color grey = new Color(0.66f, 0.64f, 0.6f, 1f);
        var renderer = collider != null ? collider.GetComponent<Renderer>() : null;
        Material material = renderer != null ? renderer.sharedMaterial : null;
        if (material != null && material.HasProperty("_Color"))
        {
            Color c = material.color;
            grey = Color.Lerp(grey, new Color(Mathf.Clamp01(c.r + 0.15f), Mathf.Clamp01(c.g + 0.15f), Mathf.Clamp01(c.b + 0.15f), 1f), 0.45f);
        }

        return grey;
    }

    private static void EnsureRoot()
    {
        if (root != null && tracers != null)
            return;

        root = new GameObject(RootName);
        Transform parent = root.transform;
        tracers = new FxPool<TracerFx>(() => TracerFx.Create(parent), TracerCapacity);
        flashes = new FxPool<MuzzleFlashFx>(() => MuzzleFlashFx.Create(parent), FlashCapacity);
        casings = new FxPool<ShellCasingFx>(() => ShellCasingFx.Create(parent), CasingCapacity);
        impacts = new FxPool<ImpactFx>(() => ImpactFx.Create(parent), ImpactCapacity);
        decals = new FxPool<BulletDecalFx>(() => BulletDecalFx.Create(parent), DecalCapacity);
    }

    internal static void ReleaseTracer(TracerFx fx) => tracers?.Release(fx);
    internal static void ReleaseFlash(MuzzleFlashFx fx) => flashes?.Release(fx);
    internal static void ReleaseCasing(ShellCasingFx fx) => casings?.Release(fx);
    internal static void ReleaseImpact(ImpactFx fx) => impacts?.Release(fx);
    internal static void ReleaseDecal(BulletDecalFx fx) => decals?.Release(fx);
}
