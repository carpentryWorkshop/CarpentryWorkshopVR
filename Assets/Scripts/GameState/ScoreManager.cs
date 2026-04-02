using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton manager for tracking player score and errors throughout the CNC workflow.
/// 
/// Features:
/// - Score tracking with add/subtract operations
/// - Error counting by type
/// - Performance metrics (accuracy, time)
/// - Event-driven updates via GameStateEvents
/// 
/// Setup:
/// 1. Add this component to a persistent GameObject in the scene
/// 2. Access via ScoreManager.Instance
/// </summary>
public class ScoreManager : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // SINGLETON
    // ══════════════════════════════════════════════════════════════════════════

    private static ScoreManager _instance;

    /// <summary>Global singleton instance.</summary>
    public static ScoreManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ScoreManager>();
                if (_instance == null)
                {
                    Debug.LogWarning("[ScoreManager] No instance found in scene. Creating one.");
                    var go = new GameObject("ScoreManager");
                    _instance = go.AddComponent<ScoreManager>();
                }
            }
            return _instance;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Score Settings")]
    [Tooltip("Starting score when the game begins.")]
    [SerializeField] private int _startingScore = 0;

    [Tooltip("Minimum score (cannot go below this).")]
    [SerializeField] private int _minimumScore = 0;

    [Tooltip("Maximum score (set to -1 for unlimited).")]
    [SerializeField] private int _maximumScore = -1;

    [Header("Debug")]
    [Tooltip("Log all score changes to console.")]
    [SerializeField] private bool _verboseLogging = true;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private int _currentScore;
    private int _totalPointsEarned;
    private int _totalPointsLost;
    private int _errorCount;
    private Dictionary<string, int> _errorsByType;
    private float _sessionStartTime;
    private List<ScoreEntry> _scoreHistory;

    // ══════════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Current total score.</summary>
    public int CurrentScore => _currentScore;

    /// <summary>Total points earned this session.</summary>
    public int TotalPointsEarned => _totalPointsEarned;

    /// <summary>Total points lost this session.</summary>
    public int TotalPointsLost => _totalPointsLost;

    /// <summary>Total number of errors recorded.</summary>
    public int ErrorCount => _errorCount;

    /// <summary>Time elapsed since session started (seconds).</summary>
    public float SessionTime => Time.time - _sessionStartTime;

    /// <summary>Accuracy percentage (points earned / total possible).</summary>
    public float Accuracy
    {
        get
        {
            int total = _totalPointsEarned + _totalPointsLost;
            return total > 0 ? (float)_totalPointsEarned / total * 100f : 100f;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // Singleton enforcement
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[ScoreManager] Duplicate instance detected. Destroying this one.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeScoring();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Score Management
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds points to the current score.
    /// </summary>
    /// <param name="points">Number of points to add (must be positive).</param>
    /// <param name="reason">Description of why points were awarded.</param>
    public void AddScore(int points, string reason)
    {
        if (points <= 0)
        {
            Debug.LogWarning($"[ScoreManager] AddScore called with non-positive value: {points}");
            return;
        }

        int oldScore = _currentScore;
        _currentScore += points;

        // Apply maximum cap if set
        if (_maximumScore >= 0)
            _currentScore = Mathf.Min(_currentScore, _maximumScore);

        _totalPointsEarned += points;

        // Record history
        _scoreHistory.Add(new ScoreEntry(points, reason, true, Time.time));

        if (_verboseLogging)
            Debug.Log($"[ScoreManager] +{points} | {reason} | Score: {oldScore} → {_currentScore}");

        GameStateEvents.RaiseScoreChanged(_currentScore, $"+{points}: {reason}");
    }

    /// <summary>
    /// Subtracts points from the current score.
    /// </summary>
    /// <param name="points">Number of points to subtract (must be positive).</param>
    /// <param name="reason">Description of why points were deducted.</param>
    public void SubtractScore(int points, string reason)
    {
        if (points <= 0)
        {
            Debug.LogWarning($"[ScoreManager] SubtractScore called with non-positive value: {points}");
            return;
        }

        int oldScore = _currentScore;
        _currentScore = Mathf.Max(_minimumScore, _currentScore - points);
        _totalPointsLost += points;

        // Record history
        _scoreHistory.Add(new ScoreEntry(-points, reason, false, Time.time));

        if (_verboseLogging)
            Debug.Log($"[ScoreManager] -{points} | {reason} | Score: {oldScore} → {_currentScore}");

        GameStateEvents.RaiseScoreChanged(_currentScore, $"-{points}: {reason}");
    }

    /// <summary>
    /// Sets the score to a specific value.
    /// </summary>
    /// <param name="score">New score value.</param>
    /// <param name="reason">Reason for setting the score.</param>
    public void SetScore(int score, string reason)
    {
        int oldScore = _currentScore;
        _currentScore = Mathf.Clamp(score, _minimumScore, _maximumScore >= 0 ? _maximumScore : int.MaxValue);

        if (_verboseLogging)
            Debug.Log($"[ScoreManager] Set score: {oldScore} → {_currentScore} | {reason}");

        GameStateEvents.RaiseScoreChanged(_currentScore, reason);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Error Tracking
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records an error of a specific type.
    /// </summary>
    /// <param name="errorType">Category of the error (e.g., "SpeedTooHigh", "PathDeviation").</param>
    public void RecordError(string errorType)
    {
        if (string.IsNullOrEmpty(errorType))
            errorType = "Unknown";

        _errorCount++;

        if (!_errorsByType.ContainsKey(errorType))
            _errorsByType[errorType] = 0;

        _errorsByType[errorType]++;

        if (_verboseLogging)
            Debug.LogWarning($"[ScoreManager] Error recorded: {errorType} | Total errors: {_errorCount}");

        GameStateEvents.RaiseErrorRecorded(errorType);
    }

    /// <summary>
    /// Gets the count of errors of a specific type.
    /// </summary>
    /// <param name="errorType">The error type to query.</param>
    /// <returns>Number of times this error type occurred.</returns>
    public int GetErrorCount(string errorType)
    {
        return _errorsByType.TryGetValue(errorType, out int count) ? count : 0;
    }

    /// <summary>
    /// Gets all error types and their counts.
    /// </summary>
    /// <returns>Dictionary of error types to counts.</returns>
    public Dictionary<string, int> GetAllErrors()
    {
        return new Dictionary<string, int>(_errorsByType);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Session Management
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resets all scores and errors to initial state.
    /// </summary>
    public void ResetSession()
    {
        InitializeScoring();

        if (_verboseLogging)
            Debug.Log("[ScoreManager] Session reset.");

        GameStateEvents.RaiseScoreChanged(_currentScore, "Session reset");
    }

    /// <summary>
    /// Gets a summary of the current session performance.
    /// </summary>
    /// <returns>Formatted summary string.</returns>
    public string GetSessionSummary()
    {
        return $"Score: {_currentScore}\n" +
               $"Points Earned: {_totalPointsEarned}\n" +
               $"Points Lost: {_totalPointsLost}\n" +
               $"Accuracy: {Accuracy:F1}%\n" +
               $"Errors: {_errorCount}\n" +
               $"Session Time: {SessionTime:F1}s";
    }

    /// <summary>
    /// Gets the score history for this session.
    /// </summary>
    /// <returns>List of score entries.</returns>
    public List<ScoreEntry> GetScoreHistory()
    {
        return new List<ScoreEntry>(_scoreHistory);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private void InitializeScoring()
    {
        _currentScore = _startingScore;
        _totalPointsEarned = 0;
        _totalPointsLost = 0;
        _errorCount = 0;
        _errorsByType = new Dictionary<string, int>();
        _sessionStartTime = Time.time;
        _scoreHistory = new List<ScoreEntry>();
    }
}

/// <summary>
/// Represents a single score change entry in the history.
/// </summary>
[System.Serializable]
public struct ScoreEntry
{
    public int points;
    public string reason;
    public bool isPositive;
    public float timestamp;

    public ScoreEntry(int points, string reason, bool isPositive, float timestamp)
    {
        this.points = points;
        this.reason = reason;
        this.isPositive = isPositive;
        this.timestamp = timestamp;
    }
}
