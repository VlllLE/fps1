using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Pool of recycled effect objects with a hard cap; spent objects are deactivated and reused.</summary>
internal sealed class FxPool<T> where T : Component
{
    private readonly Stack<T> free = new Stack<T>();
    private readonly List<T> active = new List<T>();
    private readonly Func<T> factory;
    private readonly int capacity;

    public FxPool(Func<T> factory, int capacity)
    {
        this.factory = factory;
        this.capacity = capacity;
    }

    public int Created { get; private set; }
    public int ActiveCount => active.Count;
    public int Capacity => capacity;

    /// <summary>Takes an object from the pool. When the cap is reached the oldest active one is recycled (or null if <paramref name="recycleOldest"/> is false).</summary>
    public T Get(bool recycleOldest)
    {
        while (free.Count > 0)
        {
            T pooled = free.Pop();
            if (pooled != null)
            {
                active.Add(pooled);
                return pooled;
            }
        }

        if (Created < capacity)
        {
            T created = factory();
            Created++;
            active.Add(created);
            return created;
        }

        if (!recycleOldest)
            return null;

        for (int i = 0; i < active.Count; i++)
        {
            T oldest = active[i];
            active.RemoveAt(i);
            if (oldest == null)
            {
                i--;
                continue;
            }

            active.Add(oldest);
            return oldest;
        }

        return null;
    }

    public void Release(T item)
    {
        if (item == null)
            return;
        active.Remove(item);
        item.gameObject.SetActive(false);
        free.Push(item);
    }

    public void ReleaseAll()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            T item = active[i];
            if (item != null)
                item.gameObject.SetActive(false);
            free.Push(item);
        }

        active.Clear();
    }
}

/// <summary>A fast bright streak that travels from the muzzle to the impact point (or max range) and fades out.</summary>
[DisallowMultipleComponent]
public sealed class TracerFx : MonoBehaviour
{
    private LineRenderer line;
    private Vector3 start;
    private Vector3 direction;
    private float distance;
    private float length;
    private float speed;
    private float travelled;
    private Color color;

    public float Distance => distance;
    public Vector3 Start => start;
    public Vector3 End => start + direction * distance;

    public static TracerFx Create(Transform parent)
    {
        var go = new GameObject("Tracer");
        go.transform.SetParent(parent, false);
        TracerFx fx = go.AddComponent<TracerFx>();
        fx.line = go.AddComponent<LineRenderer>();
        fx.line.useWorldSpace = true;
        fx.line.positionCount = 2;
        fx.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fx.line.receiveShadows = false;
        fx.line.alignment = LineAlignment.View;
        fx.line.numCapVertices = 2;
        fx.line.sharedMaterial = ShotFxAssets.SoftMaterial;
        go.SetActive(false);
        return fx;
    }

    public void Begin(Vector3 from, Vector3 to, float width, float tracerLength, float tracerSpeed, Color tracerColor)
    {
        start = from;
        Vector3 delta = to - from;
        distance = delta.magnitude;
        direction = distance > 1e-4f ? delta / distance : Vector3.forward;
        length = Mathf.Max(0.2f, tracerLength);
        speed = Mathf.Max(20f, tracerSpeed);
        travelled = 0f;
        color = tracerColor;

        line.startWidth = width * 0.15f;
        line.endWidth = width;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(tracerColor, 0f), new GradientColorKey(Color.Lerp(tracerColor, Color.white, 0.6f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(1f, 1f) });
        line.colorGradient = gradient;
        Place();
    }

    private void Update()
    {
        travelled += speed * Time.deltaTime;
        if (travelled - length >= distance)
        {
            ShotEffects.ReleaseTracer(this);
            return;
        }

        Place();
    }

    private void Place()
    {
        float head = Mathf.Min(travelled, distance);
        float tail = Mathf.Clamp(travelled - length, 0f, distance);
        line.SetPosition(0, start + direction * tail);
        line.SetPosition(1, start + direction * head);
    }
}

/// <summary>Short muzzle flash: a billboarded glow, two crossed forward spikes and an optional point light.</summary>
[DisallowMultipleComponent]
public sealed class MuzzleFlashFx : MonoBehaviour
{
    private MeshRenderer glow;
    private MeshRenderer spikeA;
    private MeshRenderer spikeB;
    private Light flashLight;
    private MaterialPropertyBlock block;
    private Transform follow;
    private Vector3 worldPosition;
    private Quaternion worldRotation;
    private float scale;
    private float lightIntensity;
    private float life;
    private float age;
    private float roll;

