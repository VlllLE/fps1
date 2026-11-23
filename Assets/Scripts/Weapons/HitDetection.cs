#if false // Superseded prototype retained below only to avoid destructive cross-worker edits.
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles raycast hit detection with damage zones, material effects, and penetration.
/// </summary>
public class HitDetection : MonoBehaviour
{
    [Header("Hit Zones")]
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private LayerMask penetrableLayers;
    
    [Header("Hit Effects")]
    [SerializeField] private HitEffectMapping[] hitEffects;
    [SerializeField] private GameObject defaultHitEffect;
    [SerializeField] private GameObject bloodHitEffect;
    [SerializeField] private float hitEffectLifetime = 2f;
    
    [Header("Decals")]
    [SerializeField] private bool spawnDecals = true;
    [SerializeField] private GameObject defaultDecalPrefab;
    [SerializeField] private DecalMapping[] decalMappings;
    [SerializeField] private float decalOffset = 0.01f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugRays = false;
    [SerializeField] private float debugRayDuration = 1f;
    
    private Dictionary<string, GameObject> hitEffectCache;
    private Dictionary<string, GameObject> decalCache;
    
    public static HitDetection Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeCaches();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeCaches()
    {
        hitEffectCache = new Dictionary<string, GameObject>();
        decalCache = new Dictionary<string, GameObject>();
        
        foreach (var mapping in hitEffects)
        {
            if (mapping.material != null && mapping.effect != null)
            {
                hitEffectCache[mapping.material.name] = mapping.effect;
            }
        }
        
        foreach (var mapping in decalMappings)
        {
            if (mapping.material != null && mapping.decal != null)
            {
                decalCache[mapping.material.name] = mapping.decal;
            }
        }
    }
    
