using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Placeholder joystick controller for the CNC control panel.
/// Reads 4-directional input (Forward / Backward / Left / Right) and simulates
/// the physical tilt of the joystick mesh in the scene.
///
/// Input bindings (keyboard placeholder — swap for XR hand interaction later):
///   Forward  → W / Up Arrow
///   Backward → S / Down Arrow
///   Left     → A / Left Arrow
///   Right    → D / Right Arrow
///
/// Wire up the four InputActionReferences in the Inspector, pointing them at
/// the "Move" action (or split into four button actions) in
/// Assets/Settings/InputSystem_Actions.inputactions.
///
/// When VR hand interaction is added, drive <see cref="SetInput"/> directly
/// from the grab/tilt logic instead of from the Input System.
/// </summary>
public class JoystickController : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("Input Actions (New Input System)")]
    [Tooltip("2D Vector action that maps to joystick movement. " +
             "X axis = Left/Right, Y axis = Forward/Backward.")]
    [SerializeField] private InputActionReference _moveAction;

    [Header("Physical Tilt Settings")]
    [Tooltip("Maximum tilt angle (degrees) of the joystick mesh at full deflection.")]
    [SerializeField] private float _maxTiltAngle = 20f;

    [Tooltip("How quickly the joystick mesh tilts toward the target angle (degrees/sec).")]
    [SerializeField] private float _tiltSpeed = 300f;

    [Tooltip("Transform of the joystick pivot mesh to rotate visually. " +
             "If null, this GameObject's transform is used.")]
    [SerializeField] private Transform _joystickPivot;

    [Header("Deadzone")]
    [Tooltip("Input values smaller than this are treated as zero.")]
    [SerializeField] [Range(0f, 0.5f)] private float _deadzone = 0.1f;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires every frame the joystick has non-zero input.
    /// X = lateral (-1 Left … +1 Right), Y = depth (-1 Backward … +1 Forward).
    /// </summary>
    public event Action<Vector2> OnJoystickMoved;

    /// <summary>
    /// Fires once when the joystick returns to centre (input = zero).
    /// </summary>
    public event Action OnJoystickReleased;

    // ── Private state ─────────────────────────────────────────────────────────

    private Vector2 _currentInput;
    private bool _wasActive;
    private Quaternion _restRotation;
    private Transform _pivot;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _pivot = _joystickPivot != null ? _joystickPivot : transform;
        _restRotation = _pivot.localRotation;
    }

    private void OnEnable()
    {
        if (_moveAction != null)
            _moveAction.action.Enable();
    }

    private void OnDisable()
    {
        if (_moveAction != null)
            _moveAction.action.Disable();
    }

    private void Update()
    {
        ReadInput();
        AnimatePivot();
        DispatchEvents();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current joystick input as a normalised Vector2.
    /// X = lateral, Y = depth (matches XZ in world space when cutter is aligned).
    /// </summary>
    public Vector2 GetJoystickInput() => _currentInput;

    /// <summary>
    /// Allows external systems (e.g. VR hand grab) to drive the joystick directly,
    /// bypassing the Input System. Call this from a VR tilt/grab handler.
    /// </summary>
    public void SetInput(Vector2 input)
    {
        _currentInput = ApplyDeadzone(input);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void ReadInput()
    {
        if (_moveAction == null) return;

        Vector2 raw = _moveAction.action.ReadValue<Vector2>();
        _currentInput = ApplyDeadzone(raw);
    }

    private Vector2 ApplyDeadzone(Vector2 raw)
    {
        if (raw.magnitude < _deadzone)
            return Vector2.zero;

        // Rescale so the deadzone region maps cleanly to 0–1
        float rescaled = (raw.magnitude - _deadzone) / (1f - _deadzone);
        return raw.normalized * Mathf.Clamp01(rescaled);
    }

    private void AnimatePivot()
    {
        // Tilt forward/back on the X axis, left/right on the Z axis
        // (assumes joystick sits upright; adapt axes if model differs)
        Quaternion targetRotation;

        if (_currentInput.sqrMagnitude > 0.001f)
        {
            float tiltX = -_currentInput.y * _maxTiltAngle; // forward/backward
            float tiltZ = -_currentInput.x * _maxTiltAngle; // left/right
            targetRotation = _restRotation * Quaternion.Euler(tiltX, 0f, tiltZ);
        }
        else
        {
            targetRotation = _restRotation;
        }

        _pivot.localRotation = Quaternion.RotateTowards(
            _pivot.localRotation,
            targetRotation,
            _tiltSpeed * Time.deltaTime
        );
    }

    private void DispatchEvents()
    {
        bool isActive = _currentInput.sqrMagnitude > 0.001f;

        if (isActive)
        {
            OnJoystickMoved?.Invoke(_currentInput);
        }
        else if (_wasActive)
        {
            // Joystick just returned to centre
            OnJoystickReleased?.Invoke();
        }

        _wasActive = isActive;
    }
}
