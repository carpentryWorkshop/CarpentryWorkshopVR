using UnityEngine;

public class CameraModeSwitcher1 : MonoBehaviour
{
    public PlayerController playerController;
    public GameObject playerCamera;

    public GameObject orbitCamera;
    public sCameraControlOrbit orbitScript;

    public Transform inspectTarget;

    void Update()
    {
        // Press F to inspect
        if (Input.GetKeyDown(KeyCode.F))
        {
            EnterInspectMode();
        }

        // Press Escape to exit
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitInspectMode();
        }
    }

    void EnterInspectMode()
    {
        playerController.enabled = false;
        playerCamera.SetActive(false);

        orbitCamera.SetActive(true);
        orbitScript.target = inspectTarget;
    }

    void ExitInspectMode()
    {
        orbitCamera.SetActive(false);

        playerCamera.SetActive(true);
        playerController.enabled = true;
    }
}
