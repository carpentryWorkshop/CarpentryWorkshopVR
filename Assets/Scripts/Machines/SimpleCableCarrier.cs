using UnityEngine;

/// <summary>
/// Animates a cable carrier (drag chain) that follows the CNC spindle holder.
/// 
/// Uses a LineRenderer to visualize the cable path with realistic sag,
/// and positions 3D chain segment models along the curve.
/// 
/// The cable connects a fixed anchor point (on the machine base) to the
/// moving spindle holder, creating a smooth curved path between them.
/// 
/// Setup:
/// 1. Create an anchor point Transform at the fixed cable mounting position
/// 2. Add a LineRenderer component for cable visualization (optional)
/// 3. Add your 3D chain segment models as children
/// 4. Assign all references in Inspector
/// 5. Configure which axes to follow (typically X and Y)
/// </summary>
public class SimpleCableCarrier : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - References
    // ══════════════════════════════════════════════════════════════════════════

    [Header("References")]
    [Tooltip("The CNC cutter to subscribe to for movement events.")]
    [SerializeField] private CNCCutterExtended _cncCutter;

    [Tooltip("Fixed anchor point where the cable attaches to the machine base.")]
    [SerializeField] private Transform _anchorPoint;

    [Tooltip("The spindle holder Transform that moves with the cutter.")]
    [SerializeField] private Transform _spindleHolder;

    [Tooltip("LineRenderer for visualizing the cable path (optional).")]
    [SerializeField] private LineRenderer _lineRenderer;

    [Tooltip("Array of 3D chain segment models to position along the cable path.")]
    [SerializeField] private Transform[] _chainSegmentModels;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Cable Settings
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Cable Settings")]
    [Tooltip("Number of points to generate for the cable curve (higher = smoother).")]
    [SerializeField] [Range(5, 50)] private int _curveResolution = 20;

    [Tooltip("How much the cable sags/droops in the middle (meters).")]
    [SerializeField] [Range(0f, 0.3f)] private float _sagAmount = 0.05f;

    [Tooltip("Shape of the sag curve. X-axis is position along cable (0-1), Y-axis is sag factor (0-1).")]
    [SerializeField] private AnimationCurve _sagCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);

    [Tooltip("Smoothing factor for cable movement (0 = instant, 1 = very smooth).")]
    [SerializeField] [Range(0f, 0.95f)] private float _smoothing = 0.5f;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Axes
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Axes to Follow")]
    [Tooltip("Follow spindle holder on X-axis (left/right).")]
    [SerializeField] private bool _followX = true;

    [Tooltip("Follow spindle holder on Y-axis (up/down).")]
    [SerializeField] private bool _followY = true;

    [Tooltip("Follow spindle holder on Z-axis (forward/back).")]
    [SerializeField] private bool _followZ = false;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Performance
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Performance")]
    [Tooltip("Update cable every N frames (1 = every frame, 2 = every other frame).")]
    [SerializeField] [Range(1, 5)] private int _updateEveryNFrames = 1;

    [Header("Debug")]
    [Tooltip("Show debug gizmos in Scene view.")]
    [SerializeField] private bool _showGizmos = true;

    [Tooltip("Log cable updates to console.")]
    [SerializeField] private bool _verboseLogging = false;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private Vector3[] _curvePoints;
    private Vector3 _smoothedEndPosition;
    private int _frameCounter = 0;
    private bool _initialized = false;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        _curvePoints = new Vector3[_curveResolution];
        
        // Initialize default sag curve if not set
        if (_sagCurve == null || _sagCurve.length == 0)
        {
            _sagCurve = CreateDefaultSagCurve();
        }
    }

    private void Start()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (_cncCutter != null)
        {
            _cncCutter.OnCutterMoved += HandleCutterMoved;
        }
    }

    private void OnDisable()
    {
        if (_cncCutter != null)
        {
            _cncCutter.OnCutterMoved -= HandleCutterMoved;
        }
    }

    private void Update()
    {
        _frameCounter++;
        
        if (_frameCounter % _updateEveryNFrames != 0)
            return;

        UpdateCable();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Forces an immediate cable update.
    /// </summary>
    public void ForceUpdate()
    {
        UpdateCable();
    }

    /// <summary>
    /// Sets the sag amount at runtime.
    /// </summary>
    /// <param name="sag">Sag amount in meters.</param>
    public void SetSagAmount(float sag)
    {
        _sagAmount = Mathf.Max(0f, sag);
    }

    /// <summary>
    /// Sets which axes the cable end should follow.
    /// </summary>
    public void SetAxes(bool followX, bool followY, bool followZ)
    {
        _followX = followX;
        _followY = followY;
        _followZ = followZ;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE METHODS
    // ══════════════════════════════════════════════════════════════════════════

    private void Initialize()
    {
        if (_anchorPoint == null || _spindleHolder == null)
        {
            Debug.LogWarning("[SimpleCableCarrier] Missing anchor or spindle holder reference!");
            return;
        }

        _smoothedEndPosition = GetConstrainedEndPosition();
        _initialized = true;

        // Initial cable update
        UpdateCable();

        if (_verboseLogging)
            Debug.Log("[SimpleCableCarrier] Initialized");
    }

    private void HandleCutterMoved(Vector3 newLocalPosition)
    {
        // The event gives us local position, but we use the spindleHolder's world position
        // This event just triggers us to know movement happened
        if (_verboseLogging)
            Debug.Log($"[SimpleCableCarrier] Cutter moved to local: {newLocalPosition}");
    }

    private void UpdateCable()
    {
        if (!_initialized || _anchorPoint == null || _spindleHolder == null)
            return;

        // Get positions
        Vector3 startPos = _anchorPoint.position;
        Vector3 targetEndPos = GetConstrainedEndPosition();

        // Apply smoothing to end position
        if (_smoothing > 0f)
        {
            float smoothFactor = 1f - Mathf.Pow(_smoothing, Time.deltaTime * 60f);
            _smoothedEndPosition = Vector3.Lerp(_smoothedEndPosition, targetEndPos, smoothFactor);
        }
        else
        {
            _smoothedEndPosition = targetEndPos;
        }

        // Generate cable curve
        GenerateCableCurve(startPos, _smoothedEndPosition);

        // Update visuals
        UpdateLineRenderer();
        PositionChainSegments();
    }

    private Vector3 GetConstrainedEndPosition()
    {
        Vector3 holderPos = _spindleHolder.position;
        Vector3 anchorPos = _anchorPoint.position;

        // Start from anchor position and only follow specified axes
        Vector3 constrainedPos = anchorPos;

        if (_followX) constrainedPos.x = holderPos.x;
        if (_followY) constrainedPos.y = holderPos.y;
        if (_followZ) constrainedPos.z = holderPos.z;

        return constrainedPos;
    }

    private void GenerateCableCurve(Vector3 start, Vector3 end)
    {
        if (_curvePoints == null || _curvePoints.Length != _curveResolution)
        {
            _curvePoints = new Vector3[_curveResolution];
        }

        for (int i = 0; i < _curveResolution; i++)
        {
            float t = i / (float)(_curveResolution - 1);

            // Linear interpolation between start and end
            Vector3 point = Vector3.Lerp(start, end, t);

            // Apply sag using animation curve
            // The sag curve should be bell-shaped (0 at edges, 1 in middle)
            float sagFactor = _sagCurve.Evaluate(t);
            point.y -= sagFactor * _sagAmount;

            _curvePoints[i] = point;
        }
    }

    private void UpdateLineRenderer()
    {
        if (_lineRenderer == null)
            return;

        _lineRenderer.positionCount = _curvePoints.Length;
        _lineRenderer.SetPositions(_curvePoints);
    }

    private void PositionChainSegments()
    {
        if (_chainSegmentModels == null || _chainSegmentModels.Length == 0)
            return;

        int segmentCount = _chainSegmentModels.Length;

        for (int i = 0; i < segmentCount; i++)
        {
            Transform segment = _chainSegmentModels[i];
            if (segment == null)
                continue;

            // Map segment index to position along curve (0 to 1)
            float t = i / (float)(segmentCount - 1);
            
            // Get position on curve
            Vector3 position = GetPointOnCurve(t);
            segment.position = position;

            // Orient segment to look along the curve direction
            Vector3 direction = GetDirectionOnCurve(t);
            if (direction.sqrMagnitude > 0.0001f)
            {
                segment.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    private Vector3 GetPointOnCurve(float t)
    {
        if (_curvePoints == null || _curvePoints.Length == 0)
            return Vector3.zero;

        // Clamp t to valid range
        t = Mathf.Clamp01(t);

        // Map t to curve index
        float indexFloat = t * (_curvePoints.Length - 1);
        int indexLow = Mathf.FloorToInt(indexFloat);
        int indexHigh = Mathf.Min(indexLow + 1, _curvePoints.Length - 1);

        // Interpolate between the two nearest points
        float blend = indexFloat - indexLow;
        return Vector3.Lerp(_curvePoints[indexLow], _curvePoints[indexHigh], blend);
    }

    private Vector3 GetDirectionOnCurve(float t)
    {
        if (_curvePoints == null || _curvePoints.Length < 2)
            return Vector3.forward;

        // Get two nearby points to calculate direction
        float delta = 1f / (_curvePoints.Length - 1);
        float t1 = Mathf.Clamp01(t - delta * 0.5f);
        float t2 = Mathf.Clamp01(t + delta * 0.5f);

        Vector3 p1 = GetPointOnCurve(t1);
        Vector3 p2 = GetPointOnCurve(t2);

        Vector3 direction = (p2 - p1).normalized;
        
        // Fallback if direction is zero
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;

        return direction;
    }

    private AnimationCurve CreateDefaultSagCurve()
    {
        // Create a bell curve: 0 at t=0, peaks at t=0.5, 0 at t=1
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(new Keyframe(0f, 0f, 0f, 2f));       // Start at 0, rising
        curve.AddKey(new Keyframe(0.5f, 1f, 0f, 0f));     // Peak at middle
        curve.AddKey(new Keyframe(1f, 0f, -2f, 0f));      // End at 0, falling
        return curve;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR GIZMOS
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_showGizmos)
            return;

        // Draw anchor point
        if (_anchorPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_anchorPoint.position, 0.02f);
        }

        // Draw end point (spindle holder)
        if (_spindleHolder != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_spindleHolder.position, 0.02f);
        }

        // Draw cable curve
        if (_curvePoints != null && _curvePoints.Length > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _curvePoints.Length - 1; i++)
            {
                Gizmos.DrawLine(_curvePoints[i], _curvePoints[i + 1]);
            }
        }

        // Draw chain segment positions
        if (_chainSegmentModels != null)
        {
            Gizmos.color = Color.cyan;
            foreach (Transform segment in _chainSegmentModels)
            {
                if (segment != null)
                {
                    Gizmos.DrawWireCube(segment.position, Vector3.one * 0.01f);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw constrained end position when selected
        if (_anchorPoint != null && _spindleHolder != null)
        {
            Vector3 constrainedEnd = _anchorPoint.position;
            if (_followX) constrainedEnd.x = _spindleHolder.position.x;
            if (_followY) constrainedEnd.y = _spindleHolder.position.y;
            if (_followZ) constrainedEnd.z = _spindleHolder.position.z;

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(constrainedEnd, 0.025f);
            Gizmos.DrawLine(_anchorPoint.position, constrainedEnd);
        }
    }
#endif

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR VALIDATION
    // ══════════════════════════════════════════════════════════════════════════

    private void OnValidate()
    {
        _curveResolution = Mathf.Max(2, _curveResolution);
        _sagAmount = Mathf.Max(0f, _sagAmount);
        _updateEveryNFrames = Mathf.Max(1, _updateEveryNFrames);

        // Resize curve points array if resolution changed
        if (_curvePoints == null || _curvePoints.Length != _curveResolution)
        {
            _curvePoints = new Vector3[_curveResolution];
        }
    }
}