    public HitResult ProcessHit(Vector3 origin, Vector3 direction, float range, WeaponConfig config, int ownerID = -1)
    {
        HitResult result = new HitResult();
        
        if (showDebugRays)
        {
            Debug.DrawRay(origin, direction * range, Color.yellow, debugRayDuration);
        }
        
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, range, hitLayers);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        
        float remainingDamage = config.damage;
        float traveledDistance = 0f;
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject.GetInstanceID() == ownerID) continue;
            
            traveledDistance = hit.distance;
            float distanceMultiplier = config.damageFalloff.Evaluate(traveledDistance / range);
            
            HitZone hitZone = hit.collider.GetComponent<HitZone>();
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            
            float finalDamage = remainingDamage * distanceMultiplier;
            HitZoneType zoneType = HitZoneType.Body;
            
            if (hitZone != null)
            {
                zoneType = hitZone.ZoneType;
                finalDamage *= GetZoneMultiplier(zoneType, config);
            }
            
            SingleHitInfo hitInfo = new SingleHitInfo
            {
                point = hit.point,
                normal = hit.normal,
                distance = hit.distance,
                collider = hit.collider,
                damage = finalDamage,
                zoneType = zoneType,
                isPenetration = result.hits.Count > 0
            };
            
            result.hits.Add(hitInfo);
            
            if (damageable != null)
            {
                DamageInfo damageInfo = new DamageInfo
                {
                    damage = finalDamage,
                    hitPoint = hit.point,
                    hitNormal = hit.normal,
                    hitDirection = direction,
                    attackerID = ownerID,
                    isHeadshot = zoneType == HitZoneType.Head,
                    weaponConfig = config
                };
                
                damageable.TakeDamage(damageInfo);
                result.totalDamageDealt += finalDamage;
                
                SpawnHitEffect(hit.point, hit.normal, true);
            }
            else
            {
                SpawnHitEffect(hit.point, hit.normal, hit.collider);
                SpawnDecal(hit.point, hit.normal, hit.collider);
            }
            
            if (config.canPenetrate && CanPenetrate(hit.collider, config))
            {
                remainingDamage *= config.penetrationDamageRetention;
                
                if (remainingDamage < 1f) break;
                
                continue;
            }
            
            break;
        }
        
        result.hitAnything = result.hits.Count > 0;
        
        return result;
    }
    
    public HitResult ProcessHitscan(Vector3 origin, Vector3 direction, float range, WeaponConfig config, float spreadAngle, int ownerID = -1)
    {
        if (spreadAngle > 0f)
        {
            direction = ApplySpread(direction, spreadAngle);
        }
        
        return ProcessHit(origin, direction, range, config, ownerID);
    }
    
    public List<HitResult> ProcessShotgunBlast(Vector3 origin, Vector3 direction, float range, WeaponConfig config, int pelletCount, float spreadAngle, int ownerID = -1)
    {
        List<HitResult> results = new List<HitResult>();
        
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 spreadDirection = ApplySpread(direction, spreadAngle);
            HitResult result = ProcessHit(origin, spreadDirection, range, config, ownerID);
            results.Add(result);
        }
        
        return results;
    }
    
    private Vector3 ApplySpread(Vector3 direction, float spreadAngle)
    {
        float spreadRad = spreadAngle * Mathf.Deg2Rad;
        
        float randomAngle = Random.Range(0f, 2f * Mathf.PI);
        float randomRadius = Random.Range(0f, Mathf.Tan(spreadRad));
        
        Vector3 spreadOffset = new Vector3(
            Mathf.Cos(randomAngle) * randomRadius,
            Mathf.Sin(randomAngle) * randomRadius,
            1f
        );
        
        Quaternion rotation = Quaternion.LookRotation(direction);
        return rotation * spreadOffset.normalized;
    }
    
    private float GetZoneMultiplier(HitZoneType zone, WeaponConfig config)
    {
        switch (zone)
        {
            case HitZoneType.Head:
                return config.headshotMultiplier;
            case HitZoneType.Body:
                return 1f;
            case HitZoneType.Limb:
                return config.limbMultiplier;
            default:
                return 1f;
        }
    }
    
    private bool CanPenetrate(Collider collider, WeaponConfig config)
    {
        if (!config.canPenetrate) return false;
        
        int layer = collider.gameObject.layer;
        return (penetrableLayers.value & (1 << layer)) != 0;
    }
    
    private void SpawnHitEffect(Vector3 position, Vector3 normal, bool isFlesh)
    {
        GameObject effectPrefab = isFlesh ? bloodHitEffect : defaultHitEffect;
        
        if (effectPrefab != null)
        {
            GameObject effect = Instantiate(effectPrefab, position, Quaternion.LookRotation(normal));
            Destroy(effect, hitEffectLifetime);
        }
    }
    
    private void SpawnHitEffect(Vector3 position, Vector3 normal, Collider hitCollider)
    {
        GameObject effectPrefab = GetHitEffectForCollider(hitCollider);
        
        if (effectPrefab != null)
        {
            GameObject effect = Instantiate(effectPrefab, position, Quaternion.LookRotation(normal));
            Destroy(effect, hitEffectLifetime);
        }
    }
    
    private GameObject GetHitEffectForCollider(Collider collider)
    {
        Renderer renderer = collider.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            string materialName = renderer.sharedMaterial.name;
            if (hitEffectCache.TryGetValue(materialName, out GameObject effect))
            {
                return effect;
            }
        }
        
        SurfaceType surfaceType = collider.GetComponent<SurfaceType>();
        if (surfaceType != null && surfaceType.hitEffect != null)
        {
            return surfaceType.hitEffect;
        }
        
        return defaultHitEffect;
    }
    
    private void SpawnDecal(Vector3 position, Vector3 normal, Collider hitCollider)
    {
        if (!spawnDecals) return;
        
        GameObject decalPrefab = GetDecalForCollider(hitCollider);
        
        if (decalPrefab != null)
        {
            Vector3 decalPosition = position + normal * decalOffset;
            Quaternion decalRotation = Quaternion.LookRotation(-normal);
            
            decalRotation *= Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            
            GameObject decal = Instantiate(decalPrefab, decalPosition, decalRotation);
            decal.transform.SetParent(hitCollider.transform);
        }
    }
    
    private GameObject GetDecalForCollider(Collider collider)
    {
        Renderer renderer = collider.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            string materialName = renderer.sharedMaterial.name;
            if (decalCache.TryGetValue(materialName, out GameObject decal))
            {
                return decal;
            }
        }
        
        SurfaceType surfaceType = collider.GetComponent<SurfaceType>();
        if (surfaceType != null && surfaceType.decalPrefab != null)
        {
            return surfaceType.decalPrefab;
        }
        
        return defaultDecalPrefab;
    }
}

[System.Serializable]
public class HitEffectMapping
{
    public Material material;
    public GameObject effect;
}

