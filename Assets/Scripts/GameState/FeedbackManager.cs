using UnityEngine;

/// <summary>
/// Singleton manager for delivering audiovisual feedback to the player.
/// 
/// Features:
/// - Sound effect playback (success, error, warning, task complete)
/// - Positional audio support
/// - Future-ready for VR haptics and particle effects
/// 
/// Setup:
/// 1. Add this component to a persistent GameObject in the scene
/// 2. Assign AudioClips in the Inspector
/// 3. Access via FeedbackManager.Instance
/// </summary>
public class FeedbackManager : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // SINGLETON
    // ══════════════════════════════════════════════════════════════════════════

    private static FeedbackManager _instance;

    /// <summary>Global singleton instance.</summary>
    public static FeedbackManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<FeedbackManager>();
                if (_instance == null)
                {
                    Debug.LogWarning("[FeedbackManager] No instance found in scene. Creating one.");
                    var go = new GameObject("FeedbackManager");
                    _instance = go.AddComponent<FeedbackManager>();
                }
            }
            return _instance;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Audio Clips
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Audio Clips")]
    [Tooltip("Sound played when player performs a correct action.")]
    [SerializeField] private AudioClip _successSound;

    [Tooltip("Sound played when player makes an error.")]
    [SerializeField] private AudioClip _errorSound;

    [Tooltip("Sound played for safety warnings.")]
    [SerializeField] private AudioClip _warningSound;

    [Tooltip("Sound played when a task is completed.")]
    [SerializeField] private AudioClip _taskCompleteSound;

    [Tooltip("Sound played when a step is completed.")]
    [SerializeField] private AudioClip _stepCompleteSound;

    [Tooltip("Sound played when CNC starts cutting.")]
    [SerializeField] private AudioClip _cncStartSound;

    [Tooltip("Sound played when CNC stops cutting.")]
    [SerializeField] private AudioClip _cncStopSound;

    [Tooltip("Looping sound while CNC is cutting.")]
    [SerializeField] private AudioClip _cncCuttingLoop;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Audio Settings
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Audio Settings")]
    [Tooltip("Master volume for all feedback sounds (0-1).")]
    [SerializeField] [Range(0f, 1f)] private float _masterVolume = 0.8f;

    [Tooltip("Volume for UI feedback sounds (0-1).")]
    [SerializeField] [Range(0f, 1f)] private float _uiVolume = 1f;

    [Tooltip("Volume for machine sounds (0-1).")]
    [SerializeField] [Range(0f, 1f)] private float _machineVolume = 0.7f;

    [Tooltip("Volume for warning sounds (0-1).")]
    [SerializeField] [Range(0f, 1f)] private float _warningVolume = 1f;

    [Header("Cooldowns")]
    [Tooltip("Minimum time between repeated warning sounds.")]
    [SerializeField] private float _warningCooldown = 1f;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Future Features (Placeholder)
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Haptics (Future VR Support)")]
    [Tooltip("Enable haptic feedback for VR controllers.")]
    [SerializeField] private bool _enableHaptics = false;

    [Header("Debug")]
    [Tooltip("Log all feedback events to console.")]
    [SerializeField] private bool _verboseLogging = false;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private AudioSource _uiAudioSource;
    private AudioSource _machineAudioSource;
    private float _lastWarningTime;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // Singleton enforcement
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[FeedbackManager] Duplicate instance detected. Destroying this one.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        SetupAudioSources();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Success / Error / Warning
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Plays a success sound at a specific world position.
    /// </summary>
    /// <param name="position">World position for 3D audio.</param>
    public void PlaySuccessSound(Vector3 position)
    {
        PlayClipAtPosition(_successSound, position, _uiVolume);
        LogFeedback("Success sound", position);
    }

    /// <summary>
    /// Plays a success sound (non-positional, UI style).
    /// </summary>
    public void PlaySuccessSound()
    {
        PlayClipOnSource(_uiAudioSource, _successSound, _uiVolume);
        LogFeedback("Success sound (UI)");
    }

    /// <summary>
    /// Plays an error sound at a specific world position.
    /// </summary>
    /// <param name="position">World position for 3D audio.</param>
    public void PlayErrorSound(Vector3 position)
    {
        PlayClipAtPosition(_errorSound, position, _uiVolume);
        LogFeedback("Error sound", position);
    }

    /// <summary>
    /// Plays an error sound (non-positional, UI style).
    /// </summary>
    public void PlayErrorSound()
    {
        PlayClipOnSource(_uiAudioSource, _errorSound, _uiVolume);
        LogFeedback("Error sound (UI)");
    }

    /// <summary>
    /// Plays a warning sound at a specific world position.
    /// Respects cooldown to prevent spam.
    /// </summary>
    /// <param name="position">World position for 3D audio.</param>
    public void PlayWarningSound(Vector3 position)
    {
        if (Time.time - _lastWarningTime < _warningCooldown)
            return;

        _lastWarningTime = Time.time;
        PlayClipAtPosition(_warningSound, position, _warningVolume);
        LogFeedback("Warning sound", position);
    }

    /// <summary>
    /// Plays a warning sound (non-positional).
    /// Respects cooldown to prevent spam.
    /// </summary>
    public void PlayWarningSound()
    {
        if (Time.time - _lastWarningTime < _warningCooldown)
            return;

        _lastWarningTime = Time.time;
        PlayClipOnSource(_uiAudioSource, _warningSound, _warningVolume);
        LogFeedback("Warning sound (UI)");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Task Feedback
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Plays the task completion sound.
    /// </summary>
    public void PlayTaskCompleteSound()
    {
        PlayClipOnSource(_uiAudioSource, _taskCompleteSound, _uiVolume);
        LogFeedback("Task complete sound");
    }

    /// <summary>
    /// Plays the step completion sound.
    /// </summary>
    public void PlayStepCompleteSound()
    {
        PlayClipOnSource(_uiAudioSource, _stepCompleteSound, _uiVolume * 0.7f);
        LogFeedback("Step complete sound");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - CNC Machine Sounds
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Plays the CNC start sound.
    /// </summary>
    public void PlayCNCStartSound()
    {
        PlayClipOnSource(_machineAudioSource, _cncStartSound, _machineVolume);
        LogFeedback("CNC start sound");
    }

    /// <summary>
    /// Plays the CNC stop sound.
    /// </summary>
    public void PlayCNCStopSound()
    {
        StopCNCCuttingLoop();
        PlayClipOnSource(_machineAudioSource, _cncStopSound, _machineVolume);
        LogFeedback("CNC stop sound");
    }

    /// <summary>
    /// Starts the CNC cutting loop sound.
    /// </summary>
    public void StartCNCCuttingLoop()
    {
        if (_cncCuttingLoop == null || _machineAudioSource == null)
            return;

        _machineAudioSource.clip = _cncCuttingLoop;
        _machineAudioSource.loop = true;
        _machineAudioSource.volume = _machineVolume * _masterVolume;
        _machineAudioSource.Play();
        LogFeedback("CNC cutting loop started");
    }

    /// <summary>
    /// Stops the CNC cutting loop sound.
    /// </summary>
    public void StopCNCCuttingLoop()
    {
        if (_machineAudioSource != null && _machineAudioSource.isPlaying && _machineAudioSource.loop)
        {
            _machineAudioSource.Stop();
            _machineAudioSource.loop = false;
            LogFeedback("CNC cutting loop stopped");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Volume Control
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sets the master volume for all feedback sounds.
    /// </summary>
    /// <param name="volume">Volume level (0-1).</param>
    public void SetMasterVolume(float volume)
    {
        _masterVolume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// Sets the UI feedback volume.
    /// </summary>
    /// <param name="volume">Volume level (0-1).</param>
    public void SetUIVolume(float volume)
    {
        _uiVolume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// Sets the machine sounds volume.
    /// </summary>
    /// <param name="volume">Volume level (0-1).</param>
    public void SetMachineVolume(float volume)
    {
        _machineVolume = Mathf.Clamp01(volume);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Haptics (Future VR Support)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Triggers haptic feedback. Currently a placeholder for VR integration.
    /// </summary>
    /// <param name="intensity">Intensity of the haptic pulse (0-1).</param>
    /// <param name="duration">Duration in seconds.</param>
    public void TriggerHaptic(float intensity, float duration)
    {
        if (!_enableHaptics)
            return;

        // TODO: Implement XR haptic feedback when VR is integrated
        // Example for Unity XR:
        // var leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        // leftController.SendHapticImpulse(0, intensity, duration);

        LogFeedback($"Haptic triggered: intensity={intensity}, duration={duration}");
    }

    /// <summary>
    /// Triggers a short haptic pulse for UI feedback.
    /// </summary>
    public void TriggerUIHaptic()
    {
        TriggerHaptic(0.3f, 0.05f);
    }

    /// <summary>
    /// Triggers a strong haptic pulse for warnings.
    /// </summary>
    public void TriggerWarningHaptic()
    {
        TriggerHaptic(0.8f, 0.2f);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private void SetupAudioSources()
    {
        // UI audio source (non-spatial)
        _uiAudioSource = gameObject.AddComponent<AudioSource>();
        _uiAudioSource.playOnAwake = false;
        _uiAudioSource.spatialBlend = 0f; // 2D sound
        _uiAudioSource.volume = _uiVolume * _masterVolume;

        // Machine audio source (can be spatial if needed)
        _machineAudioSource = gameObject.AddComponent<AudioSource>();
        _machineAudioSource.playOnAwake = false;
        _machineAudioSource.spatialBlend = 0f; // 2D by default
        _machineAudioSource.volume = _machineVolume * _masterVolume;
    }

    private void PlayClipOnSource(AudioSource source, AudioClip clip, float volumeMultiplier)
    {
        if (source == null || clip == null)
            return;

        source.PlayOneShot(clip, volumeMultiplier * _masterVolume);
    }

    private void PlayClipAtPosition(AudioClip clip, Vector3 position, float volumeMultiplier)
    {
        if (clip == null)
            return;

        AudioSource.PlayClipAtPoint(clip, position, volumeMultiplier * _masterVolume);
    }

    private void LogFeedback(string message, Vector3? position = null)
    {
        if (!_verboseLogging)
            return;

        string posStr = position.HasValue ? $" at {position.Value}" : "";
        Debug.Log($"[FeedbackManager] {message}{posStr}");
    }
}
