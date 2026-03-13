using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders a real-time top-down 2D preview of the CNC work area onto a
/// <see cref="RenderTexture"/>, which is then displayed on the Screen.obj mesh
/// via its material.
///
/// What is drawn each frame (using the GL immediate-mode API on a Camera):
///   • A border rectangle representing the work-area bounds
///   • A crosshair dot showing the current cutter position
///   • A continuous trail line tracing where the cutter has been
///
/// Setup in the scene:
///   1. Attach this script to the Screen.obj GameObject.
///   2. Create a RenderTexture asset (e.g. 512×512, R8G8B8 format) and assign it
///      to <see cref="_renderTexture"/>.
///   3. Create a URP/Lit (or Unlit) material, set its Base Map to that same
///      RenderTexture, and assign it to the Screen mesh renderer.
///   4. Assign the <see cref="CNCCutter"/> and <see cref="CuttingPath"/> references.
///   5. Optionally create and assign a <see cref="_drawCamera"/> — a small
///      orthographic camera that renders only the display layer.
///      If left null, GL drawing falls back to OnPostRender on the main camera.
///
/// The trail is stored as a list of normalised UV points (0–1) and replayed each
/// frame, so the display stays crisp even if the RenderTexture is resized.
/// </summary>
public class CNCScreenDisplay : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The CNCCutter whose position this display tracks.")]
    [SerializeField] private CNCCutter _cutter;

    [Tooltip("ScriptableObject defining the work area (must match the one on CNCCutter).")]
    [SerializeField] private CuttingPath _cuttingPath;

    [Tooltip("RenderTexture that this display draws into. Assign to the Screen mesh material.")]
    [SerializeField] private RenderTexture _renderTexture;

    [Tooltip("Optional dedicated orthographic Camera for GL rendering. " +
             "If null, drawing hooks into the main camera's OnPostRender.")]
    [SerializeField] private Camera _drawCamera;

    [Header("Display Settings")]
    [Tooltip("Background colour of the display screen.")]
    [SerializeField] private Color _backgroundColor = new Color(0.05f, 0.05f, 0.1f, 1f);

    [Tooltip("Colour of the work-area border rectangle.")]
    [SerializeField] private Color _borderColor = new Color(0f, 0.8f, 1f, 1f);

    [Tooltip("Colour of the cut trail line.")]
    [SerializeField] private Color _trailColor = new Color(1f, 0.4f, 0f, 1f);

    [Tooltip("Colour of the cutter position crosshair.")]
    [SerializeField] private Color _cursorColor = Color.white;

    [Tooltip("Crosshair size as a fraction of the display width.")]
    [SerializeField] [Range(0.01f, 0.05f)] private float _cursorSize = 0.025f;

    [Tooltip("Maximum number of trail points kept in memory. " +
             "Older points are discarded when the list is full.")]
    [SerializeField] [Range(64, 4096)] private int _maxTrailPoints = 1024;

    [Tooltip("Minimum distance (normalised 0–1) the cutter must move before a new " +
             "trail point is recorded. Prevents duplicate points.")]
    [SerializeField] [Range(0.001f, 0.02f)] private float _trailMinDistance = 0.004f;

    // ── Private state ─────────────────────────────────────────────────────────

    // All points are stored in normalised UV space [0,1]×[0,1]
    private readonly List<Vector2> _trailPoints = new List<Vector2>();

    private Vector2 _normalisedCursorPos = new Vector2(0.5f, 0.5f);
    private Material _glMaterial;
    private bool _isDrawing;

    // Margin inside the RT so the border has padding
    private const float MARGIN = 0.08f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        CreateGLMaterial();

        if (_renderTexture == null)
        {
            Debug.LogWarning("[CNCScreenDisplay] No RenderTexture assigned. " +
                             "Creating a default 512×512 RT.", this);
            _renderTexture = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
            _renderTexture.name = "CNCScreen_RT";
            _renderTexture.Create();
        }

        // Assign the RT to this GameObject's renderer material
        var rend = GetComponent<Renderer>();
        if (rend != null)
            rend.sharedMaterial.mainTexture = _renderTexture;
    }

    private void OnEnable()
    {
        if (_cutter != null)
            _cutter.OnCutterMoved += HandleCutterMoved;

        if (_drawCamera != null)
            _drawCamera.targetTexture = _renderTexture;
    }

    private void OnDisable()
    {
        if (_cutter != null)
            _cutter.OnCutterMoved -= HandleCutterMoved;
    }

    private void Update()
    {
        // If using a dedicated draw camera, trigger a redraw every frame
        if (_drawCamera != null)
            DrawFrame();
    }

    /// <summary>
    /// Called by Unity when the attached camera finishes rendering.
    /// Used as fallback when no dedicated _drawCamera is assigned.
    /// </summary>
    private void OnPostRender()
    {
        if (_drawCamera != null) return; // dedicated camera path handles this
        DrawFrame();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Clears the cut trail. Call this when a new cut session begins.</summary>
    public void ClearTrail()
    {
        _trailPoints.Clear();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void HandleCutterMoved(Vector3 localPosition)
    {
        if (_cuttingPath == null) return;

        Vector2 norm = _cuttingPath.Normalise(new Vector2(localPosition.x, localPosition.z));
        _normalisedCursorPos = norm;

        // Record trail point only if we've moved far enough
        if (_trailPoints.Count == 0 ||
            Vector2.Distance(_trailPoints[_trailPoints.Count - 1], norm) >= _trailMinDistance)
        {
            if (_trailPoints.Count >= _maxTrailPoints)
                _trailPoints.RemoveAt(0);

            _trailPoints.Add(norm);
        }

        // Force a redraw when not using a dedicated camera
        if (_drawCamera == null)
            DrawFrame();
    }

    private void DrawFrame()
    {
        if (_renderTexture == null || _glMaterial == null) return;

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = _renderTexture;

        // Clear to background colour
        GL.Clear(true, true, _backgroundColor);

        GL.PushMatrix();
        // Map GL clip space to [0,1]×[0,1] of the RT
        GL.LoadPixelMatrix(0, _renderTexture.width, _renderTexture.height, 0);

        _glMaterial.SetPass(0);

        DrawBorder();
        DrawTrail();
        DrawCursor();

        GL.PopMatrix();

        RenderTexture.active = previous;
    }

    private void DrawBorder()
    {
        float w = _renderTexture.width;
        float h = _renderTexture.height;
        float m = MARGIN;

        // Border rectangle corners in pixel space
        Vector2 tl = new Vector2(m * w,       m * h);
        Vector2 tr = new Vector2((1f - m) * w, m * h);
        Vector2 br = new Vector2((1f - m) * w, (1f - m) * h);
        Vector2 bl = new Vector2(m * w,        (1f - m) * h);

        GL.Begin(GL.LINES);
        GL.Color(_borderColor);

        DrawLine(tl, tr);
        DrawLine(tr, br);
        DrawLine(br, bl);
        DrawLine(bl, tl);

        // Corner tick marks
        float tick = 0.04f * w;
        DrawLine(tl, tl + new Vector2(tick, 0));
        DrawLine(tl, tl + new Vector2(0, tick));
        DrawLine(tr, tr + new Vector2(-tick, 0));
        DrawLine(tr, tr + new Vector2(0, tick));
        DrawLine(br, br + new Vector2(-tick, 0));
        DrawLine(br, br + new Vector2(0, -tick));
        DrawLine(bl, bl + new Vector2(tick, 0));
        DrawLine(bl, bl + new Vector2(0, -tick));

        GL.End();
    }

    private void DrawTrail()
    {
        if (_trailPoints.Count < 2) return;

        float w = _renderTexture.width;
        float h = _renderTexture.height;

        GL.Begin(GL.LINES);
        GL.Color(_trailColor);

        for (int i = 1; i < _trailPoints.Count; i++)
        {
            Vector2 a = NormToPixel(_trailPoints[i - 1], w, h);
            Vector2 b = NormToPixel(_trailPoints[i], w, h);
            DrawLine(a, b);
        }

        GL.End();
    }

    private void DrawCursor()
    {
        float w = _renderTexture.width;
        float h = _renderTexture.height;

        Vector2 c = NormToPixel(_normalisedCursorPos, w, h);
        float half = _cursorSize * w;

        GL.Begin(GL.LINES);
        GL.Color(_cursorColor);

        // Horizontal bar
        DrawLine(c + new Vector2(-half, 0), c + new Vector2(half, 0));
        // Vertical bar
        DrawLine(c + new Vector2(0, -half), c + new Vector2(0, half));
        // Diagonal cross (X marker)
        float d = half * 0.5f;
        DrawLine(c + new Vector2(-d, -d), c + new Vector2(d, d));
        DrawLine(c + new Vector2(d, -d), c + new Vector2(-d, d));

        GL.End();
    }

    /// <summary>Converts a normalised [0,1] position to pixel space within the work-area margin.</summary>
    private Vector2 NormToPixel(Vector2 norm, float w, float h)
    {
        float innerW = (1f - 2f * MARGIN) * w;
        float innerH = (1f - 2f * MARGIN) * h;
        return new Vector2(
            MARGIN * w + norm.x * innerW,
            MARGIN * h + norm.y * innerH
        );
    }

    private static void DrawLine(Vector2 a, Vector2 b)
    {
        GL.Vertex3(a.x, a.y, 0f);
        GL.Vertex3(b.x, b.y, 0f);
    }

    /// <summary>
    /// Creates an unlit GL material for immediate-mode drawing.
    /// Uses the hidden Unity "Internal-Colored" shader which is always available.
    /// </summary>
    private void CreateGLMaterial()
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
        {
            // Fallback: any unlit color shader
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            Debug.LogError("[CNCScreenDisplay] Could not find a GL material shader. " +
                           "The screen display will not render.", this);
            return;
        }

        _glMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _glMaterial.SetInt("_ZWrite", 0);
    }

    private void OnDestroy()
    {
        if (_glMaterial != null)
            Destroy(_glMaterial);
    }
}
