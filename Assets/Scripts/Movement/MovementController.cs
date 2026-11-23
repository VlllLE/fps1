using UnityEngine;

/// <summary>
/// CoD/Apex-style movement controller with slide cancelling, bunny hopping,
/// dolphin diving, air strafing, and wall bounces.
/// Requires: CharacterController, InputHandler, MovementConfig
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class MovementController : MonoBehaviour
{
    #region Components & References
    
    [Header("References")]
    [SerializeField] private MovementConfig config;
    [SerializeField] private InputHandler input;
    [SerializeField] private Transform cameraTransform;
    
    private CharacterController controller;
    private float defaultHeight;
    private Vector3 defaultCenter;
    
    #endregion

    #region Movement State
    
    public enum MovementState
    {
        Walking,
        Sprinting,
        Sliding,
        Airborne,
        Diving,
        DiveRecovery,
        WallRunning,
        Mantling
    }
    
    [Header("Current State (Read Only)")]
    [SerializeField] private MovementState currentState = MovementState.Walking;
    [SerializeField] private Vector3 velocity;
    [SerializeField] private float currentSpeed;
    [SerializeField] private bool isGrounded;
    
    public MovementState CurrentState => currentState;
    public Vector3 Velocity => velocity;
    public float CurrentSpeed => currentSpeed;
    public bool IsGrounded => isGrounded;
    
    #endregion

    #region Internal Timers & Cooldowns
    
    // Jumping
    private float lastGroundedTime;
    private float lastJumpInputTime;
    private bool hasJumpedThisAirtime;
    
    // Sliding
    private float slideStartTime;
    private float lastSlideTime;
    private Vector3 slideDirection;
    private bool isSlideQueued;
    
    // Bunny Hopping
    private float lastLandTime;
    private int bhopChainCount;
    
    // Diving
    private float diveStartTime;
    private float lastDiveTime;
    private Vector3 diveDirection;
    
    // Wall Mechanics
    private float lastWallBounceTime;
    private Vector3 lastWallNormal;
    private float wallRunStartTime;
    private bool isWallRunning;
    private int wallRunSide; // -1 = left, 1 = right
    
    // Mantling
    private bool isMantling;
    private float mantleStartTime;
    private Vector3 mantleStartPos;
    private Vector3 mantleEndPos;
    
    #endregion

    #region Ground Check
    
    [Header("Ground Detection")]
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private LayerMask groundLayers = ~0;
    
    private RaycastHit groundHit;
    private float groundAngle;
    
    #endregion

    #region Unity Lifecycle
    
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        
        if (config == null)
        {
            Debug.LogError("[MovementController] No MovementConfig assigned!");
        }
        
        if (input == null)
        {
            input = GetComponent<InputHandler>();
            if (input == null)
            {
                Debug.LogError("[MovementController] No InputHandler found!");
            }
        }
        
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main?.transform;
        }
        
        defaultHeight = controller.height;
        defaultCenter = controller.center;
    }
    
    private void Update()
    {
        if (config == null || input == null) return;
        
        CheckGrounded();
        UpdateTimers();
        HandleInput();
        UpdateMovementState();
        ApplyMovement();
        UpdateColliderHeight();
        
        currentSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
    }
    
    private void OnDrawGizmos()
    {
        if (config == null || !config.showDebugGizmos) return;
        
        DrawGroundCheckGizmo();
        DrawWallCheckGizmos();
        DrawMantleCheckGizmo();
        DrawVelocityGizmo();
    }
    
    #endregion

    #region Ground Check Logic
    
    private void CheckGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * (groundCheckRadius + 0.01f);
        
        isGrounded = Physics.SphereCast(
            origin,
            groundCheckRadius,
            Vector3.down,
            out groundHit,
            groundCheckDistance + 0.01f,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );
        
        if (isGrounded)
        {
            groundAngle = Vector3.Angle(groundHit.normal, Vector3.up);
            
            if (groundAngle > config.maxSlopeAngle)
            {
                isGrounded = false;
            }
        }
        
        // Track grounded time for coyote time
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            
            // Landing detection for bhop
            if (currentState == MovementState.Airborne || currentState == MovementState.Diving)
            {
                OnLand();
            }
        }
    }
    
    private void OnLand()
    {
        lastLandTime = Time.time;
        hasJumpedThisAirtime = false;
        
        // Check for bhop chain
        if (input.JumpHeld || input.JumpPressed)
        {
            float timeSinceJumpInput = Time.time - lastJumpInputTime;
            if (timeSinceJumpInput < config.bhopWindow)
            {
                ExecuteBhop();
            }
        }
        
        // Check for queued slide
        if (isSlideQueued && CanSlide())
        {
            StartSlide();
            isSlideQueued = false;
        }
    }
    
    #endregion

    #region Timer Updates
    
    private void UpdateTimers()
    {
        // Buffer jump input timing
        if (input.JumpPressed)
        {
            lastJumpInputTime = Time.time;
        }
    }
    
    #endregion

    #region Input Handling
    
    private void HandleInput()
    {
        // Jump / Bhop
        if (input.JumpPressed)
        {
            HandleJumpInput();
        }
        
        // Slide initiation (crouch while sprinting/moving fast)
        if (input.CrouchPressed)
        {
            HandleCrouchInput();
        }
        
        // Slide cancel (jump while sliding)
        if (currentState == MovementState.Sliding && input.JumpPressed)
        {
            ExecuteSlideCancel();
        }
        
        // Dive input
        if (input.DivePressed)
        {
            HandleDiveInput();
        }
    }
    
    private void HandleJumpInput()
    {
        // Check for buffered jump (landed within buffer window)
        bool canCoyoteJump = Time.time - lastGroundedTime < config.coyoteTime && !hasJumpedThisAirtime;
        bool canBufferedJump = Time.time - lastJumpInputTime < config.jumpBufferTime;
        
        if (currentState == MovementState.Sliding)
        {
            return; // Handled by slide cancel
        }
        
        if (isGrounded || canCoyoteJump)
        {
            ExecuteJump();
        }
        else if (currentState == MovementState.WallRunning)
        {
            ExecuteWallJump();
        }
        else if (config.enableWallBounce && CanWallBounce())
        {
            ExecuteWallBounce();
        }
    }
    
    private void HandleCrouchInput()
    {
        if (currentState == MovementState.Sprinting || 
            (currentState == MovementState.Walking && currentSpeed >= config.minSlideSpeed))
        {
            if (CanSlide())
            {
                StartSlide();
            }
            else if (currentState == MovementState.Airborne)
            {
                // Queue slide for landing
                isSlideQueued = true;
            }
        }
    }
    
    private void HandleDiveInput()
    {
        if (CanDive())
        {
            ExecuteDive();
        }
    }
    
    #endregion

    #region Movement State Machine
    
    private void UpdateMovementState()
    {
        switch (currentState)
        {
            case MovementState.Walking:
            case MovementState.Sprinting:
                UpdateGroundedState();
                break;
                
            case MovementState.Sliding:
                UpdateSlidingState();
                break;
                
            case MovementState.Airborne:
                UpdateAirborneState();
                break;
                
            case MovementState.Diving:
                UpdateDivingState();
                break;
                
            case MovementState.DiveRecovery:
                UpdateDiveRecoveryState();
                break;
                
            case MovementState.WallRunning:
                UpdateWallRunState();
                break;
                
            case MovementState.Mantling:
                UpdateMantleState();
                break;
        }
    }
    
    private void UpdateGroundedState()
    {
        if (!isGrounded)
        {
            currentState = MovementState.Airborne;
            return;
        }
        
        // Determine walk vs sprint
        if (input.SprintHeld && input.WantsToMoveForward)
        {
            currentState = MovementState.Sprinting;
        }
        else
        {
            currentState = MovementState.Walking;
        }
        
        // Calculate target velocity
        Vector3 wishDir = GetWishDirection();
        float targetSpeed = currentState == MovementState.Sprinting 
            ? config.walkSpeed * config.sprintMultiplier 
            : config.walkSpeed;
        
        Vector3 targetVelocity = wishDir * targetSpeed;
        
        // Apply ground acceleration with friction
        velocity = ApplyGroundAcceleration(velocity, targetVelocity, config.groundAcceleration, config.groundFriction);
        
        // Apply gravity to keep grounded
        velocity.y = -2f;
    }
    
    private void UpdateSlidingState()
    {
        float slideElapsed = Time.time - slideStartTime;
        
        // Check slide end conditions
        bool slideTimedOut = slideElapsed >= config.slideDuration;
        bool crouchReleased = !input.CrouchHeld;
        bool tooSlow = currentSpeed < config.minSlideSpeed * 0.5f;
        bool hitWall = !isGrounded && velocity.y < -0.5f;
        
        if (slideTimedOut || (crouchReleased && slideElapsed > 0.1f) || tooSlow)
        {
            EndSlide();
            return;
        }
        
        if (!isGrounded)
        {
            currentState = MovementState.Airborne;
            return;
        }
        
        // Apply slide physics
        UpdateSlideVelocity();
    }
    
    private void UpdateSlideVelocity()
    {
        // Maintain slide direction
        Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
        
        // Apply slide friction
        float speed = horizontalVel.magnitude;
        if (speed > 0.1f)
        {
            float drop = speed * config.slideFriction * Time.deltaTime;
            float newSpeed = Mathf.Max(speed - drop, 0f);
            horizontalVel = horizontalVel.normalized * newSpeed;
        }
        
        // Downhill boost
        if (groundAngle > 5f)
        {
            Vector3 slopeDir = Vector3.ProjectOnPlane(Vector3.down, groundHit.normal).normalized;
            float downhillDot = Vector3.Dot(slideDirection, slopeDir);
            if (downhillDot > 0.1f)
            {
                horizontalVel += slopeDir * config.slideDownhillBoost * downhillDot * Time.deltaTime;
            }
        }
        
        velocity = new Vector3(horizontalVel.x, -2f, horizontalVel.z);
    }
    
    private void UpdateAirborneState()
    {
        if (isGrounded)
        {
            currentState = MovementState.Walking;
            return;
        }
        
        // Check for wall interactions
        if (config.enableWallRun && CheckForWallRun())
        {
            return;
        }
        
        // Check for mantle opportunity
        if (config.enableMantle && CheckForMantle())
        {
            return;
        }
        
        // Apply air movement (source-style strafing)
        ApplyAirMovement();
        
        // Apply gravity
        velocity.y -= Physics.gravity.magnitude * config.gravityMultiplier * Time.deltaTime;
    }
    
    private void UpdateDivingState()
    {
        float diveElapsed = Time.time - diveStartTime;
        
        if (diveElapsed >= config.diveDuration || isGrounded)
        {
            if (isGrounded)
            {
                // Transition to recovery
                currentState = MovementState.DiveRecovery;
                diveStartTime = Time.time; // Reuse for recovery timing
                velocity = Vector3.zero;
            }
            else
            {
                currentState = MovementState.Airborne;
            }
            return;
        }
        
        // Apply gravity during dive
        velocity.y -= Physics.gravity.magnitude * config.gravityMultiplier * Time.deltaTime;
    }
    
    private void UpdateDiveRecoveryState()
    {
        float recoveryElapsed = Time.time - diveStartTime;
        
        if (recoveryElapsed >= config.diveRecoveryTime)
        {
            currentState = MovementState.Walking;
            lastDiveTime = Time.time;
        }
    }
    
    private void UpdateWallRunState()
    {
        if (!config.enableWallRun)
        {
            currentState = MovementState.Airborne;
            return;
        }
        
        float wallRunElapsed = Time.time - wallRunStartTime;
        
        // Check wall run end conditions
        if (wallRunElapsed >= config.maxWallRunTime || isGrounded || !IsStillOnWall())
        {
            EndWallRun();
            return;
        }
        
        // Apply wall run movement
        Vector3 wallRunDir = Vector3.Cross(lastWallNormal, Vector3.up) * wallRunSide;
        velocity = wallRunDir * config.wallRunSpeed;
        
        // Reduced gravity
        velocity.y -= Physics.gravity.magnitude * config.gravityMultiplier * 
            (1f - config.wallRunGravityReduction) * Time.deltaTime;
    }
    
    private void UpdateMantleState()
    {
        if (!isMantling) return;
        
        float mantleProgress = (Time.time - mantleStartTime) / config.mantleDuration;
        
        if (mantleProgress >= 1f)
        {
            transform.position = mantleEndPos;
            isMantling = false;
            currentState = MovementState.Walking;
            velocity = Vector3.zero;
            return;
        }
        
        // Smooth mantle interpolation
        float smoothProgress = Mathf.SmoothStep(0f, 1f, mantleProgress);
        Vector3 newPos = Vector3.Lerp(mantleStartPos, mantleEndPos, smoothProgress);
        
        // Add slight arc
        float arc = Mathf.Sin(smoothProgress * Mathf.PI) * 0.3f;
        newPos.y += arc;
        
        transform.position = newPos;
    }
    
    #endregion

    #region Movement Actions
    
    private void ExecuteJump()
    {
        velocity.y = config.jumpForce;
        hasJumpedThisAirtime = true;
        currentState = MovementState.Airborne;
        input.ConsumeJump();
        
        // Reset bhop chain if too slow
        if (currentSpeed < config.walkSpeed * 0.5f)
        {
            bhopChainCount = 0;
        }
    }
    
    private void ExecuteBhop()
    {
        // Calculate bhop bonus
        float bhopBoost = config.bhopBonus * Mathf.Min(bhopChainCount, 5);
        float targetSpeed = Mathf.Min(currentSpeed * config.bhopSpeedRetention + bhopBoost, config.bhopMaxSpeed);
        
        // Preserve horizontal direction, add jump
        Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
        if (horizontalVel.magnitude > 0.1f)
        {
            horizontalVel = horizontalVel.normalized * targetSpeed;
        }
        
        velocity = new Vector3(horizontalVel.x, config.jumpForce, horizontalVel.z);
        hasJumpedThisAirtime = true;
        currentState = MovementState.Airborne;
        bhopChainCount++;
        
        input.ConsumeJump();
    }
    
    private bool CanSlide()
    {
        bool offCooldown = Time.time - lastSlideTime >= config.slideCooldown;
        bool fastEnough = currentSpeed >= config.minSlideSpeed;
        bool notAlreadySliding = currentState != MovementState.Sliding;
        
        return offCooldown && fastEnough && notAlreadySliding && isGrounded;
    }
    
    private void StartSlide()
    {
        currentState = MovementState.Sliding;
        slideStartTime = Time.time;
        
        // Capture slide direction
        slideDirection = new Vector3(velocity.x, 0f, velocity.z).normalized;
        if (slideDirection.sqrMagnitude < 0.1f)
        {
            slideDirection = transform.forward;
        }
        
        // Apply initial boost
        float boostedSpeed = Mathf.Max(currentSpeed, config.slideBoostSpeed);
        velocity = new Vector3(
            slideDirection.x * boostedSpeed,
            velocity.y,
            slideDirection.z * boostedSpeed
        );
    }
    
    private void EndSlide()
    {
        currentState = isGrounded ? MovementState.Walking : MovementState.Airborne;
        lastSlideTime = Time.time;
    }
    
    private void ExecuteSlideCancel()
    {
        float slideElapsed = Time.time - slideStartTime;
        
        // Calculate cancel boost (more boost if cancelled early)
        float cancelMultiplier = slideElapsed < config.slideCancelWindow 
            ? config.slideCancelBoost 
            : 1f + (config.slideCancelBoost - 1f) * 0.5f;
        
        // Preserve horizontal momentum with boost
        Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
        horizontalVel *= cancelMultiplier;
        
        // Cap to bhop max
        if (horizontalVel.magnitude > config.bhopMaxSpeed)
        {
            horizontalVel = horizontalVel.normalized * config.bhopMaxSpeed;
        }
        
        velocity = new Vector3(horizontalVel.x, config.jumpForce, horizontalVel.z);
        hasJumpedThisAirtime = true;
        currentState = MovementState.Airborne;
        lastSlideTime = Time.time;
        
        input.ConsumeJump();
    }
    
    private bool CanDive()
    {
        bool offCooldown = Time.time - lastDiveTime >= config.diveCooldown;
        bool fastEnough = currentSpeed >= config.minDiveSpeed;
        bool notDiving = currentState != MovementState.Diving && currentState != MovementState.DiveRecovery;
        bool movingForward = input.WantsToMoveForward;
        
        return offCooldown && fastEnough && notDiving && movingForward && 
            (currentState == MovementState.Sprinting || currentState == MovementState.Airborne);
    }
    
    private void ExecuteDive()
    {
        currentState = MovementState.Diving;
        diveStartTime = Time.time;
        
        // Get dive direction (forward relative to camera)
        diveDirection = GetWishDirection();
        if (diveDirection.sqrMagnitude < 0.1f)
        {
            diveDirection = transform.forward;
        }
        diveDirection.Normalize();
        
        // Apply dive velocity
        velocity = diveDirection * config.diveForwardSpeed;
        velocity.y = config.diveUpwardForce;
        
        input.ConsumeDive();
    }
    
    #endregion

    #region Wall Mechanics
    
    private bool CanWallBounce()
    {
        if (Time.time - lastWallBounceTime < config.wallBounceCooldown) return false;
        if (currentSpeed < config.minWallBounceSpeed) return false;
        
        // Check for wall
        Vector3 moveDir = new Vector3(velocity.x, 0f, velocity.z).normalized;
        if (moveDir.sqrMagnitude < 0.1f) return false;
        
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, moveDir, out hit, 
            config.wallCheckDistance, config.wallLayers))
        {
            float wallAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (wallAngle >= config.minWallAngle)
            {
                // Check approach angle
                float approachAngle = Vector3.Angle(-moveDir, hit.normal);
                if (approachAngle <= config.wallBounceAngleThreshold)
                {
                    lastWallNormal = hit.normal;
                    return true;
                }
            }
        }
        
        return false;
    }
    
    private void ExecuteWallBounce()
    {
        // Reflect velocity off wall with boost
        Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 reflected = Vector3.Reflect(horizontalVel, lastWallNormal);
        
        // Add bounce force
        reflected = reflected.normalized * (horizontalVel.magnitude + config.wallBounceForce);
        
        velocity = new Vector3(reflected.x, config.wallBounceUpForce, reflected.z);
        lastWallBounceTime = Time.time;
        
        // Chain momentum preservation
        bhopChainCount = Mathf.Max(bhopChainCount, 1);
    }
    
    private bool CheckForWallRun()
    {
        if (!input.HasMoveInput) return false;
        if (velocity.y > 2f) return false; // Don't grab wall while jumping up
        
        // Check left and right for walls
        Vector3 right = transform.right;
        Vector3 left = -right;
        
        RaycastHit hitRight, hitLeft;
        bool wallOnRight = Physics.Raycast(transform.position + Vector3.up * 0.5f, right, 
            out hitRight, config.wallCheckDistance, config.wallLayers);
        bool wallOnLeft = Physics.Raycast(transform.position + Vector3.up * 0.5f, left, 
            out hitLeft, config.wallCheckDistance, config.wallLayers);
        
        RaycastHit wallHit;
        int side;
        
        if (wallOnRight && (!wallOnLeft || hitRight.distance < hitLeft.distance))
        {
            wallHit = hitRight;
            side = 1;
        }
        else if (wallOnLeft)
        {
            wallHit = hitLeft;
            side = -1;
        }
        else
        {
            return false;
        }
        
        // Verify it's a wall (not floor/ceiling)
        float angle = Vector3.Angle(wallHit.normal, Vector3.up);
        if (angle < config.minWallAngle) return false;
        
        // Start wall run
        currentState = MovementState.WallRunning;
        wallRunStartTime = Time.time;
        lastWallNormal = wallHit.normal;
        wallRunSide = side;
        isWallRunning = true;
        
        return true;
    }
    
    private bool IsStillOnWall()
    {
        Vector3 checkDir = wallRunSide > 0 ? transform.right : -transform.right;
        return Physics.Raycast(transform.position + Vector3.up * 0.5f, checkDir, 
            config.wallCheckDistance * 1.2f, config.wallLayers);
    }
    
    private void EndWallRun()
    {
        isWallRunning = false;
        currentState = MovementState.Airborne;
    }
    
    private void ExecuteWallJump()
    {
        // Jump away from wall
        Vector3 jumpDir = lastWallNormal + Vector3.up;
        jumpDir.Normalize();
        
        velocity = jumpDir * config.jumpForce * 1.2f;
        velocity.y = config.jumpForce;
        
        EndWallRun();
        hasJumpedThisAirtime = true;
    }
    
    #endregion

    #region Mantling
    
    private bool CheckForMantle()
    {
        if (!input.WantsToMoveForward) return false;
        if (velocity.y > 1f) return false; // Don't mantle while jumping up
        
        Vector3 origin = transform.position + Vector3.up * controller.height;
        Vector3 forward = GetWishDirection();
        if (forward.sqrMagnitude < 0.1f) forward = transform.forward;
        
        // Check for ledge
        RaycastHit forwardHit;
        if (!Physics.Raycast(origin, forward, out forwardHit, config.mantleCheckDistance, groundLayers))
        {
            return false;
        }
        
        // Check for surface on top
        Vector3 topCheckOrigin = forwardHit.point + forward * 0.1f + Vector3.up * config.maxMantleHeight;
        RaycastHit topHit;
        if (!Physics.Raycast(topCheckOrigin, Vector3.down, out topHit, config.maxMantleHeight, groundLayers))
        {
            return false;
        }
        
        // Verify it's a valid mantle height
        float ledgeHeight = topHit.point.y - transform.position.y;
        if (ledgeHeight < controller.stepOffset || ledgeHeight > config.maxMantleHeight)
        {
            return false;
        }
        
        // Start mantle
        StartMantle(topHit.point + Vector3.up * 0.1f + forward * 0.3f);
        return true;
    }
    
    private void StartMantle(Vector3 targetPos)
    {
        isMantling = true;
        mantleStartTime = Time.time;
        mantleStartPos = transform.position;
        mantleEndPos = targetPos;
        currentState = MovementState.Mantling;
        velocity = Vector3.zero;
    }
    
    #endregion

    #region Air Movement (Source-Style Strafing)
    
    private void ApplyAirMovement()
    {
        Vector3 wishDir = GetWishDirection();
        
        if (wishDir.sqrMagnitude < 0.01f)
        {
            // Apply air friction when no input
            ApplyAirFriction();
            return;
        }
        
        // Source-style air acceleration
        Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
        float currentSpeedInWishDir = Vector3.Dot(horizontalVel, wishDir);
        float addSpeed = config.maxAirSpeed - currentSpeedInWishDir;
        
        if (addSpeed <= 0) return;
        
        float accelSpeed = config.airAcceleration * Time.deltaTime;
        if (accelSpeed > addSpeed)
        {
            accelSpeed = addSpeed;
        }
        
        velocity.x += wishDir.x * accelSpeed;
        velocity.z += wishDir.z * accelSpeed;
        
        // Apply air control for sharper turns
        if (input.IsStrafing)
        {
            ApplyAirControl(wishDir);
        }
    }
    
    private void ApplyAirControl(Vector3 wishDir)
    {
        Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
        float speed = horizontalVel.magnitude;
        
        if (speed < 0.1f) return;
        
        // Dot product between current velocity and wish direction
        float dot = Vector3.Dot(horizontalVel.normalized, wishDir);
        
        // Only apply air control when strafing (perpendicular movement)
        if (dot < 0.9f && dot > -0.9f)
        {
            float controlAmount = config.airControl * Time.deltaTime * speed;
            Vector3 newDir = Vector3.Lerp(horizontalVel.normalized, wishDir, controlAmount / speed);
            
            velocity.x = newDir.x * speed;
            velocity.z = newDir.z * speed;
        }
    }
    
    private void ApplyAirFriction()
    {
        if (config.airFriction <= 0) return;
        
        Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
        float speed = horizontalVel.magnitude;
        
        if (speed < 0.1f) return;
        
        float drop = speed * config.airFriction * Time.deltaTime;
        float newSpeed = Mathf.Max(speed - drop, 0f);
        
        velocity.x *= newSpeed / speed;
        velocity.z *= newSpeed / speed;
    }
    
    #endregion

    #region Movement Application
    
    private void ApplyMovement()
    {
        if (currentState == MovementState.Mantling) return;
        
        controller.Move(velocity * Time.deltaTime);
    }
    
    private Vector3 ApplyGroundAcceleration(Vector3 currentVel, Vector3 targetVel, float accel, float friction)
    {
        Vector3 horizontalCurrent = new Vector3(currentVel.x, 0f, currentVel.z);
        Vector3 horizontalTarget = new Vector3(targetVel.x, 0f, targetVel.z);
        
        // Apply friction
        float speed = horizontalCurrent.magnitude;
        if (speed > 0.1f)
        {
            float drop = speed * friction * Time.deltaTime;
            horizontalCurrent *= Mathf.Max(speed - drop, 0f) / speed;
        }
        
        // Accelerate towards target
        Vector3 diff = horizontalTarget - horizontalCurrent;
        float diffMag = diff.magnitude;
        
        if (diffMag > 0.01f)
        {
            float addSpeed = Mathf.Min(accel * Time.deltaTime, diffMag);
            horizontalCurrent += diff.normalized * addSpeed;
        }
        
        return new Vector3(horizontalCurrent.x, currentVel.y, horizontalCurrent.z);
    }
    
    private Vector3 GetWishDirection()
    {
        if (cameraTransform == null) return Vector3.zero;
        return input.GetMoveDirection(cameraTransform);
    }
    
    #endregion

    #region Collider Height
    
    private void UpdateColliderHeight()
    {
        float targetHeight = defaultHeight;
        Vector3 targetCenter = defaultCenter;
        
        if (currentState == MovementState.Sliding || currentState == MovementState.Diving || 
            currentState == MovementState.DiveRecovery)
        {
            targetHeight = config.slideHeight;
            targetCenter = new Vector3(0f, config.slideHeight * 0.5f, 0f);
        }
        
        // Smooth height transitions
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * 15f);
        controller.center = Vector3.Lerp(controller.center, targetCenter, Time.deltaTime * 15f);
    }
    
    #endregion

    #region Debug Gizmos
    
    private void DrawGroundCheckGizmo()
    {
        Vector3 origin = transform.position + Vector3.up * (groundCheckRadius + 0.01f);
        
        Gizmos.color = isGrounded ? config.groundCheckColor : Color.red;
        Gizmos.DrawWireSphere(origin, groundCheckRadius);
        Gizmos.DrawLine(origin, origin + Vector3.down * groundCheckDistance);
        
        if (isGrounded && groundHit.collider != null)
        {
            Gizmos.DrawLine(groundHit.point, groundHit.point + groundHit.normal * 0.5f);
        }
    }
    
    private void DrawWallCheckGizmos()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        
        Gizmos.color = config.wallCheckColor;
        
        // Forward
        Gizmos.DrawRay(origin, transform.forward * config.wallCheckDistance);
        
        // Left/Right
        Gizmos.DrawRay(origin, transform.right * config.wallCheckDistance);
        Gizmos.DrawRay(origin, -transform.right * config.wallCheckDistance);
        
        // Movement direction
        Vector3 moveDir = new Vector3(velocity.x, 0f, velocity.z).normalized;
        if (moveDir.sqrMagnitude > 0.1f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(origin, moveDir * config.wallCheckDistance);
        }
    }
    
    private void DrawMantleCheckGizmo()
    {
        if (!config.enableMantle) return;
        
        Vector3 origin = transform.position + Vector3.up * controller.height;
        Vector3 forward = transform.forward;
        
        Gizmos.color = config.mantleCheckColor;
        Gizmos.DrawRay(origin, forward * config.mantleCheckDistance);
        
        Vector3 topCheck = origin + forward * config.mantleCheckDistance + Vector3.up * config.maxMantleHeight;
        Gizmos.DrawRay(topCheck, Vector3.down * config.maxMantleHeight);
    }
    
    private void DrawVelocityGizmo()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(transform.position + Vector3.up, velocity.normalized * (velocity.magnitude * 0.1f));
    }
    
    #endregion

    #region Public API
    
    /// <summary>
    /// Add external velocity (explosions, jump pads, etc.)
    /// </summary>
    public void AddImpulse(Vector3 impulse)
    {
        velocity += impulse;
        
        if (!isGrounded)
        {
            currentState = MovementState.Airborne;
        }
    }
    
    /// <summary>
    /// Set velocity directly (teleports, respawns, etc.)
    /// </summary>
    public void SetVelocity(Vector3 newVelocity)
    {
        velocity = newVelocity;
    }
    
    /// <summary>
    /// Reset all movement state (respawn, etc.)
    /// </summary>
    public void ResetMovement()
    {
        velocity = Vector3.zero;
        currentState = MovementState.Walking;
        bhopChainCount = 0;
        isSlideQueued = false;
        isMantling = false;
        isWallRunning = false;
        
        controller.height = defaultHeight;
        controller.center = defaultCenter;
    }
    
    /// <summary>
    /// Check if player can stand up (not obstructed above)
    /// </summary>
    public bool CanStandUp()
    {
        float checkHeight = defaultHeight - controller.height;
        if (checkHeight <= 0) return true;
        
        Vector3 origin = transform.position + Vector3.up * controller.height;
        return !Physics.Raycast(origin, Vector3.up, checkHeight + 0.1f, groundLayers);
    }
    
    #endregion
}
