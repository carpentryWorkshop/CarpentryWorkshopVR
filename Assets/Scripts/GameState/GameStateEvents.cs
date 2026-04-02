using System;
using UnityEngine;

/// <summary>
/// Static event bus for cross-system communication in the CNC workflow.
/// All events are raised through this class to avoid tight coupling between systems.
/// 
/// Usage:
/// - Subscribe in OnEnable, unsubscribe in OnDisable
/// - Raise events using the static Raise* methods
/// </summary>
public static class GameStateEvents
{
    // ══════════════════════════════════════════════════════════════════════════
    // TASK PROGRESSION EVENTS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Fires when a new task begins. Parameter: task name.</summary>
    public static event Action<string> OnTaskStarted;

    /// <summary>Fires when a task is completed successfully. Parameter: task name.</summary>
    public static event Action<string> OnTaskCompleted;

    /// <summary>Fires when a step within a task is completed. Parameters: step name, step index.</summary>
    public static event Action<string, int> OnStepCompleted;

    /// <summary>Fires when a new step begins. Parameter: step name.</summary>
    public static event Action<string> OnStepStarted;

    // ══════════════════════════════════════════════════════════════════════════
    // CNC MACHINE EVENTS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Fires when CNC machine state changes. Parameter: new state.</summary>
    public static event Action<CNCState> OnCNCStateChanged;

    /// <summary>Fires when a cutting path is loaded into the CNC. Parameter: path data.</summary>
    public static event Action<PathData> OnPathLoaded;

    /// <summary>Fires during path following to report progress. Parameter: 0-1 progress.</summary>
    public static event Action<float> OnCutProgress;

    // ══════════════════════════════════════════════════════════════════════════
    // WORKPIECE EVENTS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Fires when a new workpiece is spawned. Parameter: workpiece GameObject.</summary>
    public static event Action<GameObject> OnWorkpieceSpawned;

    /// <summary>Fires when a workpiece reaches a transfer point. Parameters: workpiece, transfer point.</summary>
    public static event Action<GameObject, TransferPoint> OnWorkpieceTransferred;

    /// <summary>Fires when a workpiece has been cut by the CNC. Parameter: workpiece GameObject.</summary>
    public static event Action<GameObject> OnWorkpieceCut;

    /// <summary>Fires when a workpiece is destroyed/despawned. Parameter: workpiece GameObject.</summary>
    public static event Action<GameObject> OnWorkpieceDespawned;

    // ══════════════════════════════════════════════════════════════════════════
    // SAFETY EVENTS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Fires when a safety violation occurs. Parameter: safety event data.</summary>
    public static event Action<SafetyEvent> OnSafetyViolation;

    // ══════════════════════════════════════════════════════════════════════════
    // SCORE EVENTS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Fires when score changes. Parameters: new total score, reason string.</summary>
    public static event Action<int, string> OnScoreChanged;

    /// <summary>Fires when an error is recorded. Parameter: error type string.</summary>
    public static event Action<string> OnErrorRecorded;

    // ══════════════════════════════════════════════════════════════════════════
    // CONVEYOR EVENTS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Fires when conveyor starts or stops. Parameters: conveyor, is running.</summary>
    public static event Action<ConveyorBelt, bool> OnConveyorStateChanged;

    // ══════════════════════════════════════════════════════════════════════════
    // RAISE METHODS - Task Progression
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Raise when a task starts.</summary>
    public static void RaiseTaskStarted(string taskName)
    {
        Debug.Log($"[GameStateEvents] Task Started: {taskName}");
        OnTaskStarted?.Invoke(taskName);
    }

    /// <summary>Raise when a task completes.</summary>
    public static void RaiseTaskCompleted(string taskName)
    {
        Debug.Log($"[GameStateEvents] Task Completed: {taskName}");
        OnTaskCompleted?.Invoke(taskName);
    }

    /// <summary>Raise when a step completes.</summary>
    public static void RaiseStepCompleted(string stepName, int stepIndex)
    {
        Debug.Log($"[GameStateEvents] Step Completed: {stepName} (index {stepIndex})");
        OnStepCompleted?.Invoke(stepName, stepIndex);
    }