[System.Serializable]
public class DecalMapping
{
    public Material material;
    public GameObject decal;
}

public class HitResult
{
    public bool hitAnything;
    public List<SingleHitInfo> hits = new List<SingleHitInfo>();
    public float totalDamageDealt;
}

public class SingleHitInfo
{
    public Vector3 point;
    public Vector3 normal;
    public float distance;
    public Collider collider;
    public float damage;
    public HitZoneType zoneType;
    public bool isPenetration;
}

public enum HitZoneType
{
    Head,
    Body,
    Limb
}

public struct DamageInfo
{
    public float damage;
    public Vector3 hitPoint;
    public Vector3 hitNormal;
    public Vector3 hitDirection;
    public int attackerID;
    public bool isHeadshot;
    public WeaponConfig weaponConfig;
}

public interface IDamageable
{
    void TakeDamage(DamageInfo damageInfo);
    float GetHealth();
    bool IsDead();
}

public class HitZone : MonoBehaviour
{
    [SerializeField] private HitZoneType zoneType = HitZoneType.Body;
    public HitZoneType ZoneType => zoneType;
}

public class SurfaceType : MonoBehaviour
{
    public string surfaceName = "Default";
    public GameObject hitEffect;
    public GameObject decalPrefab;
    public AudioClip impactSound;
}
#endif

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stateless hitscan resolver. It does not fire weapons, spend ammunition, create
/// network messages, or decide who owns a shot. The authoritative caller supplies
/// an immutable request and may gate damage through <see cref="AuthorityPolicy"/>.
/// </summary>
public sealed class HitDetection : MonoBehaviour
{
    [Header("Defaults")]
    [SerializeField] private LayerMask defaultHitLayers = ~0;
    [SerializeField] private LayerMask penetrableLayers;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Debug")]
    [SerializeField] private bool drawDebugHits;
    [SerializeField, Min(0f)] private float debugDuration = 1f;
    [SerializeField] private Color missColor = Color.yellow;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private Color penetrationColor = Color.cyan;

    /// <summary>
    /// Optional server/host policy. When omitted, Request.IsAuthoritative is used.
    /// This is a local policy hook, never a networking implementation.
    /// </summary>
    public IHitAuthorityPolicy AuthorityPolicy { get; set; }

    public event Action<HitImpact> ImpactResolved;

    public HitResult Resolve(HitRequest request)
    {
        ValidateRequest(request);

        Vector3 direction = request.Direction.normalized;
        int layers = request.HitLayers == 0 ? defaultHitLayers.value : request.HitLayers;
        QueryTriggerInteraction triggers = request.OverrideTriggerInteraction
            ? request.TriggerInteraction
            : triggerInteraction;

        RaycastHit[] rayHits = Physics.RaycastAll(
            request.Origin,
            direction,
            request.MaxDistance,
            layers,
            triggers);
        Array.Sort(rayHits, CompareHitDistance);

        var result = new HitResult(request.ShotId, request.Origin, direction);
        var damagedReceivers = new HashSet<int>();
        float remainingDamage = request.BaseDamage;
        int penetrations = 0;

        for (int i = 0; i < rayHits.Length; i++)
        {
            RaycastHit rayHit = rayHits[i];
            if (ShouldIgnore(rayHit.collider, request.IgnoreRoot))
                continue;

            HitZone zone = rayHit.collider.GetComponent<HitZone>();
            HitZoneType zoneType = zone != null ? zone.ZoneType : HitZoneType.Body;
            float zoneMultiplier = zone != null && zone.MultiplierOverride > 0f
                ? zone.MultiplierOverride
                : request.GetZoneMultiplier(zoneType);
            float distanceMultiplier = request.DamageFalloff != null
                ? Mathf.Max(0f, request.DamageFalloff.Evaluate(rayHit.distance / request.MaxDistance))
                : 1f;
            float damage = Mathf.Max(0f, remainingDamage * zoneMultiplier * distanceMultiplier);

            IHitDamageReceiver receiver = FindInterfaceInParents<IHitDamageReceiver>(rayHit.collider.transform);
            bool damageApplied = false;
            if (receiver != null && damagedReceivers.Add(GetStableReceiverId(receiver)))
            {
                var damageInfo = new HitDamageInfo(
                    request.ShotId,
                    request.SourceId,
                    damage,
                    rayHit.point,
                    rayHit.normal,
                    direction,
                    zoneType,
                    rayHit.collider);

                if (CanApplyDamage(request, damageInfo))
                {
                    receiver.ApplyHitDamage(damageInfo);
                    damageApplied = true;
                    result.TotalDamageApplied += damage;
                }
            }

            Material material = GetSharedMaterial(rayHit.collider);
            var impact = new HitImpact(
                request.ShotId,
                rayHit,
                material,
                zoneType,
                damage,
                damageApplied,
                penetrations > 0);
            result.Impacts.Add(impact);

            if (request.InvokeSurfaceResponses)
            {
                IHitSurfaceResponse surface = FindInterfaceInParents<IHitSurfaceResponse>(rayHit.collider.transform);
                if (surface != null)
                    surface.OnHitSurface(impact);
            }

            ImpactResolved?.Invoke(impact);

            bool canContinue = request.AllowPenetration
                && penetrations < request.MaxPenetrations
                && IsLayerInMask(rayHit.collider.gameObject.layer, penetrableLayers.value)
                && damage > request.MinimumPenetrationDamage;
            DrawImpact(request, rayHit, direction, canContinue);

            if (!canContinue)
                break;

            penetrations++;
            remainingDamage *= Mathf.Clamp01(request.PenetrationDamageRetention);
        }

        result.DidHit = result.Impacts.Count > 0;
        result.PenetrationCount = penetrations;

        if (drawDebugHits && !result.DidHit)
            Debug.DrawRay(request.Origin, direction * request.MaxDistance, missColor, debugDuration);

        return result;
    }

