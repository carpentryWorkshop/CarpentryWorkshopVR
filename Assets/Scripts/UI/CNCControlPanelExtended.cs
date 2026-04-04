using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Extended CNC control panel UI with path selection and mode controls.
/// 
/// This component provides the in-world UI for operating the CNC machine, including:
/// - Path selection dropdown/list
/// - Mode toggle (Manual/Auto)
/// - Start/Stop/Emergency buttons
/// - Status display
/// - Progress indicator
/// 
/// Setup:
/// 1. Attach to the CNC machine's control panel GameObject
/// 2. Assign the CNCMachineExtended reference
/// 3. Assign UI element references
/// 4. Works with both world-space and screen-space UI
/// </summary>
public class CNCControlPanelExtended : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - References
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Machine Reference")]
    [Tooltip("The CNC machine this panel controls.")]
    [SerializeField] private CNCMachineExtended _cncMachine;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Path Selection UI
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Path Selection")]
    [Tooltip("Dropdown for selecting cutting paths.")]
    [SerializeField] private TMP_Dropdown _pathDropdown;

    [Tooltip("Alternative: List of buttons for path selection (for VR).")]
    [SerializeField] private List<Button> _pathButtons;

    [Tooltip("Text displaying selected path name.")]
    [SerializeField] private TMP_Text _selectedPathText;

    [Tooltip("Text displaying path details (length, time, passes).")]
    [SerializeField] private TMP_Text _pathDetailsText;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Mode Controls
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Mode Controls")]
    [Tooltip("Button to set Manual mode.")]
    [SerializeField] private Button _manualModeButton;

    [Tooltip("Button to set Auto mode.")]
    [SerializeField] private Button _autoModeButton;

    [Tooltip("Toggle for mode selection (alternative to buttons).")]
    [SerializeField] private Toggle _autoModeToggle;

    [Tooltip("Text displaying current mode.")]
    [SerializeField] private TMP_Text _modeText;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Operation Controls
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Operation Controls")]
    [Tooltip("Button to start cutting operation.")]
    [SerializeField] private Button _startButton;

    [Tooltip("Button to stop cutting operation.")]
    [SerializeField] private Button _stopButton;

    [Tooltip("Emergency stop button.")]
    [SerializeField] private Button _emergencyStopButton;

    [Tooltip("Button to reset the machine.")]
    [SerializeField] private Button _resetButton;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Status Display
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Status Display")]
    [Tooltip("Text displaying current machine state.")]
    [SerializeField] private TMP_Text _statusText;

    [Tooltip("Text displaying workpiece status.")]
    [SerializeField] private TMP_Text _workpieceStatusText;

    [Tooltip("Progress bar for cutting progress.")]
    [SerializeField] private Slider _progressBar;

    [Tooltip("Text displaying progress percentage.")]
    [SerializeField] private TMP_Text _progressText;

    [Tooltip("Image for status indicator light.")]
    [SerializeField] private Image _statusLight;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Visual Settings
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Visual Settings")]
    [Tooltip("Color for Idle state.")]
    [SerializeField] private Color _idleColor = Color.gray;

    [Tooltip("Color for Positioning/Ready state.")]
    [SerializeField] private Color _readyColor = Color.yellow;

    [Tooltip("Color for Cutting/Active state.")]
    [SerializeField] private Color _activeColor = Color.green;

    [Tooltip("Color for Done state.")]
    [SerializeField] private Color _doneColor = Color.blue;

    [Tooltip("Color for Error/Emergency state.")]
    [SerializeField] private Color _errorColor = Color.red;

    [Tooltip("Color for selected/active button.")]
    [SerializeField] private Color _selectedButtonColor = new Color(0.2f, 0.6f, 0.2f);

    [Tooltip("Color for unselected button.")]
    [SerializeField] private Color _normalButtonColor = Color.white;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Audio
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Audio")]
    [Tooltip("Sound for button clicks.")]
    [SerializeField] private AudioClip _buttonClickSound;

    [Tooltip("Sound for emergency stop.")]
    [SerializeField] private AudioClip _emergencySound;

    [Tooltip("Audio source for UI sounds.")]
    [SerializeField] private AudioSource _audioSource;

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR - Debug
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Debug")]
    [Tooltip("Log button clicks and state changes to console.")]
    [SerializeField] private bool _verboseLogging = false;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private List<PathData> _availablePaths;
    private int _selectedPathIndex = -1;
    private bool _isInitialized;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // Auto-find CNC machine if not assigned
        if (_cncMachine == null)
            _cncMachine = GetComponentInParent<CNCMachineExtended>();

        // Auto-add audio source if needed
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnEnable()
    {
        Initialize();
        SubscribeToEvents();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void Update()
    {
        UpdateProgressDisplay();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Refreshes all UI elements to match current machine state.
    /// </summary>
    public void RefreshUI()
    {
        RefreshPathList();
        RefreshModeDisplay();
        RefreshStatusDisplay();
        RefreshButtonStates();
    }

    /// <summary>
    /// Sets the CNC machine reference.
    /// </summary>
    /// <param name="machine">The CNC machine to control.</param>
    public void SetMachine(CNCMachineExtended machine)
    {
        if (_cncMachine != null)
        {
            UnsubscribeFromMachineEvents();
        }

        _cncMachine = machine;
        _isInitialized = false;
        
        Initialize();
        RefreshUI();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Initialization
    // ══════════════════════════════════════════════════════════════════════════

    private void Initialize()
    {
        // STARTUP DIAGNOSTIC
        Debug.Log($"[CNCControlPanel] STARTUP: _startButton={(_startButton != null ? "OK" : "NULL")}, _cncMachine={(_cncMachine != null ? "OK" : "NULL")}");
        
        if (_isInitialized || _cncMachine == null)
            return;

        // Set up button listeners
        SetupButtonListeners();

        // Get available paths
        _availablePaths = _cncMachine.AvailablePaths ?? new List<PathData>();

        _isInitialized = true;
    }

    private void SetupButtonListeners()
    {
        // Mode buttons
        if (_manualModeButton != null)
            _manualModeButton.onClick.AddListener(OnManualModeClicked);

        if (_autoModeButton != null)
            _autoModeButton.onClick.AddListener(OnAutoModeClicked);

        if (_autoModeToggle != null)
            _autoModeToggle.onValueChanged.AddListener(OnAutoModeToggled);

        // Operation buttons
        if (_startButton != null)
        {
            _startButton.onClick.AddListener(OnStartClicked);
            Debug.Log("[CNCControlPanelExtended] Start button listener connected successfully.");
        }
        else
        {
            Debug.LogWarning("[CNCControlPanelExtended] _startButton is NULL - cannot connect OnStartClicked listener!");
        }

        if (_stopButton != null)
            _stopButton.onClick.AddListener(OnStopClicked);

        if (_emergencyStopButton != null)
            _emergencyStopButton.onClick.AddListener(OnEmergencyStopClicked);

        if (_resetButton != null)
            _resetButton.onClick.AddListener(OnResetClicked);

        // Path dropdown
        if (_pathDropdown != null)
            _pathDropdown.onValueChanged.AddListener(OnPathSelected);

        // Path buttons
        for (int i = 0; i < _pathButtons.Count; i++)
        {
            int index = i; // Capture for closure
            if (_pathButtons[i] != null)
                _pathButtons[i].onClick.AddListener(() => OnPathButtonClicked(index));
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Event Subscriptions
    // ══════════════════════════════════════════════════════════════════════════

    private void SubscribeToEvents()
    {
        SubscribeToMachineEvents();
        GameStateEvents.OnCNCStateChanged += HandleCNCStateChanged;
        GameStateEvents.OnPathLoaded += HandlePathLoaded;
        GameStateEvents.OnCutProgress += HandleCutProgress;
    }

    private void UnsubscribeFromEvents()
    {
        UnsubscribeFromMachineEvents();
        GameStateEvents.OnCNCStateChanged -= HandleCNCStateChanged;
        GameStateEvents.OnPathLoaded -= HandlePathLoaded;
        GameStateEvents.OnCutProgress -= HandleCutProgress;
    }

    private void SubscribeToMachineEvents()
    {
        if (_cncMachine == null)
            return;

        _cncMachine.OnStateChanged += HandleMachineStateChanged;
        _cncMachine.OnPathLoaded += HandleMachinePathLoaded;
        _cncMachine.OnCutProgress += HandleMachineCutProgress;
    }

    private void UnsubscribeFromMachineEvents()
    {
        if (_cncMachine == null)
            return;

        _cncMachine.OnStateChanged -= HandleMachineStateChanged;
        _cncMachine.OnPathLoaded -= HandleMachinePathLoaded;
        _cncMachine.OnCutProgress -= HandleMachineCutProgress;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Event Handlers
    // ══════════════════════════════════════════════════════════════════════════

    private void HandleCNCStateChanged(CNCState state)
    {
        RefreshStatusDisplay();
        RefreshButtonStates();
    }

    private void HandleMachineStateChanged(CNCState state)
    {
        RefreshStatusDisplay();
        RefreshButtonStates();
    }

    private void HandlePathLoaded(PathData path)
    {
        RefreshPathDisplay();
    }

    private void HandleMachinePathLoaded(PathData path)
    {
        RefreshPathDisplay();
    }

    private void HandleCutProgress(float progress)
    {
        // Handled in UpdateProgressDisplay for smoother animation
    }

    private void HandleMachineCutProgress(float progress)
    {
        // Handled in UpdateProgressDisplay for smoother animation
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Button Callbacks
    // ══════════════════════════════════════════════════════════════════════════

    private void OnManualModeClicked()
    {
        PlayButtonSound();
        
        if (_cncMachine != null)
        {
            _cncMachine.SetMode(CutterMode.Manual);
            RefreshModeDisplay();
        }
    }

    private void OnAutoModeClicked()
    {
        PlayButtonSound();
        
        if (_cncMachine != null)
        {
            _cncMachine.SetMode(CutterMode.Auto);
            RefreshModeDisplay();
        }
    }

    private void OnAutoModeToggled(bool isAuto)
    {
        PlayButtonSound();
        
        if (_cncMachine != null)
        {
            _cncMachine.SetMode(isAuto ? CutterMode.Auto : CutterMode.Manual);
            RefreshModeDisplay();
        }
    }

    private void OnStartClicked()
    {
        // ALWAYS log button click for debugging (not conditional on _verboseLogging)
        Debug.Log("[CNCControlPanelExtended] OnStartClicked() - Button was clicked!");
        
        PlayButtonSound();

        if (_verboseLogging)
            Debug.Log("[CNCControlPanelExtended] Start button clicked.", this);

        // Check CNC Machine reference first
        Debug.Log($"[CNCControlPanelExtended] _cncMachine reference is {(_cncMachine != null ? "assigned" : "NULL")}");

        // Check with TaskManager if machine is allowed
        if (TaskManager.Instance != null && !TaskManager.Instance.TryUseMachine(MachineType.CNCRouter))
        {
            Debug.LogWarning("[CNCControlPanelExtended] Machine locked by TaskManager. Complete previous task first.", this);
            // Machine is locked, TryUseMachine already plays error feedback
            return;
        }
        
        if (_cncMachine == null)
        {
            Debug.LogError("[CNCControlPanelExtended] Cannot start - CNC Machine reference is null!", this);
            return;
        }
        
        Debug.Log("[CNCControlPanelExtended] Calling _cncMachine.StartCut()...");
        bool success = _cncMachine.StartCut();
        Debug.Log($"[CNCControlPanelExtended] _cncMachine.StartCut() returned: {success}");
        
        if (!success)
        {
            Debug.LogWarning("[CNCControlPanelExtended] StartCut() returned false. Check CNCMachineExtended logs for details.", this);
            
            // Print diagnostics if available
            if (_verboseLogging)
            {
                Debug.Log("[CNCControlPanelExtended] Requesting diagnostics from machine...");
                Debug.Log(_cncMachine.GetStartupDiagnostics());
            }
        }
        else
        {
            Debug.Log("[CNCControlPanelExtended] CNC machine started successfully.");
        }
    }

    private void OnStopClicked()
    {
        PlayButtonSound();
        
        if (_cncMachine != null)
        {
            _cncMachine.StopCut();
        }
    }

    private void OnEmergencyStopClicked()
    {
        PlayEmergencySound();
        
        if (_cncMachine != null)
        {
            _cncMachine.EmergencyStop();
        }
    }

    private void OnResetClicked()
    {
        PlayButtonSound();
        
        if (_cncMachine != null)
        {
            _cncMachine.Reset();
        }
    }

    private void OnPathSelected(int index)
    {
        PlayButtonSound();
        
        if (_cncMachine != null && index >= 0 && index < _availablePaths.Count)
        {
            _selectedPathIndex = index;
            _cncMachine.LoadPath(_availablePaths[index]);
            RefreshPathDisplay();
        }
    }

    private void OnPathButtonClicked(int index)
    {
        PlayButtonSound();
        
        if (_cncMachine != null && index >= 0 && index < _availablePaths.Count)
        {
            _selectedPathIndex = index;
            _cncMachine.LoadPath(_availablePaths[index]);
            RefreshPathDisplay();
            RefreshPathButtons();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - UI Refresh Methods
    // ══════════════════════════════════════════════════════════════════════════

    private void RefreshPathList()
    {
        if (_cncMachine == null)
            return;

        _availablePaths = _cncMachine.AvailablePaths ?? new List<PathData>();

        // Populate dropdown
        if (_pathDropdown != null)
        {
            _pathDropdown.ClearOptions();
            
            List<string> options = new List<string>();
            foreach (var path in _availablePaths)
            {
                options.Add(path != null ? path.pathName : "Invalid Path");
            }
            
            _pathDropdown.AddOptions(options);

            // Select current path
            if (_cncMachine.LoadedPath != null)
            {
                int index = _availablePaths.IndexOf(_cncMachine.LoadedPath);
                if (index >= 0)
                {
                    _pathDropdown.value = index;
                    _selectedPathIndex = index;
                }
            }
        }

        // Update path buttons
        RefreshPathButtons();
    }

    private void RefreshPathButtons()
    {
        for (int i = 0; i < _pathButtons.Count; i++)
        {
            if (_pathButtons[i] == null)
                continue;

            bool hasPath = i < _availablePaths.Count && _availablePaths[i] != null;
            _pathButtons[i].gameObject.SetActive(hasPath);

            if (hasPath)
            {
                // Update button text
                TMP_Text buttonText = _pathButtons[i].GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                    buttonText.text = _availablePaths[i].pathName;

                // Highlight selected
                Image buttonImage = _pathButtons[i].GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = i == _selectedPathIndex ? _selectedButtonColor : _normalButtonColor;
                }
            }
        }
    }

    private void RefreshPathDisplay()
    {
        PathData loadedPath = _cncMachine?.LoadedPath;

        if (_selectedPathText != null)
        {
            _selectedPathText.text = loadedPath != null ? loadedPath.pathName : "No path selected";
        }

        if (_pathDetailsText != null)
        {
            if (loadedPath != null)
            {
                _pathDetailsText.text = 
                    $"Length: {loadedPath.TotalLength:F3}m\n" +
                    $"Est. Time: {loadedPath.EstimatedTime:F1}s\n" +
                    $"Passes: {loadedPath.passes}\n" +
                    $"Depth: {loadedPath.TotalDepth * 1000f:F1}mm";
            }
            else
            {
                _pathDetailsText.text = "";
            }
        }
    }

    private void RefreshModeDisplay()
    {
        if (_cncMachine == null)
            return;

        CutterMode mode = _cncMachine.CurrentMode;

        if (_modeText != null)
        {
            _modeText.text = mode == CutterMode.Auto ? "AUTO" : "MANUAL";
            _modeText.color = mode == CutterMode.Auto ? _activeColor : _normalButtonColor;
        }

        if (_autoModeToggle != null)
        {
            _autoModeToggle.SetIsOnWithoutNotify(mode == CutterMode.Auto);
        }

        // Update mode button colors
        if (_manualModeButton != null)
        {
            Image img = _manualModeButton.GetComponent<Image>();
            if (img != null)
                img.color = mode == CutterMode.Manual ? _selectedButtonColor : _normalButtonColor;
        }

        if (_autoModeButton != null)
        {
            Image img = _autoModeButton.GetComponent<Image>();
            if (img != null)
                img.color = mode == CutterMode.Auto ? _selectedButtonColor : _normalButtonColor;
        }
    }

    private void RefreshStatusDisplay()
    {
        if (_cncMachine == null)
            return;

        CNCState state = _cncMachine.CurrentState;

        // Update status text
        if (_statusText != null)
        {
            _statusText.text = state.ToString().ToUpper();
            _statusText.color = GetColorForState(state);
        }

        // Update status light
        if (_statusLight != null)
        {
            _statusLight.color = GetColorForState(state);
        }

        // Update workpiece status
        if (_workpieceStatusText != null)
        {
            _workpieceStatusText.text = _cncMachine.HasWorkpiece 
                ? "Workpiece loaded" 
                : "No workpiece";
            _workpieceStatusText.color = _cncMachine.HasWorkpiece ? _activeColor : _errorColor;
        }
    }

    private void RefreshButtonStates()
    {
        if (_cncMachine == null)
            return;

        CNCState state = _cncMachine.CurrentState;
        bool isCutting = _cncMachine.IsCutting;
        bool isIdle = state == CNCState.Idle;
        bool isDone = state == CNCState.Done;

        // Start button - only enabled when idle and path loaded (for auto) or just idle (for manual)
        if (_startButton != null)
        {
            bool canStart = isIdle;
            if (_cncMachine.CurrentMode == CutterMode.Auto)
                canStart = canStart && _cncMachine.LoadedPath != null;

            _startButton.interactable = canStart;
        }

        // Stop button - only enabled while cutting
        if (_stopButton != null)
        {
            _stopButton.interactable = isCutting;
        }

        // Reset button - only enabled when done
        if (_resetButton != null)
        {
            _resetButton.interactable = isDone;
        }

        // Mode buttons - disabled while cutting
        if (_manualModeButton != null)
            _manualModeButton.interactable = !isCutting;

        if (_autoModeButton != null)
            _autoModeButton.interactable = !isCutting;

        if (_autoModeToggle != null)
            _autoModeToggle.interactable = !isCutting;

        // Path selection - disabled while cutting
        if (_pathDropdown != null)
            _pathDropdown.interactable = !isCutting;

        foreach (var button in _pathButtons)
        {
            if (button != null)
                button.interactable = !isCutting;
        }
    }

    private void UpdateProgressDisplay()
    {
        if (_cncMachine == null)
            return;

        float progress = _cncMachine.PathProgress;

        if (_progressBar != null)
        {
            _progressBar.value = progress;
        }

        if (_progressText != null)
        {
            _progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE - Utilities
    // ══════════════════════════════════════════════════════════════════════════

    private Color GetColorForState(CNCState state)
    {
        return state switch
        {
            CNCState.Idle => _idleColor,
            CNCState.Positioning => _readyColor,
            CNCState.FollowingPath => _activeColor,
            CNCState.Cutting => _activeColor,
            CNCState.Done => _doneColor,
            _ => _idleColor
        };
    }

    private void PlayButtonSound()
    {
        if (_audioSource != null && _buttonClickSound != null)
        {
            _audioSource.PlayOneShot(_buttonClickSound);
        }
    }

    private void PlayEmergencySound()
    {
        if (_audioSource != null && _emergencySound != null)
        {
            _audioSource.PlayOneShot(_emergencySound);
        }
        else if (_audioSource != null && _buttonClickSound != null)
        {
            _audioSource.PlayOneShot(_buttonClickSound);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDITOR
    // ══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    [ContextMenu("Test Refresh UI")]
    private void TestRefresh()
    {
        RefreshUI();
    }

    [ContextMenu("Print Control Panel Diagnostics")]
    private void PrintControlPanelDiagnostics()
    {
        Debug.Log("=== CNCControlPanelExtended Diagnostics ===");
        Debug.Log($"CNC Machine Reference: {(_cncMachine != null ? _cncMachine.name : "NULL")}");
        Debug.Log($"Start Button Reference: {(_startButton != null ? _startButton.name : "NULL")}");
        Debug.Log($"Verbose Logging: {_verboseLogging}");
        
        if (_startButton != null)
        {
            Debug.Log($"Start Button GameObject Active: {_startButton.gameObject.activeInHierarchy}");
            Debug.Log($"Start Button Component Enabled: {_startButton.enabled}");
            Debug.Log($"Start Button Interactable: {_startButton.interactable}");
        }
        
        if (_cncMachine != null)
        {
            Debug.Log($"CNC Machine Current State: {_cncMachine.CurrentState}");
            Debug.Log($"CNC Machine Current Mode: {_cncMachine.CurrentMode}");
            Debug.Log($"CNC Machine Has Workpiece: {_cncMachine.HasWorkpiece}");
        }
        
        Debug.Log("================================================");
    }

    private void OnValidate()
    {
        // Ensure color values are visible
        if (_idleColor.a < 0.5f) _idleColor.a = 1f;
        if (_readyColor.a < 0.5f) _readyColor.a = 1f;
        if (_activeColor.a < 0.5f) _activeColor.a = 1f;
        if (_doneColor.a < 0.5f) _doneColor.a = 1f;
        if (_errorColor.a < 0.5f) _errorColor.a = 1f;
    }
#endif
}
