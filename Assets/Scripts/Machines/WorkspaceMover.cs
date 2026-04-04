using UnityEngine;

public class WorkspaceMover : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;          // Speed of movement
    public float workspaceMinX = -10f;    // Left boundary
    public float workspaceMaxX = 10f;     // Right boundary

    void Update()
    {
        float move = 0f;

        // Check input
        if (Input.GetKey(KeyCode.J))
        {
            move = -moveSpeed * Time.deltaTime; // Move left
        }
        else if (Input.GetKey(KeyCode.L))
        {
            move = moveSpeed * Time.deltaTime;  // Move right
        }

        // Apply movement with workspace boundary check
        Vector3 newPosition = transform.position + new Vector3(move, 0f, 0f);
        newPosition.x = Mathf.Clamp(newPosition.x, workspaceMinX, workspaceMaxX);
        transform.position = newPosition;
    }
}
