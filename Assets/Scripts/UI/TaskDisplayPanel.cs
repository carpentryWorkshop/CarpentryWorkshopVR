using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI panel for displaying current task and step information.
/// 
/// This component provides a HUD for guided task progression, showing:
/// - Current task name and progress
/// - Current step instructions
/// - Score and timer
/// - Step completion feedback
/// 
/// Setup:
/// 1. Create a UI Canvas with this component
/// 2. Assign UI elements (text fields, progress bar, etc.)
/// 3. Panel auto-subscribes to TaskManager events
/// </summary>
public class TaskDisplayPanel : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - UI References
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Task Info")]
    [Tooltip("Text displaying the current task name.")]
    [SerializeField] private TMP_Text _taskNameText;

    [Tooltip("Text displaying the task description.")]
    [SerializeField] private TMP_Text _taskDescriptionText;

    [Tooltip("Progress bar showing overall task progress.")]
    [SerializeField] private Slider _taskProgressBar;

    [Tooltip("Text displaying progress percentage.")]
    [SerializeField] private TMP_Text _progressText;

    [Header("Step Info")]
    [Tooltip("Text displaying the current step name.")]
    [SerializeField] private TMP_Text _stepNameText;

    [Tooltip("Text displaying step instructions.")]
    [SerializeField] private TMP_Text _instructionsText;

    [Tooltip("Text displaying the hint (shown after delay).")]
    [SerializeField] private TMP_Text _hintText;

    [Tooltip("Text displaying current step number (e.g., 'Step 2 of 5').")]
    [SerializeField] private TMP_Text _stepCountText;

    [Header("Score & Timer")]
    [Tooltip("Text displaying current score.")]
    [SerializeField] private TMP_Text _scoreText;

    [Tooltip("Text displaying elapsed time.")]
    [SerializeField] private TMP_Text _timerText;

    [Tooltip("Text displaying time bonus remaining (optional).")]
    [SerializeField] private TMP_Text _timeBonusText;

    [Header("Feedback")]
    [Tooltip("Panel shown briefly when step is completed.")]
    [SerializeField] private GameObject _stepCompletePanel;

    [Tooltip("Text in the step complete panel.")]
    [SerializeField] private TMP_Text _stepCompleteText;

    [Tooltip("Panel shown when task is completed.")]
    [SerializeField] private GameObject _taskCompletePanel;

    [Tooltip("Animator for panel transitions (optional).")]
    [SerializeField] private Animator _panelAnimator;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Display Settings
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Display Settings")]
    [Tooltip("Show the panel even when no task is active.")]
    [SerializeField] private bool _showWhenNoTask = false;

    [Tooltip("Duration to show step complete panel (seconds).")]
    [SerializeField] private float _stepCompleteDuration = 2f;

    [Tooltip("Delay before showing hint (0 = use TaskManager setting).")]
    [SerializeField] private float _hintDelay = 0f;

    [Tooltip("Animate progress bar changes.")]
    [SerializeField] private bool _animateProgress = true;

    [Tooltip("Progress bar animation speed.")]
    [SerializeField] private float _progressAnimationSpeed = 2f;

    [Header("Colors")]
    [Tooltip("Color for normal step text.")]
    [SerializeField] private Color _normalColor = Color.white;

    [Tooltip("Color for highlighted/active elements.")]
    [SerializeField] private Color _highlightColor = new Color(0.2f, 0.8f, 0.2f);

    [Tooltip("Color for warnings/errors.")]
    [SerializeField] private Color _warningColor = new Color(1f, 0.5f, 0f);

    [Tooltip("Color for time bonus indicator.")]
    [SerializeField] private Color _bonusColor = new Color(1f, 0.84f, 0f);

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private CanvasGroup _canvasGroup;
    private float _targetProgress;
    private float _currentProgress;
    private float _stepStartTime;
    private float _hintTimer;
    private bool _hintShown;
    private Coroutine _stepCompleteCoroutine;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        
        // Hide feedback panels initially
        if (_stepCompletePanel != null)
            _stepCompletePanel.SetActive(false);

        if (_taskCompletePanel != null)
            _taskCompletePanel.SetActive(false);

        // Hide hint initially
        if (_hintText != null)
            _hintText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        SubscribeToEvents();
        RefreshDisplay();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void Update()
    {
        UpdateTimer();
        UpdateProgressAnimation();
        UpdateHintTimer();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Shows the task display panel.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        
        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        if (_panelAnimator != null)
            _panelAnimator.SetTrigger("Show");
    }

    /// <summary>
    /// Hides the task display panel.
    /// </summary>
    public void Hide()
    {
        if (_panelAnimator != null)
        {
            _panelAnimator.SetTrigger("Hide");
            // Panel will be deactivated by animation event or coroutine
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Forces a refresh of all display elements.
    /// </summary>
    public void RefreshDisplay()
    {
        TaskManager tm = TaskManager.Instance;
        
        if (tm == null || !tm.HasActiveTask)
        {
            if (!_showWhenNoTask)
            {
                Hide();
                return;
            }
            
            ShowNoTaskState();
            return;
        }

        Show();
        UpdateTaskInfo(tm.CurrentTask, tm.CurrentProgress);
        UpdateStepInfo(tm.CurrentStep, tm.CurrentStepIndex, tm.CurrentTask.StepCount);
        UpdateScore();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Event Subscriptions
    // ══════════════════════════════════════════════════════════════════════════

    private void SubscribeToEvents()
    {
        if (TaskManager.Instance != null)
        {
            TaskManager.Instance.OnTaskStarted += HandleTaskStarted;
            TaskManager.Instance.OnTaskCompleted += HandleTaskCompleted;
            TaskManager.Instance.OnTaskFailed += HandleTaskFailed;
            TaskManager.Instance.OnStepChanged += HandleStepChanged;
            TaskManager.Instance.OnStepCompleted += HandleStepCompleted;
        }

        GameStateEvents.OnScoreChanged += HandleScoreChanged;
    }

    private void UnsubscribeFromEvents()
    {
        if (TaskManager.Instance != null)
        {
            TaskManager.Instance.OnTaskStarted -= HandleTaskStarted;
            TaskManager.Instance.OnTaskCompleted -= HandleTaskCompleted;
            TaskManager.Instance.OnTaskFailed -= HandleTaskFailed;
            TaskManager.Instance.OnStepChanged -= HandleStepChanged;
            TaskManager.Instance.OnStepCompleted -= HandleStepCompleted;
        }

        GameStateEvents.OnScoreChanged -= HandleScoreChanged;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Event Handlers
    // ══════════════════════════════════════════════════════════════════════════

    private void HandleTaskStarted(Task task)
    {
        Show();
        RefreshDisplay();
        _stepStartTime = Time.time;
        _hintShown = false;
        _hintTimer = 0f;
    }

    private void HandleTaskCompleted(Task task, TaskProgress progress)
    {
        ShowTaskComplete(task, progress);
    }

    private void HandleTaskFailed(Task task, TaskProgress progress)
    {
        ShowTaskFailed(task, progress);
    }

    private void HandleStepChanged(TaskStep step)
    {
        _stepStartTime = Time.time;
        _hintShown = false;
        _hintTimer = 0f;

        if (_hintText != null)
            _hintText.gameObject.SetActive(false);

        UpdateStepInfo(step, TaskManager.Instance.CurrentStepIndex, TaskManager.Instance.CurrentTask.StepCount);
    }

    private void HandleStepCompleted(TaskStep step, int score)
    {
        ShowStepComplete(step, score);
        UpdateProgress();
    }

    private void HandleScoreChanged(int newScore, string reason)
    {
        UpdateScore();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Display Updates
    // ══════════════════════════════════════════════════════════════════════════

    private void UpdateTaskInfo(Task task, TaskProgress progress)
    {
        if (task == null)
            return;

        if (_taskNameText != null)
            _taskNameText.text = task.taskName;

        if (_taskDescriptionText != null)
            _taskDescriptionText.text = task.description;

        UpdateProgress();
    }

    private void UpdateStepInfo(TaskStep step, int currentIndex, int totalSteps)
    {
        if (step == null)
        {
            if (_stepNameText != null)
                _stepNameText.text = "No current step";
            if (_instructionsText != null)
                _instructionsText.text = "";
            return;
        }

        if (_stepNameText != null)
            _stepNameText.text = step.stepName;

        if (_instructionsText != null)
            _instructionsText.text = step.instructions;

        if (_stepCountText != null)
            _stepCountText.text = $"Step {currentIndex + 1} of {totalSteps}";
    }

    private void UpdateProgress()
    {
        TaskManager tm = TaskManager.Instance;
        if (tm == null || tm.CurrentProgress == null)
            return;

        _targetProgress = tm.CurrentProgress.Progress;

        if (!_animateProgress)
        {
            _currentProgress = _targetProgress;
            ApplyProgress();
        }
    }

    private void ApplyProgress()
    {
        if (_taskProgressBar != null)
            _taskProgressBar.value = _currentProgress;

        if (_progressText != null)
            _progressText.text = $"{Mathf.RoundToInt(_currentProgress * 100)}%";
    }

    private void UpdateProgressAnimation()
    {
        if (!_animateProgress || Mathf.Approximately(_currentProgress, _targetProgress))
            return;

        _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, _progressAnimationSpeed * Time.deltaTime);
        ApplyProgress();
    }

    private void UpdateScore()
    {
        if (_scoreText != null && ScoreManager.Instance != null)
        {
            _scoreText.text = $"Score: {ScoreManager.Instance.CurrentScore}";
        }
    }

    private void UpdateTimer()
    {
        TaskManager tm = TaskManager.Instance;
        if (tm == null || tm.CurrentProgress == null)
            return;

        float elapsed = tm.CurrentProgress.ElapsedTime;

        if (_timerText != null)
        {
            int minutes = Mathf.FloorToInt(elapsed / 60f);
            int seconds = Mathf.FloorToInt(elapsed % 60f);
            _timerText.text = $"{minutes:00}:{seconds:00}";
        }

        // Update time bonus indicator
        if (_timeBonusText != null && tm.CurrentTask != null)
        {
            float bonusLimit = tm.CurrentTask.timeLimitForBonus;
            
            if (bonusLimit > 0f)
            {
                float remaining = bonusLimit - elapsed;
                
                if (remaining > 0f)
                {
                    _timeBonusText.gameObject.SetActive(true);
                    _timeBonusText.text = $"Bonus: {remaining:F0}s";
                    _timeBonusText.color = remaining < 10f ? _warningColor : _bonusColor;
                }
                else
                {
                    _timeBonusText.gameObject.SetActive(false);
                }
            }
            else
            {
                _timeBonusText.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateHintTimer()
    {
        if (_hintShown || _hintText == null)
            return;

        TaskManager tm = TaskManager.Instance;
        if (tm == null || tm.CurrentStep == null)
            return;

        float hintDelay = _hintDelay > 0f ? _hintDelay : 30f; // Default 30 seconds
        _hintTimer = Time.time - _stepStartTime;

        if (_hintTimer >= hintDelay && !string.IsNullOrEmpty(tm.CurrentStep.hint))
        {
            ShowHint(tm.CurrentStep.hint);
        }
    }

    private void ShowHint(string hint)
    {
        if (_hintText == null)
            return;

        _hintText.text = $"Hint: {hint}";
        _hintText.gameObject.SetActive(true);
        _hintShown = true;
    }

    private void ShowNoTaskState()
    {
        if (_taskNameText != null)
            _taskNameText.text = "No Active Task";

        if (_taskDescriptionText != null)
            _taskDescriptionText.text = "Select a task to begin.";

        if (_stepNameText != null)
            _stepNameText.text = "";

        if (_instructionsText != null)
            _instructionsText.text = "";

        if (_taskProgressBar != null)
            _taskProgressBar.value = 0f;

        if (_progressText != null)
            _progressText.text = "0%";
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Feedback Displays
    // ══════════════════════════════════════════════════════════════════════════

    private void ShowStepComplete(TaskStep step, int score)
    {
        if (_stepCompletePanel == null)
            return;

        if (_stepCompleteCoroutine != null)
            StopCoroutine(_stepCompleteCoroutine);

        if (_stepCompleteText != null)
        {
            string message = step.completionMessage;
            if (string.IsNullOrEmpty(message))
                message = "Step Complete!";

            _stepCompleteText.text = $"{message}\n+{score} points";
        }

        _stepCompleteCoroutine = StartCoroutine(ShowStepCompleteRoutine());
    }

    private IEnumerator ShowStepCompleteRoutine()
    {
        _stepCompletePanel.SetActive(true);
        yield return new WaitForSeconds(_stepCompleteDuration);
        _stepCompletePanel.SetActive(false);
    }

    private void ShowTaskComplete(Task task, TaskProgress progress)
    {
        if (_taskCompletePanel == null)
            return;

        // Find text components in the task complete panel
        TMP_Text[] texts = _taskCompletePanel.GetComponentsInChildren<TMP_Text>();
        
        if (texts.Length > 0)
        {
            int finalScore = progress.CalculateFinalScore();
            string summary = $"Task Complete!\n\n{task.taskName}\n\nFinal Score: {finalScore}\n" +
                           $"Time: {FormatTime(progress.ElapsedTime)}\n" +
                           $"Errors: {progress.errorCount}";
            
            texts[0].text = summary;
        }

        _taskCompletePanel.SetActive(true);
    }

    private void ShowTaskFailed(Task task, TaskProgress progress)
    {
        if (_taskCompletePanel == null)
            return;

        TMP_Text[] texts = _taskCompletePanel.GetComponentsInChildren<TMP_Text>();
        
        if (texts.Length > 0)
        {
            texts[0].text = $"Task Failed\n\n{task.taskName}\n\n" +
                           $"Errors: {progress.errorCount}\n" +
                           "Try again!";
            texts[0].color = _warningColor;
        }

        _taskCompletePanel.SetActive(true);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Utilities
    // ══════════════════════════════════════════════════════════════════════════

    private string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{minutes:00}:{secs:00}";
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    [ContextMenu("Test Refresh")]
    private void TestRefresh()
    {
        RefreshDisplay();
    }

    [ContextMenu("Test Step Complete")]
    private void TestStepComplete()
    {
        if (_stepCompletePanel != null)
        {
            if (_stepCompleteText != null)
                _stepCompleteText.text = "Test Complete!\n+25 points";
            
            StartCoroutine(ShowStepCompleteRoutine());
        }
    }
#endif
}
