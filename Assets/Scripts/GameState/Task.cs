using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject representing a complete task in the CNC workflow.
/// 
/// Create via: Assets > Create > CarpentryWorkshopVR > Task
/// 
/// A Task contains multiple TaskSteps that must be completed in order (or according to prerequisites).
/// Tasks define the high-level objectives like "Create a wooden coaster" or "Cut a picture frame".
/// 
/// Usage:
/// - Create Task assets defining complete objectives
/// - Add TaskStep children for each action required
/// - Load tasks via TaskManager to activate guided workflow
/// </summary>
[CreateAssetMenu(fileName = "Task", menuName = "CarpentryWorkshopVR/Task")]
public class Task : ScriptableObject
{
    // ══════════════════════════════════════════════════════════════════════════
    // IDENTIFICATION
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Identification")]
    [Tooltip("Unique identifier for this task.")]
    public string taskId = "task_default";

    [Tooltip("Display name shown to the player.")]
    public string taskName = "Workshop Task";

    [Tooltip("Detailed description of what this task accomplishes.")]
    [TextArea(3, 6)]
    public string description = "Complete all steps to finish this task.";

    [Tooltip("Category for organizing tasks (e.g., 'Tutorial', 'Beginner', 'Advanced').")]
    public string category = "General";

    [Tooltip("Difficulty level (1-5 stars).")]
    [Range(1, 5)]
    public int difficulty = 1;

    // ══════════════════════════════════════════════════════════════════════════
    // TASK STEPS
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Task Steps")]
    [Tooltip("Ordered list of steps to complete this task.")]
    public List<TaskStep> steps = new List<TaskStep>();

    [Tooltip("If true, steps must be completed in order. If false, any order works.")]
    public bool requireStepOrder = true;

    // ══════════════════════════════════════════════════════════════════════════
    // REQUIREMENTS
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Requirements")]
    [Tooltip("Tasks that must be completed before this one is available.")]
    public Task[] prerequisites;

    [Tooltip("Minimum player level required (for progression systems).")]
    public int requiredLevel = 0;

    [Tooltip("If true, this task is available from the start.")]
    public bool availableByDefault = true;

    // ══════════════════════════════════════════════════════════════════════════
    // SCORING
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Scoring")]
    [Tooltip("Base points awarded for completing the entire task.")]
    public int completionPoints = 100;

    [Tooltip("Bonus points for completing within time limit.")]
    public int timeBonusPoints = 50;

    [Tooltip("Time limit for bonus in seconds (0 = no limit).")]
    public float timeLimitForBonus = 0f;

    [Tooltip("Maximum error count before task fails (0 = unlimited).")]
    public int maxErrors = 0;

    [Tooltip("Penalty per error during task.")]
    public int errorPenalty = 10;

    // ══════════════════════════════════════════════════════════════════════════
    // REWARDS
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Rewards")]
    [Tooltip("Tasks unlocked upon completion.")]
    public Task[] unlocksOnComplete;

    [Tooltip("Achievement ID awarded on completion (optional).")]
    public string achievementId = "";

    [Tooltip("If true, player keeps the created workpiece.")]
    public bool keepResult = true;

    // ══════════════════════════════════════════════════════════════════════════
    // DISPLAY
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Display")]
    [Tooltip("Icon displayed in task selection UI.")]
    public Sprite icon;

    [Tooltip("Preview image of the completed result.")]
    public Sprite resultPreview;

    [Tooltip("Estimated time to complete in minutes.")]
    public float estimatedMinutes = 5f;

    // ══════════════════════════════════════════════════════════════════════════
    // AUDIO
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Audio")]
    [Tooltip("Sound played when task starts.")]
    public AudioClip taskStartSound;

    [Tooltip("Sound played when task completes successfully.")]
    public AudioClip taskCompleteSound;

    [Tooltip("Sound played when task fails.")]
    public AudioClip taskFailSound;

    [Tooltip("Voiceover introduction (optional).")]
    public AudioClip introVoiceover;

    // ══════════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Total number of steps in this task.</summary>
    public int StepCount => steps?.Count ?? 0;

    /// <summary>Total points available from all steps plus completion bonus.</summary>
    public int TotalPossiblePoints
    {
        get
        {
            int total = completionPoints + timeBonusPoints;
            if (steps != null)
            {
                foreach (var step in steps)
                {
                    if (step != null)
                        total += step.completionPoints + step.timeBonusPoints;
                }
            }
            return total;
        }
    }

