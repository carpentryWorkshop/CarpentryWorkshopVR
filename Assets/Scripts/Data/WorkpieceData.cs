using UnityEngine;

/// <summary>
/// ScriptableObject that defines the properties of a wood workpiece (blank).
/// 
/// Create via: Assets > Create > CarpentryWorkshopVR > Workpiece Data
/// 
/// Usage:
/// - Create instances for different wood blank types (small, large, plywood, etc.)
/// - Assign to ObjectSpawner to define what gets spawned
/// - Referenced by Workpiece component at runtime
/// </summary>
[CreateAssetMenu(fileName = "WorkpieceData", menuName = "CarpentryWorkshopVR/Workpiece Data")]
public class WorkpieceData : ScriptableObject
{
    // ══════════════════════════════════════════════════════════════════════════
    // IDENTIFICATION
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Identification")]
    [Tooltip("Display name for this workpiece type.")]
    public string workpieceName = "Wood Blank";

    [Tooltip("Unique identifier for this workpiece type.")]
    public string workpieceId = "wood_blank_default";

    [Tooltip("Description of this workpiece.")]
    [TextArea(2, 4)]
    public string description = "A standard wood blank for CNC cutting.";

    // ══════════════════════════════════════════════════════════════════════════
    // PHYSICAL PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Physical Properties")]
    [Tooltip("Dimensions of the workpiece in meters (width, height, depth).")]
    public Vector3 dimensions = new Vector3(0.5f, 0.05f, 0.5f);

    [Tooltip("Density of the wood in kg/m³. Pine: ~500, Oak: ~750, MDF: ~700")]
    [Range(300f, 1200f)]
    public float density = 600f;

    [Tooltip("Wood hardness factor. Affects cutting difficulty and sound. 0=soft (pine), 1=hard (oak)")]
    [Range(0f, 1f)]
    public float hardness = 0.3f;

    // ══════════════════════════════════════════════════════════════════════════
    // VISUAL
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Visual")]
    [Tooltip("The prefab to instantiate for this workpiece. Should have MeshFilter and MeshRenderer.")]
    public GameObject prefab;

    [Tooltip("Material applied to the workpiece surface.")]
    public Material surfaceMaterial;

    [Tooltip("Material applied to cut cross-sections (exposed wood).")]
    public Material crossSectionMaterial;

    // ══════════════════════════════════════════════════════════════════════════
    // CUT BEHAVIOR
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Cut Behavior")]
    [Tooltip("Can this workpiece be cut by the CNC machine?")]
    public bool isCuttable = true;

    [Tooltip("Maximum number of cuts this workpiece can sustain before it's considered waste.")]
    [Range(1, 20)]
    public int maxCuts = 5;

    [Tooltip("Minimum thickness remaining after cuts (meters). Prevents cutting all the way through.")]
    [Range(0.001f, 0.05f)]
    public float minimumThickness = 0.005f;

    // ══════════════════════════════════════════════════════════════════════════
    // PHYSICS
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Physics")]
    [Tooltip("Physics material for collision behavior.")]
    public PhysicsMaterial physicsMaterial;

    [Tooltip("Drag coefficient when moving.")]
    [Range(0f, 5f)]
    public float drag = 0.5f;

    [Tooltip("Angular drag coefficient when rotating.")]
    [Range(0f, 5f)]
    public float angularDrag = 0.5f;

    // ══════════════════════════════════════════════════════════════════════════
    // AUDIO
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Audio (Optional)")]
    [Tooltip("Sound played when this workpiece collides with something.")]
    public AudioClip impactSound;

    [Tooltip("Sound played when this workpiece is being cut.")]
    public AudioClip cuttingSound;

    // ══════════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Calculated volume in cubic meters.</summary>
    public float Volume => dimensions.x * dimensions.y * dimensions.z;

    /// <summary>Calculated mass in kilograms.</summary>
    public float Mass => Volume * density;

    /// <summary>Calculated surface area of the top face (for CNC cutting).</summary>
    public float TopSurfaceArea => dimensions.x * dimensions.z;

    // ══════════════════════════════════════════════════════════════════════════
    // VALIDATION
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Validates that this workpiece data is properly configured.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(workpieceName))
        {
            Debug.LogWarning($"[WorkpieceData] {name}: Missing workpiece name.");
            return false;
        }

        if (prefab == null)
        {
            Debug.LogWarning($"[WorkpieceData] {name}: Missing prefab.");
            return false;
        }

        if (dimensions.x <= 0f || dimensions.y <= 0f || dimensions.z <= 0f)
        {
            Debug.LogWarning($"[WorkpieceData] {name}: Invalid dimensions.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Creates a copy of this workpiece data.
    /// </summary>
    /// <returns>A new WorkpieceData instance with copied values.</returns>
    public WorkpieceData Clone()
    {
        var clone = CreateInstance<WorkpieceData>();
        clone.workpieceName = workpieceName;
        clone.workpieceId = workpieceId;
        clone.description = description;
        clone.dimensions = dimensions;
        clone.density = density;
        clone.hardness = hardness;
        clone.prefab = prefab;
        clone.surfaceMaterial = surfaceMaterial;
        clone.crossSectionMaterial = crossSectionMaterial;
        clone.isCuttable = isCuttable;
        clone.maxCuts = maxCuts;
        clone.minimumThickness = minimumThickness;
        clone.physicsMaterial = physicsMaterial;
        clone.drag = drag;
        clone.angularDrag = angularDrag;
        clone.impactSound = impactSound;
        clone.cuttingSound = cuttingSound;
        return clone;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensure positive dimensions
        dimensions.x = Mathf.Max(0.01f, dimensions.x);
        dimensions.y = Mathf.Max(0.01f, dimensions.y);
        dimensions.z = Mathf.Max(0.01f, dimensions.z);

        // Ensure minimum thickness doesn't exceed workpiece height
        minimumThickness = Mathf.Min(minimumThickness, dimensions.y * 0.9f);
    }
#endif
}
