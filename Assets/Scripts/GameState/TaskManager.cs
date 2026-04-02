using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton manager that orchestrates task progression in the CNC workflow.
/// 
/// TaskManager is responsible for:
/// - Loading and tracking task progress
/// - Validating step completion based on game events
/// - Locking/unlocking machines based on current step requirements
/// - Raising task-related events via GameStateEvents
/// 
/// Setup:
/// 1. Add this component to a persistent GameObject in the scene
/// 2. Assign available tasks in the inspector
/// 3. Access via TaskManager.Instance
/// </summary>
public class TaskManager : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // SINGLETON
    // ══════════════════════════════════════════════════════════════════════════

    private static TaskManager _instance;

    /// <summary>Global singleton instance.</summary>
    public static TaskManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TaskManager>();
                if (_instance == null)
                {
                    Debug.LogWarning("[TaskManager] No instance found in scene. Creating one.");
                    var go = new GameObject("TaskManager");
                    _instance = go.AddComponent<TaskManager>();
                }
            }
            return _instance;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EVENTS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Fires when a task is loaded and ready to start.</summary>
    public event Action<Task> OnTaskLoaded;

    /// <summary>Fires when a task starts.</summary>
    public event Action<Task> OnTaskStarted;

    /// <summary>Fires when a task is completed.</summary>
    public event Action<Task, TaskProgress> OnTaskCompleted;

    /// <summary>Fires when a task fails.</summary>
    public event Action<Task, TaskProgress> OnTaskFailed;

    /// <summary>Fires when a step is completed.</summary>
    public event Action<TaskStep, int> OnStepCompleted;

    /// <summary>Fires when the current step changes.</summary>
    public event Action<TaskStep> OnStepChanged;

    /// <summary>Fires when a machine lock state changes.</summary>
    public event Action<MachineType, bool> OnMachineLockChanged;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Tasks")]
    [Tooltip("List of available tasks.")]
    [SerializeField] private List<Task> _availableTasks = new List<Task>();

    [Tooltip("Task to auto-start on scene load (optional).")]
    [SerializeField] private Task _autoStartTask;

    [Header("Behavior")]
    [Tooltip("Enable guided mode (machines locked until required step).")]
    [SerializeField] private bool _guidedMode = true;

    [Tooltip("Auto-advance to next step when completion trigger fires.")]
    [SerializeField] private bool _autoAdvance = true;

    [Tooltip("Show hint after this many seconds of inactivity on a step (0 = disabled).")]
    [SerializeField] private float _hintDelaySeconds = 30f;

    [Header("Debug")]
    [Tooltip("Log task/step transitions to console.")]
    [SerializeField] private bool _verboseLogging = true;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private Task _currentTask;
    private TaskProgress _currentProgress;
    private float _stepStartTime;
    private List<string> _completedTaskIds;
    private Dictionary<MachineType, bool> _machineLocks;
    private bool _isInitialized;

    // ══════════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Currently active task.</summary>
    public Task CurrentTask => _currentTask;

    /// <summary>Progress tracker for the current task.</summary>
    public TaskProgress CurrentProgress => _currentProgress;

    /// <summary>Current step being worked on.</summary>
    public TaskStep CurrentStep => _currentProgress?.CurrentStep;

    /// <summary>Index of the current step.</summary>
    public int CurrentStepIndex => _currentProgress?.currentStepIndex ?? -1;

    /// <summary>True if a task is currently active.</summary>
    public bool HasActiveTask => _currentTask != null && _currentProgress != null && _currentProgress.IsInProgress;

    /// <summary>True if guided mode is enabled.</summary>
    public bool IsGuidedMode => _guidedMode;

    /// <summary>List of available tasks.</summary>
    public List<Task> AvailableTasks => _availableTasks;

    /// <summary>List of completed task IDs.</summary>
    public List<string> CompletedTaskIds => _completedTaskIds;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // Singleton enforcement
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[TaskManager] Duplicate instance detected. Destroying this one.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        Initialize();
    }

    private void OnEnable()
    {
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void Start()
    {
        // Auto-start task if configured
        if (_autoStartTask != null)
        {
            StartTask(_autoStartTask);
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Task Management
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Loads and starts a task.
    /// </summary>
    /// <param name="task">The task to start.</param>
    /// <returns>True if task was started successfully.</returns>
    public bool StartTask(Task task)
    {
        if (task == null)
        {
            Debug.LogWarning("[TaskManager] Cannot start null task.");
            return false;
        }

        if (!task.IsValid())
        {
            Debug.LogWarning($"[TaskManager] Task '{task.taskName}' is not valid.");
            return false;
        }

        // Check prerequisites
        if (!task.IsAvailable(_completedTaskIds.ToArray()))
        {
            Debug.LogWarning($"[TaskManager] Task '{task.taskName}' prerequisites not met.");
            return false;
        }

        // Stop current task if any
        if (HasActiveTask)
        {
            AbandonTask();
        }

        _currentTask = task;
        _currentProgress = task.CreateProgressTracker();
        _currentProgress.status = TaskStatus.InProgress;
        _stepStartTime = Time.time;

        // Apply machine locks for first step
        if (_guidedMode)
        {
            UpdateMachineLocks();
        }

        OnTaskLoaded?.Invoke(task);
        OnTaskStarted?.Invoke(task);
        GameStateEvents.RaiseTaskStarted(task.taskName);

        // Notify about first step
        TaskStep firstStep = _currentProgress.CurrentStep;
        if (firstStep != null)
        {
            OnStepChanged?.Invoke(firstStep);
            GameStateEvents.RaiseStepStarted(firstStep.stepName);
        }

        if (_verboseLogging)
            Debug.Log($"[TaskManager] Started task: {task.taskName}");

        return true;
    }

    /// <summary>
    /// Starts a task by ID.
    /// </summary>
    /// <param name="taskId">The task ID to start.</param>
    /// <returns>True if task was started successfully.</returns>
    public bool StartTaskById(string taskId)
    {
        Task task = GetTaskById(taskId);
        if (task == null)
        {
            Debug.LogWarning($"[TaskManager] Task with ID '{taskId}' not found.");
            return false;
        }

        return StartTask(task);
    }

    /// <summary>
    /// Abandons the current task.
    /// </summary>
    public void AbandonTask()
    {
        if (!HasActiveTask)
            return;

        _currentProgress.status = TaskStatus.Abandoned;
        
        OnTaskFailed?.Invoke(_currentTask, _currentProgress);

        if (_verboseLogging)
            Debug.Log($"[TaskManager] Abandoned task: {_currentTask.taskName}");

        ClearCurrentTask();
    }

    /// <summary>
    /// Gets a task by ID.
    /// </summary>
    /// <param name="taskId">The task ID to find.</param>
    /// <returns>The task, or null if not found.</returns>
    public Task GetTaskById(string taskId)
    {
        foreach (var task in _availableTasks)
        {
            if (task != null && task.taskId == taskId)
                return task;
        }
        return null;
    }

    /// <summary>
    /// Gets all tasks available to the player.
    /// </summary>
    /// <returns>List of available tasks.</returns>
    public List<Task> GetAvailableTasks()
    {
        var available = new List<Task>();
        string[] completed = _completedTaskIds.ToArray();

        foreach (var task in _availableTasks)
        {
            if (task != null && task.IsAvailable(completed))
                available.Add(task);
        }

        return available;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Step Management
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Manually completes the current step.
    /// </summary>
    /// <returns>True if step was completed.</returns>
    public bool CompleteCurrentStep()
    {
        if (!HasActiveTask || CurrentStep == null)
            return false;

        return CompleteStep(CurrentStep);
    }

    /// <summary>
    /// Completes a specific step.
    /// </summary>
    /// <param name="step">The step to complete.</param>
    /// <returns>True if step was completed.</returns>
    public bool CompleteStep(TaskStep step)
    {
        if (!HasActiveTask || step == null)
            return false;

        // Verify this is the current step (if ordered)
        if (_currentTask.requireStepOrder && step != CurrentStep)
        {
            Debug.LogWarning($"[TaskManager] Cannot complete step '{step.stepName}' - not the current step.");
            return false;
        }

        // Calculate score
        float stepTime = Time.time - _stepStartTime;
        int score = step.completionPoints;

        // Add time bonus if applicable
        if (step.timeLimitForBonus > 0f && stepTime <= step.timeLimitForBonus)
        {
            score += step.timeBonusPoints;
        }

        // Mark step complete
        bool taskComplete = _currentProgress.CompleteCurrentStep(score);

        OnStepCompleted?.Invoke(step, score);
        GameStateEvents.RaiseStepCompleted(step.stepName, _currentProgress.currentStepIndex - 1);

        // Award score
        ScoreManager.Instance?.AddScore(score, $"Completed: {step.stepName}");

        // Play completion sound
        FeedbackManager.Instance?.PlaySuccess();

        if (_verboseLogging)
            Debug.Log($"[TaskManager] Completed step: {step.stepName} (+{score} points)");

        if (taskComplete)
        {
            CompleteTask();
        }
        else
        {
            // Advance to next step
            _stepStartTime = Time.time;
            TaskStep nextStep = _currentProgress.CurrentStep;

            if (_guidedMode)
            {
                UpdateMachineLocks();
            }

            if (nextStep != null)
            {
                OnStepChanged?.Invoke(nextStep);
                GameStateEvents.RaiseStepStarted(nextStep.stepName);
            }
        }

        return true;
    }

    /// <summary>
    /// Records an error during the current task.
    /// </summary>
    /// <param name="errorType">Type of error.</param>
    public void RecordError(string errorType)
    {
        if (!HasActiveTask)
            return;

        _currentProgress.RecordError();
        ScoreManager.Instance?.RecordError(errorType);

        // Apply penalty
        if (CurrentStep != null)
        {
            ScoreManager.Instance?.SubtractScore(CurrentStep.failurePenalty, $"Error: {errorType}");
        }

        // Check if task failed
        if (_currentProgress.status == TaskStatus.Failed)
        {
            FailTask();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Machine Locking
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Checks if a machine is currently locked.
    /// </summary>
    /// <param name="machine">The machine type to check.</param>
    /// <returns>True if the machine is locked.</returns>
    public bool IsMachineLocked(MachineType machine)
    {
        if (!_guidedMode || !HasActiveTask)
            return false;

        if (_machineLocks.TryGetValue(machine, out bool isLocked))
            return isLocked;

        return false;
    }

    /// <summary>
    /// Attempts to use a machine, returning false if locked.
    /// </summary>
    /// <param name="machine">The machine type to use.</param>
    /// <returns>True if machine can be used.</returns>
    public bool TryUseMachine(MachineType machine)
    {
        if (IsMachineLocked(machine))
        {
            FeedbackManager.Instance?.PlayError();
            
            // Raise safety event for improper sequence
            if (CurrentStep != null)
            {
                var safetyEvent = SafetyEvent.ImproperSequence(
                    CurrentStep.stepName,
                    $"Use {machine}",
                    Vector3.zero
                );
                GameStateEvents.RaiseSafetyViolation(safetyEvent);
            }

            if (_verboseLogging)
                Debug.Log($"[TaskManager] Machine '{machine}' is locked. Current step: {CurrentStep?.stepName}");

            return false;
        }

        return true;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Initialization
    // ══════════════════════════════════════════════════════════════════════════

    private void Initialize()
    {
        if (_isInitialized)
            return;

        _completedTaskIds = new List<string>();
        _machineLocks = new Dictionary<MachineType, bool>();

        // Initialize all machine types as unlocked
        foreach (MachineType machine in Enum.GetValues(typeof(MachineType)))
        {
            _machineLocks[machine] = false;
        }

        _isInitialized = true;

        if (_verboseLogging)
            Debug.Log("[TaskManager] Initialized.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Event Subscriptions
    // ══════════════════════════════════════════════════════════════════════════

    private void SubscribeToEvents()
    {
        GameStateEvents.OnWorkpieceSpawned += HandleWorkpieceSpawned;
        GameStateEvents.OnWorkpieceTransferred += HandleWorkpieceTransferred;
        GameStateEvents.OnCNCStateChanged += HandleCNCStateChanged;
        GameStateEvents.OnPathLoaded += HandlePathLoaded;
        GameStateEvents.OnWorkpieceCut += HandleWorkpieceCut;
        GameStateEvents.OnSafetyViolation += HandleSafetyViolation;
    }

    private void UnsubscribeFromEvents()
    {
        GameStateEvents.OnWorkpieceSpawned -= HandleWorkpieceSpawned;
        GameStateEvents.OnWorkpieceTransferred -= HandleWorkpieceTransferred;
        GameStateEvents.OnCNCStateChanged -= HandleCNCStateChanged;
        GameStateEvents.OnPathLoaded -= HandlePathLoaded;
        GameStateEvents.OnWorkpieceCut -= HandleWorkpieceCut;
        GameStateEvents.OnSafetyViolation -= HandleSafetyViolation;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Event Handlers
    // ══════════════════════════════════════════════════════════════════════════

    private void HandleWorkpieceSpawned(GameObject workpiece)
    {
        if (!HasActiveTask || !_autoAdvance)
            return;

        if (CurrentStep?.completionTrigger == StepTrigger.WorkpieceSpawned)
        {
            // Optionally verify workpiece type
            if (CurrentStep.requiredWorkpiece != null)
            {
                var wp = workpiece.GetComponent<Workpiece>();
                if (wp == null || wp.Data != CurrentStep.requiredWorkpiece)
                    return;
            }

            if (CurrentStep.autoComplete)
                CompleteCurrentStep();
        }
    }

    private void HandleWorkpieceTransferred(GameObject workpiece, TransferPoint point)
    {
        if (!HasActiveTask || !_autoAdvance)
            return;

        if (CurrentStep?.completionTrigger == StepTrigger.WorkpieceTransferred)
        {
            if (CurrentStep.autoComplete)
                CompleteCurrentStep();
        }
    }

    private void HandleCNCStateChanged(CNCState state)
    {
        if (!HasActiveTask || !_autoAdvance)
            return;

        if (CurrentStep?.completionTrigger == StepTrigger.CNCStateChange)
        {
            if (CurrentStep.requiredCNCState == state && CurrentStep.autoComplete)
                CompleteCurrentStep();
        }

        // Check for cutting complete trigger
        if (CurrentStep?.completionTrigger == StepTrigger.CuttingComplete && state == CNCState.Done)
        {
            if (CurrentStep.autoComplete)
                CompleteCurrentStep();
        }
    }

    private void HandlePathLoaded(PathData path)
    {
        if (!HasActiveTask || !_autoAdvance)
            return;

        if (CurrentStep?.completionTrigger == StepTrigger.PathLoaded)
        {
            // Optionally verify path
            if (CurrentStep.requiredPath != null && path != CurrentStep.requiredPath)
                return;

            if (CurrentStep.autoComplete)
                CompleteCurrentStep();
        }
    }

    private void HandleWorkpieceCut(GameObject workpiece)
    {
        if (!HasActiveTask || !_autoAdvance)
            return;

        if (CurrentStep?.completionTrigger == StepTrigger.WorkpieceCut)
        {
            if (CurrentStep.autoComplete)
                CompleteCurrentStep();
        }
    }

    private void HandleSafetyViolation(SafetyEvent safetyEvent)
    {
        if (!HasActiveTask)
            return;

        // Record error for non-minor violations
        if (!safetyEvent.IsMinor)
        {
            RecordError(safetyEvent.safetyType.ToString());
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Task Completion
    // ══════════════════════════════════════════════════════════════════════════

    private void CompleteTask()
    {
        if (_currentTask == null || _currentProgress == null)
            return;

        int finalScore = _currentProgress.CalculateFinalScore();
        
        // Award completion bonus
        ScoreManager.Instance?.AddScore(_currentTask.completionPoints, $"Task completed: {_currentTask.taskName}");

        // Time bonus
        if (_currentTask.timeLimitForBonus > 0f && _currentProgress.ElapsedTime <= _currentTask.timeLimitForBonus)
        {
            ScoreManager.Instance?.AddScore(_currentTask.timeBonusPoints, "Time bonus!");
        }

        // Record completion
        if (!_completedTaskIds.Contains(_currentTask.taskId))
        {
            _completedTaskIds.Add(_currentTask.taskId);
        }

        OnTaskCompleted?.Invoke(_currentTask, _currentProgress);
        GameStateEvents.RaiseTaskCompleted(_currentTask.taskName);

        // Play completion sound
        FeedbackManager.Instance?.PlayTaskComplete();

        if (_verboseLogging)
            Debug.Log($"[TaskManager] Task completed: {_currentTask.taskName}, Final score: {finalScore}");

        ClearCurrentTask();
    }

    private void FailTask()
    {
        if (_currentTask == null || _currentProgress == null)
            return;

        OnTaskFailed?.Invoke(_currentTask, _currentProgress);

        // Play failure sound
        FeedbackManager.Instance?.PlayError();

        if (_verboseLogging)
            Debug.Log($"[TaskManager] Task failed: {_currentTask.taskName}");

        ClearCurrentTask();
    }

    private void ClearCurrentTask()
    {
        _currentTask = null;
        _currentProgress = null;

        // Unlock all machines
        foreach (MachineType machine in Enum.GetValues(typeof(MachineType)))
        {
            if (_machineLocks[machine])
            {
                _machineLocks[machine] = false;
                OnMachineLockChanged?.Invoke(machine, false);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Machine Locking
    // ══════════════════════════════════════════════════════════════════════════

    private void UpdateMachineLocks()
    {
        if (!_guidedMode || CurrentStep == null)
            return;

        foreach (MachineType machine in Enum.GetValues(typeof(MachineType)))
        {
            bool shouldLock = CurrentStep.lockOtherMachines && !CurrentStep.IsMachineAllowed(machine);
            
            if (_machineLocks[machine] != shouldLock)
            {
                _machineLocks[machine] = shouldLock;
                OnMachineLockChanged?.Invoke(machine, shouldLock);

                if (_verboseLogging && shouldLock)
                    Debug.Log($"[TaskManager] Locked machine: {machine}");
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    [ContextMenu("Debug - Complete Current Step")]
    private void DebugCompleteStep()
    {
        CompleteCurrentStep();
    }

    [ContextMenu("Debug - Fail Current Task")]
    private void DebugFailTask()
    {
        FailTask();
    }

    [ContextMenu("Debug - Print Status")]
    private void DebugPrintStatus()
    {
        Debug.Log($"[TaskManager Status]\n" +
                  $"  Has Active Task: {HasActiveTask}\n" +
                  $"  Current Task: {_currentTask?.taskName ?? "None"}\n" +
                  $"  Current Step: {CurrentStep?.stepName ?? "None"}\n" +
                  $"  Step Index: {CurrentStepIndex}\n" +
                  $"  Progress: {_currentProgress?.Progress:P0}\n" +
                  $"  Guided Mode: {_guidedMode}");
    }
#endif
}
