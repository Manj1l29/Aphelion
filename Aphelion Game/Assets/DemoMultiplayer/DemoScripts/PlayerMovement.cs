
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Netcode;
using Unity.Netcode.Components;

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

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int GroundedHash =
        Animator.StringToHash("IsGrounded");

    private static readonly int JumpHash =
        Animator.StringToHash("Jump");

    private float yaw;
    private float pitch;

    private Rigidbody rb;
    private Collider col;
    private NetworkAnimator networkAnimator;

    // Terminal interaction state.
    private bool terminalUIOpen;
    private bool ignoreEscapeUntilRelease;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        networkAnimator = GetComponent<NetworkAnimator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        rb.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        // No rotation interpolation.
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
            Debug.LogError("[PLAYER] Camera is not assigned!");
            enabled = false;
            return;
        }

        if (animator == null)
            Debug.LogError("[PLAYER] Animator is missing!");

        if (networkAnimator == null)
            Debug.LogError("[PLAYER] NetworkAnimator is missing!");

        yaw = transform.eulerAngles.y;

        terminalUIOpen = false;
        ignoreEscapeUntilRelease = false;

        SetCursorLocked(true);
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        // Do not control the character while using the terminal.
        if (terminalUIOpen)
        {
            UpdateAnimations();
            return;
        }

        HandleCursor();

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Look();
            Jump();
        }

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
        if (!IsOwner || cam == null || terminalUIOpen)
            return;

        // Instant first-person camera rotation.
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

        // Prevent the Escape press that closed the terminal
        // from immediately unlocking the gameplay cursor.
        if (ignoreEscapeUntilRelease)
        {
            if (!Keyboard.current.escapeKey.isPressed)
                ignoreEscapeUntilRelease = false;

            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);

        bool clickedOnUI =
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();

        if (Mouse.current.leftButton.wasPressedThisFrame && !clickedOnUI)
            SetCursorLocked(true);
    }

    // Called by TerminalInteractionController.
    public void SetTerminalUIOpen(bool open)
    {
        terminalUIOpen = open;

        ignoreEscapeUntilRelease = true;

        if (open)
        {
            Debug.Log("[PLAYER] Terminal opened. Movement disabled.");

            SetCursorLocked(false);
        }
        else
        {
            Debug.Log("[PLAYER] Terminal closed. Movement restored.");

            SetCursorLocked(true);
        }
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
        // Freeze horizontal movement while terminal is open
        // or while the cursor is unlocked.
        Vector3 input =
            Cursor.lockState == CursorLockMode.Locked &&
            !terminalUIOpen
            ? GetMoveInput()
            : Vector3.zero;

        Quaternion cameraYaw =
            Quaternion.Euler(0f, yaw, 0f);

        Vector3 moveDirection = cameraYaw * input;

        float currentSpeed = 0f;

        if (Keyboard.current != null &&
            Cursor.lockState == CursorLockMode.Locked &&
            !terminalUIOpen)
        {
            currentSpeed =
                Keyboard.current.leftShiftKey.isPressed
                ? runSpeed
                : moveSpeed;
        }

        Vector3 velocity = moveDirection * currentSpeed;

        // Preserve vertical physics, including gravity.
        velocity.y = rb.linearVelocity.y;

        rb.linearVelocity = velocity;

        // Instant body rotation, without smoothing.
        if (!terminalUIOpen &&
            Cursor.lockState == CursorLockMode.Locked)
        {
            rb.MoveRotation(
                Quaternion.Euler(0f, yaw, 0f)
            );
        }
    }

    private void Jump()
    {
        if (Keyboard.current == null || terminalUIOpen)
            return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame &&
            IsGrounded())
        {
            rb.AddForce(
                Vector3.up * jumpForce,
                ForceMode.Impulse
            );

            if (networkAnimator != null)
            {
                networkAnimator.SetTrigger(JumpHash);
            }
            else if (animator != null)
            {
                animator.SetTrigger(JumpHash);
            }
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

        animator.SetFloat(
            SpeedHash,
            flatVelocity.magnitude
        );

        animator.SetBool(
            GroundedHash,
            IsGrounded()
        );
    }
}
