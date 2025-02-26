using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpForce = 5f;

    public CharacterController controller;
    private Vector3 velocity;
    private float currentSpeed;
    public Animator playerAnimator;
    private bool isCurrentlyWalking;
    private float horizontalInput;
    private float verticalInput;

    private void Start()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();
            
        if (playerAnimator == null)
            playerAnimator = GetComponent<Animator>();
            
        Cursor.lockState = CursorLockMode.Locked;
        currentSpeed = moveSpeed;
        velocity = Vector3.zero;
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
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
    }

    private void HandleSprinting()
    {
        currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;
    }

    private void HandleMovement()
    {
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");

        bool isMoving = Mathf.Abs(horizontalInput) > 0.1f || Mathf.Abs(verticalInput) > 0.1f;

        if (isMoving != isCurrentlyWalking)
        {
            isCurrentlyWalking = isMoving;
            if (playerAnimator != null)
                playerAnimator.SetBool("isWalking", isCurrentlyWalking);
        }

        Vector3 move = transform.right * horizontalInput + transform.forward * verticalInput;
        controller.Move(move * (currentSpeed * Time.deltaTime));
    }

    private void HandleJumping()
    {
        if (Input.GetButtonDown("Jump") && controller.isGrounded)
        {
            velocity.y = jumpForce;
        }
    }

    private void HandleGravity()
    {
        if (!controller.isGrounded)
        {
            velocity.y += Physics.gravity.y * Time.deltaTime;
        }
        controller.Move(velocity * Time.deltaTime);
    }
    
    public bool IsMoving()
    {
        return isCurrentlyWalking;
    }
    
    public float GetMovementMagnitude()
    {
        return new Vector2(horizontalInput, verticalInput).magnitude;
    }
    
    public bool IsSprinting()
    {
        return currentSpeed > moveSpeed;
    }
}