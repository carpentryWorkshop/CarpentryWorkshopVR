using UnityEngine;

/// <summary>
/// ScriptableObject representing a single step within a task.
/// 
/// Create via: Assets > Create > CarpentryWorkshopVR > Task Step
/// 
/// TaskSteps define individual actions the player must complete as part of a larger Task.
/// Each step has validation conditions that determine when it's considered complete.
/// 
/// Usage:
/// - Create TaskStep assets for each action (e.g., "Load workpiece", "Start CNC")
/// - Add steps to a Task in order
/// - TaskManager tracks completion based on step validation
/// </summary>
[CreateAssetMenu(fileName = "TaskStep", menuName = "CarpentryWorkshopVR/Task Step")]
public class TaskStep : ScriptableObject
{
    // ══════════════════════════════════════════════════════════════════════════
    // IDENTIFICATION
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Identification")]
    [Tooltip("Unique identifier for this step.")]
    public string stepId = "step_default";

    [Tooltip("Display name shown to the player.")]
    public string stepName = "Task Step";

    [Tooltip("Detailed instructions for completing this step.")]
    [TextArea(2, 4)]
    public string instructions = "Complete this step to proceed.";

    [Tooltip("Short hint displayed if player is stuck (optional).")]
    public string hint = "";

    // ══════════════════════════════════════════════════════════════════════════
    // VALIDATION
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Validation")]
    [Tooltip("Type of event that completes this step.")]
    public StepTrigger completionTrigger = StepTrigger.Manual;

    [Tooltip("Required machine for machine-based triggers.")]
    public MachineType requiredMachine = MachineType.None;

    [Tooltip("Required state for state-based triggers.")]
    public CNCState requiredCNCState = CNCState.Idle;

    [Tooltip("If true, step auto-completes when trigger fires. If false, manual validation required.")]
    public bool autoComplete = true;

    // ══════════════════════════════════════════════════════════════════════════
    // REQUIREMENTS
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Requirements")]
    [Tooltip("Workpiece type required for this step (optional).")]
    public WorkpieceData requiredWorkpiece;

    [Tooltip("Path that must be loaded for this step (optional).")]
    public PathData requiredPath;

    [Tooltip("Minimum score required to attempt this step.")]
    public int minimumScore = 0;

    [Tooltip("Steps that must be completed before this one (for non-linear tasks).")]
    public TaskStep[] prerequisites;

    // ══════════════════════════════════════════════════════════════════════════
    // LOCKING
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Locking (Guided Mode)")]
    [Tooltip("If true, other machines are locked while this step is active.")]
    public bool lockOtherMachines = true;

    [Tooltip("Machines that remain accessible during this step (when lockOtherMachines is true).")]
    public MachineType[] allowedMachines;

    // ══════════════════════════════════════════════════════════════════════════
    // SCORING
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Scoring")]
    [Tooltip("Points awarded for completing this step.")]
    public int completionPoints = 10;

    [Tooltip("Bonus points for completing within time limit.")]
    public int timeBonusPoints = 5;

    [Tooltip("Time limit for bonus in seconds (0 = no time limit).")]
    public float timeLimitForBonus = 0f;

    [Tooltip("Penalty for failing this step.")]
    public int failurePenalty = 5;

    // ══════════════════════════════════════════════════════════════════════════
    // FEEDBACK
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Feedback")]
    [Tooltip("Audio clip to play when step is started (optional).")]
    public AudioClip startSound;

    [Tooltip("Audio clip to play when step is completed (optional).")]
    public AudioClip completionSound;

    [Tooltip("Message displayed on step completion.")]
    public string completionMessage = "Step complete!";

    [Tooltip("Message displayed on step failure.")]
    public string failureMessage = "Step failed. Try again.";

    // ══════════════════════════════════════════════════════════════════════════
    // VISUAL GUIDANCE
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Visual Guidance")]
    [Tooltip("If true, highlight the target object for this step.")]
    public bool highlightTarget = true;

