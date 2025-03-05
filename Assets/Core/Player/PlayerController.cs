using UnityEngine;
using UnityEngine.UI; // Added for UI elements
using System.Collections;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpForce = 5f;
    public float diveForce = 8f; // Forward force for dive

    [Header("Double Jump Settings")] 
    public float doubleJumpTimeWindow = 0.5f;
    public float diveRollDuration = 1.2f;

    [Header("Stamina Settings")]
    public float maxStamina = 100f;
    public float staminaRechargeRate = 15f;
    public float sprintStaminaCost = 15f; // Per second
    public float dodgeRollStaminaCost = 25f; // One-time cost
    public Slider staminaSlider; 

    public CharacterController controller;
    private Vector3 velocity;
    private float currentSpeed;
    public Animator playerAnimator;
    private bool isCurrentlyWalking;
    private float horizontalInput;
    private float verticalInput;

    private bool canDive = false;
    private float lastJumpTime = -10f;
    private bool isDiving = false;
    private Vector3 diveDirection;
    private float currentStamina;

    // Flag to track our own grounded state
    private bool isGrounded = false;

    // How close we need to be to the ground to be considered "grounded"
    public float groundedThreshold = 0.2f;

    private void Start()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (playerAnimator == null)
            playerAnimator = GetComponent<Animator>();

        Cursor.lockState = CursorLockMode.Locked;
        currentSpeed = moveSpeed;
        velocity = Vector3.zero;
        
        // Initialize stamina
        currentStamina = maxStamina;
        
        // Set up stamina slider
        if (staminaSlider)
        {
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = currentStamina;
        }
    }

    private void Update()
    {
        HandleGroundedState();
        UpdateStamina();

        // Only process normal movement controls if not diving
        if (!isDiving)
        {
            HandleSprinting();
            HandleMovement();
            HandleJumpAndDive();
        }

        HandleGravity();
    }

    private void UpdateStamina()
    {
        // Drain stamina when sprinting
        if (Input.GetKey(KeyCode.LeftShift) && isCurrentlyWalking && currentStamina > 0 && isGrounded)
        {
            currentStamina -= sprintStaminaCost * Time.deltaTime;
        }
        // Recharge stamina when not sprinting
        else if (currentStamina < maxStamina)
        {
            currentStamina += staminaRechargeRate * Time.deltaTime;
            currentStamina = Mathf.Min(currentStamina, maxStamina);
        }

        // Update UI
        if (staminaSlider)
        {
            staminaSlider.value = currentStamina;
        }
    }

    private void HandleGroundedState()
    {
        // Use raycast for more accurate ground detection
        float distanceToGround = CheckDistanceToGround();

        // Update our own grounded state based on raycast
        isGrounded = distanceToGround <= groundedThreshold;

        if (isGrounded)
        {
            if (velocity.y < 0)
            {
                velocity.y = -2f;

                // If we were diving and just hit the ground, end the dive
                if (isDiving && playerAnimator && playerAnimator.GetBool("isDiving"))
                {
                    StartCoroutine(EndDiveRoll());
                }
            }
        }
    }

    private float CheckDistanceToGround()
    {
        // Cast a ray downward from slightly above character's feet to check distance to ground
        // Get the bottom center of the character controller
        Vector3 rayStart = transform.position + new Vector3(0, 0.1f, 0);

        // Cast a ray downward to check distance to ground
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10f))
        {
            return hit.distance - 0.1f; // Subtract the offset we added
        }

        return 9999f; // No ground detected within range
    }

    private void HandleSprinting()
    {
        // Only allow sprinting if there's stamina
        bool canSprint = Input.GetKey(KeyCode.LeftShift) && currentStamina > 0;
        currentSpeed = canSprint ? sprintSpeed : moveSpeed;
    }

    private void HandleMovement()
    {
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");

        bool isMoving = Mathf.Abs(horizontalInput) > 0.1f || Mathf.Abs(verticalInput) > 0.1f;

        if (isMoving != isCurrentlyWalking)
        {
            isCurrentlyWalking = isMoving;
            if (playerAnimator)
                playerAnimator.SetBool("isWalking", isCurrentlyWalking);
        }

        Vector3 move = transform.right * horizontalInput + transform.forward * verticalInput;
        controller.Move(move * (currentSpeed * Time.deltaTime));
    }

    private void HandleJumpAndDive()
    {
        bool jumpButtonDown = Input.GetButtonDown("Jump");

        if (jumpButtonDown)
        {
            if (isGrounded)
            {
                // First jump - always works when grounded
                velocity.y = jumpForce;
                canDive = true;
                lastJumpTime = Time.time;

                if (playerAnimator)
                    playerAnimator.SetTrigger("jump");
            }
            else if (canDive && Time.time - lastJumpTime < doubleJumpTimeWindow && currentStamina >= dodgeRollStaminaCost)
            {
                // Check if player has enough stamina for dodge roll
                StartDiveRoll();
                // Consume stamina for dodge roll
                currentStamina -= dodgeRollStaminaCost;
            }
        }

        if (Input.GetKeyDown(KeyCode.R) && playerAnimator != null)
        {
            playerAnimator.SetTrigger("reload");
        }
    }

    private void StartDiveRoll()
    {
        Vector3 moveDirection;
        Vector3 horizontalVelocity = new Vector3(controller.velocity.x, 0, controller.velocity.z);

        if (horizontalVelocity.magnitude > 0.5f)
        {
            moveDirection = horizontalVelocity.normalized;
        }
        else
        {
            moveDirection = new Vector3(horizontalInput, 0, verticalInput).normalized;
            if (moveDirection.magnitude < 0.1f)
            {
                moveDirection = transform.forward;
            }
        }
        diveDirection = moveDirection;
        velocity.y = jumpForce * 0.5f;
        if (playerAnimator)
        {
            playerAnimator.SetBool("isDiving", true);
            playerAnimator.SetTrigger("diveRoll");
        }

        isDiving = true;
        canDive = false; 
    }

    private IEnumerator EndDiveRoll()
    {
        yield return new WaitForSeconds(diveRollDuration);
        if (playerAnimator)
        {
            playerAnimator.SetBool("isDiving", false);
        }

        isDiving = false;
    }

    private void HandleGravity()
    {
        if (!controller.isGrounded)
        {
            velocity.y += Physics.gravity.y * Time.deltaTime;

            if (isDiving)
            {
                controller.Move(diveDirection * (diveForce * Time.deltaTime));
            }
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

    public bool IsDiving()
    {
        return isDiving;
    }
    
    public float GetStaminaPercentage()
    {
        return currentStamina / maxStamina;
    }
}