    private bool CanApplyDamage(HitRequest request, HitDamageInfo damage)
    {
        return AuthorityPolicy != null
            ? AuthorityPolicy.CanApplyDamage(request, damage)
            : request.IsAuthoritative;
    }

    private void DrawImpact(HitRequest request, RaycastHit hit, Vector3 direction, bool penetrates)
    {
        if (!drawDebugHits)
            return;

        Color color = penetrates ? penetrationColor : hitColor;
        Debug.DrawLine(request.Origin, hit.point, color, debugDuration);
        Debug.DrawRay(hit.point, hit.normal * 0.25f, color, debugDuration);
        if (penetrates)
            Debug.DrawRay(hit.point, direction * 0.25f, penetrationColor, debugDuration);
    }

    private static int CompareHitDistance(RaycastHit left, RaycastHit right)
    {
        return left.distance.CompareTo(right.distance);
    }

    private static void ValidateRequest(HitRequest request)
    {
        if (request.Direction.sqrMagnitude < 0.000001f)
            throw new ArgumentException("Hit direction must be non-zero.", nameof(request));
        if (request.MaxDistance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(request), "Max distance must be positive.");
        if (request.BaseDamage < 0f)
            throw new ArgumentOutOfRangeException(nameof(request), "Base damage cannot be negative.");
        if (request.MaxPenetrations < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Max penetrations cannot be negative.");
    }

    private static bool ShouldIgnore(Collider collider, Transform ignoreRoot)
    {
        return ignoreRoot != null
            && (collider.transform == ignoreRoot || collider.transform.IsChildOf(ignoreRoot));
    }

    private static bool IsLayerInMask(int layer, int mask)
    {
        return (mask & (1 << layer)) != 0;
    }

    private static Material GetSharedMaterial(Collider collider)
    {
        Renderer renderer = collider.GetComponent<Renderer>();
        return renderer != null ? renderer.sharedMaterial : null;
    }

    private static int GetStableReceiverId(IHitDamageReceiver receiver)
    {
        UnityEngine.Object unityObject = receiver as UnityEngine.Object;
        return unityObject != null ? unityObject.GetHashCode() : receiver.GetHashCode();
    }

    private static T FindInterfaceInParents<T>(Transform start) where T : class
    {
        for (Transform current = start; current != null; current = current.parent)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                T match = behaviours[i] as T;
                if (match != null)
                    return match;
            }
        }

        return null;
    }
}

