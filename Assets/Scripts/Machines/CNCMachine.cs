using System;
using UnityEngine;

/// <summary>
/// State machine for the CNC router machine.
/// External systems (control panel, task manager) call <see cref="StartCut"/> and
/// <see cref="StopCut"/>; internal state transitions drive <see cref="CNCCutter"/>.
///
/// State flow:
///   Idle ──StartCut()──► Positioning ──Ready()──► Cutting ──StopCut()/done──► Done ──Reset()──► Idle
/// </summary>
public class CNCMachine : MonoBehaviour
{
    // ── State enum ────────────────────────────────────────────────────────────

    public enum CNCState
    {
        Idle,
        Positioning,
        Cutting,
        Done
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The CNCCutter component that physically moves the tool head.")]
    [SerializeField] private CNCCutter _cutter;

    [Header("Behaviour")]
    [Tooltip("Seconds spent in the Positioning state before cutting begins.")]
    [SerializeField] [Range(0f, 5f)] private float _positioningDuration = 1f;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fires whenever the machine transitions to a new state.</summary>
    public event Action<CNCState> OnStateChanged;

    /// <summary>Fires when the machine reaches the Done state.</summary>
    public event Action OnCutComplete;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Current state of the CNC machine.</summary>
    public CNCState CurrentState { get; private set; } = CNCState.Idle;

    // ── Private state ─────────────────────────────────────────────────────────

    private float _positioningTimer;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (_cutter == null)
            _cutter = GetComponentInChildren<CNCCutter>();

        if (_cutter == null)
            Debug.LogWarning($"[CNCMachine] No CNCCutter found on {name} or its children. " +
                             "Assign it in the Inspector.", this);
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case CNCState.Positioning:
                TickPositioning();
                break;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Begins the cut sequence from Idle. Transitions through Positioning → Cutting.
    /// No-op if the machine is not in the Idle state.
    /// </summary>
    public void StartCut()
    {
        if (CurrentState != CNCState.Idle)
        {
            Debug.Log($"[CNCMachine] StartCut() ignored — current state is {CurrentState}.");
            return;
        }

        TransitionTo(CNCState.Positioning);
    }

    /// <summary>
    /// Halts cutting and transitions to Done, then back to Idle after one frame.
    /// Can be called from the control panel Stop button or by the task manager.
    /// </summary>
    public void StopCut()
    {
        if (CurrentState != CNCState.Cutting && CurrentState != CNCState.Positioning)
        {
            Debug.Log($"[CNCMachine] StopCut() ignored — current state is {CurrentState}.");
            return;
        }

        TransitionTo(CNCState.Done);
    }

    /// <summary>
    /// Resets the machine to Idle. Must only be called from the Done state
    /// (it is called automatically via Invoke after the Done state is entered).
    /// Calling this mid-cut (Positioning or Cutting) is a no-op and logs a warning.
    /// </summary>
    public void Reset()
    {
        if (CurrentState == CNCState.Idle) return;

        if (CurrentState != CNCState.Done)
        {
            Debug.LogWarning($"[CNCMachine] Reset() called while in {CurrentState} state. " +
                             "Call StopCut() first to end the current operation.", this);
            return;
        }

        TransitionTo(CNCState.Idle);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void TickPositioning()
    {
        _positioningTimer += Time.deltaTime;
        if (_positioningTimer >= _positioningDuration)
            TransitionTo(CNCState.Cutting);
    }

    private void TransitionTo(CNCState next)
    {
        CancelInvoke(nameof(Reset));
        ExitState(CurrentState);
        CurrentState = next;
        EnterState(next);
        OnStateChanged?.Invoke(next);
    }

    private void EnterState(CNCState state)
    {
        switch (state)
        {
            case CNCState.Idle:
                _cutter?.SetEnabled(false);
                break;

            case CNCState.Positioning:
                _positioningTimer = 0f;
                _cutter?.SetEnabled(false);
                break;

            case CNCState.Cutting:
                _cutter?.SetEnabled(true);
                break;

            case CNCState.Done:
                _cutter?.SetEnabled(false);
                OnCutComplete?.Invoke();
                // Auto-return to Idle so the panel can start a new cycle
                Invoke(nameof(Reset), 0.1f);
                break;
        }
    }

    private void ExitState(CNCState state)
    {
        // Reserved for per-state cleanup if needed in future phases
        _ = state;
    }
}