    public bool HasLight => flashLight != null && flashLight.enabled;
    public float Scale => scale;

    public static MuzzleFlashFx Create(Transform parent)
    {
        var go = new GameObject("MuzzleFlash");
        go.transform.SetParent(parent, false);
        MuzzleFlashFx fx = go.AddComponent<MuzzleFlashFx>();
        fx.block = new MaterialPropertyBlock();
        fx.glow = MakeQuad(go.transform, "Glow", ShotFxAssets.QuadMesh(), ShotFxAssets.StarMaterial);
        fx.spikeA = MakeQuad(go.transform, "SpikeA", ShotFxAssets.StreakMesh(), ShotFxAssets.StreakMaterial);
        fx.spikeB = MakeQuad(go.transform, "SpikeB", ShotFxAssets.StreakMesh(), ShotFxAssets.StreakMaterial);

        var lightObject = new GameObject("Light");
        lightObject.transform.SetParent(go.transform, false);
        fx.flashLight = lightObject.AddComponent<Light>();
        fx.flashLight.type = LightType.Point;
        fx.flashLight.color = new Color(1f, 0.72f, 0.38f);
        fx.flashLight.range = 5.5f;
        fx.flashLight.shadows = LightShadows.None;
        fx.flashLight.enabled = false;
        go.SetActive(false);
        return fx;
    }

    private static MeshRenderer MakeQuad(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    public void Begin(Transform followTarget, Vector3 position, Quaternion rotation, float flashScale, float intensity, int layer, bool withLight)
    {
        follow = followTarget;
        worldPosition = position;
        worldRotation = rotation;
        scale = flashScale;
        lightIntensity = intensity;
        life = UnityEngine.Random.Range(0.05f, 0.075f);
        age = 0f;
        roll = UnityEngine.Random.Range(0f, 360f);
        SetLayerRecursive(transform, layer);
        flashLight.enabled = withLight && intensity > 0f;
        flashLight.intensity = intensity;
        Apply();
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= life)
        {
            ShotEffects.ReleaseFlash(this);
            return;
        }

        Apply();
    }

    private void Apply()
    {
        if (follow != null)
        {
            worldPosition = follow.position;
            worldRotation = follow.rotation;
        }

        float t = Mathf.Clamp01(age / life);
        float fade = 1f - t * t;
        float pop = Mathf.Lerp(0.7f, 1.15f, Mathf.Clamp01(t * 3f));
        transform.SetPositionAndRotation(worldPosition, worldRotation);

        Camera cam = Camera.main;
        glow.transform.rotation = cam != null
            ? cam.transform.rotation * Quaternion.Euler(0f, 0f, roll)
            : worldRotation * Quaternion.Euler(0f, 0f, roll);
        glow.transform.localScale = Vector3.one * (0.34f * scale * pop);
        glow.transform.position = worldPosition + worldRotation * Vector3.forward * (0.02f * scale);

        float spikeLength = 0.5f * scale * pop;
        float spikeWidth = 0.16f * scale;
        spikeA.transform.localRotation = Quaternion.identity;
        spikeA.transform.localPosition = Vector3.forward * 0.01f;
        spikeA.transform.localScale = new Vector3(spikeWidth, 1f, spikeLength);
        spikeB.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        spikeB.transform.localPosition = Vector3.forward * 0.01f;
        spikeB.transform.localScale = new Vector3(spikeWidth, 1f, spikeLength);

        Tint(glow, fade);
        Tint(spikeA, fade);
        Tint(spikeB, fade);
        if (flashLight.enabled)
            flashLight.intensity = lightIntensity * fade;
    }

    private void Tint(MeshRenderer renderer, float alpha)
    {
        renderer.GetPropertyBlock(block);
        block.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
        renderer.SetPropertyBlock(block);
    }

    private static void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursive(root.GetChild(i), layer);
    }
}

/// <summary>
/// Ejected cartridge case: simple ballistic motion with a couple of bounces on world geometry, a rest period, then it
/// shrinks away. Moves by hand (no Rigidbody) so it never pushes the player and costs almost nothing.
/// </summary>
[DisallowMultipleComponent]
public sealed class ShellCasingFx : MonoBehaviour
{
    private const float Gravity = 9.81f;
    private const float RestSeconds = 2.2f;
    private const float FadeSeconds = 0.7f;
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[6];

