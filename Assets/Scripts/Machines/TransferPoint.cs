using System;
using UnityEngine;

/// <summary>
/// Detects when workpieces arrive at a specific location.
/// 
/// Features:
/// - Trigger-based detection
/// - Tag filtering
/// - Event firing for workflow automation
/// - Visual debugging with Gizmos
/// 
/// Setup:
/// 1. Add this component to a GameObject with a BoxCollider (set to trigger)
/// 2. Position at the transfer location (conveyor endpoint, CNC loading zone, etc.)
/// 3. Subscribe to OnObjectArrived event or use GameStateEvents
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TransferPoint : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Transfer Settings")]
    [Tooltip("Unique identifier for this transfer point.")]
    [SerializeField] private string _pointId = "transfer_point";

    [Tooltip("Display name for this transfer point.")]
    [SerializeField] private string _displayName = "Transfer Point";

    [Tooltip("Tag to filter incoming objects. Leave empty to accept all.")]
    [SerializeField] private string _expectedTag = "Workpiece";

    [Tooltip("Is this transfer point currently active?")]
    [SerializeField] private bool _isActive = true;

    [Header("Behavior")]
    [Tooltip("Stop objects when they reach this point (set kinematic).")]
    [SerializeField] private bool _stopObjectsOnArrival = false;

    [Tooltip("Snap objects to this point's position on arrival.")]
    [SerializeField] private bool _snapToPosition = false;

    [Tooltip("Snap objects to this point's rotation on arrival.")]
    [SerializeField] private bool _snapToRotation = false;

    [Tooltip("Delay before raising arrival event (seconds).")]
    [SerializeField] [Range(0f, 2f)] private float _arrivalDelay = 0f;

    [Header("Capacity")]
    [Tooltip("Maximum objects that can be at this point (0 = unlimited).")]
    [SerializeField] private int _maxCapacity = 1;

    [Tooltip("Current number of objects at this point.")]
    [SerializeField] private int _currentCount;

    [Header("Visual Debug")]
    [Tooltip("Color of the Gizmo when active.")]
    [SerializeField] private Color _activeColor = Color.green;

    [Tooltip("Color of the Gizmo when inactive.")]
    [SerializeField] private Color _inactiveColor = Color.red;

    [Tooltip("Color of the Gizmo when at capacity.")]
    [SerializeField] private Color _fullColor = Color.yellow;

    [Tooltip("Show Gizmo in editor.")]
    [SerializeField] private bool _showGizmo = true;

    // ══════════════════════════════════════════════════════════════════════════
    // EVENTS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Fires when an object arrives at this transfer point.</summary>
    public event Action<GameObject> OnObjectArrived;

    /// <summary>Fires when an object leaves this transfer point.</summary>
    public event Action<GameObject> OnObjectLeft;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private BoxCollider _trigger;
    private GameObject _currentObject;

    // ══════════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Unique identifier for this transfer point.</summary>
    public string PointId => _pointId;

    /// <summary>Display name of this transfer point.</summary>
    public string DisplayName => _displayName;

    /// <summary>Is this transfer point currently active?</summary>
    public bool IsActive => _isActive;

    /// <summary>Is this transfer point at capacity?</summary>
    public bool IsFull => _maxCapacity > 0 && _currentCount >= _maxCapacity;

    /// <summary>Can this transfer point accept more objects?</summary>
    public bool CanAccept => _isActive && !IsFull;

    /// <summary>Number of objects currently at this point.</summary>
    public int CurrentCount => _currentCount;

    /// <summary>The most recent object to arrive (if stopOnArrival is enabled).</summary>
    public GameObject CurrentObject => _currentObject;

    /// <summary>World position of this transfer point.</summary>
    public Vector3 Position => transform.position;

    /// <summary>World rotation of this transfer point.</summary>
    public Quaternion Rotation => transform.rotation;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        _trigger = GetComponent<BoxCollider>();
        _trigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!CanAccept)
            return;

        // Check tag filter
        if (!string.IsNullOrEmpty(_expectedTag) && !other.CompareTag(_expectedTag))
            return;

        // Get the root object (in case we hit a child collider)
        GameObject obj = other.attachedRigidbody != null 
            ? other.attachedRigidbody.gameObject 
            : other.gameObject;

        // Handle arrival with optional delay
        if (_arrivalDelay > 0f)
        {
            StartCoroutine(HandleArrivalDelayed(obj, _arrivalDelay));
        }
        else
        {
            HandleArrival(obj);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_isActive)
            return;

        // Check tag filter
        if (!string.IsNullOrEmpty(_expectedTag) && !other.CompareTag(_expectedTag))
            return;

        GameObject obj = other.attachedRigidbody != null 
            ? other.attachedRigidbody.gameObject 
            : other.gameObject;

        HandleDeparture(obj);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Enables or disables this transfer point.
    /// </summary>
    /// <param name="active">True to enable.</param>
    public void SetActive(bool active)
    {
        _isActive = active;
        Debug.Log($"[TransferPoint] {_displayName} is now {(active ? "active" : "inactive")}");
    }

    /// <summary>
    /// Releases the current object, allowing it to move freely.
    /// </summary>
    public void ReleaseObject()
    {
        if (_currentObject == null)
            return;

        Rigidbody rb = _currentObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        Workpiece wp = _currentObject.GetComponent<Workpiece>();
        if (wp != null)
        {
            wp.SetProcessing(false);
        }

        _currentObject = null;

        Debug.Log($"[TransferPoint] {_displayName} released object");
    }

    /// <summary>
    /// Manually registers an object at this transfer point.
    /// </summary>
    /// <param name="obj">Object to register.</param>
    public void RegisterObject(GameObject obj)
    {
        if (!CanAccept || obj == null)
            return;

        HandleArrival(obj);
    }

    /// <summary>
    /// Resets the transfer point state.
    /// </summary>
    public void Reset()
    {
        _currentCount = 0;
        _currentObject = null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private void HandleArrival(GameObject obj)
    {
        _currentCount++;

        // Stop object if configured
        if (_stopObjectsOnArrival)
        {
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            _currentObject = obj;
        }

        // Snap position/rotation if configured
        if (_snapToPosition)
        {
            obj.transform.position = transform.position;
        }

        if (_snapToRotation)
        {
            obj.transform.rotation = transform.rotation;
        }

        // Mark workpiece as processing
        Workpiece wp = obj.GetComponent<Workpiece>();
        if (wp != null && _stopObjectsOnArrival)
        {
            wp.SetProcessing(true);
        }

        Debug.Log($"[TransferPoint] Object arrived at {_displayName}: {obj.name}");

        // Fire events
        OnObjectArrived?.Invoke(obj);
        GameStateEvents.RaiseWorkpieceTransferred(obj, this);
    }

    private System.Collections.IEnumerator HandleArrivalDelayed(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);

        // Check if object is still valid
        if (obj != null && CanAccept)
        {
            HandleArrival(obj);
        }
    }

    private void HandleDeparture(GameObject obj)
    {
        _currentCount = Mathf.Max(0, _currentCount - 1);

        if (_currentObject == obj)
        {
            _currentObject = null;
        }

        Debug.Log($"[TransferPoint] Object left {_displayName}: {obj.name}");

        OnObjectLeft?.Invoke(obj);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_showGizmo)
            return;

        DrawTransferPointGizmo(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawTransferPointGizmo(true);
    }

    private void DrawTransferPointGizmo(bool selected)
    {
        // Determine color based on state
        Color color;
        if (!_isActive)
            color = _inactiveColor;
        else if (IsFull)
            color = _fullColor;
        else
            color = _activeColor;

        // Get collider bounds
        BoxCollider col = GetComponent<BoxCollider>();
        Vector3 size = col != null ? col.size : Vector3.one * 0.5f;
        Vector3 center = col != null ? col.center : Vector3.zero;

        Gizmos.matrix = transform.localToWorldMatrix;

        // Draw wire cube
        Gizmos.color = color;
        Gizmos.DrawWireCube(center, size);

        // Draw semi-transparent fill when selected
        if (selected)
        {
            color.a = 0.2f;
            Gizmos.color = color;
            Gizmos.DrawCube(center, size);
        }

        Gizmos.matrix = Matrix4x4.identity;

        // Draw label
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, _displayName);
        #endif
    }
#endif
}
