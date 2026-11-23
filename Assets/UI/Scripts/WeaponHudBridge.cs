using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace CodMovementShooter.UI
{
    /// <summary>
    /// Thin player-prefab bridge. Uses reflection / optional Component fields so the UI
    /// assembly never hard-references Scripts types (WeaponController, HitDetection, etc.).
    ///
    /// Attach next to the local player (or leave empty and let auto-find run).
    /// Documented wiring lives in Assets/UI/README.md.
    /// </summary>
    public sealed class WeaponHudBridge : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] private FpsHudController hud;

        [Header("Optional soft refs (any Component on the player / weapon stack)")]
        [Tooltip("WeaponController, or a parent that has one / WeaponSwitcher.")]
        [SerializeField] private Component weaponSource;
        [Tooltip("HitDetection in the scene (ImpactResolved).")]
        [SerializeField] private Component hitDetectionSource;
        [Tooltip("MovementController or CharacterController for crosshair speed expand.")]
        [SerializeField] private Component movementSource;

        [Header("Behaviour")]
        [SerializeField] private bool autoFindOnEnable = true;
        [Tooltip("Used only when WeaponController hit events are unavailable.")]
        [SerializeField] private bool listenToHitDetectionFallback = true;
        [SerializeField] private float rebindRetrySeconds = 0.5f;
        [SerializeField] private float hitMarkerDedupSeconds = 0.04f;

        [Header("Optional UnityEvents (manual / designer wiring)")]
        public UnityEvent OnWeaponFired;
        public UnityEvent<int, int> OnAmmoChanged;
        public UnityEvent OnHitConfirmed;
        public UnityEvent<float, bool> OnHitConfirmedDetailed;

        private SoftEventSubscription _weaponEvents;
        private SoftEventSubscription _switcherEvents;
        private SoftEventSubscription _hitEvents;
        private object _boundWeapon;
        private object _boundSwitcher;
        private object _boundHitDetection;
        private object _boundMovement;
        private Coroutine _rebindRoutine;
        private PropertyInfo _currentSpeedProperty;
        private PropertyInfo _velocityProperty;
        private CharacterController _characterController;
        private bool _weaponHitEventsBound;
        private float _lastHitMarkerTime;

        public FpsHudController Hud
        {
            get => hud;
            set => hud = value;
        }

        private void OnEnable()
        {
            if (autoFindOnEnable)
                TryBindAll();

            if (_rebindRoutine != null)
                StopCoroutine(_rebindRoutine);
            _rebindRoutine = StartCoroutine(RebindLoop());
        }

        private void OnDisable()
        {
            if (_rebindRoutine != null)
            {
                StopCoroutine(_rebindRoutine);
                _rebindRoutine = null;
            }

            UnbindAll();
        }

        private void Update()
        {
            if (hud == null)
                return;

            float speed = ReadHorizontalSpeed();
            if (speed >= 0f)
                hud.SetMovementSpeed(speed);
        }

        /// <summary>
        /// Manual API if designers prefer UnityEvent / SendMessage style calls.
        /// </summary>
        public void HandleAmmoChanged(int magazine, int reserve)
        {
            OnAmmoChanged?.Invoke(magazine, reserve);
            if (hud != null)
                hud.SetAmmo(magazine, reserve);
        }

        public void HandleWeaponFired()
        {
            OnWeaponFired?.Invoke();
            if (hud != null)
                hud.NotifyFired();
        }

        public void HandleHitConfirmed(float damage = 0f, bool headshot = false)
        {
            if (Time.unscaledTime - _lastHitMarkerTime < hitMarkerDedupSeconds)
                return;

            _lastHitMarkerTime = Time.unscaledTime;
            OnHitConfirmed?.Invoke();
            OnHitConfirmedDetailed?.Invoke(damage, headshot);
            if (hud != null)
            {
                if (damage > 0f)
                    hud.ShowHitMarker(damage, headshot);
                else
                    hud.ShowHitMarker(headshot);
            }
        }

        [ContextMenu("Rebind Soft References")]
        public void TryBindAll()
        {
            if (hud == null)
                hud = GetComponent<FpsHudController>() ?? FindFirstObjectByType<FpsHudController>();

            object weapon = ResolveWeapon();
            object switcher = ResolveByTypeName(weaponSource, "WeaponSwitcher") ?? FindByTypeName("WeaponSwitcher");
            object hitDetection = ResolveByTypeName(hitDetectionSource, "HitDetection")
                                  ?? FindByTypeName("HitDetection");
            ResolveMovement();

            BindWeapon(weapon);
            BindSwitcher(switcher);

            if (listenToHitDetectionFallback && !_weaponHitEventsBound)
                BindHitDetection(hitDetection);
            else
                ClearHitDetectionBinding();
        }

        private IEnumerator RebindLoop()
        {
            var wait = new WaitForSecondsRealtime(Mathf.Max(0.1f, rebindRetrySeconds));
            while (enabled)
            {
                object weapon = ResolveWeapon();
                if (!ReferenceEquals(weapon, _boundWeapon))
                    BindWeapon(weapon);

                yield return wait;
            }
        }

        private void BindWeapon(object weapon)
        {
            if (ReferenceEquals(weapon, _boundWeapon))
                return;

            _weaponEvents?.Dispose();
            _weaponEvents = null;
            _boundWeapon = weapon;
            _weaponHitEventsBound = false;

            if (weapon == null)
                return;

            _weaponEvents = new SoftEventSubscription();
            _weaponEvents.TrySubscribe(weapon, "OnAmmoChanged", args =>
            {
                if (args == null || args.Length < 2)
                    return;
                HandleAmmoChanged(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]));
            });
            _weaponEvents.TrySubscribe(weapon, "OnWeaponFired", _ => HandleWeaponFired());

            bool hitResolved = _weaponEvents.TrySubscribe(weapon, "OnHitResolved", args =>
            {
                if (args == null || args.Length < 1 || args[0] == null)
                    return;
                TryHandleHitResult(args[0]);
            });
            bool shotgunResolved = _weaponEvents.TrySubscribe(weapon, "OnShotgunHitsResolved", args =>
            {
                if (args == null || args.Length < 1 || args[0] == null)
                    return;
                TryHandleShotgunResults(args[0]);
            });
            _weaponHitEventsBound = hitResolved || shotgunResolved;

            TrySeedAmmo(weapon);
            TrySeedWeaponName(weapon);

            if (listenToHitDetectionFallback && !_weaponHitEventsBound)
            {
                object hitDetection = ResolveByTypeName(hitDetectionSource, "HitDetection")
                                      ?? FindByTypeName("HitDetection");
                BindHitDetection(hitDetection);
            }
            else
            {
                ClearHitDetectionBinding();
            }
        }

        private void BindSwitcher(object switcher)
        {
            if (ReferenceEquals(switcher, _boundSwitcher))
                return;

            _switcherEvents?.Dispose();
            _switcherEvents = null;
            _boundSwitcher = switcher;

            if (switcher == null)
                return;

            _switcherEvents = new SoftEventSubscription();
            _switcherEvents.TrySubscribe(switcher, "OnWeaponChanged", args =>
            {
                object next = args != null && args.Length > 0 ? args[0] : null;
                BindWeapon(next);
            });
        }

        private void BindHitDetection(object hitDetection)
        {
            if (ReferenceEquals(hitDetection, _boundHitDetection))
                return;

            ClearHitDetectionBinding();
            _boundHitDetection = hitDetection;

            if (hitDetection == null)
                return;

            _hitEvents = new SoftEventSubscription();
            _hitEvents.TrySubscribe(hitDetection, "ImpactResolved", args =>
            {
                if (args == null || args.Length < 1 || args[0] == null)
                    return;
                TryHandleHitImpact(args[0]);
            });
        }

        private void ClearHitDetectionBinding()
        {
            _hitEvents?.Dispose();
            _hitEvents = null;
            _boundHitDetection = null;
        }

        private void UnbindAll()
        {
            _weaponEvents?.Dispose();
            _switcherEvents?.Dispose();
            _hitEvents?.Dispose();
            _weaponEvents = null;
            _switcherEvents = null;
            _hitEvents = null;
            _boundWeapon = null;
            _boundSwitcher = null;
            _boundHitDetection = null;
            _weaponHitEventsBound = false;
        }

        private void TryHandleShotgunResults(object resultsObject)
        {
            if (!(resultsObject is IEnumerable enumerable))
                return;

            bool any = false;
            float totalDamage = 0f;
            bool headshot = false;
            foreach (object item in enumerable)
            {
                if (item == null)
                    continue;
                if (!TryExtractHit(item, out float damage, out bool wasHeadshot))
                    continue;
                any = true;
                totalDamage += damage;
                headshot |= wasHeadshot;
            }

            if (any)
                HandleHitConfirmed(totalDamage, headshot);
        }

        private void TryHandleHitResult(object hitResult)
        {
            if (!TryExtractHit(hitResult, out float damage, out bool headshot))
                return;

            HandleHitConfirmed(damage, headshot);
        }

        private static bool TryExtractHit(object hitResult, out float damage, out bool headshot)
        {
            damage = 0f;
            headshot = false;
            Type type = hitResult.GetType();
            bool didHit = ReadBool(type, hitResult, "DidHit", false);
            damage = ReadFloat(type, hitResult, "TotalDamageApplied", 0f);

            PropertyInfo impactsProp = type.GetProperty("Impacts", BindingFlags.Instance | BindingFlags.Public);
            object impacts = impactsProp?.GetValue(hitResult);
            if (impacts is IEnumerable list)
            {
                foreach (object impact in list)
                {
                    if (impact == null)
                        continue;
                    if (ReadBool(impact.GetType(), impact, "DamageApplied", false))
                    {
                        didHit = true;
                        float dmg = ReadFloat(impact.GetType(), impact, "ComputedDamage", damage);
                        damage = Mathf.Max(damage, dmg);
                        headshot |= IsHeadshotZone(impact);
                    }
                }
            }

            return didHit || damage > 0f;
        }

        private void TryHandleHitImpact(object impact)
        {
            Type type = impact.GetType();
            bool applied = ReadBool(type, impact, "DamageApplied", false);
            if (!applied)
                return;

            float damage = ReadFloat(type, impact, "ComputedDamage", 0f);
            bool headshot = IsHeadshotZone(impact);
            HandleHitConfirmed(damage, headshot);
        }

        private static bool IsHeadshotZone(object impact)
        {
            object zone = impact.GetType().GetField("Zone", BindingFlags.Instance | BindingFlags.Public)?.GetValue(impact)
                          ?? impact.GetType().GetProperty("Zone", BindingFlags.Instance | BindingFlags.Public)?.GetValue(impact);
            return zone != null && string.Equals(zone.ToString(), "Head", StringComparison.Ordinal);
        }

        private void TrySeedAmmo(object weapon)
        {
            Type type = weapon.GetType();
            PropertyInfo mag = type.GetProperty("CurrentAmmo", BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo reserve = type.GetProperty("ReserveAmmo", BindingFlags.Instance | BindingFlags.Public);
            if (mag == null || reserve == null)
                return;

            try
            {
                HandleAmmoChanged(Convert.ToInt32(mag.GetValue(weapon)), Convert.ToInt32(reserve.GetValue(weapon)));
            }
            catch
            {
                // Ignore seed failures.
            }
        }

        private void TrySeedWeaponName(object weapon)
        {
            if (hud == null || weapon == null)
                return;

            try
            {
                Type type = weapon.GetType();
                PropertyInfo configProp = type.GetProperty("Config", BindingFlags.Instance | BindingFlags.Public);
                object config = configProp?.GetValue(weapon);
                if (config == null)
                    return;

                FieldInfo nameField = config.GetType().GetField("weaponName", BindingFlags.Instance | BindingFlags.Public);
                string weaponName = nameField?.GetValue(config) as string;
                if (!string.IsNullOrEmpty(weaponName))
                    hud.SetWeaponName(weaponName);
            }
            catch
            {
                // Ignore seed failures.
            }
        }

        private object ResolveWeapon()
        {
            object fromField = ResolveByTypeName(weaponSource, "WeaponController");
            if (fromField != null)
                return fromField;

            if (weaponSource != null)
            {
                object nested = FindComponentByTypeName(weaponSource.gameObject, "WeaponController", true);
                if (nested != null)
                    return nested;

                object switcher = FindComponentByTypeName(weaponSource.gameObject, "WeaponSwitcher", true);
                object current = ReadCurrentWeapon(switcher);
                if (current != null)
                    return current;
            }

            object sceneSwitcher = FindByTypeName("WeaponSwitcher");
            object fromSwitcher = ReadCurrentWeapon(sceneSwitcher);
            if (fromSwitcher != null)
                return fromSwitcher;

            return FindByTypeName("WeaponController");
        }

        private void ResolveMovement()
        {
            _currentSpeedProperty = null;
            _velocityProperty = null;
            _characterController = null;
            _boundMovement = null;

            object movement = ResolveByTypeName(movementSource, "MovementController")
                              ?? FindByTypeName("MovementController");
            if (movement != null)
            {
                _boundMovement = movement;
                Type type = movement.GetType();
                _currentSpeedProperty = type.GetProperty("CurrentSpeed", BindingFlags.Instance | BindingFlags.Public);
                _velocityProperty = type.GetProperty("Velocity", BindingFlags.Instance | BindingFlags.Public);
                if (movement is Component c)
                    _characterController = c.GetComponent<CharacterController>();
                return;
            }

            if (movementSource is CharacterController cc)
            {
                _characterController = cc;
                return;
            }

            _characterController = FindFirstObjectByType<CharacterController>();
        }

        private float ReadHorizontalSpeed()
        {
            try
            {
                if (_boundMovement != null && _currentSpeedProperty != null)
                {
                    object value = _currentSpeedProperty.GetValue(_boundMovement);
                    if (value is float f)
                        return f;
                }

                if (_boundMovement != null && _velocityProperty != null)
                {
                    object value = _velocityProperty.GetValue(_boundMovement);
                    if (value is Vector3 v)
                        return new Vector3(v.x, 0f, v.z).magnitude;
                }
            }
            catch
            {
                // Fall through to CharacterController.
            }

            if (_characterController != null)
            {
                Vector3 v = _characterController.velocity;
                return new Vector3(v.x, 0f, v.z).magnitude;
            }

            return -1f;
        }

        private static object ReadCurrentWeapon(object switcher)
        {
            if (switcher == null)
                return null;

            PropertyInfo prop = switcher.GetType().GetProperty("CurrentWeapon", BindingFlags.Instance | BindingFlags.Public);
            return prop?.GetValue(switcher);
        }

        private static object ResolveByTypeName(Component source, string typeName)
        {
            if (source == null)
                return null;
            if (string.Equals(source.GetType().Name, typeName, StringComparison.Ordinal))
                return source;
            return FindComponentByTypeName(source.gameObject, typeName, true);
        }

        private static object FindByTypeName(string typeName)
        {
            Type type = FindType(typeName);
            if (type != null && typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                // Unity 6 non-generic lookup when available.
                MethodInfo typed = typeof(UnityEngine.Object).GetMethod(
                    nameof(FindFirstObjectByType),
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Type) },
                    null);
                if (typed != null)
                    return typed.Invoke(null, new object[] { type });
            }

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && string.Equals(behaviour.GetType().Name, typeName, StringComparison.Ordinal))
                    return behaviour;
            }

            return null;
        }

        private static object FindComponentByTypeName(GameObject go, string typeName, bool includeInactive)
        {
            if (go == null)
                return null;

            Component[] components = go.GetComponentsInChildren<Component>(includeInactive);
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c != null && string.Equals(c.GetType().Name, typeName, StringComparison.Ordinal))
                    return c;
            }

            Transform root = go.transform.root;
            if (root != null && root.gameObject != go)
            {
                components = root.GetComponentsInChildren<Component>(includeInactive);
                for (int i = 0; i < components.Length; i++)
                {
                    Component c = components[i];
                    if (c != null && string.Equals(c.GetType().Name, typeName, StringComparison.Ordinal))
                        return c;
                }
            }

            return null;
        }

        private static Type FindType(string typeName)
        {
            Type direct = Type.GetType(typeName + ", Scripts") ?? Type.GetType(typeName);
            if (direct != null)
                return direct;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(typeName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static bool ReadBool(Type type, object instance, string name, bool fallback)
        {
            PropertyInfo prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (prop != null && prop.PropertyType == typeof(bool))
                return (bool)prop.GetValue(instance);

            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field != null && field.FieldType == typeof(bool))
                return (bool)field.GetValue(instance);

            return fallback;
        }

        private static float ReadFloat(Type type, object instance, string name, float fallback)
        {
            PropertyInfo prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (prop != null)
            {
                object value = prop.GetValue(instance);
                if (value is float f)
                    return f;
            }

            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                object value = field.GetValue(instance);
                if (value is float f)
                    return f;
            }

            return fallback;
        }
    }
}
