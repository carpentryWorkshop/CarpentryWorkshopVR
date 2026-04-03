using UnityEngine;

/// <summary>
/// Rotates the CNC meche (drill bit) around its local Y-axis when the machine is active.
/// 
/// The meche rotates constantly at a configurable RPM when the CNC machine is in
/// Positioning, FollowingPath, or Cutting states. It stops when the machine is Idle or Done.
/// 
/// Setup:
/// 1. Add this component to the CNC machine root or any persistent GameObject
/// 2. Assign the meche Transform (the drill bit model that should spin)
/// 3. Assign the CNCMachineExtended reference
/// 4. Configure rotation speed (RPM) in Inspector
/// </summary>
public class CNCMecheRotator : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR
    // ══════════════════════════════════════════════════════════════════════════

    [Header("References")]
    [Tooltip("The CNC machine to monitor for state changes.")]
    [SerializeField] private CNCMachineExtended _cncMachine;

    [Tooltip("The meche (drill bit) Transform to rotate. Should point down (-Y).")]
    [SerializeField] private Transform _mecheTransform;

    [Header("Rotation Settings")]
    [Tooltip("Rotation speed in RPM (rotations per minute). Typical CNC spindle: 8000-24000 RPM.")]
    [SerializeField] [Range(1000f, 30000f)] private float _rotationRPM = 12000f;

    [Tooltip("Reverse the rotation direction.")]
    [SerializeField] private bool _reverseDirection = false;

    [Tooltip("Smoothly ramp up/down rotation speed when starting/stopping.")]
    [SerializeField] private bool _smoothStartStop = true;

    [Tooltip("Time in seconds to reach full speed (if smooth start/stop enabled).")]
    [SerializeField] [Range(0.1f, 2f)] private float _rampDuration = 0.5f;

    [Header("Debug")]
    [Tooltip("Log rotation state changes to console.")]
    [SerializeField] private bool _verboseLogging = false;

    // ══════════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Is the meche currently rotating?</summary>
    public bool IsRotating => _isRotating;

    /// <summary>Current rotation speed as a factor of target RPM (0-1).</summary>
    public float CurrentSpeedFactor => _currentSpeedFactor;

    /// <summary>Current RPM setting.</summary>
    public float RotationRPM => _rotationRPM;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private bool _isRotating = false;
    private float _currentSpeedFactor = 0f;
    private float _targetSpeedFactor = 0f;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void OnEnable()
    {
        if (_cncMachine != null)
        {
            _cncMachine.OnStateChanged += HandleStateChanged;
            
            // Check current state in case machine is already running
            HandleStateChanged(_cncMachine.CurrentState);
        }
    }

    private void OnDisable()
    {
        if (_cncMachine != null)
        {
            _cncMachine.OnStateChanged -= HandleStateChanged;
        }
    }

    private void Update()
    {
        UpdateSpeedFactor();
        RotateMeche();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts the meche rotation.
    /// </summary>
    public void StartRotation()
    {
        if (_isRotating)
            return;

        _isRotating = true;
        _targetSpeedFactor = 1f;

        if (!_smoothStartStop)
            _currentSpeedFactor = 1f;

        if (_verboseLogging)
            Debug.Log("[CNCMecheRotator] Started rotation");
    }

    /// <summary>
    /// Stops the meche rotation.
    /// </summary>
    public void StopRotation()
    {
        if (!_isRotating)
            return;

        _isRotating = false;
        _targetSpeedFactor = 0f;

        if (!_smoothStartStop)
            _currentSpeedFactor = 0f;

        if (_verboseLogging)
            Debug.Log("[CNCMecheRotator] Stopped rotation");
    }

    /// <summary>
    /// Sets the rotation speed in RPM.
    /// </summary>
    /// <param name="rpm">Rotations per minute.</param>
    public void SetRPM(float rpm)
    {
        _rotationRPM = Mathf.Max(0f, rpm);
    }

    /// <summary>
    /// Sets the rotation direction.
    /// </summary>
    /// <param name="reverse">True to reverse direction.</param>
    public void SetReverseDirection(bool reverse)
    {
        _reverseDirection = reverse;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE METHODS
    // ══════════════════════════════════════════════════════════════════════════

    private void HandleStateChanged(CNCState newState)
    {
        switch (newState)
        {
            case CNCState.Positioning:
            case CNCState.FollowingPath:
            case CNCState.Cutting:
                StartRotation();
                break;

            case CNCState.Idle:
            case CNCState.Done:
                StopRotation();
                break;
        }

        if (_verboseLogging)
            Debug.Log($"[CNCMecheRotator] State changed to {newState}, rotating: {_isRotating}");
    }

    private void UpdateSpeedFactor()
    {
        if (!_smoothStartStop)
            return;

        if (Mathf.Approximately(_currentSpeedFactor, _targetSpeedFactor))
            return;

        float rampSpeed = 1f / _rampDuration;
        _currentSpeedFactor = Mathf.MoveTowards(
            _currentSpeedFactor,
            _targetSpeedFactor,
            rampSpeed * Time.deltaTime
        );
    }

    private void RotateMeche()
    {
        if (_mecheTransform == null)
            return;

        if (_currentSpeedFactor <= 0.001f)
            return;

        // Convert RPM to degrees per second: RPM * 360° / 60s = RPM * 6
        float degreesPerSecond = _rotationRPM * 6f * _currentSpeedFactor;
        float rotationThisFrame = degreesPerSecond * Time.deltaTime;

        if (_reverseDirection)
            rotationThisFrame = -rotationThisFrame;

        // Rotate around local Y-axis (meche points down -Y, so it spins around Y)
        _mecheTransform.Rotate(0f, rotationThisFrame, 0f, Space.Self);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR VALIDATION
    // ══════════════════════════════════════════════════════════════════════════

    private void OnValidate()
    {
        _rotationRPM = Mathf.Max(0f, _rotationRPM);
        _rampDuration = Mathf.Max(0.1f, _rampDuration);
    }
}
