using UnityEngine;

/// <summary>
/// Component attached to workpiece GameObjects to track their state and data.
/// 
/// This component is automatically added by ObjectSpawner when a workpiece is created.
/// It stores runtime information about the workpiece such as cut count and damage.
/// 
/// Usage:
/// - Attached automatically by ObjectSpawner
/// - Query via GetComponent<Workpiece>() on workpiece GameObjects
/// - Used by CNCResultGenerator to track cutting operations
/// </summary>
public class Workpiece : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR (Read-only at runtime, set by Initialize)
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Workpiece Data")]
    [Tooltip("The WorkpieceData asset this workpiece was created from.")]
    [SerializeField] private WorkpieceData _data;

    [Header("Runtime State")]
    [Tooltip("Number of cuts performed on this workpiece.")]
    [SerializeField] private int _cutCount;

    [Tooltip("Has this workpiece been cut at least once?")]
    [SerializeField] private bool _isCut;

    [Tooltip("Is this workpiece currently being processed by a machine?")]
    [SerializeField] private bool _isBeingProcessed;

    [Tooltip("Current thickness remaining after cuts (meters).")]
    [SerializeField] private float _currentThickness;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private Rigidbody _rigidbody;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Collider _collider;
    private Vector3 _originalDimensions;
    private Mesh _originalMesh;

    // ══════════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>The WorkpieceData asset this workpiece was created from.</summary>
    public WorkpieceData Data => _data;

    /// <summary>Number of cuts performed on this workpiece.</summary>
    public int CutCount => _cutCount;

    /// <summary>Has this workpiece been cut at least once?</summary>
    public bool IsCut => _isCut;

    /// <summary>Is this workpiece currently being processed by a machine?</summary>
    public bool IsBeingProcessed => _isBeingProcessed;

    /// <summary>Current thickness remaining after cuts (meters).</summary>
    public float CurrentThickness => _currentThickness;

    /// <summary>Original dimensions from the WorkpieceData.</summary>
    public Vector3 OriginalDimensions => _originalDimensions;

    /// <summary>Can this workpiece be cut further?</summary>
    public bool CanBeCut => _data != null && 
                            _data.isCuttable && 
                            _cutCount < _data.maxCuts &&
                            _currentThickness > _data.minimumThickness;

    /// <summary>Remaining cuts allowed before workpiece is exhausted.</summary>
    public int RemainingCuts => _data != null ? Mathf.Max(0, _data.maxCuts - _cutCount) : 0;

    /// <summary>The Rigidbody attached to this workpiece (cached).</summary>
    public Rigidbody Rigidbody => _rigidbody;

    /// <summary>The MeshFilter attached to this workpiece (cached).</summary>
    public MeshFilter MeshFilter => _meshFilter;

    /// <summary>The MeshRenderer attached to this workpiece (cached).</summary>
    public MeshRenderer MeshRenderer => _meshRenderer;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        CacheComponents();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Initialization
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initializes this workpiece with the given data. Called by ObjectSpawner.
    /// </summary>
    /// <param name="data">The WorkpieceData to initialize from.</param>
    public void Initialize(WorkpieceData data)
    {
        if (data == null)
        {
            Debug.LogError("[Workpiece] Cannot initialize with null data!");
            return;
        }

        _data = data;
        _cutCount = 0;
        _isCut = false;
        _isBeingProcessed = false;
        _originalDimensions = data.dimensions;
        _currentThickness = data.dimensions.y;

        CacheComponents();
        ApplyPhysicsSettings();
        ApplyMaterials();

        // Store original mesh for potential restoration
        if (_meshFilter != null && _meshFilter.sharedMesh != null)
        {
            _originalMesh = _meshFilter.sharedMesh;
        }

        Debug.Log($"[Workpiece] Initialized: {data.workpieceName}");
    }

    /// <summary>
    /// Resets the workpiece to its initial state (useful for object pooling).
    /// </summary>
    public void Reset()
    {
        _cutCount = 0;
        _isCut = false;
        _isBeingProcessed = false;

        if (_data != null)
        {
            _currentThickness = _data.dimensions.y;
        }

        // Restore original mesh if we have it
        if (_originalMesh != null && _meshFilter != null)
        {
            _meshFilter.sharedMesh = _originalMesh;
        }

        // Reset rigidbody
        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        Debug.Log($"[Workpiece] Reset: {gameObject.name}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Cut Operations
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records that a cut has been performed on this workpiece.
    /// </summary>
    /// <param name="depthRemoved">Depth of material removed in meters.</param>
    public void RecordCut(float depthRemoved = 0f)
    {
        if (!CanBeCut)
        {
            Debug.LogWarning($"[Workpiece] Cannot record cut - workpiece {gameObject.name} cannot be cut further.");
            return;
        }

        _cutCount++;
        _isCut = true;
        _currentThickness = Mathf.Max(_data.minimumThickness, _currentThickness - depthRemoved);

        Debug.Log($"[Workpiece] Cut recorded on {gameObject.name}. " +
                  $"Total cuts: {_cutCount}/{_data.maxCuts}, " +
                  $"Remaining thickness: {_currentThickness:F4}m");
    }

    /// <summary>
    /// Sets whether this workpiece is currently being processed.
    /// </summary>
    /// <param name="processing">True if processing, false otherwise.</param>
    public void SetProcessing(bool processing)
    {
        _isBeingProcessed = processing;

        // Optionally freeze physics while processing
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = processing;
        }

        Debug.Log($"[Workpiece] {gameObject.name} processing: {processing}");
    }

    /// <summary>
    /// Updates the mesh of this workpiece (called after cutting).
    /// </summary>
    /// <param name="newMesh">The new mesh to apply.</param>
    public void UpdateMesh(Mesh newMesh)
    {
        if (_meshFilter == null || newMesh == null)
            return;

        _meshFilter.mesh = newMesh;

        // Update collider if it's a MeshCollider
        if (_collider is MeshCollider meshCollider)
        {
            meshCollider.sharedMesh = newMesh;
        }

        Debug.Log($"[Workpiece] Mesh updated on {gameObject.name}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Physics
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Enables or disables physics simulation on this workpiece.
    /// </summary>
    /// <param name="enabled">True to enable physics.</param>
    public void SetPhysicsEnabled(bool enabled)
    {
        if (_rigidbody == null)
            return;

        _rigidbody.isKinematic = !enabled;
        _rigidbody.useGravity = enabled;
    }

    /// <summary>
    /// Freezes the workpiece in place (zero velocity, kinematic).
    /// </summary>
    public void Freeze()
    {
        if (_rigidbody == null)
            return;

        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.isKinematic = true;
    }

    /// <summary>
    /// Unfreezes the workpiece (enables physics).
    /// </summary>
    public void Unfreeze()
    {
        if (_rigidbody == null)
            return;

        _rigidbody.isKinematic = false;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private void CacheComponents()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();

        if (_meshFilter == null)
            _meshFilter = GetComponent<MeshFilter>();

        if (_meshRenderer == null)
            _meshRenderer = GetComponent<MeshRenderer>();

        if (_collider == null)
            _collider = GetComponent<Collider>();
    }

    private void ApplyPhysicsSettings()
    {
        if (_rigidbody == null || _data == null)
            return;

        _rigidbody.mass = _data.Mass;
        _rigidbody.linearDamping = _data.drag;
        _rigidbody.angularDamping = _data.angularDrag;
        _rigidbody.useGravity = true;
        _rigidbody.isKinematic = false;

        // Apply physics material to collider
        if (_collider != null && _data.physicsMaterial != null)
        {
            _collider.material = _data.physicsMaterial;
        }
    }

    private void ApplyMaterials()
    {
        if (_meshRenderer == null || _data == null)
            return;

        if (_data.surfaceMaterial != null)
        {
            _meshRenderer.material = _data.surfaceMaterial;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DEBUG
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns a string summary of this workpiece's state.
    /// </summary>
    public override string ToString()
    {
        string dataName = _data != null ? _data.workpieceName : "Unknown";
        return $"Workpiece[{gameObject.name}]: {dataName}, Cuts: {_cutCount}/{(_data != null ? _data.maxCuts : 0)}, " +
               $"Thickness: {_currentThickness:F4}m, CanCut: {CanBeCut}";
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_data == null)
            return;

        // Draw workpiece bounds
        Gizmos.color = CanBeCut ? Color.green : Color.red;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, _data.dimensions);
    }
#endif
}
