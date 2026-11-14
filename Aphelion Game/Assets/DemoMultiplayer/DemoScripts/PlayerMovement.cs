using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public Transform cameraTransform;
    public float walkSpeed = 4f;
    public float lookSensitivity = 0.1f;
    public float gravity = -20f;

    CharacterController controller;
    float pitch;
    float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);

        if (Mouse.current.leftButton.wasPressedThisFrame
            && Cursor.lockState != CursorLockMode.Locked
            && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            SetCursorLocked(true);
        }

        if (Cursor.lockState != CursorLockMode.Locked) return;

        Look();
        Move();
    }

    void Look()
    {
        Vector2 delta = Mouse.current.delta.ReadValue();
        transform.Rotate(0f, delta.x * lookSensitivity, 0f);

        pitch -= delta.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -85f, 85f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        var kb = Keyboard.current;
        float x = 0f;
        float z = 0f;
        if (kb.dKey.isPressed) x += 1f;
        if (kb.aKey.isPressed) x -= 1f;
        if (kb.wKey.isPressed) z += 1f;
        if (kb.sKey.isPressed) z -= 1f;

        Vector3 move = transform.right * x + transform.forward * z;
        if (move.sqrMagnitude > 1f) move.Normalize();

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * walkSpeed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}