using UnityEngine;

public class Button3D : MonoBehaviour
{
    public CNCControlPanel panel; // assign CNCControlPanel here
    public enum ButtonType { Start, Stop, SpeedUp, SpeedDown }
    public ButtonType buttonType;

    private Renderer _renderer;
    private Color _originalColor;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
        {
            switch (buttonType)
            {
                case ButtonType.Start:
            _originalColor = new Color(0.2f, 0.8f, 0.4f);  // Mint green
            break;
        case ButtonType.Stop:
            _originalColor = new Color(0.9f, 0.2f, 0.2f);  // Bright red
            break;
        case ButtonType.SpeedUp:
            _originalColor = new Color(1f, 0.5f, 0f);      // Orange (for "accelerate")
            break;
        case ButtonType.SpeedDown:
            _originalColor = new Color(0.5f, 0.2f, 0.8f);  // Purple (for "decelerate")
            break;
            }
            _renderer.material.color = _originalColor;
        } // closes the if
    } // closes Awake()

    void OnMouseDown()
    {
        if (panel == null) return;

        // Call the CNCControlPanel functions
        switch (buttonType)
        {
            case ButtonType.Start: panel.PressStart(); break;
            case ButtonType.Stop: panel.PressStop(); break;
            case ButtonType.SpeedUp: panel.IncreaseSpeed(); break;
            case ButtonType.SpeedDown: panel.DecreaseSpeed(); break;
        }

        // Optional visual feedback: flash white when clicked
        if (_renderer != null)
        {
            _renderer.material.color = Color.white;
            Invoke(nameof(ResetColor), 0.2f);
        }
    }

    void ResetColor()
    {
        if (_renderer != null)
            _renderer.material.color = _originalColor;
    }
}