    /// <summary>Estimated total time in seconds.</summary>
    public float EstimatedSeconds => estimatedMinutes * 60f;

    /// <summary>True if this task has prerequisites.</summary>
    public bool HasPrerequisites => prerequisites != null && prerequisites.Length > 0;

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC METHODS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets a step by index.
    /// </summary>
    /// <param name="index">Step index.</param>
    /// <returns>The TaskStep, or null if index is out of range.</returns>
    public TaskStep GetStep(int index)
    {
        if (steps == null || index < 0 || index >= steps.Count)
            return null;

        return steps[index];
    }

    /// <summary>
    /// Gets a step by ID.
    /// </summary>
    /// <param name="stepId">The step's unique ID.</param>
    /// <returns>The TaskStep, or null if not found.</returns>
    public TaskStep GetStepById(string stepId)
    {
        if (steps == null || string.IsNullOrEmpty(stepId))
            return null;

        foreach (var step in steps)
        {
            if (step != null && step.stepId == stepId)
                return step;
        }

        return null;
    }

    /// <summary>
    /// Gets the index of a step.
    /// </summary>
    /// <param name="step">The step to find.</param>
    /// <returns>Index of the step, or -1 if not found.</returns>
    public int GetStepIndex(TaskStep step)
    {
        if (steps == null || step == null)
            return -1;

        return steps.IndexOf(step);
    }

