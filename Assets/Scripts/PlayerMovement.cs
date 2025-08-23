using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Speeds")]
    public float walkSpeed;     // Speed while walking
    public float sprintSpeed;   // Speed while sprinting
    public float crouchSpeed;   // Speed while crouching
    private float moveSpeed;    // Current movement speed, depends on state

    [Header("Jumping")]
    public float jumpForce;     // Strength of jump
    public float jumpCooldown;  // Time before player can jump again
    public float airMultiplier; // Movement control while in air
    private bool readyToJump;   // Prevents spamming jump input

    [Header("Crouching")]
    public float crouchYScale;  // Height of player while crouched
    private float startYScale;  // Player’s original standing height

    [Header("Control Settings")]
    public float groundDrag;    // Friction applied when grounded
    public float playerHeight;  // Player’s height (used for ground checks)
    public LayerMask whatIsGround; // Layers considered as ground
    public float maxSlopeAngle; // Maximum angle that counts as “walkable”
    public KeyCode jumpKey = KeyCode.Space;        // Key for jumping
    public KeyCode sprintKey = KeyCode.LeftShift;  // Key for sprinting
    public KeyCode crouchKey = KeyCode.LeftControl;// Key for crouching

    [Header("References")]
    public Transform orientation; // Used to determine forward/right direction

    // Input & movement tracking
    private float horizontalInput;
    private float verticalInput;
    private Vector3 moveDirection;
    private Rigidbody rb;
    private bool grounded;
    private bool exitingSlope;
    private RaycastHit slopeHit;

    // Movement lock (used for stuns/knockbacks)
    private bool movementDisabled = false;
    private float disableTimer = 0f;
    private bool forceLook = false;
    private Vector3 lookTarget;

    // Possible movement states
    public enum MovementState { walking, sprinting, crouching, air }
    public MovementState state;

    private void Start()
    {
        // Cache Rigidbody and configure
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // Prevent unwanted rotation
        readyToJump = true;
        startYScale = transform.localScale.y; // Store original player height
    }

    private void Update()
    {
        // Handle temporary movement lock (e.g. stun effects)
        if (movementDisabled)
        {
            disableTimer -= Time.deltaTime;
            if (disableTimer <= 0f) movementDisabled = false;

            // Freeze player movement completely
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Force player to face a target if required
            if (forceLook)
            {
                Vector3 lookDir = (lookTarget - transform.position).normalized;
                lookDir.y = 0f;
                if (lookDir != Vector3.zero)
                    transform.forward = lookDir;
            }
            return;
        }

        // Check if grounded via raycast
        grounded = Physics.Raycast(transform.position, Vector3.down,
                                   playerHeight * 0.5f + 0.3f, whatIsGround);

        // Handle inputs, update movement state, and enforce speed limits
        MyInput();
        StateHandler();
        SpeedControl();

        // Apply drag only when grounded
        rb.drag = grounded ? groundDrag : 0f;
    }

    private void FixedUpdate()
    {
        // Handle physics-based movement
        if (!movementDisabled)
            MovePlayer();
    }

    private void MyInput()
    {
        // Movement inputs (WASD / Arrow keys)
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        // Jumping input
        if (Input.GetKey(jumpKey) && readyToJump && grounded)
        {
            readyToJump = false;
            Jump();
            Invoke(nameof(ResetJump), jumpCooldown);
        }

        // Crouch input
        if (Input.GetKeyDown(crouchKey))
        {
            // Shrink player height and add downward force
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        }
        if (Input.GetKeyUp(crouchKey))
        {
            // Restore original standing height
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        }
    }

    private void StateHandler()
    {
        // Sets movement state and corresponding speed
        if (Input.GetKey(crouchKey))
        {
            state = MovementState.crouching;
            moveSpeed = crouchSpeed;
        }
        else if (grounded && Input.GetKey(sprintKey))
        {
            state = MovementState.sprinting;
            moveSpeed = sprintSpeed;
        }
        else if (grounded)
        {
            state = MovementState.walking;
            moveSpeed = walkSpeed;
        }
        else
        {
            state = MovementState.air;
        }
    }

    private void MovePlayer()
    {
        // Movement direction relative to player orientation
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        // Different handling for slopes, ground, and air
        if (OnSlope() && !exitingSlope)
        {
            // Move along slope
            rb.AddForce(GetSlopeMoveDirection() * moveSpeed * 20f, ForceMode.Force);

            // Stick player to slope when jumping upwards
            if (rb.velocity.y > 0)
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }
        else if (grounded)
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        }
        else
        {
            // Air movement control (reduced force)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
        }

        // Disable gravity on slopes for smoother sticking
        rb.useGravity = !OnSlope();
    }

    private void SpeedControl()
    {
        // Prevent player from exceeding max speed
        if (OnSlope() && !exitingSlope)
        {
            if (rb.velocity.magnitude > moveSpeed)
                rb.velocity = rb.velocity.normalized * moveSpeed;
        }
        else
        {
            // Clamp velocity on XZ plane only (don’t touch Y)
            Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }
    }

    private void Jump()
    {
        // Cancel vertical velocity before applying jump
        exitingSlope = true;
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        // Apply upward force
        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    private void ResetJump()
    {
        // Allow jumping again
        readyToJump = true;
        exitingSlope = false;
    }

    private bool OnSlope()
    {
        // Raycast down to check slope angle
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }
        return false;
    }

    private Vector3 GetSlopeMoveDirection()
    {
        // Adjusts movement direction to follow slope surface
        return Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
    }

    public void DisableMovement(float duration)
    {
        // Temporarily stop player movement (e.g. stun effect)
        movementDisabled = true;
        disableTimer = duration;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public void ApplyKnockStun(float duration, Vector3 lookAtTarget)
    {
        // Disable movement and force player to look at a target
        DisableMovement(duration);
        forceLook = true;
        lookTarget = lookAtTarget;
    }
}
