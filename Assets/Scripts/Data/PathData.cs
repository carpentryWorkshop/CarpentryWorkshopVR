using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that defines a cutting path for the CNC machine.
/// 
/// Create via: Assets > Create > CarpentryWorkshopVR > Path Data
/// 
/// Usage:
/// - Create instances for different cutting patterns (rectangle, circle, custom)
/// - Load into CNC machine via CNCControlPanel or TaskManager
/// - CNCCutter follows the waypoints during auto mode
/// </summary>
[CreateAssetMenu(fileName = "PathData", menuName = "CarpentryWorkshopVR/Path Data")]
public class PathData : ScriptableObject
{
    // ══════════════════════════════════════════════════════════════════════════
    // PATH INFORMATION
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Path Information")]
    [Tooltip("Display name for this cutting path.")]
    public string pathName = "Cutting Path";

    [Tooltip("Unique identifier for this path.")]
    public string pathId = "path_default";

    [Tooltip("Type of path geometry.")]
    public PathType pathType = PathType.Custom;

    [Tooltip("Description of what this path cuts.")]
    [TextArea(2, 4)]
    public string description = "A cutting path for the CNC machine.";

    // ══════════════════════════════════════════════════════════════════════════
    // WAYPOINTS
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Waypoints")]
    [Tooltip("List of points defining the path in local CNC space (XZ plane). Y is typically 0.")]
    public List<Vector3> waypoints = new List<Vector3>();

    [Tooltip("If true, the path forms a closed loop (last point connects to first).")]
    public bool isClosedLoop = false;

    // ══════════════════════════════════════════════════════════════════════════
    // CUTTING PARAMETERS
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Cutting Parameters")]
    [Tooltip("Feed rate - how fast the cutter moves along the path (meters/second).")]
    [Range(0.01f, 0.5f)]
    public float feedRate = 0.1f;

    [Tooltip("Plunge depth - how deep the cutter goes into the material (meters).")]
    [Range(0.001f, 0.1f)]
    public float plungeDepth = 0.02f;

    [Tooltip("Number of passes for deeper cuts. Each pass cuts plungeDepth deeper.")]
    [Range(1, 10)]
    public int passes = 1;

    [Tooltip("Plunge rate - how fast the cutter moves down into the material (meters/second).")]
    [Range(0.01f, 0.2f)]
    public float plungeRate = 0.05f;

    [Tooltip("Retract height - height to lift cutter between moves (meters above workpiece).")]
    [Range(0.01f, 0.1f)]
    public float retractHeight = 0.03f;

    // ══════════════════════════════════════════════════════════════════════════
    // TOOL SETTINGS
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Tool Settings")]
    [Tooltip("Diameter of the cutting tool in meters.")]
    [Range(0.001f, 0.05f)]
    public float toolDiameter = 0.006f; // 6mm default

    [Tooltip("How to offset the tool relative to the path (inside, outside, or on the line).")]
    public ToolOffsetMode toolOffset = ToolOffsetMode.OnLine;

    // ══════════════════════════════════════════════════════════════════════════
    // PREVIEW
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Preview")]
    [Tooltip("Color used to preview this path in the editor and on the CNC display.")]
    public Color previewColor = Color.cyan;

    [Tooltip("Width of the preview line.")]
    [Range(0.001f, 0.01f)]
    public float previewLineWidth = 0.003f;

    // ══════════════════════════════════════════════════════════════════════════
    // SHAPE PARAMETERS (for auto-generation)
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Shape Parameters (for Rectangle/Circle types)")]
    [Tooltip("Width of rectangle or diameter of circle (meters).")]
    public float shapeWidth = 0.1f;

    [Tooltip("Height of rectangle (meters). Ignored for circles.")]
    public float shapeHeight = 0.1f;

    [Tooltip("Center position of the shape in local CNC space.")]
    public Vector3 shapeCenter = Vector3.zero;

    [Tooltip("Number of segments for circles (higher = smoother).")]
    [Range(8, 64)]
    public int circleSegments = 32;

    // ══════════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Total number of waypoints in this path.</summary>
    public int WaypointCount => waypoints?.Count ?? 0;

