using UnityEngine;

public class PlayerCam : MonoBehaviour
{
    [Header("Mouse Sensitivity")]
    public float sensX; // Horizontal sensitivity
    public float sensY; // Vertical sensitivity

    [Header("References")]
    public Transform orientation; // Used to rotate player movement direction

    // Rotation tracking
    float xRotation; // Vertical rotation (up/down)
    float yRotation; // Horizontal rotation (left/right)

    private void Start()
    {
        // Lock cursor to the center of the screen and hide it
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // --- Get raw mouse input ---
        float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sensX;
        float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sensY;

        // Accumulate horizontal rotation (left/right look)
        yRotation += mouseX;

        // Accumulate vertical rotation (up/down look), inverted so it feels natural
        xRotation -= mouseY;
        // Clamp vertical rotation so player can’t over-rotate past straight up/down
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // --- Apply rotation ---
        // Rotate the actual camera (this GameObject) on both X and Y axes
        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        // Rotate the orientation (used by movement script) only on the Y axis
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);
    }
}
