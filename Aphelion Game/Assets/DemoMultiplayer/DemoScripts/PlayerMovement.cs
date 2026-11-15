
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Camera")]
    public Transform cam;
    public float mouseSensitivity = 0.1f;
    public float minPitch = -85f;
    public float maxPitch = 85f;

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float runSpeed = 9f;
    public float jumpForce = 5f;

    [Header("Animation")]
    public Animator animator;

    private float yaw;
    private float pitch;

    private Rigidbody rb;
    private Collider col;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // Allow horizontal rotation, freeze unwanted tilting.
        rb.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        // Disable Rigidbody interpolation for unsmoothed rotation.
        rb.interpolation = RigidbodyInterpolation.None;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (cam != null)
                cam.gameObject.SetActive(false);

            enabled = false;
            return;
        }

        if (cam == null)
        {
            Debug.LogError("[PLAYER] First-person camera is not assigned!");
            enabled = false;
            return;
        }

        yaw = transform.eulerAngles.y;

        SetCursorLocked(true);
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        HandleCursor();

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            UpdateAnimations();
            return;
        }

        Look();
        Jump();
        UpdateAnimations();
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
            return;

        Movement();
    }

    private void LateUpdate()
    {
        if (!IsOwner || cam == null)
            return;

        // Apply camera rotation directly with no smoothing.
        cam.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void Look()
    {
        if (Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void HandleCursor()
    {
        if (Keyboard.current == null || Mouse.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);

        bool clickedOnUI =
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();

        if (Mouse.current.leftButton.wasPressedThisFrame && !clickedOnUI)
            SetCursorLocked(true);
    }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        Cursor.visible = !locked;
    }

    private Vector3 GetMoveInput()
    {
        if (Keyboard.current == null)
            return Vector3.zero;

        var kb = Keyboard.current;

        float x = 0f;
        float z = 0f;

        if (kb.wKey.isPressed) z += 1f;
        if (kb.sKey.isPressed) z -= 1f;
        if (kb.aKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed) x += 1f;

        return new Vector3(x, 0f, z).normalized;
    }

    private void Movement()
    {
        Vector3 input =
            Cursor.lockState == CursorLockMode.Locked
            ? GetMoveInput()
            : Vector3.zero;

        Quaternion cameraYaw = Quaternion.Euler(0f, yaw, 0f);

        Vector3 moveDirection = cameraYaw * input;

        float currentSpeed = 0f;

        if (Keyboard.current != null)
        {
            currentSpeed = Keyboard.current.leftShiftKey.isPressed
                ? runSpeed
                : moveSpeed;
        }

        Vector3 velocity = moveDirection * currentSpeed;

        velocity.y = rb.linearVelocity.y;

        rb.linearVelocity = velocity;

        // Rotate directly. No Slerp or turn-speed interpolation.
        rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
    }

    private void Jump()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame && IsGrounded())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            if (animator != null)
                animator.SetTrigger("Jump");
        }
    }

    private bool IsGrounded()
    {
        if (col == null)
            return false;

        Vector3 origin = col.bounds.center;
        float distance = col.bounds.extents.y + 0.1f;

        return Physics.Raycast(
            origin,
            Vector3.down,
            distance
        );
    }

    private void UpdateAnimations()
    {
        if (animator == null)
            return;

        Vector3 flatVelocity = rb.linearVelocity;
        flatVelocity.y = 0f;

        animator.SetFloat("Speed", flatVelocity.magnitude);
        animator.SetBool("IsGrounded", IsGrounded());
    }
}
