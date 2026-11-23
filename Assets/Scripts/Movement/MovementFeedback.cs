using UnityEngine;

/// <summary>
/// Juice layer for <see cref="MovementController"/>: particles, camera shake/tilt, and SFX.
/// Does not alter movement physics — only reacts to controller events/state.
/// </summary>
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(MovementController))]
public class MovementFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MovementController movement;
    [Tooltip("Usually MainCamera under CameraHolder. Shake/tilt applied here so FirstPersonCamera pitch is undisturbed.")]
    [SerializeField] private Transform cameraEffectsTarget;
    [SerializeField] private AudioSource audioSource;

    [Header("Particles")]
    [SerializeField] private bool createRuntimeParticles = true;
    [SerializeField] private ParticleSystem slideDust;
    [SerializeField] private ParticleSystem jumpBurst;
    [SerializeField] private ParticleSystem wallBounceSparks;
    [SerializeField] private ParticleSystem landPuff;

    [Header("Audio Clips (procedural placeholders OK)")]
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip landClip;
    [SerializeField] private AudioClip slideStartClip;
    [SerializeField] private AudioClip slideLoopClip;
    [SerializeField] private AudioClip wallBounceClip;
    [SerializeField] private AudioClip diveClip;
    [SerializeField] private AudioClip slideCancelClip;
    [SerializeField] private AudioClip bunnyHopClip;

    [Header("Audio Volumes")]
    [SerializeField] [Range(0f, 1f)] private float oneShotVolume = 0.55f;
    [SerializeField] [Range(0f, 1f)] private float slideLoopVolume = 0.35f;

    [Header("Camera Shake")]
    [SerializeField] private float jumpShake = 0.12f;
    [SerializeField] private float landShake = 0.18f;
    [SerializeField] private float slideStartShake = 0.1f;
    [SerializeField] private float wallBounceShake = 0.35f;
    [SerializeField] private float diveShake = 0.22f;
    [SerializeField] private float shakeDecay = 1.8f;
    [SerializeField] private float maxShakeOffset = 0.08f;
    [SerializeField] private float maxShakeAngle = 2.5f;

    [Header("Camera Tilt")]
    [SerializeField] private float slideRollDegrees = 4f;
    [SerializeField] private float wallRunRollDegrees = 8f;
    [SerializeField] private float sprintStrafeRollDegrees = 2f;
    [SerializeField] private float tiltLerpSpeed = 10f;
    [SerializeField] private float wallBounceKickDegrees = 6f;

    private float trauma;
    private float kickPitch;
    private float kickRoll;
    private float targetRoll;
    private float currentRoll;
    private Vector3 baseLocalPos;
    private Quaternion baseLocalRot;
    private bool slideLoopPlaying;

    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<MovementController>();

        if (cameraEffectsTarget == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null)
                cameraEffectsTarget = cam.transform;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.loop = false;

        if (createRuntimeParticles)
            EnsureParticles();

        // Effects are applied as offsets from the camera's rest local pose (identity rotation).
        if (cameraEffectsTarget != null)
        {
            baseLocalPos = cameraEffectsTarget.localPosition;
            baseLocalRot = Quaternion.identity;
        }
    }

    private void OnEnable()
    {
        if (movement == null)
            return;

        movement.Jumped += OnJumped;
        movement.Landed += OnLanded;
        movement.SlideStarted += OnSlideStarted;
        movement.WallBounced += OnWallBounced;
        movement.Dived += OnDived;
        movement.BunnyHopped += OnBunnyHopped;
        movement.SlideCancelled += OnSlideCancelled;
        movement.StateChanged += OnStateChanged;
    }

    private void OnDisable()
    {
        if (movement == null)
            return;

        movement.Jumped -= OnJumped;
        movement.Landed -= OnLanded;
        movement.SlideStarted -= OnSlideStarted;
        movement.WallBounced -= OnWallBounced;
        movement.Dived -= OnDived;
        movement.BunnyHopped -= OnBunnyHopped;
        movement.SlideCancelled -= OnSlideCancelled;
        movement.StateChanged -= OnStateChanged;
        StopSlideLoop();
    }

    private void LateUpdate()
    {
        if (movement == null)
            return;

        UpdateSlideEmission();
        UpdateTargetTilt();
        ApplyCameraEffects();
    }

    private void OnJumped()
    {
        PlayOneShot(jumpClip, 0.95f, 1.05f);
        AddTrauma(jumpShake);
        kickPitch = -1.2f;
        Emit(jumpBurst, 12);
    }

    private void OnLanded()
    {
        float impact = Mathf.Clamp01(Mathf.Abs(movement.Velocity.y) / 12f);
        PlayOneShot(landClip, 0.9f, 1.05f);
        AddTrauma(landShake * (0.4f + impact));
        kickPitch = 1.5f + impact;
        Emit(landPuff, 8 + (int)(impact * 10f));
    }

    private void OnSlideStarted()
    {
        PlayOneShot(slideStartClip, 0.9f, 1.1f);
        StartSlideLoop();
        AddTrauma(slideStartShake);
        Emit(slideDust, 20);
        if (slideDust != null && !slideDust.isPlaying)
            slideDust.Play();
    }

    private void OnWallBounced()
    {
        PlayOneShot(wallBounceClip, 0.85f, 1.15f);
        AddTrauma(wallBounceShake);
        kickRoll = movement.LastWallNormal.x >= 0f ? -wallBounceKickDegrees : wallBounceKickDegrees;
        kickPitch = -2f;

        if (wallBounceSparks != null)
        {
            var t = wallBounceSparks.transform;
            t.position = transform.position + Vector3.up * 0.9f - movement.LastWallNormal * 0.2f;
            t.rotation = Quaternion.LookRotation(movement.LastWallNormal);
            wallBounceSparks.Emit(24);
        }
    }

    private void OnDived()
    {
        PlayOneShot(diveClip, 0.9f, 1.1f);
        AddTrauma(diveShake);
        kickPitch = 2f;
    }

    private void OnBunnyHopped()
    {
        PlayOneShot(bunnyHopClip != null ? bunnyHopClip : jumpClip, 1.05f, 1.2f);
        AddTrauma(jumpShake * 0.7f);
        Emit(jumpBurst, 8);
    }

    private void OnSlideCancelled()
    {
        StopSlideLoop();
        PlayOneShot(slideCancelClip != null ? slideCancelClip : jumpClip, 1f, 1.15f);
        AddTrauma(slideStartShake + jumpShake);
        kickPitch = -2f;
    }

    private void OnStateChanged(
        MovementController.MovementState from,
        MovementController.MovementState to)
    {
        if (from == MovementController.MovementState.Sliding &&
            to != MovementController.MovementState.Sliding)
        {
            StopSlideLoop();
            if (slideDust != null)
                slideDust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void UpdateSlideEmission()
    {
        if (slideDust == null || movement == null)
            return;

        bool sliding = movement.CurrentState == MovementController.MovementState.Sliding;
        var emission = slideDust.emission;
        emission.enabled = sliding;
        if (sliding)
        {
            slideDust.transform.position = transform.position + Vector3.up * 0.15f;
            var vel = movement.Velocity;
            vel.y = 0f;
            if (vel.sqrMagnitude > 0.01f)
                slideDust.transform.rotation = Quaternion.LookRotation(-vel.normalized, Vector3.up);
        }
    }

    private void UpdateTargetTilt()
    {
        targetRoll = 0f;
        var state = movement.CurrentState;

        if (state == MovementController.MovementState.Sliding)
        {
            // Slight bank opposite to horizontal velocity for speed feel
            Vector3 lateral = transform.InverseTransformDirection(movement.Velocity);
            targetRoll = Mathf.Clamp(-lateral.x * 0.35f, -1f, 1f) * slideRollDegrees;
        }
        else if (state == MovementController.MovementState.WallRunning)
        {
            targetRoll = -movement.WallRunSide * wallRunRollDegrees;
        }
        else if (state == MovementController.MovementState.Sprinting)
        {
            Vector3 localVel = transform.InverseTransformDirection(movement.Velocity);
            targetRoll = Mathf.Clamp(-localVel.x / Mathf.Max(movement.Config != null ? movement.Config.walkSpeed : 6f, 0.1f), -1f, 1f)
                * sprintStrafeRollDegrees;
        }

        kickPitch = Mathf.MoveTowards(kickPitch, 0f, Time.deltaTime * 12f);
        kickRoll = Mathf.MoveTowards(kickRoll, 0f, Time.deltaTime * 14f);
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, 1f - Mathf.Exp(-tiltLerpSpeed * Time.deltaTime));
    }

    private void ApplyCameraEffects()
    {
        if (cameraEffectsTarget == null)
            return;

        trauma = Mathf.Clamp01(trauma - shakeDecay * Time.deltaTime * Mathf.Max(trauma, 0.05f));
        float shake = trauma * trauma;

        Vector3 posOffset = Vector3.zero;
        Vector3 rotOffset = Vector3.zero;
        if (shake > 0.001f)
        {
            float t = Time.time * 40f;
            posOffset = new Vector3(
                (Mathf.PerlinNoise(t, 0.3f) * 2f - 1f) * maxShakeOffset * shake,
                (Mathf.PerlinNoise(0.7f, t) * 2f - 1f) * maxShakeOffset * shake,
                0f);
            rotOffset = new Vector3(
                (Mathf.PerlinNoise(t, 1.1f) * 2f - 1f) * maxShakeAngle * shake,
                (Mathf.PerlinNoise(1.7f, t) * 2f - 1f) * maxShakeAngle * shake * 0.5f,
                (Mathf.PerlinNoise(t, 2.3f) * 2f - 1f) * maxShakeAngle * shake);
        }

        rotOffset.x += kickPitch;
        rotOffset.z += currentRoll + kickRoll;

        cameraEffectsTarget.localPosition = baseLocalPos + posOffset;
        cameraEffectsTarget.localRotation = baseLocalRot * Quaternion.Euler(rotOffset);
    }

    private void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    private void PlayOneShot(AudioClip clip, float pitchMin, float pitchMax)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.pitch = Random.Range(pitchMin, pitchMax);
        audioSource.PlayOneShot(clip, oneShotVolume);
    }

    private void StartSlideLoop()
    {
        if (slideLoopClip == null || audioSource == null)
            return;

        // Dedicated loop via second Play — keep simple: restart source in loop mode briefly
        // Prefer one-shots if loop clip missing; if present, use Play with loop while sliding.
        audioSource.clip = slideLoopClip;
        audioSource.loop = true;
        audioSource.volume = slideLoopVolume;
        audioSource.pitch = 1f;
        audioSource.Play();
        slideLoopPlaying = true;
    }

    private void StopSlideLoop()
    {
        if (!slideLoopPlaying || audioSource == null)
            return;

        if (audioSource.clip == slideLoopClip)
            audioSource.Stop();
        audioSource.loop = false;
        audioSource.clip = null;
        audioSource.volume = 1f;
        slideLoopPlaying = false;
    }

    private static void Emit(ParticleSystem system, int count)
    {
        if (system == null)
            return;
        system.Emit(count);
    }

    private void EnsureParticles()
    {
        if (slideDust == null)
            slideDust = CreateDustSystem("VFX_SlideDust", new Color(0.55f, 0.5f, 0.42f, 0.7f), 40f, 0.6f, true);
        if (jumpBurst == null)
            jumpBurst = CreateBurstSystem("VFX_JumpBurst", new Color(0.85f, 0.9f, 1f, 0.8f), 0.35f);
        if (landPuff == null)
            landPuff = CreateBurstSystem("VFX_LandPuff", new Color(0.6f, 0.55f, 0.45f, 0.75f), 0.4f);
        if (wallBounceSparks == null)
            wallBounceSparks = CreateSparkSystem("VFX_WallBounceSparks", new Color(1f, 0.85f, 0.4f, 1f));
    }

    private ParticleSystem CreateDustSystem(string name, Color color, float rate, float lifetime, bool looping)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = looping;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.4f;
        main.maxParticles = 80;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.rateOverTime = rate;
        emission.enabled = false;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.25f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        ApplyDefaultParticleMaterial(renderer);

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    private ParticleSystem CreateBurstSystem(string name, Color color, float lifetime)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.2f;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.15f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        ApplyDefaultParticleMaterial(renderer);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    private ParticleSystem CreateSparkSystem(string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.startLifetime = 0.25f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.5f;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;

        var trails = ps.trails;
        trails.enabled = true;
        trails.lifetime = 0.15f;
        trails.ratio = 0.6f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2f;
        ApplyDefaultParticleMaterial(renderer);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    private static void ApplyDefaultParticleMaterial(ParticleSystemRenderer renderer)
    {
        var shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader != null)
            renderer.sharedMaterial = new Material(shader);
    }
}
