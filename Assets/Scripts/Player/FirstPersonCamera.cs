using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerBody;
    [SerializeField] private InputHandler inputHandler;
    
    [Header("Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 89f;
    [SerializeField] private bool invertY = false;
    
    private float xRotation = 0f;
    private bool cursorLocked = true;
    
    private void Awake()
    {
        if (inputHandler == null)
            inputHandler = GetComponentInParent<InputHandler>();
        if (playerBody == null && transform.parent != null)
            playerBody = transform.parent.parent;
    }
    
    private void Start()
    {
        SetCursorLock(true);
    }
    
    private void Update()
    {
        // LoadoutPauseMenu owns Escape when present (opens class/weapon menu).
        if (Input.GetKeyDown(KeyCode.Escape) && FindAnyObjectByType<LoadoutPauseMenu>() == null)
        {
            SetCursorLock(!cursorLocked);
        }
        
        if (!cursorLocked) return;
        
        Vector2 lookInput = inputHandler != null ? inputHandler.LookInput : Vector2.zero;
        
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity * (invertY ? 1f : -1f);
        
        xRotation = Mathf.Clamp(xRotation + mouseY, -maxLookAngle, maxLookAngle);
        
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }
    
    public void SetCursorLock(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
    
    public void SetSensitivity(float sensitivity)
    {
        mouseSensitivity = sensitivity;
    }
}
