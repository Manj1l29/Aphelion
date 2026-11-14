using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class ThirdPerson : MonoBehaviour
{
    public Transform cam;
    public float moveSpeed = 6.0f;
    public float jumpForce = 6.0f;
    public float mouseSensitivity = 0.1f;
    public float cameraDistance = 5f;
    public float cameraHeight = 1.6f;
    public float zoomSpeed = 1f;
    public float minDistance = 2f;
    public float maxDistance = 10f;
    public float turnSpeed = 7f;

    private float yaw;
    private float pitch; 
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        Look();
        Zoom();
        Jump();
    }

    private void FixedUpdate()
    {
        Movement();
    }

    private void LateUpdate()
    {
        PositionCamera();
    }

    void Zoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;

        if (scroll != 0f)
            cameraDistance -= Mathf.Sign(scroll) * zoomSpeed;
            cameraDistance = Mathf.Clamp(cameraDistance, minDistance, maxDistance);
    }

    void Look()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        
        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -90f, 90f);
    }

    Vector3 GetMoveInput()
    {
        var kb = Keyboard.current;
        float x = 0f, z = 0f;

        if(kb.wKey.isPressed) z += 1f;
        if(kb.aKey.isPressed) x -= 1f;
        if(kb.sKey.isPressed) z -= 1f;
        if(kb.dKey.isPressed) x += 1f;

        return new Vector3(x, 0f, z).normalized;
    }

    void Jump()
    {
        if(Keyboard.current.spaceKey.wasPressedThisFrame && IsGrounded())
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    bool IsGrounded()
    {
        //If the raycast hits something below the player, then the player is on the ground
        return Physics.Raycast(transform.position, Vector3.down, 1.1f);
    }

    void PositionCamera()
    {
        Quaternion camRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focusPoint = transform.position + Vector3.up * cameraHeight;

        cam.position = focusPoint - camRotation * Vector3.forward * cameraDistance;
        cam.rotation = camRotation;
    }

    void Movement()
    {
        Quaternion camYaw = Quaternion.Euler(0f, yaw, 0f);
        Vector3 input = GetMoveInput();
        Vector3 moveDirection = camYaw * input;

        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;

        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime));
        }
    }
}