    [Tooltip("Tag of the object to highlight (e.g., 'CNCMachine', 'Workpiece').")]
    public string targetTag = "";

    [Tooltip("Transform path for finding the target (e.g., 'CNCMachine/ControlPanel/StartButton').")]
    public string targetPath = "";

    [Tooltip("Color for highlighting the target.")]
    public Color highlightColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);

    // ══════════════════════════════════════════════════════════════════════════
    // VALIDATION METHODS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Checks if all prerequisites for this step are met.
    /// </summary>
    /// <param name="completedSteps">Array of completed step IDs.</param>
    /// <returns>True if all prerequisites are completed.</returns>
    public bool ArePrerequisitesMet(string[] completedSteps)
    {
        if (prerequisites == null || prerequisites.Length == 0)
            return true;

        foreach (var prereq in prerequisites)
        {
            if (prereq == null)
                continue;

            bool found = false;
            foreach (var completed in completedSteps)
            {
                if (completed == prereq.stepId)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a machine is allowed during this step.
    /// </summary>
    /// <param name="machine">The machine type to check.</param>
    /// <returns>True if the machine can be used.</returns>
    public bool IsMachineAllowed(MachineType machine)
    {
        if (!lockOtherMachines)
            return true;

        if (machine == requiredMachine)
            return true;

        if (allowedMachines != null)
        {
            foreach (var allowed in allowedMachines)
            {
                if (allowed == machine)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates this step configuration.
    /// </summary>
    /// <returns>True if the step is properly configured.</returns>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(stepId))
        {
            Debug.LogWarning($"[TaskStep] {name}: Missing step ID.");
            return false;
        }

        if (string.IsNullOrEmpty(stepName))
        {
            Debug.LogWarning($"[TaskStep] {name}: Missing step name.");
            return false;
        }

        return true;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UTILITY
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns a summary string for debugging.
    /// </summary>
    public override string ToString()
    {
        return $"TaskStep[{stepId}]: {stepName} (Trigger: {completionTrigger})";
    }
}

/// <summary>
/// Types of events that can trigger step completion.
/// </summary>
public enum StepTrigger
{
    /// <summary>Step is manually marked complete by code or UI.</summary>
    Manual,

    /// <summary>Step completes when a workpiece is spawned.</summary>
    WorkpieceSpawned,

    /// <summary>Step completes when a workpiece reaches a transfer point.</summary>
    WorkpieceTransferred,

    /// <summary>Step completes when CNC enters a specific state.</summary>
    CNCStateChange,

    /// <summary>Step completes when a path is loaded into the CNC.</summary>
    PathLoaded,

    /// <summary>Step completes when CNC cutting finishes.</summary>
    CuttingComplete,

    /// <summary>Step completes when a workpiece is cut.</summary>
    WorkpieceCut,

    /// <summary>Step completes when a button is pressed.</summary>
    ButtonPressed,

    /// <summary>Step completes when player enters a trigger zone.</summary>
    TriggerEntered,

    /// <summary>Step completes after a timer expires.</summary>
    TimerExpired,

    /// <summary>Step completes when a specific score is reached.</summary>
    ScoreReached
}

/// <summary>
/// Types of machines in the workshop.
/// </summary>
public enum MachineType
{
    /// <summary>No specific machine.</summary>
    None,

    /// <summary>CNC Router machine.</summary>
    CNCRouter,

    /// <summary>Table Saw machine.</summary>
    TableSaw,

    /// <summary>Band Saw machine.</summary>
    BandSaw,

    /// <summary>Drill Press machine.</summary>
    DrillPress,

    /// <summary>Sander machine.</summary>
    Sander,

    /// <summary>Lathe machine.</summary>
    Lathe,

    /// <summary>Conveyor belt system.</summary>
    Conveyor,

    /// <summary>Workpiece spawner/storage.</summary>
    Spawner,

    /// <summary>Assembly station.</summary>
    AssemblyStation,

    /// <summary>Quality inspection station.</summary>
    InspectionStation
}
