using UnityEngine;

/// <summary>
/// ScriptableObject that defines the bounding rectangle of the CNC work area.
/// Used by CNCCutter for clamping and by CNCScreenDisplay for the preview overlay.
/// Create via: Assets > Create > CarpentryWorkshopVR > Cutting Path
/// </summary>
[CreateAssetMenu(fileName = "CuttingPath", menuName = "CarpentryWorkshopVR/Cutting Path")]
public class CuttingPath : ScriptableObject
{
    [Header("Work Area Bounds (local space, XZ plane)")]
    [Tooltip("Minimum X and Z coordinates the cutter can reach (local to CNC machine).")]
    [SerializeField] private Vector2 _workAreaMin = new Vector2(-0.5f, -0.5f);

    [Tooltip("Maximum X and Z coordinates the cutter can reach (local to CNC machine).")]
    [SerializeField] private Vector2 _workAreaMax = new Vector2(0.5f, 0.5f);

    [Header("Cut Settings")]
    [Tooltip("Maximum cutting depth on the Y axis (downward, positive value).")]
    [SerializeField] private float _maxCutDepth = 0.05f;

    [Tooltip("Default Y position (height) of the cutter when not plunging.")]
    [SerializeField] private float _idleHeight = 0.1f;

    // ── Public accessors ────────────────────────────────────────────────────

    /// <summary>Minimum X and Z boundary of the work area (local space).</summary>
    public Vector2 WorkAreaMin => _workAreaMin;

    /// <summary>Maximum X and Z boundary of the work area (local space).</summary>
    public Vector2 WorkAreaMax => _workAreaMax;

    /// <summary>Width (X span) of the work area.</summary>
    public float Width => _workAreaMax.x - _workAreaMin.x;

    /// <summary>Depth (Z span) of the work area.</summary>
    public float Depth => _workAreaMax.y - _workAreaMin.y;

    /// <summary>Maximum plunge depth on the Y axis.</summary>
    public float MaxCutDepth => _maxCutDepth;

    /// <summary>Y height of the cutter when idling above the workpiece.</summary>
    public float IdleHeight => _idleHeight;

    /// <summary>
    /// Clamps a local-space XZ position to the work area bounds.
    /// </summary>
    public Vector2 Clamp(Vector2 position)
    {
        return new Vector2(
            Mathf.Clamp(position.x, _workAreaMin.x, _workAreaMax.x),
            Mathf.Clamp(position.y, _workAreaMin.y, _workAreaMax.y)
        );
    }

    /// <summary>
    /// Returns a 0–1 normalised position within the work area (useful for screen mapping).
    /// </summary>
    public Vector2 Normalise(Vector2 localXZ)
    {
        return new Vector2(
            Mathf.InverseLerp(_workAreaMin.x, _workAreaMax.x, localXZ.x),
            Mathf.InverseLerp(_workAreaMin.y, _workAreaMax.y, localXZ.y)
        );
    }
}
