using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    public Transform cam;
    public float moveSpeed = 6f;
    public float runSpeed = 9f;
    public float jumpForce = 5f;
    public float mouseSensitivity = 0.1f;
    public float cameraDistance = 5f;
    public float cameraHeight = 1.6f;
    public float zoomSpeed = 1f;
    public float minDistance = 2f;
    public float maxDistance = 10f;
    public float turnSpeed = 7f;

    public Animator animator;

    private float yaw;
    private float pitch;
    private Rigidbody rb;
    private Collider col;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rb.freezeRotation = true;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            cam.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        SetCursorLocked(true);
    }

    void Update()
    {
        UpdateAnimations();
        
        HandleCursor();
        if (Cursor.lockState != CursorLockMode.Locked) return;

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

    void UpdateAnimations()
    {
        Vector3 flatVelocity = rb.linearVelocity;
        flatVelocity.y = 0f;

        Debug.Log("Speed sent: " + flatVelocity.magnitude);

        animator.SetFloat("Speed", flatVelocity.magnitude);
        animator.SetBool("IsGrounded", IsGrounded());
    }



    void HandleCursor()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);

        bool clickedOnUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (Mouse.current.leftButton.wasPressedThisFrame && !clickedOnUI)
            SetCursorLocked(true);
    }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void Zoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;

        if (scroll != 0f)
        {
            cameraDistance -= Mathf.Sign(scroll) * zoomSpeed;
            cameraDistance = Mathf.Clamp(cameraDistance, minDistance, maxDistance);
        }
    }

    void Look()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -40f, 70f);
    }

    Vector3 GetMoveInput()
    {
        var kb = Keyboard.current;
        float x = 0f, z = 0f;

        if (kb.wKey.isPressed) z += 1f;
        if (kb.aKey.isPressed) x -= 1f;
        if (kb.sKey.isPressed) z -= 1f;
        if (kb.dKey.isPressed) x += 1f;

        if (kb.shiftKey.isPressed)
        {
            animator.SetTrigger("Shift");
            moveSpeed = runSpeed;
        }

        return new Vector3(x, 0f, z).normalized;
    }

    void Jump()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame && IsGrounded())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            animator.SetTrigger("Jump");
        }
    }

    bool IsGrounded()
    {
        Vector3 origin = col.bounds.center;
        float distance = col.bounds.extents.y + 0.1f;
        return Physics.Raycast(origin, Vector3.down, distance);
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
        Vector3 input = Cursor.lockState == CursorLockMode.Locked ? GetMoveInput() : Vector3.zero;
        Quaternion camYaw = Quaternion.Euler(0f, yaw, 0f);
        Vector3 moveDirection = camYaw * input;

        float currentSpeed;
        if (Keyboard.current.leftShiftKey.isPressed)
        {
            currentSpeed = runSpeed;
        }
        else
        {
            currentSpeed = moveSpeed;
        }

        Vector3 velocity = moveDirection * currentSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;

        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime));
        }
    }
}