    private Vector3 velocity;
    private Vector3 spin;
    private Vector3 baseScale;
    private float radius;
    private float age;
    private float restedFor = -1f;
    private int bounces;
    private float spawnDelay;
    private MeshRenderer meshRenderer;
    private Color tint;
    private MaterialPropertyBlock block;

    public bool AtRest => restedFor >= 0f;
    public Vector3 Velocity => velocity;
    public float Age => age;

    public static ShellCasingFx Create(Transform parent)
    {
        var go = new GameObject("ShellCasing");
        go.transform.SetParent(parent, false);
        ShellCasingFx fx = go.AddComponent<ShellCasingFx>();
        go.AddComponent<MeshFilter>().sharedMesh = ShotFxAssets.CasingMesh();
        fx.meshRenderer = go.AddComponent<MeshRenderer>();
        fx.meshRenderer.sharedMaterial = ShotFxAssets.CasingMaterial;
        fx.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fx.meshRenderer.receiveShadows = false;
        fx.block = new MaterialPropertyBlock();
        go.SetActive(false);
        return fx;
    }

    public void Begin(Vector3 position, Quaternion rotation, Vector3 initialVelocity, float scale, Color color, int layer, float delay)
    {
        transform.SetPositionAndRotation(position, rotation);
        velocity = initialVelocity;
        spin = new Vector3(UnityEngine.Random.Range(-900f, 900f), UnityEngine.Random.Range(-300f, 300f), UnityEngine.Random.Range(-900f, 900f));
        baseScale = new Vector3(0.0125f, 0.0125f, 0.045f) * scale;
        radius = baseScale.x * 0.5f;
        transform.localScale = baseScale;
        age = 0f;
        restedFor = -1f;
        bounces = 0;
        spawnDelay = Mathf.Max(0f, delay);
        tint = color;
        gameObject.layer = layer;
        meshRenderer.enabled = spawnDelay <= 0f;
        meshRenderer.GetPropertyBlock(block);
        block.SetColor("_Color", color);
        block.SetColor("_BaseColor", color);
        meshRenderer.SetPropertyBlock(block);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (spawnDelay > 0f)
        {
            spawnDelay -= dt;
            if (spawnDelay > 0f)
                return;
            meshRenderer.enabled = true;
        }

        age += dt;
        if (restedFor >= 0f)
        {
            restedFor += dt;
            if (restedFor > RestSeconds)
            {
                float k = 1f - Mathf.Clamp01((restedFor - RestSeconds) / FadeSeconds);
                transform.localScale = baseScale * k;
                if (k <= 0f)
                    ShotEffects.ReleaseCasing(this);
            }

            return;
        }

        if (age > 6f)
        {
            ShotEffects.ReleaseCasing(this);
            return;
        }

        velocity += Vector3.down * (Gravity * dt);
        Vector3 step = velocity * dt;
        float stepLength = step.magnitude;
        if (stepLength > 1e-5f && TryFindGround(transform.position, step / stepLength, stepLength + radius, out RaycastHit hit))
        {
            transform.position = hit.point + hit.normal * radius;
            velocity = Vector3.Reflect(velocity, hit.normal) * 0.32f;
            spin *= 0.5f;
            bounces++;
            if (velocity.magnitude < 0.7f || bounces >= 3)
            {
                velocity = Vector3.zero;
                spin = Vector3.zero;
                restedFor = 0f;
                Vector3 forward = Vector3.ProjectOnPlane(transform.forward, hit.normal);
                if (forward.sqrMagnitude < 1e-4f)
                    forward = Vector3.ProjectOnPlane(Vector3.forward, hit.normal);
                transform.rotation = Quaternion.LookRotation(forward.normalized, hit.normal);
            }
        }
        else
        {
            transform.position += step;
        }

        transform.Rotate(spin * dt, Space.Self);
    }

    private static bool TryFindGround(Vector3 origin, Vector3 direction, float distance, out RaycastHit best)
    {
        best = default;
        int count = Physics.RaycastNonAlloc(origin, direction, HitBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            Collider collider = HitBuffer[i].collider;
            if (collider == null || HitBuffer[i].distance <= 0f || !ShotEffects.IsWorldCollider(collider))
                continue;
            if (HitBuffer[i].distance < bestDistance)
            {
                bestDistance = HitBuffer[i].distance;
                best = HitBuffer[i];
                found = true;
            }
        }

        return found;
    }
}

