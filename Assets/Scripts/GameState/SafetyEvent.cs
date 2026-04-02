using UnityEngine;

/// <summary>
/// Data class representing a safety violation event in the CNC workflow.
/// 
/// This is a simple POCO class (not a MonoBehaviour or ScriptableObject)
/// used to pass safety violation data through the event system.
/// 
/// Usage:
/// - Created when a safety violation occurs
/// - Passed to GameStateEvents.RaiseSafetyViolation()
/// - Consumed by ConsequenceSystem for penalty application
/// </summary>
[System.Serializable]
public class SafetyEvent
{
    // ══════════════════════════════════════════════════════════════════════════
    // FIELDS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Type/category of the safety violation.</summary>
    public SafetyType safetyType;

    /// <summary>Severity level (1 = minor, 2 = moderate, 3 = severe).</summary>
    public int severity;

    /// <summary>Human-readable warning message for display.</summary>
    public string warningMessage;

    /// <summary>World position where the violation occurred.</summary>
    public Vector3 position;

    /// <summary>Timestamp when the violation occurred (Time.time).</summary>
    public float timestamp;

    /// <summary>Optional: The GameObject involved in the violation.</summary>
    public GameObject involvedObject;

    /// <summary>Optional: Additional context data.</summary>
    public string additionalData;

    // ══════════════════════════════════════════════════════════════════════════
    // CONSTRUCTORS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a new SafetyEvent with basic parameters.
    /// </summary>
    /// <param name="type">Type of safety violation.</param>
    /// <param name="severity">Severity level (1-3).</param>
    /// <param name="message">Warning message.</param>
    /// <param name="position">World position of the violation.</param>
    public SafetyEvent(SafetyType type, int severity, string message, Vector3 position)
    {
        this.safetyType = type;
        this.severity = Mathf.Clamp(severity, 1, 3);
        this.warningMessage = message;
        this.position = position;
        this.timestamp = Time.time;
        this.involvedObject = null;
        this.additionalData = string.Empty;
    }

