using UnityEngine;

/// <summary>
/// What the local player sees and does when <see cref="PlayerHealth"/> reports death: input and movement are
/// frozen (momentum slides out), the third-person body plays a procedural death matching the killing hit, the gun
/// drops and a <see cref="DeathCamera"/> shows the body. Everything is undone on respawn. Added at runtime next to
/// <see cref="PlayerHealth"/> by <see cref="BotManager"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDeathPresentation : MonoBehaviour
{
    private const float Gravity = 22f;

    private PlayerHealth health;
    private HumanoidBody body;
    private CharacterController controller;
    private MovementController movement;
    private FirstPersonCamera lookCamera;
    private PlayerWeaponBridge weaponBridge;
    private DeathCamera deathCamera;
    private bool presenting;
    private bool restoreMovement;
    private bool restoreLook;
    private bool restoreBridge;
    private bool bodyEndPending;
    private Vector3 lastSource;
    private float lastDamage;
    private Vector3 slide;
    private float verticalSpeed;

    public bool IsPresenting => presenting;

    private void OnEnable()
    {
        health = GetComponent<PlayerHealth>();
        if (health == null)
            return;
        health.OnDamaged += HandleDamaged;
        health.OnDied += HandleDied;
        health.OnRespawned += HandleRespawned;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
            health.OnRespawned -= HandleRespawned;
        }

        if (presenting)
            Finish(true);
    }

    private void Update()
    {
        if (!presenting || controller == null || !controller.enabled)
            return;

        float dt = Time.deltaTime;
        verticalSpeed = controller.isGrounded ? -1f : verticalSpeed - Gravity * dt;
        slide = Vector3.MoveTowards(slide, Vector3.zero, 9f * dt);
        controller.Move((slide + Vector3.up * verticalSpeed) * dt);
    }

    private void HandleDamaged(float amount, Vector3 source, bool fatal)
    {
        if (!fatal)
            return;
        lastDamage = amount;
        lastSource = source;
    }

    private void HandleDied()
    {
        Begin();
    }

    private void HandleRespawned()
    {
        Finish();
        if (bodyEndPending && body != null)
            body.EndDeath();
        bodyEndPending = false;
    }

    private void Begin()
    {
        if (presenting)
            return;

        body = GetComponent<HumanoidBody>();
        controller = GetComponent<CharacterController>();
        movement = GetComponent<MovementController>();
        lookCamera = GetComponentInChildren<FirstPersonCamera>();
        weaponBridge = GetComponent<PlayerWeaponBridge>();
        presenting = true;

        Vector3 velocity = movement != null ? movement.Velocity : Vector3.zero;
        slide = new Vector3(velocity.x, 0f, velocity.z) * 0.9f;
        verticalSpeed = Mathf.Min(0f, velocity.y);

        if (movement != null && movement.enabled)
        {
            movement.enabled = false;
            restoreMovement = true;
        }

        if (lookCamera != null && lookCamera.enabled)
        {
            lookCamera.enabled = false;
            restoreLook = true;
        }

        if (weaponBridge != null && weaponBridge.enabled)
        {
            weaponBridge.enabled = false;
            restoreBridge = true;
        }

        Vector3 fallWorld = -transform.forward;
        if (body != null && body.IsBuilt)
        {
            Vector3 local = Vector3.zero;
            Vector3 flat = lastSource - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
                local = transform.InverseTransformDirection(-flat.normalized);

            var context = new DeathContext
            {
                localHitDirection = local,
                headshot = false,
                damage = lastDamage,
                forwardSpeed = transform.InverseTransformDirection(velocity).z,
                crouched = body.Animator != null && body.Animator.CrouchWeight > 0.5f,
                roll = Random.value
            };
            body.BeginDeath(context, Mathf.Clamp(0.7f + lastDamage / 80f, 0.7f, 1.6f));
            fallWorld = transform.TransformDirection(body.DeathAnimator.FallDirection);
        }

        Camera cam = lookCamera != null ? lookCamera.GetComponentInChildren<Camera>() : GetComponentInChildren<Camera>();
        if (cam == null && lookCamera != null)
            cam = lookCamera.GetComponent<Camera>();
        if (cam != null)
        {
            if (deathCamera == null)
                deathCamera = gameObject.AddComponent<DeathCamera>();
            Transform focus = body != null ? body.GetBone(HumanoidBone.Hips) : null;
            deathCamera.Begin(cam, transform, focus, fallWorld, LayerMask.NameToLayer("PlayerLocalHide"));
        }
    }

    private void Finish(bool deactivating = false)
    {
        if (!presenting)
            return;
        presenting = false;

        if (deathCamera != null)
            deathCamera.End();
        if (body != null)
        {
            if (deactivating)
                bodyEndPending = true;
            else
                body.EndDeath();
        }

        if (restoreMovement && movement != null)
            movement.enabled = true;
        if (restoreLook && lookCamera != null)
            lookCamera.enabled = true;
        if (restoreBridge && weaponBridge != null)
            weaponBridge.enabled = true;
        restoreMovement = restoreLook = restoreBridge = false;
    }
}