    /// <summary>Total path length in meters.</summary>
    public float TotalLength
    {
        get
        {
            if (waypoints == null || waypoints.Count < 2)
                return 0f;

            float length = 0f;
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                length += Vector3.Distance(waypoints[i], waypoints[i + 1]);
            }

            if (isClosedLoop && waypoints.Count > 2)
            {
                length += Vector3.Distance(waypoints[waypoints.Count - 1], waypoints[0]);
            }

            return length;
        }
    }

    /// <summary>Estimated time to complete this path at the current feed rate (seconds).</summary>
    public float EstimatedTime => feedRate > 0f ? TotalLength / feedRate * passes : 0f;

    /// <summary>Total cut depth across all passes.</summary>
    public float TotalDepth => plungeDepth * passes;

    // ══════════════════════════════════════════════════════════════════════════
    // VALIDATION
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Validates that this path data is properly configured.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(pathName))
        {
            Debug.LogWarning($"[PathData] {name}: Missing path name.");
            return false;
        }

        if (waypoints == null || waypoints.Count < 2)
        {
            Debug.LogWarning($"[PathData] {name}: Path must have at least 2 waypoints.");
            return false;
        }

        if (feedRate <= 0f)
        {
            Debug.LogWarning($"[PathData] {name}: Feed rate must be positive.");
            return false;
        }

        return true;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SHAPE GENERATION
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates waypoints for a rectangle shape.
    /// </summary>
    public void GenerateRectangle()
    {
        waypoints.Clear();

        float halfW = shapeWidth / 2f;
        float halfH = shapeHeight / 2f;

        // Counter-clockwise from bottom-left
        waypoints.Add(shapeCenter + new Vector3(-halfW, 0f, -halfH));
        waypoints.Add(shapeCenter + new Vector3(halfW, 0f, -halfH));
        waypoints.Add(shapeCenter + new Vector3(halfW, 0f, halfH));
        waypoints.Add(shapeCenter + new Vector3(-halfW, 0f, halfH));

        isClosedLoop = true;
        pathType = PathType.Rectangle;

        Debug.Log($"[PathData] Generated rectangle: {shapeWidth}m x {shapeHeight}m");
    }

    /// <summary>
    /// Generates waypoints for a circle shape.
    /// </summary>
    public void GenerateCircle()
    {
        waypoints.Clear();

        float radius = shapeWidth / 2f;

        for (int i = 0; i < circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            waypoints.Add(shapeCenter + new Vector3(x, 0f, z));
        }

        isClosedLoop = true;
        pathType = PathType.Circle;

        Debug.Log($"[PathData] Generated circle: diameter {shapeWidth}m, {circleSegments} segments");
    }

    /// <summary>
    /// Generates waypoints for a simple line.
    /// </summary>
    /// <param name="start">Start point in local CNC space.</param>
    /// <param name="end">End point in local CNC space.</param>
    public void GenerateLine(Vector3 start, Vector3 end)
    {
        waypoints.Clear();
        waypoints.Add(start);
        waypoints.Add(end);

        isClosedLoop = false;
        pathType = PathType.Line;

        Debug.Log($"[PathData] Generated line: {start} → {end}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PATH QUERIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets the waypoint at a specific index, handling closed loops.
    /// </summary>
    /// <param name="index">Index of the waypoint.</param>
    /// <returns>The waypoint position.</returns>
    public Vector3 GetWaypoint(int index)
    {
        if (waypoints == null || waypoints.Count == 0)
            return Vector3.zero;

        if (isClosedLoop)
            index = index % waypoints.Count;
        else
            index = Mathf.Clamp(index, 0, waypoints.Count - 1);

        return waypoints[index];
    }

    /// <summary>
    /// Gets the direction from one waypoint to the next.
    /// </summary>
    /// <param name="index">Index of the starting waypoint.</param>
    /// <returns>Normalized direction vector.</returns>
    public Vector3 GetDirection(int index)
    {
        if (waypoints == null || waypoints.Count < 2)
            return Vector3.forward;

        Vector3 current = GetWaypoint(index);
        Vector3 next = GetWaypoint(index + 1);

        return (next - current).normalized;
    }

    /// <summary>
    /// Finds the closest point on the path to a given position.
    /// </summary>
    /// <param name="position">Position to check.</param>
    /// <returns>Closest point on the path.</returns>
    public Vector3 GetClosestPointOnPath(Vector3 position)
    {
        if (waypoints == null || waypoints.Count == 0)
            return position;

        if (waypoints.Count == 1)
            return waypoints[0];

        Vector3 closest = waypoints[0];
        float minDist = float.MaxValue;

        int count = isClosedLoop ? waypoints.Count : waypoints.Count - 1;

        for (int i = 0; i < count; i++)
        {
            Vector3 a = waypoints[i];
            Vector3 b = waypoints[(i + 1) % waypoints.Count];

            Vector3 point = GetClosestPointOnSegment(position, a, b);
            float dist = Vector3.Distance(position, point);

            if (dist < minDist)
            {
                minDist = dist;
                closest = point;
            }
        }

        return closest;
    }

    private Vector3 GetClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
        return a + t * ab;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensure positive values
        feedRate = Mathf.Max(0.01f, feedRate);
        plungeDepth = Mathf.Max(0.001f, plungeDepth);
        passes = Mathf.Max(1, passes);
        shapeWidth = Mathf.Max(0.01f, shapeWidth);
        shapeHeight = Mathf.Max(0.01f, shapeHeight);
    }

    /// <summary>
    /// Context menu to regenerate shape based on current pathType.
    /// </summary>
    [ContextMenu("Regenerate Shape")]
    private void RegenerateShape()
    {
        switch (pathType)
        {
            case PathType.Rectangle:
                GenerateRectangle();
                break;
            case PathType.Circle:
                GenerateCircle();
                break;
            case PathType.Line:
                if (waypoints.Count >= 2)
                    GenerateLine(waypoints[0], waypoints[waypoints.Count - 1]);
                break;
        }
    }
#endif
}

/// <summary>
/// Type of cutting path geometry.
/// </summary>
public enum PathType
{
    /// <summary>Simple straight line from start to end.</summary>
    Line,

    /// <summary>Rectangular pocket or outline.</summary>
    Rectangle,

    /// <summary>Circular pocket or outline.</summary>
    Circle,

    /// <summary>Custom path defined by manual waypoints.</summary>
    Custom
}

/// <summary>
/// How the tool is offset relative to the path line.
/// </summary>
public enum ToolOffsetMode
{
    /// <summary>Tool center follows the path line exactly.</summary>
    OnLine,

    /// <summary>Tool edge follows the inside of the path (for pockets).</summary>
    Inside,

    /// <summary>Tool edge follows the outside of the path (for outlines).</summary>
    Outside
}