[Serializable]
public struct HitRequest
{
    public long ShotId;
    public int SourceId;
    public Vector3 Origin;
    public Vector3 Direction;
    public float MaxDistance;
    public float BaseDamage;
    public int HitLayers;
    public Transform IgnoreRoot;
    public bool IsAuthoritative;
    public bool InvokeSurfaceResponses;
    public float HeadMultiplier;
    public float BodyMultiplier;
    public float LimbMultiplier;
    public AnimationCurve DamageFalloff;
    public bool AllowPenetration;
    public int MaxPenetrations;
    public float PenetrationDamageRetention;
    public float MinimumPenetrationDamage;
    public bool OverrideTriggerInteraction;
    public QueryTriggerInteraction TriggerInteraction;

    public static HitRequest Create(Vector3 origin, Vector3 direction, float distance, float damage)
    {
        return new HitRequest
        {
            Origin = origin,
            Direction = direction,
            MaxDistance = distance,
            BaseDamage = damage,
            HitLayers = ~0,
            IsAuthoritative = true,
            InvokeSurfaceResponses = true,
            HeadMultiplier = 2f,
            BodyMultiplier = 1f,
            LimbMultiplier = 0.75f,
            DamageFalloff = AnimationCurve.Linear(0f, 1f, 1f, 1f),
            PenetrationDamageRetention = 0.5f,
            MinimumPenetrationDamage = 0.01f
        };
    }

    public float GetZoneMultiplier(HitZoneType zone)
    {
        switch (zone)
        {
            case HitZoneType.Head:
                return HeadMultiplier;
            case HitZoneType.Limb:
                return LimbMultiplier;
            default:
                return BodyMultiplier;
        }
    }
}

public sealed class HitResult
{
    public long ShotId { get; }
    public Vector3 Origin { get; }
    public Vector3 Direction { get; }
    public bool DidHit { get; internal set; }
    public float TotalDamageApplied { get; internal set; }
    public int PenetrationCount { get; internal set; }
    public List<HitImpact> Impacts { get; } = new List<HitImpact>();

    public HitResult(long shotId, Vector3 origin, Vector3 direction)
    {
        ShotId = shotId;
        Origin = origin;
        Direction = direction;
    }
}

public struct HitImpact
{
    public readonly long ShotId;
    public readonly RaycastHit RaycastHit;
    public readonly Material Material;
    public readonly HitZoneType Zone;
    public readonly float ComputedDamage;
    public readonly bool DamageApplied;
    public readonly bool IsAfterPenetration;

    public HitImpact(
        long shotId,
        RaycastHit raycastHit,
        Material material,
        HitZoneType zone,
        float computedDamage,
        bool damageApplied,
        bool isAfterPenetration)
    {
        ShotId = shotId;
        RaycastHit = raycastHit;
        Material = material;
        Zone = zone;
        ComputedDamage = computedDamage;
        DamageApplied = damageApplied;
        IsAfterPenetration = isAfterPenetration;
    }
}

public struct HitDamageInfo
{
    public readonly long ShotId;
    public readonly int SourceId;
    public readonly float Amount;
    public readonly Vector3 Point;
    public readonly Vector3 Normal;
    public readonly Vector3 Direction;
    public readonly HitZoneType Zone;
    public readonly Collider Collider;

    public HitDamageInfo(
        long shotId,
        int sourceId,
        float amount,
        Vector3 point,
        Vector3 normal,
        Vector3 direction,
        HitZoneType zone,
        Collider collider)
    {
        ShotId = shotId;
        SourceId = sourceId;
        Amount = amount;
        Point = point;
        Normal = normal;
        Direction = direction;
        Zone = zone;
        Collider = collider;
    }
}

public interface IHitDamageReceiver
{
    void ApplyHitDamage(HitDamageInfo damage);
}

public interface IHitSurfaceResponse
{
    void OnHitSurface(HitImpact impact);
}

public interface IHitAuthorityPolicy
{
    bool CanApplyDamage(HitRequest request, HitDamageInfo damage);
}

public enum HitZoneType
{
    Head,
    Body,
    Limb
}

public sealed class HitZone : MonoBehaviour
{
    [SerializeField] private HitZoneType zoneType = HitZoneType.Body;
    [SerializeField, Min(0f)] private float multiplierOverride;

    public HitZoneType ZoneType => zoneType;
    public float MultiplierOverride => multiplierOverride;

    public void Configure(HitZoneType zone, float overrideMultiplier = 0f)
    {
        zoneType = zone;
        multiplierOverride = Mathf.Max(0f, overrideMultiplier);
    }
}
