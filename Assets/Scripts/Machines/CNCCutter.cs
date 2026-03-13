using System;
using UnityEngine;

/// <summary>
/// Moves the CNC tool-head GameObject along the X (lateral) and Z (depth) axes
/// in response to joystick input. Clamps movement to the bounds defined in a
/// <see cref="CuttingPath"/> ScriptableObject.
///
/// Attach this to the tool-head child of the CNC2.fbx hierarchy.
/// Wire <see cref="JoystickController"/> in the Inspector; the cutter subscribes
/// to its <see cref="JoystickController.OnJoystickMoved"/> event.
///
/// <see cref="CNCMachine"/> enables/disables cutting via <see cref="SetEnabled"/>.
/// </summary>
[RequireComponent(typeof(Transform))]
public class CNCCutter : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Joystick that drives this cutter. Must have a JoystickController component.")]
    [SerializeField] private JoystickController _joystick;

    [Tooltip("ScriptableObject that defines work-area bounds and cut depth.")]
    [SerializeField] private CuttingPath _cuttingPath;

    [Header("Movement")]
    [Tooltip("Maximum movement speed of the tool head (metres per second).")]
    [SerializeField] [Range(0.01f, 1f)] private float _cuttingSpeed = 0.15f;

    [Header("Debug")]
    [Tooltip("Draw the work-area bounds as a Gizmo in the Scene view.")]
    [SerializeField] private bool _showGizmos = true;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires every frame the cutter moves. Provides the new LOCAL position of
    /// the tool head (X and Z only — Y is fixed at idle height during movement).
    /// Subscribed to by <see cref="CNCScreenDisplay"/> for the preview overlay.
    /// </summary>
    public event Action<Vector3> OnCutterMoved;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Whether the cutter is currently active (set by CNCMachine).</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Current local-space position of the cutter (X/Z within work area).</summary>
    public Vector3 LocalCutterPosition => transform.localPosition;

    // ── Private state ─────────────────────────────────────────────────────────

    private Vector2 _joystickInput;
    private Vector3 _startLocalPosition;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _startLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        if (_joystick != null)
            _joystick.OnJoystickMoved += HandleJoystickMoved;
    }

    private void OnDisable()
    {
        if (_joystick != null)
            _joystick.OnJoystickMoved -= HandleJoystickMoved;
    }

    private void Update()
    {
        if (!IsEnabled) return;
        if (_cuttingPath == null)
        {
            Debug.LogWarning("[CNCCutter] No CuttingPath assigned — cutter cannot move.", this);
            return;
        }

        MoveCutter();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Enables or disables cutter movement. Called by <see cref="CNCMachine"/>
    /// when transitioning into/out of the Cutting state.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;

        if (!enabled)
            _joystickInput = Vector2.zero;
    }

    /// <summary>
    /// Sets the cutting speed at runtime (called by <see cref="CNCControlPanel"/> speed buttons).
    /// </summary>
    public void SetSpeed(float speed)
    {
        _cuttingSpeed = Mathf.Max(0.001f, speed);
    }

    /// <summary>
    /// Returns the current cutter position normalised within the work area (0–1 on both axes).
    /// Useful for mapping to screen UV coordinates in <see cref="CNCScreenDisplay"/>.
    /// </summary>
    public Vector2 GetNormalisedPosition()
    {
        if (_cuttingPath == null) return Vector2.one * 0.5f;

        Vector3 local = transform.localPosition;
        return _cuttingPath.Normalise(new Vector2(local.x, local.z));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void HandleJoystickMoved(Vector2 input)
    {
        _joystickInput = input;
    }

    private void MoveCutter()
    {
        // Joystick X → world X (lateral), Joystick Y → world Z (depth)
        Vector3 delta = new Vector3(
            _joystickInput.x * _cuttingSpeed * Time.deltaTime,
            0f,
            _joystickInput.y * _cuttingSpeed * Time.deltaTime
        );

        Vector3 newLocal = transform.localPosition + delta;

        // Clamp to work-area bounds (XZ plane)
        Vector2 clampedXZ = _cuttingPath.Clamp(new Vector2(newLocal.x, newLocal.z));
        newLocal.x = clampedXZ.x;
        newLocal.z = clampedXZ.y;

        // Keep Y at idle height during movement (no plunge in Phase 1)
        newLocal.y = _startLocalPosition.y;

        transform.localPosition = newLocal;

        OnCutterMoved?.Invoke(newLocal);
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (!_showGizmos || _cuttingPath == null) return;

        // Draw work-area bounds in local space
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.parent != null
            ? transform.parent.localToWorldMatrix
            : Matrix4x4.identity;

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.5f);

        Vector2 min = _cuttingPath.WorkAreaMin;
        Vector2 max = _cuttingPath.WorkAreaMax;
        float y = transform.localPosition.y;

        // Draw border lines of the work rectangle
        Gizmos.DrawLine(new Vector3(min.x, y, min.y), new Vector3(max.x, y, min.y));
        Gizmos.DrawLine(new Vector3(max.x, y, min.y), new Vector3(max.x, y, max.y));
        Gizmos.DrawLine(new Vector3(max.x, y, max.y), new Vector3(min.x, y, max.y));
        Gizmos.DrawLine(new Vector3(min.x, y, max.y), new Vector3(min.x, y, min.y));

        // Draw cutter position marker
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.localPosition, 0.01f);

        Gizmos.matrix = oldMatrix;
    }
}
