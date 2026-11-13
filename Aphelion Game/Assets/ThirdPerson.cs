using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR.Haptics;

[RequireComponent(typeof(Rigidbody))]
public class ThirdPerson : MonoBehaviour
{
    public Transform cam;
    public float moveSpeed = 6.0f;
    public float jumpForce = 6.0f;
    public float mouseSensitivity = 0.1f;
    public float cameraDistance = 5f;
    public float cameraHeight = 1.6f;

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
        
    }

    private void FixedUpdate()
    {
        
    }

    private void LateUpdate()
    {
        
    }

    void Look()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        
        yaw = mouseDelta.x * mouseSensitivity;
        pitch = mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -40f, 70f);
        
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    Vector3 GetMoveInput()
    {
        var kb = Keyboard.current;
        float x = 0f, z = 0f;

        if(kb.wKey.isPressed) z += 1f;
        if(kb.aKey.isPressed) x -= 1f;
        if(kb.sKey.isPressed) z -= 1f;
        if(kb.dKey.isPressed) x += 1f;

        return new Vector3(x, 0f, z);
    }

    void Jump()
    {
        if(Keyboard.current.spaceKey.wasPressedThisFrame && IsGrounded())
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    bool IsGrounded()
    {
        //If the raycast hits something below the player, then the player is on the ground
        return Physics.Raycast(transform.position, Vector3.down, 0.2f);
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
        Vector3 input = GetMoveInput();
        Vector3 moveDirection = transform.forward * input.z + transform.right * input.x;

        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = rb.linearVelocity.y;
        
    }
}