/// <summary>One pooled burst of particles for a bullet impact: sparks and dust on the world, a soft pink puff on targets.</summary>
[DisallowMultipleComponent]
public sealed class ImpactFx : MonoBehaviour
{
    private ParticleSystem sparks;
    private ParticleSystem dust;
    private ParticleSystem puff;
    private float age;

    public int LastEmitted { get; private set; }

    public static ImpactFx Create(Transform parent)
    {
        var go = new GameObject("Impact");
        go.transform.SetParent(parent, false);
        ImpactFx fx = go.AddComponent<ImpactFx>();
        fx.sparks = MakeSystem(go.transform, "Sparks", 0.35f, 0.14f, 0.012f, true);
        fx.dust = MakeSystem(go.transform, "Dust", 0.9f, 0.55f, 0.1f, false);
        fx.puff = MakeSystem(go.transform, "Puff", 0.5f, 0.4f, 0.07f, false);
        go.SetActive(false);
        return fx;
    }

    private static ParticleSystem MakeSystem(Transform parent, string name, float lifetime, float speed, float size, bool stretch)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed * 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.gravityModifier = stretch ? 1.4f : 0.05f;
        main.maxParticles = 40;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = stretch ? 38f : 28f;
        shape.radius = 0.01f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(stretch ? 1f : 0.7f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        if (!stretch)
        {
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2.2f));
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = ShotFxAssets.SoftMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (stretch)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 2.2f;
        }

        return system;
    }

    public void BeginWorld(Vector3 point, Vector3 normal, Color dustColor, bool withSparks)
    {
        Orient(point, normal);
        age = 0f;
        int dustCount = UnityEngine.Random.Range(5, 9);
        int sparkCount = withSparks ? UnityEngine.Random.Range(4, 8) : 0;
        Emit(dust, dustCount, dustColor);
        Emit(sparks, sparkCount, new Color(1f, 0.82f, 0.4f, 1f));
        LastEmitted = dustCount + sparkCount;
    }

    public void BeginTarget(Vector3 point, Vector3 normal)
    {
        Orient(point, normal);
        age = 0f;
        int count = UnityEngine.Random.Range(6, 10);
        Emit(puff, count, new Color(0.78f, 0.2f, 0.2f, 1f));
        LastEmitted = count;
    }

    private void Orient(Vector3 point, Vector3 normal)
    {
        Quaternion rotation = normal.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(normal) : Quaternion.identity;
        transform.SetPositionAndRotation(point + normal * 0.01f, rotation);
    }

    private static void Emit(ParticleSystem system, int count, Color color)
    {
        if (count <= 0)
            return;
        system.Clear();
        ParticleSystem.MainModule main = system.main;
        main.startColor = color;
        system.Emit(count);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age > 1.2f)
            ShotEffects.ReleaseImpact(this);
    }
}

/// <summary>A bullet hole on world geometry: a small dark quad that stays a while and then fades.</summary>
[DisallowMultipleComponent]
public sealed class BulletDecalFx : MonoBehaviour
{
    private const float Lifetime = 14f;
    private const float FadeSeconds = 3f;

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock block;
    private float age;

    public static BulletDecalFx Create(Transform parent)
    {
        var go = new GameObject("BulletDecal");
        go.transform.SetParent(parent, false);
        BulletDecalFx fx = go.AddComponent<BulletDecalFx>();
        go.AddComponent<MeshFilter>().sharedMesh = ShotFxAssets.QuadMesh();
        fx.meshRenderer = go.AddComponent<MeshRenderer>();
        fx.meshRenderer.sharedMaterial = ShotFxAssets.HoleMaterial;
        fx.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fx.meshRenderer.receiveShadows = false;
        fx.block = new MaterialPropertyBlock();
        go.SetActive(false);
        return fx;
    }

    public void Begin(Vector3 point, Vector3 normal, float size)
    {
        Quaternion facing = Quaternion.LookRotation(-normal, Vector3.up);
        if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.98f)
            facing = Quaternion.LookRotation(-normal, Vector3.forward);
        transform.SetPositionAndRotation(point + normal * 0.012f, facing * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)));
        transform.localScale = Vector3.one * size;
        age = 0f;
        SetAlpha(1f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Lifetime)
        {
            ShotEffects.ReleaseDecal(this);
            return;
        }

        if (age > Lifetime - FadeSeconds)
            SetAlpha(Mathf.Clamp01((Lifetime - age) / FadeSeconds));
    }

    private void SetAlpha(float alpha)
    {
        meshRenderer.GetPropertyBlock(block);
        block.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
        meshRenderer.SetPropertyBlock(block);
    }
}
