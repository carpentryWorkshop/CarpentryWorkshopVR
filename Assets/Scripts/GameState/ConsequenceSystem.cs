using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies penalties and feedback for safety violations.
/// 
/// ConsequenceSystem listens to safety events via GameStateEvents and applies
/// appropriate consequences based on the user's preferences:
/// - Score penalties
/// - Audio warnings
/// - Visual feedback (optional)
/// 
/// This is a "basic" safety system as per user requirements - it does not
/// slow down machines or block operations, only applies score penalties and warnings.
/// 
/// Setup:
/// 1. Add this component to a persistent GameObject in the scene
/// 2. Configure penalty amounts and cooldowns in the inspector
/// 3. System auto-subscribes to safety events
/// </summary>
public class ConsequenceSystem : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // SINGLETON (Optional)
    // ══════════════════════════════════════════════════════════════════════════

    private static ConsequenceSystem _instance;

    /// <summary>Global singleton instance (if using singleton pattern).</summary>
    public static ConsequenceSystem Instance => _instance;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Penalty Configuration
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Penalty Configuration")]
    [Tooltip("Base penalty multiplier applied to all violations.")]
    [Range(0.5f, 3f)]
    [SerializeField] private float _penaltyMultiplier = 1f;

    [Tooltip("Custom penalties per safety type. If not set, uses SafetyEvent.DefaultPenalty.")]
    [SerializeField] private List<SafetyPenaltyConfig> _customPenalties = new List<SafetyPenaltyConfig>();

    [Header("Cooldowns")]
    [Tooltip("Minimum time between penalties for the same violation type (seconds).")]
    [SerializeField] private float _samePenaltyCooldown = 2f;

    [Tooltip("Minimum time between any penalties (seconds).")]
    [SerializeField] private float _globalPenaltyCooldown = 0.5f;

    [Tooltip("Maximum penalties per minute before entering 'warning overload' mode.")]
    [SerializeField] private int _maxPenaltiesPerMinute = 10;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Audio Feedback
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Audio Feedback")]
    [Tooltip("Enable audio warnings for safety violations.")]
    [SerializeField] private bool _enableAudioWarnings = true;

    [Tooltip("Default warning sound for minor violations.")]
    [SerializeField] private AudioClip _minorWarningSound;

    [Tooltip("Warning sound for moderate violations.")]
    [SerializeField] private AudioClip _moderateWarningSound;

    [Tooltip("Alert sound for severe violations.")]
    [SerializeField] private AudioClip _severeWarningSound;

    [Tooltip("Audio source for playing warning sounds.")]
    [SerializeField] private AudioSource _audioSource;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Visual Feedback
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Visual Feedback")]
    [Tooltip("Enable visual warnings (screen flash, etc.).")]
    [SerializeField] private bool _enableVisualWarnings = true;

    [Tooltip("UI Image for screen flash effect (optional).")]
    [SerializeField] private UnityEngine.UI.Image _flashImage;

    [Tooltip("Color for minor warning flash.")]
    [SerializeField] private Color _minorFlashColor = new Color(1f, 1f, 0f, 0.2f);

    [Tooltip("Color for moderate warning flash.")]
    [SerializeField] private Color _moderateFlashColor = new Color(1f, 0.5f, 0f, 0.3f);

    [Tooltip("Color for severe warning flash.")]
    [SerializeField] private Color _severeFlashColor = new Color(1f, 0f, 0f, 0.4f);

    [Tooltip("Duration of screen flash in seconds.")]
    [SerializeField] private float _flashDuration = 0.3f;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Debug
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Debug")]
    [Tooltip("Log all safety violations to console.")]
    [SerializeField] private bool _verboseLogging = true;

    [Tooltip("Track violation statistics.")]
    [SerializeField] private bool _trackStatistics = true;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private Dictionary<SafetyType, float> _lastPenaltyTimeByType;
    private float _lastPenaltyTime;
    private Queue<float> _recentPenaltyTimes;
    private Dictionary<SafetyType, int> _violationCounts;
    private bool _inWarningOverload;
    private float _flashTimer;

    // ══════════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Total number of violations recorded.</summary>
    public int TotalViolations
    {
        get
        {
            int total = 0;
            foreach (var count in _violationCounts.Values)
                total += count;
            return total;
        }
    }

    /// <summary>True if system is in warning overload mode (too many violations).</summary>
    public bool InWarningOverload => _inWarningOverload;

    /// <summary>Penalty multiplier for adjusting difficulty.</summary>
    public float PenaltyMultiplier
    {
        get => _penaltyMultiplier;
        set => _penaltyMultiplier = Mathf.Clamp(value, 0.5f, 3f);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        _instance = this;

        _lastPenaltyTimeByType = new Dictionary<SafetyType, float>();
        _recentPenaltyTimes = new Queue<float>();
        _violationCounts = new Dictionary<SafetyType, int>();

        // Initialize audio source if not assigned
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnEnable()
    {
        GameStateEvents.OnSafetyViolation += HandleSafetyViolation;
    }

    private void OnDisable()
    {
        GameStateEvents.OnSafetyViolation -= HandleSafetyViolation;
    }

    private void Update()
    {
        // Update screen flash
        if (_flashTimer > 0f && _flashImage != null)
        {
            _flashTimer -= Time.deltaTime;
            
            if (_flashTimer <= 0f)
            {
                _flashImage.color = Color.clear;
            }
            else
            {
                // Fade out
                Color currentColor = _flashImage.color;
                currentColor.a = Mathf.Lerp(0f, currentColor.a, _flashTimer / _flashDuration);
                _flashImage.color = currentColor;
            }
        }

        // Update warning overload status
        UpdateWarningOverload();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets the violation count for a specific safety type.
    /// </summary>
    /// <param name="type">The safety type to query.</param>
    /// <returns>Number of violations of this type.</returns>
    public int GetViolationCount(SafetyType type)
    {
        return _violationCounts.TryGetValue(type, out int count) ? count : 0;
    }

    /// <summary>
    /// Gets all violation counts.
    /// </summary>
    /// <returns>Dictionary of safety types to counts.</returns>
    public Dictionary<SafetyType, int> GetAllViolationCounts()
    {
        return new Dictionary<SafetyType, int>(_violationCounts);
    }

    /// <summary>
    /// Resets all violation counts and cooldowns.
    /// </summary>
    public void ResetStatistics()
    {
        _violationCounts.Clear();
        _lastPenaltyTimeByType.Clear();
        _recentPenaltyTimes.Clear();
        _inWarningOverload = false;

        if (_verboseLogging)
            Debug.Log("[ConsequenceSystem] Statistics reset.");
    }

    /// <summary>
    /// Manually triggers a safety violation (for testing or custom triggers).
    /// </summary>
    /// <param name="safetyEvent">The safety event to process.</param>
    public void TriggerViolation(SafetyEvent safetyEvent)
    {
        HandleSafetyViolation(safetyEvent);
    }

    /// <summary>
    /// Sets a custom penalty for a specific safety type.
    /// </summary>
    /// <param name="type">The safety type.</param>
    /// <param name="penalty">The penalty amount.</param>
    public void SetCustomPenalty(SafetyType type, int penalty)
    {
        foreach (var config in _customPenalties)
        {
            if (config.safetyType == type)
            {
                config.penalty = penalty;
                return;
            }
        }

        // Not found, add new
        _customPenalties.Add(new SafetyPenaltyConfig
        {
            safetyType = type,
            penalty = penalty
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Event Handling
    // ══════════════════════════════════════════════════════════════════════════

    private void HandleSafetyViolation(SafetyEvent safetyEvent)
    {
        if (safetyEvent == null)
            return;

        // Check global cooldown
        if (Time.time - _lastPenaltyTime < _globalPenaltyCooldown)
            return;

        // Check per-type cooldown
        if (_lastPenaltyTimeByType.TryGetValue(safetyEvent.safetyType, out float lastTime))
        {
            if (Time.time - lastTime < _samePenaltyCooldown)
                return;
        }

        // Track statistics
        if (_trackStatistics)
        {
            if (!_violationCounts.ContainsKey(safetyEvent.safetyType))
                _violationCounts[safetyEvent.safetyType] = 0;

            _violationCounts[safetyEvent.safetyType]++;
        }

        // Update cooldowns
        _lastPenaltyTime = Time.time;
        _lastPenaltyTimeByType[safetyEvent.safetyType] = Time.time;

        // Track for overload detection
        _recentPenaltyTimes.Enqueue(Time.time);

        // Apply consequences
        ApplyPenalty(safetyEvent);
        PlayWarningAudio(safetyEvent);
        ShowVisualWarning(safetyEvent);

        if (_verboseLogging)
            Debug.Log($"[ConsequenceSystem] {safetyEvent}");
    }

    private void ApplyPenalty(SafetyEvent safetyEvent)
    {
        int basePenalty = GetPenaltyForType(safetyEvent.safetyType);
        
        // Apply severity modifier
        float severityModifier = safetyEvent.severity switch
        {
            1 => 0.5f,
            2 => 1f,
            3 => 2f,
            _ => 1f
        };

        int finalPenalty = Mathf.RoundToInt(basePenalty * severityModifier * _penaltyMultiplier);

        // Reduce penalty if in warning overload
        if (_inWarningOverload)
        {
            finalPenalty = Mathf.RoundToInt(finalPenalty * 0.25f);
        }

        if (finalPenalty > 0 && ScoreManager.Instance != null)
        {
            ScoreManager.Instance.SubtractScore(finalPenalty, safetyEvent.warningMessage);
        }
    }

    private int GetPenaltyForType(SafetyType type)
    {
        // Check custom penalties first
        foreach (var config in _customPenalties)
        {
            if (config.safetyType == type)
                return config.penalty;
        }

        // Use default penalty from SafetyEvent
        return type switch
        {
            SafetyType.SpeedTooHigh => 10,
            SafetyType.PathDeviation => 5,
            SafetyType.NoWorkpieceLoaded => 15,
            SafetyType.Emergency => 50,
            SafetyType.ImproperSequence => 5,
            SafetyType.OutOfBounds => 10,
            SafetyType.ExcessiveDepth => 15,
            SafetyType.ToolCollision => 30,
            SafetyType.SafetyEquipmentOff => 20,
            SafetyType.ProximityWarning => 5,
            _ => 10
        };
    }

    private void PlayWarningAudio(SafetyEvent safetyEvent)
    {
        if (!_enableAudioWarnings || _audioSource == null)
            return;

        // Use FeedbackManager if available for consistent audio
        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.PlayWarningSound();
            return;
        }

        // Otherwise use local audio
        AudioClip clip = safetyEvent.severity switch
        {
            1 => _minorWarningSound,
            2 => _moderateWarningSound,
            3 => _severeWarningSound,
            _ => _moderateWarningSound
        };

        if (clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    private void ShowVisualWarning(SafetyEvent safetyEvent)
    {
        if (!_enableVisualWarnings || _flashImage == null)
            return;

        Color flashColor = safetyEvent.severity switch
        {
            1 => _minorFlashColor,
            2 => _moderateFlashColor,
            3 => _severeFlashColor,
            _ => _moderateFlashColor
        };

        _flashImage.color = flashColor;
        _flashTimer = _flashDuration;
    }

    private void UpdateWarningOverload()
    {
        // Remove old penalty times (older than 1 minute)
        float oneMinuteAgo = Time.time - 60f;
        while (_recentPenaltyTimes.Count > 0 && _recentPenaltyTimes.Peek() < oneMinuteAgo)
        {
            _recentPenaltyTimes.Dequeue();
        }

        // Check if in overload
        bool wasInOverload = _inWarningOverload;
        _inWarningOverload = _recentPenaltyTimes.Count >= _maxPenaltiesPerMinute;

        if (_inWarningOverload && !wasInOverload && _verboseLogging)
        {
            Debug.LogWarning("[ConsequenceSystem] Warning overload mode activated - penalties reduced.");
        }
        else if (!_inWarningOverload && wasInOverload && _verboseLogging)
        {
            Debug.Log("[ConsequenceSystem] Warning overload mode deactivated.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    [ContextMenu("Test Minor Violation")]
    private void TestMinorViolation()
    {
        TriggerViolation(new SafetyEvent(SafetyType.PathDeviation, 1, "Test minor violation", Vector3.zero));
    }

    [ContextMenu("Test Moderate Violation")]
    private void TestModerateViolation()
    {
        TriggerViolation(new SafetyEvent(SafetyType.SpeedTooHigh, 2, "Test moderate violation", Vector3.zero));
    }

    [ContextMenu("Test Severe Violation")]
    private void TestSevereViolation()
    {
        TriggerViolation(new SafetyEvent(SafetyType.Emergency, 3, "Test severe violation", Vector3.zero));
    }

    [ContextMenu("Print Statistics")]
    private void PrintStatistics()
    {
        Debug.Log($"[ConsequenceSystem Statistics]\n" +
                  $"  Total Violations: {TotalViolations}\n" +
                  $"  In Warning Overload: {_inWarningOverload}\n" +
                  $"  Recent Penalties (1 min): {_recentPenaltyTimes.Count}");

        foreach (var kvp in _violationCounts)
        {
            Debug.Log($"    {kvp.Key}: {kvp.Value}");
        }
    }
#endif
}

/// <summary>
/// Configuration for custom penalties per safety type.
/// </summary>
[System.Serializable]
public class SafetyPenaltyConfig
{
    [Tooltip("The safety violation type.")]
    public SafetyType safetyType;

    [Tooltip("Penalty points for this violation type.")]
    [Range(0, 100)]
    public int penalty = 10;

    [Tooltip("Custom audio clip for this violation (optional).")]
    public AudioClip customSound;

    [Tooltip("Custom message override (optional).")]
    public string customMessage;
}