    /// <summary>Raise when a new step starts.</summary>
    public static void RaiseStepStarted(string stepName)
    {
        Debug.Log($"[GameStateEvents] Step Started: {stepName}");
        OnStepStarted?.Invoke(stepName);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RAISE METHODS - CNC Machine
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Raise when CNC state changes.</summary>
    public static void RaiseCNCStateChanged(CNCState state)
    {
        Debug.Log($"[GameStateEvents] CNC State Changed: {state}");
        OnCNCStateChanged?.Invoke(state);
    }

    /// <summary>Raise when a path is loaded.</summary>
    public static void RaisePathLoaded(PathData path)
    {
        string pathName = path != null ? path.pathName : "null";
        Debug.Log($"[GameStateEvents] Path Loaded: {pathName}");
        OnPathLoaded?.Invoke(path);
    }

    /// <summary>Raise cut progress updates.</summary>
    public static void RaiseCutProgress(float progress)
    {
        // Don't log this one - it fires every frame during cutting
        OnCutProgress?.Invoke(progress);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RAISE METHODS - Workpiece
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Raise when a workpiece is spawned.</summary>
    public static void RaiseWorkpieceSpawned(GameObject workpiece)
    {
        string name = workpiece != null ? workpiece.name : "null";
        Debug.Log($"[GameStateEvents] Workpiece Spawned: {name}");
        OnWorkpieceSpawned?.Invoke(workpiece);
    }

    /// <summary>Raise when a workpiece reaches a transfer point.</summary>
    public static void RaiseWorkpieceTransferred(GameObject workpiece, TransferPoint point)
    {
        string wpName = workpiece != null ? workpiece.name : "null";
        string ptName = point != null ? point.name : "null";
        Debug.Log($"[GameStateEvents] Workpiece Transferred: {wpName} → {ptName}");
        OnWorkpieceTransferred?.Invoke(workpiece, point);
    }

    /// <summary>Raise when a workpiece is cut.</summary>
    public static void RaiseWorkpieceCut(GameObject workpiece)
    {
        string name = workpiece != null ? workpiece.name : "null";
        Debug.Log($"[GameStateEvents] Workpiece Cut: {name}");
        OnWorkpieceCut?.Invoke(workpiece);
    }

    /// <summary>Raise when a workpiece is despawned.</summary>
    public static void RaiseWorkpieceDespawned(GameObject workpiece)
    {
        string name = workpiece != null ? workpiece.name : "null";
        Debug.Log($"[GameStateEvents] Workpiece Despawned: {name}");
        OnWorkpieceDespawned?.Invoke(workpiece);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RAISE METHODS - Safety
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Raise a safety violation.</summary>
    public static void RaiseSafetyViolation(SafetyEvent safetyEvent)
    {
        string msg = safetyEvent != null ? safetyEvent.warningMessage : "Unknown";
        Debug.LogWarning($"[GameStateEvents] Safety Violation: {msg}");
        OnSafetyViolation?.Invoke(safetyEvent);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RAISE METHODS - Score
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Raise when score changes.</summary>
    public static void RaiseScoreChanged(int newScore, string reason)
    {
        Debug.Log($"[GameStateEvents] Score Changed: {newScore} ({reason})");
        OnScoreChanged?.Invoke(newScore, reason);
    }

    /// <summary>Raise when an error is recorded.</summary>
    public static void RaiseErrorRecorded(string errorType)
    {
        Debug.LogWarning($"[GameStateEvents] Error Recorded: {errorType}");
        OnErrorRecorded?.Invoke(errorType);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RAISE METHODS - Conveyor
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Raise when conveyor state changes.</summary>
    public static void RaiseConveyorStateChanged(ConveyorBelt conveyor, bool isRunning)
    {
        string name = conveyor != null ? conveyor.name : "null";
        Debug.Log($"[GameStateEvents] Conveyor State Changed: {name} → {(isRunning ? "Running" : "Stopped")}");
        OnConveyorStateChanged?.Invoke(conveyor, isRunning);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UTILITY - Clear all subscribers (for testing/scene reload)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Clears all event subscribers. Call this on scene unload or when resetting the game state.
    /// </summary>
    public static void ClearAllSubscribers()
    {
        OnTaskStarted = null;
        OnTaskCompleted = null;
        OnStepCompleted = null;
        OnStepStarted = null;
        OnCNCStateChanged = null;
        OnPathLoaded = null;
        OnCutProgress = null;
        OnWorkpieceSpawned = null;
        OnWorkpieceTransferred = null;
        OnWorkpieceCut = null;
        OnWorkpieceDespawned = null;
        OnSafetyViolation = null;
        OnScoreChanged = null;
        OnErrorRecorded = null;
        OnConveyorStateChanged = null;

        Debug.Log("[GameStateEvents] All subscribers cleared.");
    }
}

/// <summary>
/// CNC Machine state enum (used by GameStateEvents for type safety)
/// </summary>
public enum CNCState
{
    Idle,
    Positioning,
    FollowingPath,
    Cutting,
    Done
}

/// <summary>
/// Cutter operation mode
/// </summary>
public enum CutterMode
{
    Manual,
    Auto
}
