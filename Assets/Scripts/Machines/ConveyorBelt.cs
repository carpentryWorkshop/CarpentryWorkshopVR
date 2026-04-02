using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves objects along a conveyor belt surface.
/// 
/// Features:
/// - Physics-based movement via OnTriggerStay
/// - Configurable speed and direction
/// - Start/Stop control
/// - Reversible direction
/// - Tag filtering
/// 
/// Setup:
/// 1. Add this component to the conveyor belt GameObject
/// 2. Add a BoxCollider (set to trigger) covering the belt surface
/// 3. Set the movement direction and speed
/// 4. Optionally enable auto-start
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ConveyorBelt : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Movement
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Movement")]
    [Tooltip("Direction of movement in local space (typically forward = Z).")]
    [SerializeField] private Vector3 _direction = Vector3.forward;

    [Tooltip("Speed of the conveyor belt in meters per second.")]
    [SerializeField] [Range(0f, 5f)] private float _speed = 0.5f;

    [Tooltip("How quickly objects accelerate to belt speed (higher = snappier).")]
    [SerializeField] [Range(0.1f, 1f)] private float _acceleration = 0.8f;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Behavior
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Behavior")]
    [Tooltip("Start the conveyor running when the scene loads.")]
    [SerializeField] private bool _autoStart = false;

    [Tooltip("Tag of objects that should be moved by this belt. Empty = all objects.")]
    [SerializeField] private string _objectTag = "Workpiece";

    [Tooltip("Only affect objects with Rigidbody components.")]
    [SerializeField] private bool _requireRigidbody = true;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Visual Feedback
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Visual Feedback")]
    [Tooltip("Animate the belt texture UV offset.")]
    [SerializeField] private bool _animateTexture = true;

    [Tooltip("The Renderer component for the belt surface.")]
    [SerializeField] private Renderer _beltRenderer;

    [Tooltip("Name of the texture property to animate (e.g., _MainTex, _BaseMap).")]
    [SerializeField] private string _textureProperty = "_BaseMap";

    [Tooltip("Texture scroll speed multiplier.")]
    [SerializeField] private float _textureScrollMultiplier = 1f;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Audio
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Audio")]
    [Tooltip("Audio source for conveyor sounds.")]
    [SerializeField] private AudioSource _audioSource;

    [Tooltip("Sound to play while conveyor is running.")]
    [SerializeField] private AudioClip _runningSound;

    [Tooltip("Sound to play when conveyor starts.")]
    [SerializeField] private AudioClip _startSound;

    [Tooltip("Sound to play when conveyor stops.")]
    [SerializeField] private AudioClip _stopSound;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Debug
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Debug")]
    [Tooltip("Show conveyor direction in editor.")]
    [SerializeField] private bool _showGizmo = true;

    [Tooltip("Log conveyor events to console.")]
    [SerializeField] private bool _verboseLogging = false;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private BoxCollider _trigger;
    private bool _isRunning;
    private HashSet<Rigidbody> _objectsOnBelt;
    private Material _beltMaterial;
    private Vector2 _textureOffset;

    // ══════════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Is the conveyor belt currently running?</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Current speed of the conveyor belt.</summary>
    public float Speed => _speed;

    /// <summary>Number of objects currently on the belt.</summary>
    public int ObjectCount => _objectsOnBelt?.Count ?? 0;

    /// <summary>Movement direction in world space.</summary>
    public Vector3 WorldDirection => transform.TransformDirection(_direction.normalized);

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        _trigger = GetComponent<BoxCollider>();
        _trigger.isTrigger = true;
        _objectsOnBelt = new HashSet<Rigidbody>();

        // Get material instance for texture animation
        if (_animateTexture && _beltRenderer != null)
        {
            _beltMaterial = _beltRenderer.material; // Creates instance
        }

        // Setup audio source if not assigned
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
    }

    private void Start()
    {
        if (_autoStart)
        {
            StartBelt();
        }
    }

    private void FixedUpdate()
    {
        if (!_isRunning)
            return;

        MoveObjectsOnBelt();
    }

    private void Update()
    {
        if (!_isRunning)
            return;

        AnimateBeltTexture();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isRunning)
            return;

        TryAddObject(other);
    }

    private void OnTriggerExit(Collider other)
    {
        TryRemoveObject(other);
    }

    private void OnDestroy()
    {
        // Clean up material instance
        if (_beltMaterial != null)
        {
            Destroy(_beltMaterial);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Belt Control
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts the conveyor belt.
    /// </summary>
    public void StartBelt()
    {
        if (_isRunning)
            return;

        _isRunning = true;

        // Play start sound
        if (_audioSource != null && _startSound != null)
        {
            _audioSource.PlayOneShot(_startSound);
        }

        // Start running sound loop
        if (_audioSource != null && _runningSound != null)
        {
            _audioSource.clip = _runningSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }

        GameStateEvents.RaiseConveyorStateChanged(this, true);

        if (_verboseLogging)
            Debug.Log($"[ConveyorBelt] {gameObject.name} started");
    }

    /// <summary>
    /// Stops the conveyor belt.
    /// </summary>
    public void StopBelt()
    {
        if (!_isRunning)
            return;

        _isRunning = false;

        // Stop running sound
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }

        // Play stop sound
        if (_audioSource != null && _stopSound != null)
        {
            _audioSource.PlayOneShot(_stopSound);
        }

        GameStateEvents.RaiseConveyorStateChanged(this, false);

        if (_verboseLogging)
            Debug.Log($"[ConveyorBelt] {gameObject.name} stopped");
    }

    /// <summary>
    /// Toggles the conveyor belt on/off.
    /// </summary>
    public void Toggle()
    {
        if (_isRunning)
            StopBelt();
        else
            StartBelt();
    }

    /// <summary>
    /// Reverses the direction of the conveyor belt.
    /// </summary>
    public void Reverse()
    {
        _direction = -_direction;

        if (_verboseLogging)
            Debug.Log($"[ConveyorBelt] {gameObject.name} reversed direction");
    }

    /// <summary>
    /// Sets the speed of the conveyor belt.
    /// </summary>
    /// <param name="speed">New speed in meters per second.</param>
    public void SetSpeed(float speed)
    {
        _speed = Mathf.Max(0f, speed);

        if (_verboseLogging)
            Debug.Log($"[ConveyorBelt] {gameObject.name} speed set to {_speed}");
    }

    /// <summary>
    /// Sets the movement direction.
    /// </summary>
    /// <param name="direction">New direction in local space.</param>
    public void SetDirection(Vector3 direction)
    {
        _direction = direction.normalized;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private void MoveObjectsOnBelt()
    {
        if (_objectsOnBelt.Count == 0)
            return;

        Vector3 worldDirection = WorldDirection;
        Vector3 targetVelocity = worldDirection * _speed;

        // Clean up null references and move objects
        _objectsOnBelt.RemoveWhere(rb => rb == null);

        foreach (Rigidbody rb in _objectsOnBelt)
        {
            if (rb == null || rb.isKinematic)
                continue;

            // Smoothly accelerate object to belt speed
            // Only affect horizontal velocity (preserve vertical for gravity)
            Vector3 currentVel = rb.velocity;
            Vector3 horizontalTarget = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
            Vector3 horizontalCurrent = new Vector3(currentVel.x, 0f, currentVel.z);

            Vector3 newHorizontal = Vector3.Lerp(horizontalCurrent, horizontalTarget, _acceleration);
            rb.velocity = new Vector3(newHorizontal.x, currentVel.y, newHorizontal.z);
        }
    }

    private void AnimateBeltTexture()
    {
        if (!_animateTexture || _beltMaterial == null)
            return;

        // Calculate scroll based on direction and speed
        float scroll = _speed * _textureScrollMultiplier * Time.deltaTime;

        // Assuming belt moves in local Z (forward) direction
        if (_direction.z > 0)
            _textureOffset.y -= scroll;
        else if (_direction.z < 0)
            _textureOffset.y += scroll;

        if (_direction.x > 0)
            _textureOffset.x -= scroll;
        else if (_direction.x < 0)
            _textureOffset.x += scroll;

        // Keep values in reasonable range
        _textureOffset.x = _textureOffset.x % 1f;
        _textureOffset.y = _textureOffset.y % 1f;

        _beltMaterial.SetTextureOffset(_textureProperty, _textureOffset);
    }

    private void TryAddObject(Collider other)
    {
        // Check tag filter
        if (!string.IsNullOrEmpty(_objectTag) && !other.CompareTag(_objectTag))
            return;

        Rigidbody rb = other.attachedRigidbody;

        if (_requireRigidbody && rb == null)
            return;

        if (rb != null && !_objectsOnBelt.Contains(rb))
        {
            _objectsOnBelt.Add(rb);

            if (_verboseLogging)
                Debug.Log($"[ConveyorBelt] Object entered: {other.gameObject.name}");
        }
    }

    private void TryRemoveObject(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;

        if (rb != null && _objectsOnBelt.Contains(rb))
        {
            _objectsOnBelt.Remove(rb);

            if (_verboseLogging)
                Debug.Log($"[ConveyorBelt] Object exited: {other.gameObject.name}");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_showGizmo)
            return;

        DrawConveyorGizmo(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawConveyorGizmo(true);
    }

    private void DrawConveyorGizmo(bool selected)
    {
        // Get collider for bounds
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
            return;

        Vector3 center = transform.TransformPoint(col.center);
        Vector3 size = Vector3.Scale(col.size, transform.lossyScale);

        // Draw belt outline
        Gizmos.color = _isRunning ? Color.green : Color.gray;
        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);

        if (selected)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
            Gizmos.DrawCube(Vector3.zero, size);
        }

        Gizmos.matrix = Matrix4x4.identity;

        // Draw direction arrow
        Gizmos.color = Color.blue;
        Vector3 worldDir = transform.TransformDirection(_direction.normalized);
        float arrowLength = Mathf.Min(size.x, size.z) * 0.8f;

        Vector3 arrowStart = center - worldDir * arrowLength * 0.5f;
        Vector3 arrowEnd = center + worldDir * arrowLength * 0.5f;

        Gizmos.DrawLine(arrowStart, arrowEnd);

        // Arrowhead
        Vector3 right = Vector3.Cross(worldDir, Vector3.up).normalized * 0.1f;
        Gizmos.DrawLine(arrowEnd, arrowEnd - worldDir * 0.15f + right);
        Gizmos.DrawLine(arrowEnd, arrowEnd - worldDir * 0.15f - right);

        // Speed label
        #if UNITY_EDITOR
        string label = $"{gameObject.name}\n{(_isRunning ? "Running" : "Stopped")}\n{_speed:F2} m/s";
        UnityEditor.Handles.Label(center + Vector3.up * (size.y * 0.5f + 0.2f), label);
        #endif
    }
#endif
}