    /// <summary>
    /// Creates a new SafetyEvent with all parameters.
    /// </summary>
    /// <param name="type">Type of safety violation.</param>
    /// <param name="severity">Severity level (1-3).</param>
    /// <param name="message">Warning message.</param>
    /// <param name="position">World position of the violation.</param>
    /// <param name="involvedObject">GameObject involved in the violation.</param>
    /// <param name="additionalData">Additional context data.</param>
    public SafetyEvent(
        SafetyType type, 
        int severity, 
        string message, 
        Vector3 position, 
        GameObject involvedObject, 
        string additionalData)
    {
        this.safetyType = type;
        this.severity = Mathf.Clamp(severity, 1, 3);
        this.warningMessage = message;
        this.position = position;
        this.timestamp = Time.time;
        this.involvedObject = involvedObject;
        this.additionalData = additionalData;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Returns true if this is a minor violation (severity 1).</summary>
    public bool IsMinor => severity == 1;

    /// <summary>Returns true if this is a moderate violation (severity 2).</summary>
    public bool IsModerate => severity == 2;

    /// <summary>Returns true if this is a severe violation (severity 3).</summary>
    public bool IsSevere => severity == 3;

    /// <summary>
    /// Gets the default score penalty for this safety event based on severity.
    /// </summary>
    public int DefaultPenalty
    {
        get
        {
            switch (severity)
            {
                case 1: return 5;   // Minor penalty
                case 2: return 15;  // Moderate penalty
                case 3: return 50;  // Severe penalty
                default: return 10;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UTILITY METHODS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns a formatted string representation of this safety event.
    /// </summary>
    public override string ToString()
    {
        string severityLabel = severity switch
        {
            1 => "MINOR",
            2 => "MODERATE",
            3 => "SEVERE",
            _ => "UNKNOWN"
        };

        return $"[{severityLabel}] {safetyType}: {warningMessage}";
    }

    /// <summary>
    /// Creates a log-friendly detailed description of this event.
    /// </summary>
    public string ToDetailedString()
    {
        return $"SafetyEvent:\n" +
               $"  Type: {safetyType}\n" +
               $"  Severity: {severity}\n" +
               $"  Message: {warningMessage}\n" +
               $"  Position: {position}\n" +
               $"  Timestamp: {timestamp:F2}s\n" +
               $"  Object: {(involvedObject != null ? involvedObject.name : "None")}\n" +
               $"  Additional: {(string.IsNullOrEmpty(additionalData) ? "None" : additionalData)}";
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FACTORY METHODS - Common Safety Events
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Creates a safety event for excessive cutting speed.</summary>
    public static SafetyEvent SpeedViolation(float currentSpeed, float maxSpeed, Vector3 position)
    {
        return new SafetyEvent(
            SafetyType.SpeedTooHigh,
            2,
            $"Cutting speed too high: {currentSpeed:F2} (max: {maxSpeed:F2})",
            position
        );
    }

    /// <summary>Creates a safety event for path deviation.</summary>
    public static SafetyEvent PathDeviation(float deviationAmount, Vector3 position)
    {
        int severity = deviationAmount > 0.02f ? 2 : 1;
        return new SafetyEvent(
            SafetyType.PathDeviation,
            severity,
            $"Path deviation: {deviationAmount * 1000f:F1}mm",
            position
        );
    }

    /// <summary>Creates a safety event for missing workpiece.</summary>
    public static SafetyEvent NoWorkpiece(Vector3 position)
    {
        return new SafetyEvent(
            SafetyType.NoWorkpieceLoaded,
            2,
            "No workpiece loaded in machine",
            position
        );
    }

    /// <summary>Creates a safety event for emergency stop.</summary>
    public static SafetyEvent EmergencyStop(Vector3 position, string reason = "Emergency stop activated")
    {
        return new SafetyEvent(
            SafetyType.Emergency,
            3,
            reason,
            position
        );
    }

    /// <summary>Creates a safety event for improper operation sequence.</summary>
    public static SafetyEvent ImproperSequence(string expectedStep, string attemptedAction, Vector3 position)
    {
        return new SafetyEvent(
            SafetyType.ImproperSequence,
            1,
            $"Attempted '{attemptedAction}' but expected '{expectedStep}'",
            position
        );
    }

    /// <summary>Creates a safety event for cutting outside workpiece bounds.</summary>
    public static SafetyEvent OutOfBounds(Vector3 cutterPosition, Vector3 workpieceBounds)
    {
        return new SafetyEvent(
            SafetyType.OutOfBounds,
            2,
            "Cutter operating outside workpiece bounds",
            cutterPosition
        );
    }

    /// <summary>Creates a safety event for excessive plunge depth.</summary>
    public static SafetyEvent ExcessiveDepth(float depth, float maxDepth, Vector3 position)
    {
        return new SafetyEvent(
            SafetyType.ExcessiveDepth,
            2,
            $"Plunge depth {depth * 1000f:F1}mm exceeds maximum {maxDepth * 1000f:F1}mm",
            position
        );
    }

    /// <summary>Creates a safety event for tool collision.</summary>
    public static SafetyEvent ToolCollision(GameObject collidedWith, Vector3 position)
    {
        return new SafetyEvent(
            SafetyType.ToolCollision,
            3,
            $"Tool collided with {collidedWith?.name ?? "unknown object"}",
            position,
            collidedWith,
            string.Empty
        );
    }
}

/// <summary>
/// Categories of safety violations that can occur during CNC operation.
/// </summary>
public enum SafetyType
{
    /// <summary>Cutting speed exceeds safe limits.</summary>
    SpeedTooHigh,

    /// <summary>Tool has deviated from the programmed path.</summary>
    PathDeviation,

    /// <summary>Attempted to cut without a workpiece loaded.</summary>
    NoWorkpieceLoaded,

    /// <summary>Emergency stop was triggered.</summary>
    Emergency,

    /// <summary>Operations performed out of required sequence.</summary>
    ImproperSequence,

    /// <summary>Cutter operating outside workpiece boundaries.</summary>
    OutOfBounds,

    /// <summary>Plunge depth exceeds safe limits or workpiece thickness.</summary>
    ExcessiveDepth,

    /// <summary>Tool has collided with an obstacle or clamp.</summary>
    ToolCollision,

    /// <summary>Machine operated without required safety equipment active.</summary>
    SafetyEquipmentOff,

    /// <summary>Operator in unsafe proximity to moving parts.</summary>
    ProximityWarning,

    /// <summary>Generic/unspecified safety violation.</summary>
    Other
}
