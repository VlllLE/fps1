using UnityEngine;

/// <summary>
/// ScriptableObject containing all tunable movement parameters.
/// Create multiple profiles for different character types or game modes.
/// </summary>
[CreateAssetMenu(fileName = "MovementConfig", menuName = "FPS/Movement Config", order = 1)]
public class MovementConfig : ScriptableObject
{
    #region Ground Movement
    
    [Header("Ground Movement")]
    [Tooltip("Base walking speed")]
    public float walkSpeed = 6f;
    
    [Tooltip("Sprint speed multiplier")]
    public float sprintMultiplier = 1.6f;
    
    [Tooltip("How quickly player reaches target speed on ground")]
    public float groundAcceleration = 14f;
    
    [Tooltip("Friction applied when grounded")]
    public float groundFriction = 6f;
    
    [Tooltip("Maximum slope angle player can walk up")]
    public float maxSlopeAngle = 45f;
    
    #endregion

    #region Jumping & Air Movement
    
    [Header("Jumping")]
    [Tooltip("Initial upward velocity when jumping")]
    public float jumpForce = 8f;
    
    [Tooltip("Gravity multiplier (Unity's default is -9.81)")]
    public float gravityMultiplier = 2.5f;
    
    [Tooltip("Brief window after leaving ground where jump is still allowed")]
    public float coyoteTime = 0.12f;
    
    [Tooltip("Buffer window for jump input before landing")]
    public float jumpBufferTime = 0.1f;
    
    [Header("Air Control")]
    [Tooltip("Air acceleration for strafing")]
    public float airAcceleration = 12f;
    
    [Tooltip("Maximum air speed from strafing (source-style)")]
    public float maxAirSpeed = 1.2f;
    
    [Tooltip("Air control amount (0 = no control, 1 = full)")]
    [Range(0f, 1f)]
    public float airControl = 0.3f;
    
    [Tooltip("Air friction (usually 0 for momentum preservation)")]
    public float airFriction = 0f;
    
    #endregion

    #region Sliding
    
    [Header("Sliding")]
    [Tooltip("Initial burst speed when entering slide")]
    public float slideBoostSpeed = 12f;
    
    [Tooltip("Minimum speed required to initiate slide")]
    public float minSlideSpeed = 4f;
    
    [Tooltip("How long slide lasts before auto-ending")]
    public float slideDuration = 0.8f;
    
    [Tooltip("Friction during slide (lower = more slippery)")]
    public float slideFriction = 0.3f;
    
    [Tooltip("Downhill slide acceleration bonus")]
    public float slideDownhillBoost = 8f;
    
    [Tooltip("Height of collider while sliding")]
    public float slideHeight = 0.6f;
    
    [Tooltip("Cooldown before next slide")]
    public float slideCooldown = 0.3f;
    
    [Header("Slide Cancel")]
    [Tooltip("Speed boost when cancelling slide with jump")]
    public float slideCancelBoost = 1.3f;
    
    [Tooltip("Window after slide start where cancel gives max boost")]
    public float slideCancelWindow = 0.3f;
    
    #endregion

    #region Bunny Hopping
    
    [Header("Bunny Hopping")]
    [Tooltip("Window after landing to chain bhop")]
    public float bhopWindow = 0.15f;
    
    [Tooltip("Speed preservation on bhop (1 = full, 0.9 = 10% loss)")]
    [Range(0.5f, 1.1f)]
    public float bhopSpeedRetention = 0.98f;
    
    [Tooltip("Maximum speed achievable through bhop")]
    public float bhopMaxSpeed = 18f;
    
    [Tooltip("Small boost per successful bhop")]
    public float bhopBonus = 0.5f;
    
    #endregion

    #region Dolphin Dive
    
    [Header("Dolphin Dive")]
    [Tooltip("Forward velocity when initiating dive")]
    public float diveForwardSpeed = 14f;
    
    [Tooltip("Upward arc velocity at dive start")]
    public float diveUpwardForce = 3f;
    
    [Tooltip("Duration of the dive before recovery")]
    public float diveDuration = 0.6f;
    
    [Tooltip("Recovery time after dive before movement resumes")]
    public float diveRecoveryTime = 0.4f;
    
    [Tooltip("Minimum speed required to dolphin dive")]
    public float minDiveSpeed = 5f;
    
    [Tooltip("Cooldown before next dive")]
    public float diveCooldown = 1.2f;
    
    #endregion

    #region Wall Mechanics
    
    [Header("Wall Detection")]
    [Tooltip("Distance to check for walls")]
    public float wallCheckDistance = 0.6f;
    
    [Tooltip("Layer mask for walls")]
    public LayerMask wallLayers;
    
    [Tooltip("Minimum angle to consider surface a wall (vs floor/ceiling)")]
    public float minWallAngle = 70f;
    
    [Header("Wall Bounce (Apex Style)")]
    [Tooltip("Enable wall bounce/launch mechanic")]
    public bool enableWallBounce = true;
    
    [Tooltip("Outward force when bouncing off wall")]
    public float wallBounceForce = 10f;
    
    [Tooltip("Upward force component of wall bounce")]
    public float wallBounceUpForce = 6f;
    
    [Tooltip("Minimum speed to trigger wall bounce")]
    public float minWallBounceSpeed = 6f;
    
    [Tooltip("Cooldown between wall bounces")]
    public float wallBounceCooldown = 0.4f;
    
    [Tooltip("Required approach angle for bounce (degrees from wall normal)")]
    public float wallBounceAngleThreshold = 45f;
    
    [Header("Wall Run (Optional)")]
    [Tooltip("Enable wall running")]
    public bool enableWallRun = false;
    
    [Tooltip("Speed while wall running")]
    public float wallRunSpeed = 10f;
    
    [Tooltip("Maximum wall run duration")]
    public float maxWallRunTime = 1.5f;
    
    [Tooltip("Gravity reduction while wall running (0 = no gravity)")]
    [Range(0f, 1f)]
    public float wallRunGravityReduction = 0.7f;
    
    #endregion

    #region Mantling
    
    [Header("Mantling")]
    [Tooltip("Enable automatic mantling")]
    public bool enableMantle = true;
    
    [Tooltip("Maximum height player can mantle")]
    public float maxMantleHeight = 2f;
    
    [Tooltip("Distance to check for mantle surfaces")]
    public float mantleCheckDistance = 0.8f;
    
    [Tooltip("Duration of mantle animation")]
    public float mantleDuration = 0.3f;
    
    #endregion

    #region Debug
    
    [Header("Debug")]
    [Tooltip("Draw gizmos for ground checks, wall detection, etc.")]
    public bool showDebugGizmos = true;
    
    [Tooltip("Color for ground check visualization")]
    public Color groundCheckColor = Color.green;
    
    [Tooltip("Color for wall detection visualization")]
    public Color wallCheckColor = Color.blue;
    
    [Tooltip("Color for mantle check visualization")]
    public Color mantleCheckColor = Color.yellow;
    
    #endregion
}
