using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

public class PlayerController : MonoBehaviour
{
    public float speed = 5.0f;
    public float gravity = -9.8f;
    public float mouseSensitivity = 100f;
    public float jumpHeight = 9.0f;
    private float xRotation = 0f;
    private float yRotation = 0f;

    private CharacterController _controller;
    private Vector3 _velocity;
    public Transform playerBody;

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }


    private void _mouseMovement()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        playerBody.Rotate(Vector3.up * mouseX);
        playerBody.Rotate(Vector3.right * mouseY);
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -45f, 45f);
        yRotation += mouseX;
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
    }


    private void _keyboardMovement()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 move = playerBody.right * moveX + playerBody.forward * moveZ;
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? speed * 2 : speed;
        _controller.Move(move * (currentSpeed * Time.deltaTime));
        if (_controller.isGrounded)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                _velocity.y = -2f;
            }
        }

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    void Update()
    {
        _mouseMovement();
        _keyboardMovement();
    }
}