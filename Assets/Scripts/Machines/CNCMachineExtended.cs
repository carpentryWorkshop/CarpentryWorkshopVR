using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Extended CNC Machine with path following capabilities.
/// 
/// This is a complete CNC machine implementation that supports both:
/// - Manual mode: Player controls cutter via joystick
/// - Auto mode: Cutter follows a pre-defined PathData
/// 
/// State flow:
///   Idle ──StartCut()──► Positioning ──Ready()──► FollowingPath/Cutting ──Complete──► Done ──Reset()──► Idle
/// 
/// Setup:
/// 1. Add this component to the CNC machine GameObject
/// 2. Assign the CNCCutterExtended child component
/// 3. Optionally assign a CNCResultGenerator for mesh cutting
/// </summary>
public class CNCMachineExtended : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - References
    // ══════════════════════════════════════════════════════════════════════════

    [Header("References")]
    [Tooltip("The CNCCutterExtended component that physically moves the tool head.")]
    [SerializeField] private CNCCutterExtended _cutter;

    [Tooltip("The CNCResultGenerator for mesh cutting (optional).")]
    [SerializeField] private CNCResultGenerator _resultGenerator;

    [Tooltip("Transfer point where workpieces are loaded onto the CNC.")]
    [SerializeField] private TransferPoint _loadingZone;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Behavior
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Behavior")]
    [Tooltip("Seconds spent in the Positioning state before cutting begins.")]
    [SerializeField] [Range(0f, 5f)] private float _positioningDuration = 1f;

    [Tooltip("Default cutting mode.")]
    [SerializeField] private CutterMode _defaultMode = CutterMode.Manual;

    [Tooltip("Automatically reset to Idle after Done state (seconds). 0 = manual reset.")]
    [SerializeField] [Range(0f, 5f)] private float _autoResetDelay = 0.5f;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Path Following
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Path Following")]
    [Tooltip("Currently loaded cutting path (for auto mode).")]
    [SerializeField] private PathData _loadedPath;

    [Tooltip("List of available paths for selection.")]
    [SerializeField] private List<PathData> _availablePaths = new List<PathData>();

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Safety
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Safety")]
    [Tooltip("Require a workpiece to be loaded before cutting can start.")]
    [SerializeField] private bool _requireWorkpiece = true;

    [Tooltip("Maximum allowed speed multiplier before triggering safety warning.")]
    [SerializeField] [Range(1f, 3f)] private float _maxSafeSpeedMultiplier = 1.5f;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Debug
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Debug")]
    [Tooltip("Log state transitions to console.")]
    [SerializeField] private bool _verboseLogging = true;

    // ══════════════════════════════════════════════════════════════════════════
    // EVENTS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Fires whenever the machine transitions to a new state.</summary>
    public event Action<CNCState> OnStateChanged;

    /// <summary>Fires when the machine reaches the Done state.</summary>
    public event Action OnCutComplete;

    /// <summary>Fires when a path is loaded.</summary>
    public event Action<PathData> OnPathLoaded;

    /// <summary>Fires during path following with progress (0-1).</summary>
    public event Action<float> OnCutProgress;

    // ══════════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Current state of the CNC machine.</summary>
    public CNCState CurrentState { get; private set; } = CNCState.Idle;

    /// <summary>Current cutting mode (Manual or Auto).</summary>
    public CutterMode CurrentMode { get; private set; } = CutterMode.Manual;

    /// <summary>Currently loaded path data.</summary>
    public PathData LoadedPath => _loadedPath;

    /// <summary>Progress through the current path (0-1).</summary>
    public float PathProgress { get; private set; }

    /// <summary>Current waypoint index during path following.</summary>
    public int CurrentWaypointIndex { get; private set; }

    /// <summary>Is the machine currently in a cutting state?</summary>
    public bool IsCutting => CurrentState == CNCState.Cutting || CurrentState == CNCState.FollowingPath;

    /// <summary>Is a workpiece currently loaded in the machine?</summary>
    public bool HasWorkpiece => _currentWorkpiece != null;

    /// <summary>The cutter component.</summary>
    public CNCCutterExtended Cutter => _cutter;

    /// <summary>Available paths for selection.</summary>
    public List<PathData> AvailablePaths => _availablePaths;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private float _positioningTimer;
    private GameObject _currentWorkpiece;
    private int _currentPass;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // Auto-find cutter if not assigned
        if (_cutter == null)
            _cutter = GetComponentInChildren<CNCCutterExtended>();

        if (_cutter == null)
            Debug.LogWarning($"[CNCMachineExtended] No CNCCutterExtended found on {name} or its children.", this);

        // Auto-find result generator if not assigned
        if (_resultGenerator == null)
            _resultGenerator = GetComponent<CNCResultGenerator>();

        CurrentMode = _defaultMode;
    }

    private void OnEnable()
    {
        // Subscribe to loading zone events
        if (_loadingZone != null)
        {
            _loadingZone.OnObjectArrived += HandleWorkpieceArrived;
            _loadingZone.OnObjectLeft += HandleWorkpieceLeft;
        }
    }

    private void OnDisable()
    {
        if (_loadingZone != null)
        {
            _loadingZone.OnObjectArrived -= HandleWorkpieceArrived;
            _loadingZone.OnObjectLeft -= HandleWorkpieceLeft;
        }
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case CNCState.Positioning:
                TickPositioning();
                break;

            case CNCState.FollowingPath:
                TickPathFollowing();
                break;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Path Management
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Loads a path for automatic cutting.
    /// </summary>
    /// <param name="path">The PathData to load.</param>
    /// <returns>True if path was loaded successfully.</returns>
    public bool LoadPath(PathData path)
    {
        if (path == null)
        {
            Debug.LogWarning("[CNCMachineExtended] Cannot load null path.");
            return false;
        }

        if (!path.IsValid())
        {
            Debug.LogWarning($"[CNCMachineExtended] Path '{path.pathName}' is not valid.");
            return false;
        }

        _loadedPath = path;
        CurrentWaypointIndex = 0;
        PathProgress = 0f;
        _currentPass = 0;

        OnPathLoaded?.Invoke(path);
        GameStateEvents.RaisePathLoaded(path);

        if (_verboseLogging)
            Debug.Log($"[CNCMachineExtended] Loaded path: {path.pathName}");

        return true;
    }

    /// <summary>
    /// Loads a path by index from the available paths list.
    /// </summary>
    /// <param name="index">Index in the available paths list.</param>
    /// <returns>True if path was loaded successfully.</returns>
    public bool LoadPathByIndex(int index)
    {
        if (index < 0 || index >= _availablePaths.Count)
        {
            Debug.LogWarning($"[CNCMachineExtended] Path index {index} out of range.");
            return false;
        }

        return LoadPath(_availablePaths[index]);
    }

    /// <summary>
    /// Clears the currently loaded path.
    /// </summary>
    public void ClearPath()
    {
        _loadedPath = null;
        CurrentWaypointIndex = 0;
        PathProgress = 0f;

        if (_verboseLogging)
            Debug.Log("[CNCMachineExtended] Path cleared.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Cutting Operations
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts automatic cutting using the loaded path.
    /// </summary>
    /// <returns>True if cutting started successfully.</returns>
    public bool StartAutoCut()
    {
        if (_loadedPath == null)
        {
            Debug.LogWarning("[CNCMachineExtended] Cannot start auto cut - no path loaded.");
            return false;
        }

        return StartAutoCut(_loadedPath);
    }

    /// <summary>
    /// Starts automatic cutting with a specific path.
    /// </summary>
    /// <param name="path">The PathData to follow.</param>
    /// <returns>True if cutting started successfully.</returns>
    public bool StartAutoCut(PathData path)
    {
        if (CurrentState != CNCState.Idle)
        {
            Debug.LogWarning($"[CNCMachineExtended] Cannot start auto cut - current state is {CurrentState}.");
            return false;
        }

        if (!CanStartCutting())
            return false;

        if (!LoadPath(path))
            return false;

        CurrentMode = CutterMode.Auto;
        _currentPass = 1;

        TransitionTo(CNCState.Positioning);
        return true;
    }

    /// <summary>
    /// Starts manual cutting (joystick-controlled).
    /// </summary>
    /// <returns>True if cutting started successfully.</returns>
    public bool StartManualCut()
    {
        if (CurrentState != CNCState.Idle)
        {
            Debug.LogWarning($"[CNCMachineExtended] Cannot start manual cut - current state is {CurrentState}.");
            return false;
        }

        if (!CanStartCutting())
            return false;

        CurrentMode = CutterMode.Manual;
        _loadedPath = null;

        TransitionTo(CNCState.Positioning);
        return true;
    }

    /// <summary>
    /// Starts cutting based on current mode and loaded path.
    /// </summary>
    /// <returns>True if cutting started successfully.</returns>
    public bool StartCut()
    {
        if (CurrentMode == CutterMode.Auto && _loadedPath != null)
            return StartAutoCut();
        else
            return StartManualCut();
    }

    /// <summary>
    /// Stops the current cutting operation.
    /// </summary>
    public void StopCut()
    {
        if (CurrentState != CNCState.Cutting && 
            CurrentState != CNCState.FollowingPath &&
            CurrentState != CNCState.Positioning)
        {
            if (_verboseLogging)
                Debug.Log($"[CNCMachineExtended] StopCut() ignored - current state is {CurrentState}.");
            return;
        }

        TransitionTo(CNCState.Done);
    }

    /// <summary>
    /// Emergency stop - immediately halts all operations.
    /// </summary>
    public void EmergencyStop()
    {
        CancelInvoke();
        
        if (_cutter != null)
        {
            _cutter.SetEnabled(false);
            _cutter.EmergencyStop();
        }

        TransitionTo(CNCState.Idle);

        // Raise safety event
        var safetyEvent = new SafetyEvent(
            SafetyType.Emergency,
            3,
            "Emergency stop activated",
            transform.position
        );
        GameStateEvents.RaiseSafetyViolation(safetyEvent);

        Debug.LogWarning("[CNCMachineExtended] EMERGENCY STOP!");
    }

    /// <summary>
    /// Resets the machine to Idle state.
    /// </summary>
    public void Reset()
    {
        if (CurrentState == CNCState.Idle)
            return;

        if (CurrentState != CNCState.Done)
        {
            Debug.LogWarning($"[CNCMachineExtended] Reset() called while in {CurrentState} state. " +
                             "Call StopCut() first to end the current operation.", this);
            return;
        }

        TransitionTo(CNCState.Idle);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Mode Control
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sets the cutting mode.
    /// </summary>
    /// <param name="mode">The cutting mode to set.</param>
    public void SetMode(CutterMode mode)
    {
        if (IsCutting)
        {
            Debug.LogWarning("[CNCMachineExtended] Cannot change mode while cutting.");
            return;
        }

        CurrentMode = mode;

        if (_cutter != null)
            _cutter.SetMode(mode);

        if (_verboseLogging)
            Debug.Log($"[CNCMachineExtended] Mode set to: {mode}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - State Machine
    // ══════════════════════════════════════════════════════════════════════════

    private void TransitionTo(CNCState nextState)
    {
        CancelInvoke(nameof(Reset));

        CNCState previousState = CurrentState;
        ExitState(previousState);
        CurrentState = nextState;
        EnterState(nextState);

        OnStateChanged?.Invoke(nextState);
        GameStateEvents.RaiseCNCStateChanged(nextState);

        if (_verboseLogging)
            Debug.Log($"[CNCMachineExtended] State: {previousState} → {nextState}");
    }

    private void EnterState(CNCState state)
    {
        switch (state)
        {
            case CNCState.Idle:
                _cutter?.SetEnabled(false);
                _cutter?.SetMode(CutterMode.Manual);
                PathProgress = 0f;
                CurrentWaypointIndex = 0;
                break;

            case CNCState.Positioning:
                _positioningTimer = 0f;
                _cutter?.SetEnabled(false);
                _cutter?.ClearRecordedPath();
                _cutter?.MoveToStart(_loadedPath);
                FeedbackManager.Instance?.PlayCNCStartSound();
                break;

            case CNCState.FollowingPath:
                _cutter?.SetEnabled(true);
                _cutter?.SetMode(CutterMode.Auto);
                _cutter?.Plunge(_loadedPath?.plungeDepth ?? 0.02f);
                FeedbackManager.Instance?.StartCNCCuttingLoop();
                break;

            case CNCState.Cutting:
                _cutter?.SetEnabled(true);
                _cutter?.SetMode(CutterMode.Manual);
                FeedbackManager.Instance?.StartCNCCuttingLoop();
                break;

            case CNCState.Done:
                _cutter?.SetEnabled(false);
                _cutter?.Retract();
                FeedbackManager.Instance?.StopCNCCuttingLoop();
                FeedbackManager.Instance?.PlayCNCStopSound();

                // Generate result if we have a result generator and were in auto mode
                if (CurrentMode == CutterMode.Auto && _resultGenerator != null)
                {
                    _resultGenerator.GenerateResult(_currentWorkpiece, _cutter?.GetRecordedPath(), _loadedPath);
                }

                OnCutComplete?.Invoke();

                // Auto-reset if configured
                if (_autoResetDelay > 0f)
                    Invoke(nameof(Reset), _autoResetDelay);
                break;
        }
    }

    private void ExitState(CNCState state)
    {
        // Reserved for per-state cleanup if needed
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - State Tick Methods
    // ══════════════════════════════════════════════════════════════════════════

    private void TickPositioning()
    {
        _positioningTimer += Time.deltaTime;

        if (_positioningTimer >= _positioningDuration)
        {
            if (CurrentMode == CutterMode.Auto && _loadedPath != null)
                TransitionTo(CNCState.FollowingPath);
            else
                TransitionTo(CNCState.Cutting);
        }
    }

    private void TickPathFollowing()
    {
        if (_loadedPath == null || _cutter == null)
        {
            TransitionTo(CNCState.Done);
            return;
        }

        // Let the cutter handle the path following
        bool reachedEnd = _cutter.FollowPathStep(
            _loadedPath,
            ref CurrentWaypointIndex,
            out PathProgress
        );

        // Report progress
        OnCutProgress?.Invoke(PathProgress);
        GameStateEvents.RaiseCutProgress(PathProgress);

        if (reachedEnd)
        {
            // Check if we need more passes
            if (_currentPass < _loadedPath.passes)
            {
                _currentPass++;
                CurrentWaypointIndex = 0;
                
                // Retract and reposition for next pass
                _cutter.Retract();
                _cutter.MoveToStart(_loadedPath);
                _cutter.Plunge(_loadedPath.plungeDepth * _currentPass);
                
                if (_verboseLogging)
                    Debug.Log($"[CNCMachineExtended] Starting pass {_currentPass} of {_loadedPath.passes}");
            }
            else
            {
                TransitionTo(CNCState.Done);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Workpiece Handling
    // ══════════════════════════════════════════════════════════════════════════

    private void HandleWorkpieceArrived(GameObject workpiece)
    {
        _currentWorkpiece = workpiece;

        if (_verboseLogging)
            Debug.Log($"[CNCMachineExtended] Workpiece loaded: {workpiece.name}");

        // Notify result generator
        if (_resultGenerator != null)
            _resultGenerator.SetCurrentWorkpiece(workpiece);
    }

    private void HandleWorkpieceLeft(GameObject workpiece)
    {
        if (_currentWorkpiece == workpiece)
        {
            _currentWorkpiece = null;

            if (_verboseLogging)
                Debug.Log($"[CNCMachineExtended] Workpiece removed: {workpiece.name}");
        }
    }

    private bool CanStartCutting()
    {
        if (_requireWorkpiece && _currentWorkpiece == null)
        {
            Debug.LogWarning("[CNCMachineExtended] Cannot start cutting - no workpiece loaded.");
            
            var safetyEvent = new SafetyEvent(
                SafetyType.NoWorkpieceLoaded,
                1,
                "No workpiece loaded",
                transform.position
            );
            GameStateEvents.RaiseSafetyViolation(safetyEvent);
            
            return false;
        }

        // Check if workpiece can be cut
        if (_currentWorkpiece != null)
        {
            Workpiece wp = _currentWorkpiece.GetComponent<Workpiece>();
            if (wp != null && !wp.CanBeCut)
            {
                Debug.LogWarning("[CNCMachineExtended] Cannot start cutting - workpiece cannot be cut further.");
                return false;
            }
        }

        return true;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw current state
        string stateText = $"CNC: {CurrentState}\nMode: {CurrentMode}";
        if (_loadedPath != null)
            stateText += $"\nPath: {_loadedPath.pathName}";
        if (IsCutting)
            stateText += $"\nProgress: {PathProgress:P0}";

        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, stateText);
    }
#endif
}
