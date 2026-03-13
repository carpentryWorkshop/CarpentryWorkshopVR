using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Abstract base class for all machine control panels in the workshop.
/// Subclasses implement <see cref="UpdateDisplay"/> to refresh their specific UI.
///
/// Holds shared references (Canvas, machine) and exposes inspector-wired
/// <see cref="UnityEvent"/>s so designers can hook up buttons without code changes.
/// </summary>
public abstract class ControlPanel : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Panel References")]
    [Tooltip("World-space Canvas that hosts this panel's UI elements.")]
    [SerializeField] private Canvas _panelCanvas;

    [Header("Panel Events (inspector-wired)")]
    [Tooltip("Fired when the Start button is pressed on this panel.")]
    [SerializeField] protected UnityEvent _onStartPressed;

    [Tooltip("Fired when the Stop button is pressed on this panel.")]
    [SerializeField] protected UnityEvent _onStopPressed;

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>The world-space Canvas for this panel.</summary>
    protected Canvas PanelCanvas => _panelCanvas;

    /// <summary>Whether the panel is currently interactive.</summary>
    public bool IsInteractable { get; private set; } = true;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        if (_panelCanvas == null)
            _panelCanvas = GetComponentInChildren<Canvas>();
    }

    protected virtual void Start()
    {
        UpdateDisplay();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by physical panel buttons (via UnityEvent or VR interaction) to
    /// trigger the start action.
    /// </summary>
    public virtual void PressStart()
    {
        if (!IsInteractable) return;
        _onStartPressed?.Invoke();
    }

    /// <summary>
    /// Called by physical panel buttons (via UnityEvent or VR interaction) to
    /// trigger the stop action.
    /// </summary>
    public virtual void PressStop()
    {
        if (!IsInteractable) return;
        _onStopPressed?.Invoke();
    }

    /// <summary>
    /// Enables or disables all panel interactions (e.g. while a safety event is active).
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        IsInteractable = interactable;

        if (_panelCanvas != null)
        {
            var canvasGroup = _panelCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.interactable = interactable;
                canvasGroup.alpha = interactable ? 1f : 0.5f;
            }
        }
    }

    // ── Abstract interface ────────────────────────────────────────────────────

    /// <summary>
    /// Refreshes all visible display elements (text, indicators, speed readouts).
    /// Called on Start and whenever machine state changes.
    /// Subclasses must implement this.
    /// </summary>
    public abstract void UpdateDisplay();
}
