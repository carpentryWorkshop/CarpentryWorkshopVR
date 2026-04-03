using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Extended CNC Cutter with path following and position recording capabilities.
/// 
/// This component moves the CNC tool head and supports:
/// - Manual mode: Joystick-controlled movement
/// - Auto mode: Following PathData waypoints
/// - Position recording for result generation
/// - Plunge/retract operations
/// 
/// Setup:
/// 1. Attach to the tool-head child of the CNC machine
/// 2. Assign a JoystickController for manual control
/// 3. Assign a WorkAreaBounds ScriptableObject for work area bounds
/// </summary>
[RequireComponent(typeof(Transform))]
public class CNCCutterExtended : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - References
    // ══════════════════════════════════════════════════════════════════════════

    [Header("References")]
    [Tooltip("Joystick that drives this cutter in manual mode (legacy 2D control).")]
    [SerializeField] private JoystickController _joystick;

    [Tooltip("Multi-axis controller for 3-stage manual control (X/Z/Y axes).")]
    [SerializeField] private CNCMultiAxisController _multiAxisController;

    [Tooltip("ScriptableObject that defines work-area bounds.")]
    [SerializeField] private WorkAreaBounds _workAreaBounds;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Movement
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Movement")]
    [Tooltip("Speed for manual joystick movement (meters per second).")]
    [SerializeField] [Range(0.01f, 1f)] private float _manualSpeed = 0.15f;

    [Tooltip("Speed multiplier for auto path following.")]
    [SerializeField] [Range(0.5f, 2f)] private float _autoSpeedMultiplier = 1f;

    [Tooltip("Speed for plunge/retract operations (meters per second).")]
    [SerializeField] [Range(0.01f, 0.5f)] private float _plungeSpeed = 0.05f;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Recording
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Path Recording")]
    [Tooltip("Minimum distance between recorded path points (meters).")]
    [SerializeField] [Range(0.001f, 0.05f)] private float _recordInterval = 0.005f;

    [Tooltip("Maximum number of recorded points (memory safety).")]
    [SerializeField] private int _maxRecordedPoints = 10000;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Visual
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Visual")]
    [Tooltip("Particle system to play while cutting.")]
    [SerializeField] private ParticleSystem _cuttingParticles;

    [Tooltip("Light to enable while cutting.")]
    [SerializeField] private Light _cuttingLight;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Debug
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Debug")]
    [Tooltip("Draw the work-area bounds as a Gizmo.")]
    [SerializeField] private bool _showGizmos = true;

    [Tooltip("Draw the recorded path as a Gizmo.")]
    [SerializeField] private bool _showRecordedPath = true;

    [Tooltip("Log movement events to console.")]
    [SerializeField] private bool _verboseLogging = false;

    // ══════════════════════════════════════════════════════════════════════════
    // EVENTS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fires every frame the cutter moves.
    /// Parameter is the new local position.
    /// </summary>
    public event Action<Vector3> OnCutterMoved;

    /// <summary>
    /// Fires when the cutter starts plunging into material.
    /// </summary>
    public event Action OnPlungeStarted;

    /// <summary>
    /// Fires when the cutter finishes plunging.
    /// </summary>
    public event Action OnPlungeComplete;

    /// <summary>
    /// Fires when the cutter retracts from material.
    /// </summary>
    public event Action OnRetracted;

    // ══════════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Whether the cutter is currently active.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Current operation mode.</summary>
    public CutterMode Mode { get; private set; } = CutterMode.Manual;

    /// <summary>Current local position of the cutter.</summary>
    public Vector3 LocalPosition => transform.localPosition;

    /// <summary>Is the cutter currently plunged into material?</summary>
    public bool IsPlunged { get; private set; }

    /// <summary>Current plunge depth.</summary>
    public float CurrentDepth => _startLocalPosition.y - transform.localPosition.y;

    /// <summary>Current cutting speed.</summary>
    public float CurrentSpeed => Mode == CutterMode.Auto ? _currentPathFeedRate : _manualSpeed;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private Vector2 _joystickInput;
    private float _xAxisInput;  // Multi-axis: Cutter left/right
    private float _zAxisInput;  // Multi-axis: Holder forward/backward
    private float _yAxisInput;  // Multi-axis: Spindle up/down
    private Vector3 _startLocalPosition;
    private List<Vector3> _recordedPath;
    private Vector3 _lastRecordedPosition;
    private float _targetDepth;
    private float _currentPathFeedRate;
    private bool _isPlunging;
    private bool _isRetracting;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        _startLocalPosition = transform.localPosition;
        _recordedPath = new List<Vector3>();
        _lastRecordedPosition = _startLocalPosition;
    }

    private void OnEnable()
    {
        if (_joystick != null)
            _joystick.OnJoystickMoved += HandleJoystickMoved;

        if (_multiAxisController != null)
        {
            _multiAxisController.OnXAxisInput += HandleXAxisInput;
            _multiAxisController.OnZAxisInput += HandleZAxisInput;
            _multiAxisController.OnYAxisInput += HandleYAxisInput;
        }
    }

    private void OnDisable()
    {
        if (_joystick != null)
            _joystick.OnJoystickMoved -= HandleJoystickMoved;

        if (_multiAxisController != null)
        {
            _multiAxisController.OnXAxisInput -= HandleXAxisInput;
            _multiAxisController.OnZAxisInput -= HandleZAxisInput;
            _multiAxisController.OnYAxisInput -= HandleYAxisInput;
        }
    }

    private void Update()
    {
        if (!IsEnabled)
            return;

        // Handle plunge/retract operations
        if (_isPlunging)
        {
            TickPlunge();
            return;
        }

        if (_isRetracting)
        {
            TickRetract();
            return;
        }

        // Handle movement based on mode
        if (Mode == CutterMode.Manual)
        {
            MoveCutterManual();
        }
        // Auto mode movement is handled by FollowPathStep()

        // Update visual effects
        UpdateVisuals();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Enable/Disable
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Enables or disables cutter movement.
    /// </summary>
    /// <param name="enabled">True to enable cutting.</param>
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;

        if (!enabled)
        {
            _joystickInput = Vector2.zero;
            StopVisuals();
        }

        if (_verboseLogging)
            Debug.Log($"[CNCCutterExtended] Enabled: {enabled}");
    }

    /// <summary>
    /// Sets the operation mode.
    /// </summary>
    /// <param name="mode">The mode to set.</param>
    public void SetMode(CutterMode mode)
    {
        Mode = mode;

        if (_verboseLogging)
            Debug.Log($"[CNCCutterExtended] Mode: {mode}");
    }

    /// <summary>
    /// Emergency stop - immediately halts all movement.
    /// </summary>
    public void EmergencyStop()
    {
        IsEnabled = false;
        _isPlunging = false;
        _isRetracting = false;
        _joystickInput = Vector2.zero;
        StopVisuals();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Speed Control
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sets the manual cutting speed.
    /// </summary>
    /// <param name="speed">Speed in meters per second.</param>
    public void SetSpeed(float speed)
    {
        _manualSpeed = Mathf.Max(0.001f, speed);
    }

    /// <summary>
    /// Sets the auto speed multiplier.
    /// </summary>
    /// <param name="multiplier">Speed multiplier (1.0 = normal).</param>
    public void SetAutoSpeedMultiplier(float multiplier)
    {
        _autoSpeedMultiplier = Mathf.Clamp(multiplier, 0.1f, 3f);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Path Following
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Moves the cutter to the start of a path (without cutting).
    /// </summary>
    /// <param name="path">The path to position for.</param>
    public void MoveToStart(PathData path)
    {
        if (path == null || path.WaypointCount == 0)
            return;

        Vector3 startPoint = path.GetWaypoint(0);
        Vector3 newPosition = new Vector3(
            startPoint.x,
            _startLocalPosition.y, // Keep at idle height
            startPoint.z
        );

        transform.localPosition = newPosition;
        _lastRecordedPosition = newPosition;

        if (_verboseLogging)
            Debug.Log($"[CNCCutterExtended] Moved to path start: {newPosition}");
    }

    /// <summary>
    /// Advances the cutter one step along a path.
    /// Called by CNCMachineExtended during FollowingPath state.
    /// </summary>
    /// <param name="path">The path to follow.</param>
    /// <param name="waypointIndex">Current waypoint index (will be updated).</param>
    /// <param name="progress">Overall progress 0-1 (output).</param>
    /// <returns>True if the path is complete.</returns>
    public bool FollowPathStep(PathData path, ref int waypointIndex, out float progress)
    {
        progress = 0f;

        if (path == null || path.WaypointCount == 0)
            return true;

        // Handle closed loop path completion
        int totalWaypoints = path.isClosedLoop ? path.WaypointCount + 1 : path.WaypointCount;

        if (waypointIndex >= totalWaypoints)
        {
            progress = 1f;
            return true;
        }

        // Get current target
        Vector3 targetWaypoint = path.GetWaypoint(waypointIndex);
        Vector3 currentPos = transform.localPosition;

        // Calculate feed rate
        _currentPathFeedRate = path.feedRate * _autoSpeedMultiplier;
        float step = _currentPathFeedRate * Time.deltaTime;

        // Move toward target (XZ only, Y is handled by plunge)
        Vector3 targetXZ = new Vector3(targetWaypoint.x, currentPos.y, targetWaypoint.z);
        Vector3 newPos = Vector3.MoveTowards(currentPos, targetXZ, step);

        // Apply position
        transform.localPosition = newPos;
        OnCutterMoved?.Invoke(newPos);

        // Record position
        RecordPosition(newPos);

        // Check if reached waypoint
        float distanceXZ = Vector2.Distance(
            new Vector2(newPos.x, newPos.z),
            new Vector2(targetWaypoint.x, targetWaypoint.z)
        );

        if (distanceXZ < 0.001f)
        {
            waypointIndex++;
        }

        // Calculate progress
        progress = (float)waypointIndex / totalWaypoints;

        return waypointIndex >= totalWaypoints;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Plunge/Retract
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Plunges the cutter into the material.
    /// </summary>
    /// <param name="depth">Depth to plunge in meters.</param>
    public void Plunge(float depth)
    {
        _targetDepth = depth;
        _isPlunging = true;
        _isRetracting = false;
        OnPlungeStarted?.Invoke();

        if (_verboseLogging)
            Debug.Log($"[CNCCutterExtended] Plunging to depth: {depth}m");
    }

    /// <summary>
    /// Retracts the cutter to idle height.
    /// </summary>
    public void Retract()
    {
        _isRetracting = true;
        _isPlunging = false;

        if (_verboseLogging)
            Debug.Log("[CNCCutterExtended] Retracting");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Path Recording
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Clears the recorded path.
    /// </summary>
    public void ClearRecordedPath()
    {
        _recordedPath.Clear();
        _lastRecordedPosition = transform.localPosition;

        if (_verboseLogging)
            Debug.Log("[CNCCutterExtended] Recorded path cleared");
    }

    /// <summary>
    /// Gets a copy of the recorded path.
    /// </summary>
    /// <returns>List of recorded positions.</returns>
    public List<Vector3> GetRecordedPath()
    {
        return new List<Vector3>(_recordedPath);
    }

    /// <summary>
    /// Gets the recorded path count.
    /// </summary>
    public int RecordedPathCount => _recordedPath.Count;

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Position Queries
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the current cutter position normalized within the work area (0-1).
    /// </summary>
    public Vector2 GetNormalizedPosition()
    {
        if (_workAreaBounds == null)
            return Vector2.one * 0.5f;

        Vector3 local = transform.localPosition;
        return _workAreaBounds.Normalise(new Vector2(local.x, local.z));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Manual Movement
    // ══════════════════════════════════════════════════════════════════════════

    private void HandleJoystickMoved(Vector2 input)
    {
        _joystickInput = input;
    }

    private void HandleXAxisInput(float value)
    {
        _xAxisInput = value;
    }

    private void HandleZAxisInput(float value)
    {
        _zAxisInput = value;
    }

    private void HandleYAxisInput(float value)
    {
        _yAxisInput = value;
    }

    private void MoveCutterManual()
    {
        if (_workAreaBounds == null)
        {
            if (_verboseLogging)
                Debug.LogWarning("[CNCCutterExtended] No WorkAreaBounds assigned - cannot move.");
            return;
        }

        bool moved = false;
        Vector3 newLocal = transform.localPosition;

        // Multi-axis controller takes priority if assigned
        if (_multiAxisController != null)
        {
            // Stage 1: Move CUTTER in X-axis (left/right)
            if (_xAxisInput != 0f)
            {
                newLocal.x += _xAxisInput * _manualSpeed * Time.deltaTime;
                
                // Clamp X to bounds
                newLocal.x = Mathf.Clamp(newLocal.x, _workAreaBounds.WorkAreaMin.x, _workAreaBounds.WorkAreaMax.x);
                moved = true;
                
                if (_verboseLogging)
                    Debug.Log($"[CNCCutterExtended] X-axis movement: {_xAxisInput}");
            }

            // Stage 2: Move SPINDLE HOLDER in Z-axis (forward/backward)
            if (_zAxisInput != 0f && transform.parent != null)
            {
                Transform holder = transform.parent;
                Vector3 holderPos = holder.localPosition;
                holderPos.z += _zAxisInput * _manualSpeed * Time.deltaTime;
                
                // Clamp Z to bounds (WorkAreaMin.y and WorkAreaMax.y represent Z-axis)
                holderPos.z = Mathf.Clamp(holderPos.z, _workAreaBounds.WorkAreaMin.y, _workAreaBounds.WorkAreaMax.y);
                holder.localPosition = holderPos;
                moved = true;
                
                if (_verboseLogging)
                    Debug.Log($"[CNCCutterExtended] Z-axis (holder) movement: {_zAxisInput}");
            }

            // Stage 3: Move SPINDLE in Y-axis (up/down)
            if (_yAxisInput != 0f)
            {
                newLocal.y += _yAxisInput * _manualSpeed * Time.deltaTime;
                
                // Clamp Y to bounds (plunge depth)
                float minY = _startLocalPosition.y - _workAreaBounds.MaxCutDepth;
                float maxY = _workAreaBounds.IdleHeight;
                newLocal.y = Mathf.Clamp(newLocal.y, minY, maxY);
                moved = true;
                
                if (_verboseLogging)
                    Debug.Log($"[CNCCutterExtended] Y-axis (spindle) movement: {_yAxisInput}");
            }

            // Apply cutter position changes (X and Y)
            if (_xAxisInput != 0f || _yAxisInput != 0f)
            {
                transform.localPosition = newLocal;
            }
        }
        // Fallback to legacy joystick control if no multi-axis controller
        else if (_joystick != null && _joystickInput.sqrMagnitude > 0.001f)
        {
            // Joystick X → local X (lateral), Joystick Y → local Z (depth)
            Vector3 delta = new Vector3(
                _joystickInput.x * _manualSpeed * Time.deltaTime,
                0f,
                _joystickInput.y * _manualSpeed * Time.deltaTime
            );

            newLocal = transform.localPosition + delta;

            // Clamp to work-area bounds
            Vector2 clampedXZ = _workAreaBounds.Clamp(new Vector2(newLocal.x, newLocal.z));
            newLocal.x = clampedXZ.x;
            newLocal.z = clampedXZ.y;

            // Keep current Y (plunge depth)
            newLocal.y = transform.localPosition.y;

            transform.localPosition = newLocal;
            moved = true;
        }

        // Fire events and record if movement occurred
        if (moved)
        {
            OnCutterMoved?.Invoke(newLocal);
            RecordPosition(newLocal);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Plunge/Retract
    // ══════════════════════════════════════════════════════════════════════════

    private void TickPlunge()
    {
        float targetY = _startLocalPosition.y - _targetDepth;
        Vector3 pos = transform.localPosition;

        pos.y = Mathf.MoveTowards(pos.y, targetY, _plungeSpeed * Time.deltaTime);
        transform.localPosition = pos;

        if (Mathf.Approximately(pos.y, targetY))
        {
            _isPlunging = false;
            IsPlunged = true;
            OnPlungeComplete?.Invoke();

            if (_verboseLogging)
                Debug.Log("[CNCCutterExtended] Plunge complete");
        }
    }

    private void TickRetract()
    {
        Vector3 pos = transform.localPosition;

        pos.y = Mathf.MoveTowards(pos.y, _startLocalPosition.y, _plungeSpeed * Time.deltaTime);
        transform.localPosition = pos;

        if (Mathf.Approximately(pos.y, _startLocalPosition.y))
        {
            _isRetracting = false;
            IsPlunged = false;
            OnRetracted?.Invoke();

            if (_verboseLogging)
                Debug.Log("[CNCCutterExtended] Retract complete");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Recording
    // ══════════════════════════════════════════════════════════════════════════

    private void RecordPosition(Vector3 position)
    {
        if (_recordedPath.Count >= _maxRecordedPoints)
            return;

        float distance = Vector3.Distance(position, _lastRecordedPosition);

        if (distance >= _recordInterval)
        {
            _recordedPath.Add(position);
            _lastRecordedPosition = position;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Visuals
    // ══════════════════════════════════════════════════════════════════════════

    private void UpdateVisuals()
    {
        bool shouldBeActive = IsEnabled && IsPlunged;

        if (_cuttingParticles != null)
        {
            if (shouldBeActive && !_cuttingParticles.isPlaying)
                _cuttingParticles.Play();
            else if (!shouldBeActive && _cuttingParticles.isPlaying)
                _cuttingParticles.Stop();
        }

        if (_cuttingLight != null)
        {
            _cuttingLight.enabled = shouldBeActive;
        }
    }

    private void StopVisuals()
    {
        if (_cuttingParticles != null && _cuttingParticles.isPlaying)
            _cuttingParticles.Stop();

        if (_cuttingLight != null)
            _cuttingLight.enabled = false;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR - Gizmos
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!_showGizmos)
            return;

        // Draw work area bounds
        if (_workAreaBounds != null)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.parent != null
                ? transform.parent.localToWorldMatrix
                : Matrix4x4.identity;

            Gizmos.color = new Color(0f, 1f, 0.4f, 0.5f);

            Vector2 min = _workAreaBounds.WorkAreaMin;
            Vector2 max = _workAreaBounds.WorkAreaMax;
            float y = transform.localPosition.y;

            // Draw work area rectangle
            Gizmos.DrawLine(new Vector3(min.x, y, min.y), new Vector3(max.x, y, min.y));
            Gizmos.DrawLine(new Vector3(max.x, y, min.y), new Vector3(max.x, y, max.y));
            Gizmos.DrawLine(new Vector3(max.x, y, max.y), new Vector3(min.x, y, max.y));
            Gizmos.DrawLine(new Vector3(min.x, y, max.y), new Vector3(min.x, y, min.y));

            Gizmos.matrix = oldMatrix;
        }

        // Draw cutter position
        Gizmos.color = IsEnabled ? Color.red : Color.gray;
        Gizmos.DrawSphere(transform.position, 0.01f);

        // Draw recorded path
        if (_showRecordedPath && _recordedPath != null && _recordedPath.Count > 1)
        {
            Gizmos.color = Color.cyan;
            Matrix4x4 parentMatrix = transform.parent != null
                ? transform.parent.localToWorldMatrix
                : Matrix4x4.identity;

            for (int i = 0; i < _recordedPath.Count - 1; i++)
            {
                Vector3 from = parentMatrix.MultiplyPoint3x4(_recordedPath[i]);
                Vector3 to = parentMatrix.MultiplyPoint3x4(_recordedPath[i + 1]);
                Gizmos.DrawLine(from, to);
            }
        }
    }
#endif
}
