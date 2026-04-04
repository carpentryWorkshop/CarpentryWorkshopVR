using System;
using UnityEngine;

/// <summary>
/// Multi-axis manual control system for CNC machine.
/// Provides sequential 3-stage control: X-axis (cutter left/right), 
/// Z-axis (spindle holder forward/back), Y-axis (spindle up/down).
/// Automatically switches modes based on which keys are pressed.
/// </summary>
public class CNCMultiAxisController : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // ENUMS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Current control mode determining which axis is active.
    /// </summary>
    public enum ControlMode
    {
        XAxis,  // Cutter left/right (J/L keys)
        ZAxis,  // Spindle holder forward/backward (I/K keys)
        YAxis   // Spindle up/down (W/X keys)
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EVENTS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Fired when X-axis input changes (cutter left/right).</summary>
    public event Action<float> OnXAxisInput;

    /// <summary>Fired when Z-axis input changes (holder forward/backward).</summary>
    public event Action<float> OnZAxisInput;

    /// <summary>Fired when Y-axis input changes (spindle up/down).</summary>
    public event Action<float> OnYAxisInput;

    /// <summary>Fired when control mode changes.</summary>
    public event Action<ControlMode> OnModeChanged;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR FIELDS
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Key Bindings")]
    [Tooltip("Key to move cutter left (X-axis negative).")]
    [SerializeField] private KeyCode _leftKey = KeyCode.J;

    [Tooltip("Key to move cutter right (X-axis positive).")]
    [SerializeField] private KeyCode _rightKey = KeyCode.L;

    [Tooltip("Key to move spindle holder forward (Z-axis positive).")]
    [SerializeField] private KeyCode _forwardKey = KeyCode.I;

    [Tooltip("Key to move spindle holder backward (Z-axis negative).")]
    [SerializeField] private KeyCode _backKey = KeyCode.K;

    [Tooltip("Key to move spindle up (Y-axis positive).")]
    [SerializeField] private KeyCode _upKey = KeyCode.W;

    [Tooltip("Key to move spindle down (Y-axis negative).")]
    [SerializeField] private KeyCode _downKey = KeyCode.X;

    [Header("Behavior")]
    [Tooltip("Enable automatic mode switching based on key presses.")]
    [SerializeField] private bool _autoModeSwitching = true;

    [Tooltip("Starting control mode.")]
    [SerializeField] private ControlMode _startingMode = ControlMode.XAxis;

    [Header("Debug")]
    [Tooltip("Log mode changes and input events.")]
    [SerializeField] private bool _verboseLogging = false;

    // ══════════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Current active control mode.</summary>
    public ControlMode CurrentMode { get; private set; }

    /// <summary>Is the controller enabled and processing input?</summary>
    public bool IsEnabled { get; private set; } = true;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE FIELDS
    // ══════════════════════════════════════════════════════════════════════════

    private float _lastXInput = 0f;
    private float _lastZInput = 0f;
    private float _lastYInput = 0f;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Start()
    {
        CurrentMode = _startingMode;
        
        if (_verboseLogging)
            Debug.Log($"[CNCMultiAxisController] Initialized. Starting mode: {CurrentMode}");
    }

    private void Update()
    {
        if (!IsEnabled)
            return;

        // Handle automatic mode switching based on key presses
        if (_autoModeSwitching)
        {
            HandleModeSwitch();
        }

        // Read input for all axes
        float xInput = GetXAxisInput();
        float zInput = GetZAxisInput();
        float yInput = GetYAxisInput();

        // Fire events only if values changed (reduce event spam)
        if (xInput != _lastXInput)
        {
            _lastXInput = xInput;
            OnXAxisInput?.Invoke(xInput);
            
            if (_verboseLogging && xInput != 0f)
                Debug.Log($"[CNCMultiAxisController] X-axis input: {xInput}");
        }

        if (zInput != _lastZInput)
        {
            _lastZInput = zInput;
            OnZAxisInput?.Invoke(zInput);
            
            if (_verboseLogging && zInput != 0f)
                Debug.Log($"[CNCMultiAxisController] Z-axis input: {zInput}");
        }

        if (yInput != _lastYInput)
        {
            _lastYInput = yInput;
            OnYAxisInput?.Invoke(yInput);
            
            if (_verboseLogging && yInput != 0f)
                Debug.Log($"[CNCMultiAxisController] Y-axis input: {yInput}");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Enables or disables input processing.
    /// </summary>
    /// <param name="enabled">True to enable input.</param>
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        
        if (_verboseLogging)
            Debug.Log($"[CNCMultiAxisController] Input {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Manually switches to a specific control mode.
    /// </summary>
    /// <param name="mode">The mode to switch to.</param>
    public void SwitchMode(ControlMode mode)
    {
        if (CurrentMode == mode)
            return;

        ControlMode previousMode = CurrentMode;
        CurrentMode = mode;
        
        OnModeChanged?.Invoke(mode);
        
        if (_verboseLogging)
            Debug.Log($"[CNCMultiAxisController] Mode changed: {previousMode} → {mode}");
    }

    /// <summary>
    /// Resets to the starting mode.
    /// </summary>
    public void ResetMode()
    {
        SwitchMode(_startingMode);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE METHODS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Handles automatic mode switching based on which keys are pressed.
    /// </summary>
    private void HandleModeSwitch()
    {
        // Priority: Most recently pressed keys determine mode
        // Check in order: Z-axis (I/K), Y-axis (W/X), X-axis (J/L)
        
        if (Input.GetKey(_forwardKey) || Input.GetKey(_backKey))
        {
            SwitchMode(ControlMode.ZAxis);
        }
        else if (Input.GetKey(_upKey) || Input.GetKey(_downKey))
        {
            SwitchMode(ControlMode.YAxis);
        }
        else if (Input.GetKey(_leftKey) || Input.GetKey(_rightKey))
        {
            SwitchMode(ControlMode.XAxis);
        }
    }

    /// <summary>
    /// Reads X-axis input (cutter left/right).
    /// </summary>
    /// <returns>-1 (left), 0 (none), or +1 (right).</returns>
    private float GetXAxisInput()
    {
        bool left = Input.GetKey(_leftKey);
        bool right = Input.GetKey(_rightKey);

        if (left && !right)
            return -1f;
        else if (right && !left)
            return 1f;
        else
            return 0f;
    }

    /// <summary>
    /// Reads Z-axis input (holder forward/backward).
    /// </summary>
    /// <returns>-1 (backward), 0 (none), or +1 (forward).</returns>
    private float GetZAxisInput()
    {
        bool forward = Input.GetKey(_forwardKey);
        bool back = Input.GetKey(_backKey);

        if (forward && !back)
            return 1f;
        else if (back && !forward)
            return -1f;
        else
            return 0f;
    }

    /// <summary>
    /// Reads Y-axis input (spindle up/down).
    /// </summary>
    /// <returns>-1 (down), 0 (none), or +1 (up).</returns>
    private float GetYAxisInput()
    {
        bool up = Input.GetKey(_upKey);
        bool down = Input.GetKey(_downKey);

        if (up && !down)
            return 1f;
        else if (down && !up)
            return -1f;
        else
            return 0f;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR HELPERS
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    [ContextMenu("Print Current State")]
    private void PrintCurrentState()
    {
        Debug.Log($"[CNCMultiAxisController] Current State:\n" +
                  $"  Mode: {CurrentMode}\n" +
                  $"  Enabled: {IsEnabled}\n" +
                  $"  X Input: {GetXAxisInput()}\n" +
                  $"  Z Input: {GetZAxisInput()}\n" +
                  $"  Y Input: {GetYAxisInput()}");
    }

    [ContextMenu("Test Mode Switching")]
    private void TestModeSwitching()
    {
        Debug.Log("[CNCMultiAxisController] Testing mode switching...");
        SwitchMode(ControlMode.XAxis);
        SwitchMode(ControlMode.ZAxis);
        SwitchMode(ControlMode.YAxis);
        Debug.Log("[CNCMultiAxisController] Mode switching test complete.");
    }
#endif
}
