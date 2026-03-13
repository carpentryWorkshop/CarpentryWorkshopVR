using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float verticalSpeed = 5f;   // Q/E
    public bool flyMode = true;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2.0f;
    public Transform cameraRoot;       // Main Camera transform
    public float pitchMin = -90f;
    public float pitchMax = 90f;

    [Header("Cursor Lock")]
    public bool lockCursorOnStart = true;
    public KeyCode unlockKey = KeyCode.Escape;  // optional: ESC toggles cursor lock

    CharacterController controller;
    float pitch;
    bool cursorLocked;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraRoot == null && Camera.main != null)
            cameraRoot = Camera.main.transform;

        cursorLocked = lockCursorOnStart;
        ApplyCursorState();
    }

    void Update()
    {
        if (Input.GetKeyDown(unlockKey))
        {
            cursorLocked = !cursorLocked;
            ApplyCursorState();
        }

        if (controller == null || cameraRoot == null)
            return;

        // Mauslook nur wenn Cursor gelockt ist (wie vorher)
        if (cursorLocked)
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

            transform.Rotate(Vector3.up * mouseX);
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Movement (WASD + Q/E)
        float moveX = Input.GetAxisRaw("Horizontal"); // A/D
        float moveZ = Input.GetAxisRaw("Vertical");   // W/S

        Vector3 move = (transform.right * moveX + transform.forward * moveZ).normalized * moveSpeed;

        float upDown = 0f;
        if (flyMode)
        {
            if (Input.GetKey(KeyCode.Q)) upDown += 1f; // hoch
            if (Input.GetKey(KeyCode.E)) upDown -= 1f; // runter
        }

        Vector3 vertical = Vector3.up * (upDown * verticalSpeed);

        controller.Move((move + vertical) * Time.deltaTime);
    }

    void ApplyCursorState()
    {
        Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !cursorLocked;
    }
}