    /// <summary>
    /// Checks if all prerequisites for this task are met.
    /// </summary>
    /// <param name="completedTaskIds">Array of completed task IDs.</param>
    /// <returns>True if all prerequisites are completed.</returns>
    public bool ArePrerequisitesMet(string[] completedTaskIds)
    {
        if (prerequisites == null || prerequisites.Length == 0)
            return true;

        foreach (var prereq in prerequisites)
        {
            if (prereq == null)
                continue;

            bool found = false;
            foreach (var completed in completedTaskIds)
            {
                if (completed == prereq.taskId)
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
    /// Checks if this task is available for the player.
    /// </summary>
    /// <param name="completedTaskIds">Array of completed task IDs.</param>
    /// <param name="playerLevel">Current player level.</param>
    /// <returns>True if the task can be started.</returns>
    public bool IsAvailable(string[] completedTaskIds, int playerLevel = 1)
    {
        if (!availableByDefault && !ArePrerequisitesMet(completedTaskIds))
            return false;

        if (playerLevel < requiredLevel)
            return false;

        return true;
    }

    /// <summary>
    /// Calculates the final score for task completion.
    /// </summary>
    /// <param name="stepScores">Points earned from each step.</param>
    /// <param name="completionTime">Time taken to complete the task.</param>
    /// <param name="errorCount">Number of errors made.</param>
    /// <returns>Final calculated score.</returns>
    public int CalculateFinalScore(int[] stepScores, float completionTime, int errorCount)
    {
        int total = completionPoints;

        // Add step scores
        if (stepScores != null)
        {
            foreach (int score in stepScores)
            {
                total += score;
            }
        }

        // Add time bonus if within limit
        if (timeLimitForBonus > 0f && completionTime <= timeLimitForBonus)
        {
            total += timeBonusPoints;
        }

        // Apply error penalties
        total -= errorCount * errorPenalty;

        return Mathf.Max(0, total);
    }

    /// <summary>
    /// Validates that this task is properly configured.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(taskId))
        {
            Debug.LogWarning($"[Task] {name}: Missing task ID.");
            return false;
        }

        if (string.IsNullOrEmpty(taskName))
        {
            Debug.LogWarning($"[Task] {name}: Missing task name.");
            return false;
        }

        if (steps == null || steps.Count == 0)
        {
            Debug.LogWarning($"[Task] {name}: No steps defined.");
            return false;
        }

        // Validate all steps
        foreach (var step in steps)
        {
            if (step == null)
            {
                Debug.LogWarning($"[Task] {name}: Contains null step.");
                return false;
            }

            if (!step.IsValid())
                return false;
        }

        return true;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UTILITY
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a runtime instance of this task for tracking progress.
    /// </summary>
    /// <returns>New TaskProgress instance.</returns>
    public TaskProgress CreateProgressTracker()
    {
        return new TaskProgress(this);
    }

    /// <summary>
    /// Returns a summary string for debugging.
    /// </summary>
    public override string ToString()
    {
        return $"Task[{taskId}]: {taskName} ({StepCount} steps, {difficulty} stars)";
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensure positive values
        difficulty = Mathf.Clamp(difficulty, 1, 5);
        completionPoints = Mathf.Max(0, completionPoints);
        estimatedMinutes = Mathf.Max(0.5f, estimatedMinutes);
    }

    /// <summary>
    /// Context menu to validate the task.
    /// </summary>
    [ContextMenu("Validate Task")]
    private void ValidateTask()
    {
        if (IsValid())
            Debug.Log($"[Task] {name}: Validation passed!");
        else
            Debug.LogError($"[Task] {name}: Validation failed. See warnings above.");
    }
#endif
}

/// <summary>
/// Runtime progress tracker for a task instance.
/// </summary>
[System.Serializable]
public class TaskProgress
{
    /// <summary>Reference to the task being tracked.</summary>
    public Task task;

    /// <summary>Current step index.</summary>
    public int currentStepIndex;

    /// <summary>List of completed step IDs.</summary>
    public List<string> completedSteps;

    /// <summary>Scores earned for each completed step.</summary>
    public List<int> stepScores;

    /// <summary>Time when task was started (Time.time).</summary>
    public float startTime;

    /// <summary>Time when task was completed (0 if not complete).</summary>
    public float completionTime;

    /// <summary>Number of errors during this task.</summary>
    public int errorCount;

    /// <summary>Current status of the task.</summary>
    public TaskStatus status;

    /// <summary>True if all steps are complete.</summary>
    public bool IsComplete => status == TaskStatus.Completed;

    /// <summary>True if task is currently in progress.</summary>
    public bool IsInProgress => status == TaskStatus.InProgress;

    /// <summary>Progress percentage (0-1).</summary>
    public float Progress => task != null && task.StepCount > 0 
        ? (float)completedSteps.Count / task.StepCount 
        : 0f;

    /// <summary>Current step being worked on.</summary>
    public TaskStep CurrentStep => task?.GetStep(currentStepIndex);

    /// <summary>Time elapsed since task started.</summary>
    public float ElapsedTime => status == TaskStatus.InProgress 
        ? Time.time - startTime 
        : completionTime - startTime;

    public TaskProgress(Task task)
    {
        this.task = task;
        this.currentStepIndex = 0;
        this.completedSteps = new List<string>();
        this.stepScores = new List<int>();
        this.startTime = Time.time;
        this.completionTime = 0f;
        this.errorCount = 0;
        this.status = TaskStatus.NotStarted;
    }

    /// <summary>
    /// Marks the current step as complete and advances to the next.
    /// </summary>
    /// <param name="score">Score earned for this step.</param>
    /// <returns>True if task is now complete.</returns>
    public bool CompleteCurrentStep(int score)
    {
        if (task == null || currentStepIndex >= task.StepCount)
            return false;

        TaskStep step = task.GetStep(currentStepIndex);
        if (step != null)
        {
            completedSteps.Add(step.stepId);
            stepScores.Add(score);
        }

        currentStepIndex++;

        if (currentStepIndex >= task.StepCount)
        {
            status = TaskStatus.Completed;
            completionTime = Time.time;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Records an error during this task.
    /// </summary>
    public void RecordError()
    {
        errorCount++;

        // Check if max errors exceeded
        if (task != null && task.maxErrors > 0 && errorCount >= task.maxErrors)
        {
            status = TaskStatus.Failed;
            completionTime = Time.time;
        }
    }

    /// <summary>
    /// Calculates the final score for this task progress.
    /// </summary>
    /// <returns>Final score.</returns>
    public int CalculateFinalScore()
    {
        if (task == null)
            return 0;

        return task.CalculateFinalScore(stepScores.ToArray(), ElapsedTime, errorCount);
    }
}

/// <summary>
/// Status of a task.
/// </summary>
public enum TaskStatus
{
    /// <summary>Task has not been started.</summary>
    NotStarted,

    /// <summary>Task is currently in progress.</summary>
    InProgress,

    /// <summary>Task was completed successfully.</summary>
    Completed,

    /// <summary>Task failed (too many errors, time limit exceeded, etc.).</summary>
    Failed,

    /// <summary>Task was abandoned/cancelled.</summary>
    Abandoned
}
