using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns workpiece (wood blank) GameObjects at a designated position.
/// 
/// Features:
/// - Object pooling for performance
/// - Configurable spawn position and rotation
/// - Auto-spawn mode with interval
/// - Support for multiple workpiece types
/// 
/// Setup:
/// 1. Add this component to an empty GameObject at the spawn location
/// 2. Assign a WorkpieceData asset in the Inspector
/// 3. Set the spawn point (or use this transform)
/// 4. Call SpawnWorkpiece() or enable auto-spawn
/// </summary>
public class ObjectSpawner : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Spawn Configuration
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Spawn Configuration")]
    [Tooltip("The default WorkpieceData to spawn.")]
    [SerializeField] private WorkpieceData _defaultWorkpieceData;

    [Tooltip("Transform where workpieces spawn. If null, uses this GameObject's transform.")]
    [SerializeField] private Transform _spawnPoint;

    [Tooltip("Apply a random rotation offset to spawned objects.")]
    [SerializeField] private bool _randomRotation = false;

    [Tooltip("Maximum random rotation offset in degrees (Y axis only).")]
    [SerializeField] [Range(0f, 180f)] private float _maxRandomRotation = 15f;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Auto Spawn
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Auto Spawn")]
    [Tooltip("Automatically spawn workpieces at regular intervals.")]
    [SerializeField] private bool _autoSpawn = false;

    [Tooltip("Time between auto-spawns in seconds.")]
    [SerializeField] [Range(1f, 60f)] private float _spawnInterval = 5f;

    [Tooltip("Maximum number of workpieces that can exist at once (0 = unlimited).")]
    [SerializeField] private int _maxActiveWorkpieces = 5;

    [Tooltip("Start auto-spawning when the game starts.")]
    [SerializeField] private bool _spawnOnStart = false;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Object Pooling
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Object Pooling")]
    [Tooltip("Enable object pooling for better performance.")]
    [SerializeField] private bool _usePooling = true;

    [Tooltip("Initial pool size (pre-instantiated objects).")]
    [SerializeField] [Range(0, 20)] private int _initialPoolSize = 3;

    [Tooltip("Maximum pool size.")]
    [SerializeField] [Range(1, 50)] private int _maxPoolSize = 10;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Physics
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Physics")]
    [Tooltip("Add initial velocity to spawned objects.")]
    [SerializeField] private Vector3 _initialVelocity = Vector3.zero;

    [Tooltip("Spawn objects as kinematic (no physics) initially.")]
    [SerializeField] private bool _spawnKinematic = false;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Debug
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Debug")]
    [Tooltip("Show spawn point gizmo in editor.")]
    [SerializeField] private bool _showGizmo = true;

    [Tooltip("Size of the spawn point gizmo.")]
    [SerializeField] private float _gizmoSize = 0.2f;

    [Tooltip("Log spawn events to console.")]
    [SerializeField] private bool _verboseLogging = true;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private Queue<GameObject> _pool;
    private List<GameObject> _activeWorkpieces;
    private float _nextSpawnTime;
    private Transform _poolParent;
    private int _spawnCounter;

    // ══════════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Number of currently active workpieces spawned by this spawner.</summary>
    public int ActiveWorkpieceCount => _activeWorkpieces?.Count ?? 0;

    /// <summary>Number of objects available in the pool.</summary>
    public int PoolCount => _pool?.Count ?? 0;

    /// <summary>Is auto-spawn currently enabled?</summary>
    public bool IsAutoSpawnEnabled => _autoSpawn;

    /// <summary>The spawn position in world space.</summary>
    public Vector3 SpawnPosition => _spawnPoint != null ? _spawnPoint.position : transform.position;

    /// <summary>The spawn rotation in world space.</summary>
    public Quaternion SpawnRotation => _spawnPoint != null ? _spawnPoint.rotation : transform.rotation;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        InitializeSpawner();
    }

    private void Start()
    {
        if (_spawnOnStart && _autoSpawn)
        {
            SpawnWorkpiece();
            _nextSpawnTime = Time.time + _spawnInterval;
        }
    }

    private void Update()
    {
        if (_autoSpawn && Time.time >= _nextSpawnTime)
        {
            TryAutoSpawn();
            _nextSpawnTime = Time.time + _spawnInterval;
        }
    }

    private void OnDestroy()
    {
        // Clean up pool
        if (_pool != null)
        {
            while (_pool.Count > 0)
            {
                var obj = _pool.Dequeue();
                if (obj != null)
                    Destroy(obj);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Spawning
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Spawns a workpiece using the default WorkpieceData.
    /// </summary>
    /// <returns>The spawned workpiece GameObject, or null if spawn failed.</returns>
    public GameObject SpawnWorkpiece()
    {
        return SpawnWorkpiece(_defaultWorkpieceData);
    }

    /// <summary>
    /// Spawns a workpiece using the specified WorkpieceData.
    /// </summary>
    /// <param name="data">The WorkpieceData to use for spawning.</param>
    /// <returns>The spawned workpiece GameObject, or null if spawn failed.</returns>
    public GameObject SpawnWorkpiece(WorkpieceData data)
    {
        if (data == null)
        {
            Debug.LogError("[ObjectSpawner] Cannot spawn: No WorkpieceData provided!");
            return null;
        }

        if (data.prefab == null)
        {
            Debug.LogError($"[ObjectSpawner] Cannot spawn: WorkpieceData '{data.workpieceName}' has no prefab assigned!");
            return null;
        }

        // Check max active limit
        if (_maxActiveWorkpieces > 0 && _activeWorkpieces.Count >= _maxActiveWorkpieces)
        {
            if (_verboseLogging)
                Debug.LogWarning($"[ObjectSpawner] Cannot spawn: Max active workpieces ({_maxActiveWorkpieces}) reached.");
            return null;
        }

        // Get or create workpiece GameObject
        GameObject workpiece = GetFromPoolOrCreate(data);

        if (workpiece == null)
        {
            Debug.LogError("[ObjectSpawner] Failed to create workpiece!");
            return null;
        }

        // Position and orient
        Vector3 position = SpawnPosition;
        Quaternion rotation = SpawnRotation;

        if (_randomRotation)
        {
            float randomYaw = Random.Range(-_maxRandomRotation, _maxRandomRotation);
            rotation *= Quaternion.Euler(0f, randomYaw, 0f);
        }

        workpiece.transform.SetPositionAndRotation(position, rotation);

        // Initialize the Workpiece component
        Workpiece workpieceComponent = workpiece.GetComponent<Workpiece>();
        if (workpieceComponent == null)
            workpieceComponent = workpiece.AddComponent<Workpiece>();

        workpieceComponent.Initialize(data);

        // Setup Rigidbody
        Rigidbody rb = workpiece.GetComponent<Rigidbody>();
        if (rb == null)
            rb = workpiece.AddComponent<Rigidbody>();

        rb.isKinematic = _spawnKinematic;
        rb.velocity = _initialVelocity;

        // Ensure collider exists
        Collider col = workpiece.GetComponent<Collider>();
        if (col == null)
        {
            // Add box collider as fallback
            BoxCollider box = workpiece.AddComponent<BoxCollider>();
            box.size = data.dimensions;
        }

        // Tag for identification
        workpiece.tag = "Workpiece";

        // Track active workpiece
        _activeWorkpieces.Add(workpiece);

        // Activate
        workpiece.SetActive(true);

        // Raise event
        GameStateEvents.RaiseWorkpieceSpawned(workpiece);

        if (_verboseLogging)
            Debug.Log($"[ObjectSpawner] Spawned: {workpiece.name} at {position}");

        return workpiece;
    }

    /// <summary>
    /// Spawns a workpiece at a specific position and rotation.
    /// </summary>
    /// <param name="data">The WorkpieceData to use.</param>
    /// <param name="position">World position to spawn at.</param>
    /// <param name="rotation">World rotation to spawn with.</param>
    /// <returns>The spawned workpiece GameObject.</returns>
    public GameObject SpawnWorkpieceAt(WorkpieceData data, Vector3 position, Quaternion rotation)
    {
        Transform originalSpawnPoint = _spawnPoint;
        
        // Temporarily override spawn point
        GameObject tempPoint = new GameObject("TempSpawnPoint");
        tempPoint.transform.SetPositionAndRotation(position, rotation);
        _spawnPoint = tempPoint.transform;

        GameObject workpiece = SpawnWorkpiece(data);

        // Restore original
        _spawnPoint = originalSpawnPoint;
        Destroy(tempPoint);

        return workpiece;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Despawning
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Despawns a workpiece, returning it to the pool or destroying it.
    /// </summary>
    /// <param name="workpiece">The workpiece to despawn.</param>
    public void DespawnWorkpiece(GameObject workpiece)
    {
        if (workpiece == null)
            return;

        // Remove from active list
        _activeWorkpieces.Remove(workpiece);

        // Raise event
        GameStateEvents.RaiseWorkpieceDespawned(workpiece);

        if (_usePooling && _pool.Count < _maxPoolSize)
        {
            // Return to pool
            workpiece.SetActive(false);
            workpiece.transform.SetParent(_poolParent);
            
            // Reset workpiece component
            Workpiece wp = workpiece.GetComponent<Workpiece>();
            if (wp != null)
                wp.Reset();

            // Reset rigidbody
            Rigidbody rb = workpiece.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            _pool.Enqueue(workpiece);

            if (_verboseLogging)
                Debug.Log($"[ObjectSpawner] Returned to pool: {workpiece.name}");
        }
        else
        {
            // Destroy
            Destroy(workpiece);

            if (_verboseLogging)
                Debug.Log($"[ObjectSpawner] Destroyed: {workpiece.name}");
        }
    }

    /// <summary>
    /// Despawns all active workpieces.
    /// </summary>
    public void DespawnAll()
    {
        // Create a copy to iterate since DespawnWorkpiece modifies the list
        var toRemove = new List<GameObject>(_activeWorkpieces);
        
        foreach (var workpiece in toRemove)
        {
            DespawnWorkpiece(workpiece);
        }

        if (_verboseLogging)
            Debug.Log($"[ObjectSpawner] Despawned all workpieces.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Auto Spawn Control
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Enables or disables auto-spawn mode.
    /// </summary>
    /// <param name="enabled">True to enable auto-spawn.</param>
    /// <param name="interval">Optional new spawn interval. Pass -1 to keep current.</param>
    public void SetAutoSpawn(bool enabled, float interval = -1f)
    {
        _autoSpawn = enabled;

        if (interval > 0f)
            _spawnInterval = interval;

        if (enabled)
            _nextSpawnTime = Time.time + _spawnInterval;

        if (_verboseLogging)
            Debug.Log($"[ObjectSpawner] Auto-spawn {(enabled ? "enabled" : "disabled")}, interval: {_spawnInterval}s");
    }

    /// <summary>
    /// Sets the maximum number of active workpieces allowed.
    /// </summary>
    /// <param name="max">Maximum count (0 = unlimited).</param>
    public void SetMaxActiveWorkpieces(int max)
    {
        _maxActiveWorkpieces = Mathf.Max(0, max);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Pool Management
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Pre-warms the pool by creating objects ahead of time.
    /// </summary>
    /// <param name="count">Number of objects to create.</param>
    public void PrewarmPool(int count)
    {
        if (!_usePooling || _defaultWorkpieceData == null || _defaultWorkpieceData.prefab == null)
            return;

        int toCreate = Mathf.Min(count, _maxPoolSize - _pool.Count);

        for (int i = 0; i < toCreate; i++)
        {
            GameObject obj = CreatePooledObject(_defaultWorkpieceData);
            if (obj != null)
            {
                obj.SetActive(false);
                _pool.Enqueue(obj);
            }
        }

        if (_verboseLogging)
            Debug.Log($"[ObjectSpawner] Pre-warmed pool with {toCreate} objects. Pool size: {_pool.Count}");
    }

    /// <summary>
    /// Clears the object pool, destroying all pooled objects.
    /// </summary>
    public void ClearPool()
    {
        while (_pool.Count > 0)
        {
            var obj = _pool.Dequeue();
            if (obj != null)
                Destroy(obj);
        }

        if (_verboseLogging)
            Debug.Log("[ObjectSpawner] Pool cleared.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private void InitializeSpawner()
    {
        _pool = new Queue<GameObject>();
        _activeWorkpieces = new List<GameObject>();
        _spawnCounter = 0;

        // Use this transform if no spawn point specified
        if (_spawnPoint == null)
            _spawnPoint = transform;

        // Create pool parent for organization
        if (_usePooling)
        {
            var poolGO = new GameObject($"{gameObject.name}_Pool");
            _poolParent = poolGO.transform;
            _poolParent.SetParent(transform);
            _poolParent.localPosition = Vector3.zero;

            // Pre-warm pool
            if (_initialPoolSize > 0 && _defaultWorkpieceData != null)
            {
                PrewarmPool(_initialPoolSize);
            }
        }
    }

    private void TryAutoSpawn()
    {
        if (_maxActiveWorkpieces > 0 && _activeWorkpieces.Count >= _maxActiveWorkpieces)
            return;

        SpawnWorkpiece();
    }

    private GameObject GetFromPoolOrCreate(WorkpieceData data)
    {
        if (_usePooling && _pool.Count > 0)
        {
            // Try to get from pool
            while (_pool.Count > 0)
            {
                var obj = _pool.Dequeue();
                if (obj != null)
                {
                    obj.transform.SetParent(null);
                    return obj;
                }
            }
        }

        // Create new
        return CreatePooledObject(data);
    }

    private GameObject CreatePooledObject(WorkpieceData data)
    {
        if (data == null || data.prefab == null)
            return null;

        _spawnCounter++;
        string uniqueName = $"{data.workpieceName}_{_spawnCounter:D4}";

        GameObject obj = Instantiate(data.prefab);
        obj.name = uniqueName;

        return obj;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_showGizmo)
            return;

        Vector3 pos = _spawnPoint != null ? _spawnPoint.position : transform.position;
        Quaternion rot = _spawnPoint != null ? _spawnPoint.rotation : transform.rotation;

        // Draw spawn point
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(pos, _gizmoSize * 0.5f);

        // Draw forward direction
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(pos, rot * Vector3.forward * _gizmoSize);

        // Draw workpiece preview if data is assigned
        if (_defaultWorkpieceData != null)
        {
            Gizmos.color = new Color(0.5f, 0.3f, 0.1f, 0.5f); // Brown
            Gizmos.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, _defaultWorkpieceData.dimensions);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_defaultWorkpieceData != null)
        {
            Vector3 pos = _spawnPoint != null ? _spawnPoint.position : transform.position;
            Quaternion rot = _spawnPoint != null ? _spawnPoint.rotation : transform.rotation;

            // Draw solid workpiece preview
            Gizmos.color = new Color(0.5f, 0.3f, 0.1f, 0.3f);
            Gizmos.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, _defaultWorkpieceData.dimensions);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
#endif
}
