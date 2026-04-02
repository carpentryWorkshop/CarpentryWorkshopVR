using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates visual results from CNC cutting operations.
/// 
/// This component works alongside CNCMachineExtended to create deformed meshes
/// that represent the material removed during cutting. It supports:
/// - Real-time vertex deformation during cutting
/// - Final mesh generation based on recorded cutter path
/// - Sawdust/particle effects during cutting
/// 
/// Setup:
/// 1. Add this component to the same GameObject as CNCMachineExtended
/// 2. Assign the cross-section material for cut surfaces
/// 3. Optionally assign particle system for sawdust effects
/// </summary>
public class CNCResultGenerator : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - References
    // ══════════════════════════════════════════════════════════════════════════

    [Header("References")]
    [Tooltip("Reference to the CNC cutter for position tracking.")]
    [SerializeField] private CNCCutterExtended _cutter;

    [Tooltip("Transform representing the cutter tip position.")]
    [SerializeField] private Transform _cutterTip;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Materials
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Materials")]
    [Tooltip("Material applied to cut/exposed surfaces.")]
    [SerializeField] private Material _crossSectionMaterial;

    [Tooltip("If true, use the workpiece's cross-section material instead.")]
    [SerializeField] private bool _useWorkpieceMaterial = true;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Deformation Settings
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Deformation Settings")]
    [Tooltip("Enable real-time mesh deformation during cutting.")]
    [SerializeField] private bool _enableRealTimeDeformation = true;

    [Tooltip("Resolution of the deformation grid (higher = more detailed but slower).")]
    [Range(10, 100)]
    [SerializeField] private int _deformationResolution = 32;

    [Tooltip("How often to update deformation during cutting (seconds).")]
    [Range(0.01f, 0.2f)]
    [SerializeField] private float _deformationUpdateInterval = 0.05f;

    [Tooltip("Smoothing factor for deformation edges (0 = sharp, 1 = smooth).")]
    [Range(0f, 1f)]
    [SerializeField] private float _edgeSmoothness = 0.3f;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Effects
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Effects")]
    [Tooltip("Particle system for sawdust/chips during cutting (optional).")]
    [SerializeField] private ParticleSystem _sawdustParticles;

    [Tooltip("Emission rate multiplier based on cutting speed.")]
    [Range(0f, 100f)]
    [SerializeField] private float _particleEmissionRate = 20f;

    [Tooltip("Spawn debris pieces when cutting completes.")]
    [SerializeField] private bool _spawnDebris = true;

    [Tooltip("Prefab for debris pieces (optional).")]
    [SerializeField] private GameObject _debrisPrefab;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Debug
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Debug")]
    [Tooltip("Show deformation gizmos in editor.")]
    [SerializeField] private bool _showDebugGizmos = false;

    [Tooltip("Log detailed deformation info.")]
    [SerializeField] private bool _verboseLogging = false;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private GameObject _currentWorkpiece;
    private Workpiece _workpieceComponent;
    private Mesh _originalMesh;
    private Mesh _workingMesh;
    private MeshFilter _workpieceMeshFilter;
    private List<Vector3> _cutPoints;
    private float _lastDeformationTime;
    private bool _isDeforming;
    private PathData _currentPath;
    private float _currentDepth;

    // Cached deformation data
    private Vector3[] _originalVertices;
    private Vector3[] _deformedVertices;
    private float[] _vertexDepths;

    // ══════════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>True if a workpiece is currently loaded and can be modified.</summary>
    public bool HasWorkpiece => _currentWorkpiece != null && _workpieceMeshFilter != null;

    /// <summary>True if currently performing real-time deformation.</summary>
    public bool IsDeforming => _isDeforming;

    /// <summary>Number of cut points recorded.</summary>
    public int CutPointCount => _cutPoints?.Count ?? 0;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        _cutPoints = new List<Vector3>();

        // Auto-find cutter if not assigned
        if (_cutter == null)
            _cutter = GetComponentInChildren<CNCCutterExtended>();

        // Find cutter tip if not assigned
        if (_cutterTip == null && _cutter != null)
            _cutterTip = _cutter.transform;
    }

    private void OnEnable()
    {
        // Subscribe to CNC events
        GameStateEvents.OnCNCStateChanged += HandleCNCStateChanged;
        GameStateEvents.OnCutProgress += HandleCutProgress;
    }

    private void OnDisable()
    {
        GameStateEvents.OnCNCStateChanged -= HandleCNCStateChanged;
        GameStateEvents.OnCutProgress -= HandleCutProgress;
    }

    private void Update()
    {
        if (_isDeforming && _enableRealTimeDeformation)
        {
            if (Time.time - _lastDeformationTime >= _deformationUpdateInterval)
            {
                UpdateRealTimeDeformation();
                _lastDeformationTime = Time.time;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Workpiece Management
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sets the current workpiece to be modified by cutting operations.
    /// </summary>
    /// <param name="workpiece">The workpiece GameObject.</param>
    public void SetCurrentWorkpiece(GameObject workpiece)
    {
        if (workpiece == _currentWorkpiece)
            return;

        // Clean up previous workpiece
        if (_workingMesh != null && _workingMesh != _originalMesh)
        {
            Destroy(_workingMesh);
        }

        _currentWorkpiece = workpiece;
        _workpieceComponent = null;
        _workpieceMeshFilter = null;
        _originalMesh = null;
        _workingMesh = null;
        _cutPoints.Clear();

        if (workpiece == null)
        {
            if (_verboseLogging)
                Debug.Log("[CNCResultGenerator] Workpiece cleared.");
            return;
        }

        // Get components
        _workpieceComponent = workpiece.GetComponent<Workpiece>();
        _workpieceMeshFilter = workpiece.GetComponent<MeshFilter>();

        if (_workpieceMeshFilter == null || _workpieceMeshFilter.sharedMesh == null)
        {
            Debug.LogWarning($"[CNCResultGenerator] Workpiece '{workpiece.name}' has no valid mesh.");
            return;
        }

        // Store original mesh and create working copy
        _originalMesh = _workpieceMeshFilter.sharedMesh;
        _workingMesh = Instantiate(_originalMesh);
        _workingMesh.name = _originalMesh.name + "_Working";
        _workpieceMeshFilter.mesh = _workingMesh;

        // Cache vertex data for deformation
        CacheVertexData();

        if (_verboseLogging)
            Debug.Log($"[CNCResultGenerator] Workpiece set: {workpiece.name}, " +
                      $"vertices: {_workingMesh.vertexCount}");
    }

    /// <summary>
    /// Clears the current workpiece reference.
    /// </summary>
    public void ClearWorkpiece()
    {
        SetCurrentWorkpiece(null);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Result Generation
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates the final cut result based on recorded path or provided path data.
    /// </summary>
    /// <param name="workpiece">The workpiece that was cut.</param>
    /// <param name="recordedPath">List of cutter positions during cutting.</param>
    /// <param name="pathData">The PathData used for cutting.</param>
    public void GenerateResult(GameObject workpiece, List<Vector3> recordedPath, PathData pathData)
    {
        if (workpiece == null)
        {
            Debug.LogWarning("[CNCResultGenerator] Cannot generate result - no workpiece.");
            return;
        }

        // Use current workpiece if same, otherwise set it
        if (_currentWorkpiece != workpiece)
        {
            SetCurrentWorkpiece(workpiece);
        }

        if (!HasWorkpiece)
        {
            Debug.LogWarning("[CNCResultGenerator] Cannot generate result - workpiece has no valid mesh.");
            return;
        }

        List<Vector3> pathPoints = recordedPath ?? _cutPoints;
        if (pathPoints.Count < 2)
        {
            // Use path data waypoints if no recorded path
            if (pathData != null && pathData.waypoints.Count >= 2)
            {
                pathPoints = new List<Vector3>(pathData.waypoints);
            }
            else
            {
                Debug.LogWarning("[CNCResultGenerator] No path points available for result generation.");
                return;
            }
        }

        // Get cross-section material
        Material crossMaterial = GetCrossSectionMaterial();

        // Apply final deformation using the complete path
        ApplyPathDeformation(pathPoints, pathData);

        // Update workpiece component
        if (_workpieceComponent != null)
        {
            _workpieceComponent.RecordCut(pathData?.TotalDepth ?? 0.02f);
            _workpieceComponent.UpdateMesh(_workingMesh);
        }

        // Spawn debris if enabled
        if (_spawnDebris)
        {
            SpawnDebrisEffects(pathPoints, pathData);
        }

        // Raise event
        GameStateEvents.RaiseWorkpieceCut(_currentWorkpiece);

        if (_verboseLogging)
            Debug.Log($"[CNCResultGenerator] Result generated for {workpiece.name}, " +
                      $"path points: {pathPoints.Count}");
    }

    /// <summary>
    /// Records a cut point during real-time cutting.
    /// </summary>
    /// <param name="worldPosition">World position of the cut.</param>
    public void RecordCutPoint(Vector3 worldPosition)
    {
        if (_currentWorkpiece == null)
            return;

        // Convert to local space
        Vector3 localPosition = _currentWorkpiece.transform.InverseTransformPoint(worldPosition);
        _cutPoints.Add(localPosition);
    }

    /// <summary>
    /// Starts real-time deformation mode.
    /// </summary>
    /// <param name="pathData">The path being followed.</param>
    public void StartDeformation(PathData pathData)
    {
        if (!HasWorkpiece)
            return;

        _currentPath = pathData;
        _currentDepth = pathData?.plungeDepth ?? 0.02f;
        _isDeforming = true;
        _lastDeformationTime = Time.time;

        // Start particle effects
        if (_sawdustParticles != null)
        {
            _sawdustParticles.Play();
        }

        if (_verboseLogging)
            Debug.Log("[CNCResultGenerator] Started real-time deformation.");
    }

    /// <summary>
    /// Stops real-time deformation mode.
    /// </summary>
    public void StopDeformation()
    {
        _isDeforming = false;

        // Stop particle effects
        if (_sawdustParticles != null)
        {
            _sawdustParticles.Stop();
        }

        if (_verboseLogging)
            Debug.Log("[CNCResultGenerator] Stopped real-time deformation.");
    }

    /// <summary>
    /// Resets the workpiece mesh to its original state.
    /// </summary>
    public void ResetMesh()
    {
        if (!HasWorkpiece || _originalMesh == null)
            return;

        // Restore original vertices
        if (_originalVertices != null)
        {
            _workingMesh.vertices = (Vector3[])_originalVertices.Clone();
            _workingMesh.RecalculateNormals();
            _workingMesh.RecalculateBounds();
        }

        _cutPoints.Clear();

        if (_verboseLogging)
            Debug.Log("[CNCResultGenerator] Mesh reset to original state.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Event Handlers
    // ══════════════════════════════════════════════════════════════════════════

    private void HandleCNCStateChanged(CNCState state)
    {
        switch (state)
        {
            case CNCState.FollowingPath:
            case CNCState.Cutting:
                if (HasWorkpiece)
                    StartDeformation(_currentPath);
                break;

            case CNCState.Done:
            case CNCState.Idle:
                StopDeformation();
                break;
        }
    }

    private void HandleCutProgress(float progress)
    {
        // Update particle emission based on progress
        if (_sawdustParticles != null && _isDeforming)
        {
            var emission = _sawdustParticles.emission;
            emission.rateOverTime = _particleEmissionRate * (1f + progress);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Deformation
    // ══════════════════════════════════════════════════════════════════════════

    private void CacheVertexData()
    {
        if (_workingMesh == null)
            return;

        _originalVertices = _workingMesh.vertices;
        _deformedVertices = (Vector3[])_originalVertices.Clone();
        _vertexDepths = new float[_originalVertices.Length];
    }

    private void UpdateRealTimeDeformation()
    {
        if (!HasWorkpiece || _cutterTip == null)
            return;

        // Get cutter position in workpiece local space
        Vector3 cutterLocal = _currentWorkpiece.transform.InverseTransformPoint(_cutterTip.position);

        // Record cut point
        _cutPoints.Add(cutterLocal);

        // Deform vertices near the cutter
        float toolRadius = _currentPath?.toolDiameter / 2f ?? 0.003f;
        float cutDepth = _currentDepth;

        bool anyChanged = false;

        for (int i = 0; i < _deformedVertices.Length; i++)
        {
            Vector3 vertex = _deformedVertices[i];

            // Calculate 2D distance on XZ plane
            float dx = vertex.x - cutterLocal.x;
            float dz = vertex.z - cutterLocal.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);

            if (distance < toolRadius)
            {
                // Calculate depth at this distance (rounded bottom profile)
                float normalizedDist = distance / toolRadius;
                float profileDepth = Mathf.Sqrt(1f - normalizedDist * normalizedDist) * cutDepth;

                // Apply smoothing at edges
                profileDepth *= Mathf.Lerp(1f, 0f, Mathf.Pow(normalizedDist, 1f / (1f - _edgeSmoothness + 0.01f)));

                // Track maximum depth at each vertex
                if (profileDepth > _vertexDepths[i])
                {
                    _vertexDepths[i] = profileDepth;
                    
                    // Push vertex down (assuming Y+ is up in local space)
                    float originalY = _originalVertices[i].y;
                    _deformedVertices[i].y = Mathf.Min(vertex.y, originalY - profileDepth);
                    anyChanged = true;
                }
            }
        }

        if (anyChanged)
        {
            _workingMesh.vertices = _deformedVertices;
            _workingMesh.RecalculateNormals();
            _workingMesh.RecalculateBounds();
        }
    }

    private void ApplyPathDeformation(List<Vector3> pathPoints, PathData pathData)
    {
        if (!HasWorkpiece || pathPoints.Count < 2)
            return;

        float toolRadius = pathData?.toolDiameter / 2f ?? 0.003f;
        float totalDepth = pathData?.TotalDepth ?? 0.02f;

        // Reset to original first
        _deformedVertices = (Vector3[])_originalVertices.Clone();
        _vertexDepths = new float[_originalVertices.Length];

        // Apply deformation along entire path
        for (int i = 0; i < _deformedVertices.Length; i++)
        {
            Vector3 vertex = _deformedVertices[i];

            // Find minimum distance to path (2D, ignoring Y)
            float minDistance = float.MaxValue;
            for (int p = 0; p < pathPoints.Count - 1; p++)
            {
                float dist = GetDistanceToSegment2D(
                    new Vector2(vertex.x, vertex.z),
                    new Vector2(pathPoints[p].x, pathPoints[p].z),
                    new Vector2(pathPoints[p + 1].x, pathPoints[p + 1].z)
                );
                minDistance = Mathf.Min(minDistance, dist);
            }

            // Check closed loop
            if (pathData != null && pathData.isClosedLoop && pathPoints.Count > 2)
            {
                float closingDist = GetDistanceToSegment2D(
                    new Vector2(vertex.x, vertex.z),
                    new Vector2(pathPoints[pathPoints.Count - 1].x, pathPoints[pathPoints.Count - 1].z),
                    new Vector2(pathPoints[0].x, pathPoints[0].z)
                );
                minDistance = Mathf.Min(minDistance, closingDist);
            }

            if (minDistance < toolRadius)
            {
                // Calculate depth at this distance
                float normalizedDist = minDistance / toolRadius;
                float profileDepth = Mathf.Sqrt(1f - normalizedDist * normalizedDist) * totalDepth;
                profileDepth *= Mathf.Lerp(1f, 0f, Mathf.Pow(normalizedDist, 1f / (1f - _edgeSmoothness + 0.01f)));

                // Apply depth
                float originalY = _originalVertices[i].y;
                _deformedVertices[i].y = originalY - profileDepth;
            }
        }

        _workingMesh.vertices = _deformedVertices;
        _workingMesh.RecalculateNormals();
        _workingMesh.RecalculateBounds();

        // Update mesh collider if present
        MeshCollider collider = _currentWorkpiece.GetComponent<MeshCollider>();
        if (collider != null)
        {
            collider.sharedMesh = null; // Force refresh
            collider.sharedMesh = _workingMesh;
        }
    }

    private float GetDistanceToSegment2D(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float abLengthSq = ab.sqrMagnitude;

        if (abLengthSq < 0.0001f)
            return Vector2.Distance(point, a);

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / abLengthSq);
        Vector2 projection = a + t * ab;

        return Vector2.Distance(point, projection);
    }

    private Material GetCrossSectionMaterial()
    {
        if (_useWorkpieceMaterial && _workpieceComponent != null && 
            _workpieceComponent.Data != null && 
            _workpieceComponent.Data.crossSectionMaterial != null)
        {
            return _workpieceComponent.Data.crossSectionMaterial;
        }

        return _crossSectionMaterial;
    }

    private void SpawnDebrisEffects(List<Vector3> pathPoints, PathData pathData)
    {
        if (_debrisPrefab == null || _currentWorkpiece == null)
            return;

        // Calculate approximate number of debris pieces based on cut length
        float cutLength = 0f;
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            cutLength += Vector3.Distance(pathPoints[i], pathPoints[i + 1]);
        }

        int debrisCount = Mathf.Clamp(Mathf.RoundToInt(cutLength * 50f), 2, 20);

        for (int i = 0; i < debrisCount; i++)
        {
            // Random position along path
            float t = Random.value;
            int segmentIndex = Mathf.FloorToInt(t * (pathPoints.Count - 1));
            segmentIndex = Mathf.Clamp(segmentIndex, 0, pathPoints.Count - 2);
            
            Vector3 localPos = Vector3.Lerp(
                pathPoints[segmentIndex],
                pathPoints[segmentIndex + 1],
                (t * (pathPoints.Count - 1)) - segmentIndex
            );

            Vector3 worldPos = _currentWorkpiece.transform.TransformPoint(localPos);
            worldPos += Vector3.up * 0.01f; // Slightly above surface

            GameObject debris = Instantiate(_debrisPrefab, worldPos, Random.rotation);
            debris.transform.localScale *= Random.Range(0.5f, 1.5f);

            // Add some random velocity
            Rigidbody rb = debris.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = new Vector3(
                    Random.Range(-0.5f, 0.5f),
                    Random.Range(0.5f, 1.5f),
                    Random.Range(-0.5f, 0.5f)
                );
            }

            // Auto-destroy after some time
            Destroy(debris, 5f);
        }

        if (_verboseLogging)
            Debug.Log($"[CNCResultGenerator] Spawned {debrisCount} debris pieces.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!_showDebugGizmos)
            return;

        // Draw cut points
        if (_cutPoints != null && _cutPoints.Count > 0 && _currentWorkpiece != null)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < _cutPoints.Count - 1; i++)
            {
                Vector3 worldA = _currentWorkpiece.transform.TransformPoint(_cutPoints[i]);
                Vector3 worldB = _currentWorkpiece.transform.TransformPoint(_cutPoints[i + 1]);
                Gizmos.DrawLine(worldA, worldB);
            }
        }

        // Draw cutter tip position
        if (_cutterTip != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_cutterTip.position, 0.005f);
        }
    }
#endif
}
