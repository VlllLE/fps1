using UnityEngine;

/// <summary>
/// Wrapper for Unity's legacy Input Manager. Provides clean access to all movement inputs.
/// Attach to the same GameObject as MovementController, or use as a singleton.
/// </summary>
[DefaultExecutionOrder(-200)]
public class InputHandler : MonoBehaviour
{
    #region Singleton (Optional)
    
    public static InputHandler Instance { get; private set; }
    
    [Header("Setup")]
    [Tooltip("Use singleton pattern for global access")]
    [SerializeField] private bool useSingleton = false;
    
    #endregion

    #region Input State
    
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    
    public bool JumpHeld { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool CrouchHeld { get; private set; }
    
    public bool JumpPressed { get; private set; }
    public bool JumpReleased { get; private set; }
    public bool SprintPressed { get; private set; }
    public bool CrouchPressed { get; private set; }
    public bool CrouchReleased { get; private set; }
    public bool DivePressed { get; private set; }
    
    public float RawHorizontalInput => MoveInput.x;
    public float RawVerticalInput => MoveInput.y;
    
    public bool HasMoveInput => MoveInput.sqrMagnitude > 0.01f;
    public bool WantsToMoveForward => MoveInput.y > 0.5f;
    public bool WantsToMoveBackward => MoveInput.y < -0.5f;
    public bool IsStrafing => Mathf.Abs(MoveInput.x) > 0.3f && Mathf.Abs(MoveInput.y) < 0.5f;
    
    #endregion

    #region Input Settings

    [Header("Input Axes")]
    [SerializeField] private string horizontalAxis = "Horizontal";
    [SerializeField] private string verticalAxis = "Vertical";
    [SerializeField] private string mouseXAxis = "Mouse X";
    [SerializeField] private string mouseYAxis = "Mouse Y";

    [Header("Input Keys")]
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode crouchKey = KeyCode.C;
    [SerializeField] private KeyCode crouchKeyAlt = KeyCode.LeftControl;
    [SerializeField] private KeyCode diveKey = KeyCode.V;

    [Header("Mouse Settings")]
    [SerializeField] private float mouseSensitivity = 1f;
    
    [Header("Deadzone")]
    [SerializeField] private float moveDeadzone = 0.15f;

    [Header("Gamepad")]
    [Tooltip("Opt-in gamepad stick movement via Horizontal_Joy/Vertical_Joy. Off by default so plugged-in controllers or HID devices cannot cause stick drift.")]
    [SerializeField] private bool enableGamepadMove = false;

    #endregion

    #region Unity Lifecycle
    
    private void Awake()
    {
        if (useSingleton)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
    
    private void Update()
    {
        UpdateInputStates();
    }
    
    #endregion

    #region Input Update
    
    private bool testInputLocked;
    
    private void UpdateInputStates()
    {
        LookInput = new Vector2(
            Input.GetAxis(mouseXAxis) * mouseSensitivity,
            Input.GetAxis(mouseYAxis) * mouseSensitivity
        );
        
        if (!testInputLocked)
        {
            // Prefer digital keys so joystick noise on shared "Horizontal"/"Vertical" axes
            // cannot invent standing drift. Gamepad uses separate *_Joy axes with a hard deadzone.
            MoveInput = ReadDigitalOrGamepadMove();
            
            bool wasJumpHeld = JumpHeld;
            JumpHeld = Input.GetKey(jumpKey);
            JumpPressed = Input.GetKeyDown(jumpKey) || pendingJumpPress;
            JumpReleased = wasJumpHeld && !JumpHeld;
            pendingJumpPress = false;
            
            SprintHeld = Input.GetKey(sprintKey);
            SprintPressed = Input.GetKeyDown(sprintKey);
            
            bool wasCrouchHeld = CrouchHeld;
            CrouchHeld = Input.GetKey(crouchKey) || Input.GetKey(crouchKeyAlt);
            CrouchPressed = Input.GetKeyDown(crouchKey) || Input.GetKeyDown(crouchKeyAlt) || pendingCrouchPress;
            CrouchReleased = wasCrouchHeld && !CrouchHeld;
            pendingCrouchPress = false;
            
            DivePressed = Input.GetKeyDown(diveKey) || pendingDivePress;
            pendingDivePress = false;
        }
        else
        {
            bool wasJumpHeld = JumpHeld;
            JumpPressed = pendingJumpPress;
            JumpReleased = wasJumpHeld && !JumpHeld;
            pendingJumpPress = false;
            
            bool wasCrouchHeld = CrouchHeld;
            CrouchPressed = pendingCrouchPress;
            CrouchReleased = wasCrouchHeld && !CrouchHeld;
            pendingCrouchPress = false;
            
            SprintPressed = false;
            DivePressed = pendingDivePress;
            pendingDivePress = false;
        }
    }
    
    // PlayMode tests / automation
    private bool pendingJumpPress;
    private bool pendingCrouchPress;
    private bool pendingDivePress;
    
    public void SimulateJumpPress()
    {
        pendingJumpPress = true;
        JumpHeld = true;
    }
    
    public void SimulateCrouchPress()
    {
        pendingCrouchPress = true;
        CrouchHeld = true;
    }

    public void SimulateDivePress()
    {
        pendingDivePress = true;
    }
    
    public void SimulateSprintHeld(bool held)
    {
        SprintHeld = held;
    }
    
    public void SetMoveInput(Vector2 move)
    {
        MoveInput = move;
    }
    
    public void SetCrouchHeld(bool held)
    {
        CrouchHeld = held;
    }
    
    public void SetJumpHeld(bool held)
    {
        JumpHeld = held;
    }
    
    public void SetTestInputLocked(bool locked)
    {
        testInputLocked = locked;
        if (locked)
            MoveInput = Vector2.zero;
    }
    
    #endregion

    #region Public Methods
    
    public void EnableInput()
    {
        enabled = true;
    }
    
    public void DisableInput()
    {
        enabled = false;
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        JumpHeld = false;
        SprintHeld = false;
        CrouchHeld = false;
    }
    
    private bool joyAxesAvailable = true;

    private Vector2 ReadDigitalOrGamepadMove()
    {
        float x = 0f;
        float y = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey("a")) x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey("d")) x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey("s")) y -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey("w")) y += 1f;

        if (Mathf.Abs(x) >= 0.01f || Mathf.Abs(y) >= 0.01f)
        {
            Vector2 keys = new Vector2(x, y);
            return keys.sqrMagnitude > 1f ? keys.normalized : keys;
        }

        // Never poll legacy Horizontal/Vertical axes — plugged-in controllers or HID devices
        // bleed stick drift into them (especially before Unity reloads InputManager.asset).
        if (!enableGamepadMove || !joyAxesAvailable)
            return Vector2.zero;

        try
        {
            Vector2 stick = new Vector2(
                Input.GetAxisRaw("Horizontal_Joy"),
                Input.GetAxisRaw("Vertical_Joy"));

            if (stick.magnitude < Mathf.Max(moveDeadzone, 0.5f))
                return Vector2.zero;

            return stick.sqrMagnitude > 1f ? stick.normalized : stick;
        }
        catch (System.ArgumentException)
        {
            // InputManager.asset not reloaded in running Editor yet — stick to keyboard safely.
            joyAxesAvailable = false;
            return Vector2.zero;
        }
    }

    public Vector3 GetMoveDirection(Transform relativeTo)
    {
        if (relativeTo == null)
            return GetMoveDirectionYaw(0f);

        return GetMoveDirectionYaw(relativeTo.eulerAngles.y);
    }

    /// <summary>
    /// Flat WASD basis from yaw degrees only. Avoids projecting pitched camera forward
    /// (look-down used to flip into backward / unstable wish dirs after zeroing Y).
    /// </summary>
    public Vector3 GetMoveDirectionYaw(float yawDegrees)
    {
        if (MoveInput.sqrMagnitude <= 0.01f)
            return Vector3.zero;

        Quaternion yaw = Quaternion.Euler(0f, yawDegrees, 0f);
        Vector3 dir = yaw * new Vector3(MoveInput.x, 0f, MoveInput.y);
        return dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.zero;
    }
    
    public Vector3 GetRawMoveDirection()
    {
        return new Vector3(MoveInput.x, 0f, MoveInput.y).normalized;
    }
    
    public void ConsumeJump()
    {
        JumpPressed = false;
    }
    
    public void ConsumeCrouch()
    {
        CrouchPressed = false;
    }
    
    public void ConsumeDive()
    {
        DivePressed = false;
    }
    
    #endregion
}
