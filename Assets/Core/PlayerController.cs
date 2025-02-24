using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpForce = 5f;

    private CharacterController _controller;
    private Vector3 _velocity;
    private float _currentSpeed;
    private Animator _playerAnimator;
    private bool _isCurrentlyWalking;

    private void Start()
    {
        _controller = GetComponent<CharacterController>();
        _playerAnimator = GetComponent<Animator>();
        Cursor.lockState = CursorLockMode.Locked;
        _currentSpeed = moveSpeed;
    }

    private void Update()
    {
        HandleGroundedState();
        HandleSprinting();
        HandleMovement();
        HandleJumping();
        HandleGravity();
    }

    private void HandleGroundedState()
    {
        if (_controller.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }
    }

    private void HandleSprinting()
    {
        _currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;
    }

    private void HandleMovement()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // Check if any movement input is being pressed
        bool isMoving = Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f;

        // Only update animator if state changes
        if (isMoving != _isCurrentlyWalking)
        {
            _isCurrentlyWalking = isMoving;
            _playerAnimator.SetBool("isWalking", _isCurrentlyWalking);
        }

        Vector3 move = transform.right * x + transform.forward * z;
        _controller.Move(move * (_currentSpeed * Time.deltaTime));
    }

    private void HandleJumping()
    {
        if (Input.GetButtonDown("Jump") && _controller.isGrounded)
        {
            _velocity.y = jumpForce;
        }
    }

    private void HandleGravity()
    {
        if (!_controller.isGrounded)
        {
            _velocity.y += Physics.gravity.y * Time.deltaTime;
        }
        _controller.Move(_velocity * Time.deltaTime);
    }
}