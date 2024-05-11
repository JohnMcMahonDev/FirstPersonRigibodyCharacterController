using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource)), RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private LayerMask layerMask;
    [SerializeField]
    private PhysicMaterial frictionlessMaterial;

    [SerializeField]
    private bool enableCameraMovement = true;
    [SerializeField]
    private bool enableMovement = true;

    private AudioSource audioSource;
    private Rigidbody rb;
    private CapsuleCollider playerCollider;
    private CapsuleCollider frictionlessCollider;
    private GameObject mainCamera;
     
    [Header("GameObject References")]
    [Tooltip("Camera parent object. Should be child of player and should have camera child object.")]
    [SerializeField]
    private GameObject headJoint;

    [Header("Rotation Settings")]
    [Tooltip("Horizontal rotation sensitivity")]
    [SerializeField]
    private float horizontalRotationSpeed = 1f;
    [Tooltip("Vertical rotation sensitivity")]
    [SerializeField]
    private float verticalRotationSpeed = 1f;
    [Tooltip("Vertical camera rotation limit")]
    [SerializeField]
    private float verticalRotationLimit = 1f;
    [Tooltip("Whether vertical rotation should be inverted")]
    [SerializeField]
    private bool invertY = false;

    [Header("Character Settings")]
    [Tooltip("Height of character when standing")]
    [SerializeField]
    private float characterHeight = 1.5f;
    [Tooltip("Radius of character. One half of character diameter")]
    [SerializeField]
    private float characterRadius = 0.4f;
    [Tooltip("How high the player can step when standing")]
    [SerializeField]
    private float stepHeight = 0.4f;
    [Tooltip("Height of the player when crouching")]
    [SerializeField]
    private float crouchHeight = 1f;
    [Tooltip("How high the player can step when crouching")]
    [SerializeField]
    private float crouchStepHeight = 0.2f;
    [Tooltip("How quick the transition up a step is")]
    [SerializeField]
    private float stepSmooth = 0.1f;

    [Header("Movement Settings")]
    [Tooltip("Base speed of the player when walking")]
    [SerializeField]
    private float walkSpeed = 5f;
    [SerializeField]
    private KeyCode sprintKey = KeyCode.LeftShift;
    [Tooltip("Base speed of the player when sprinting")]
    [SerializeField]
    private float sprintSpeed = 8f;
    [SerializeField]
    private KeyCode crouchKey = KeyCode.LeftControl;
    [Tooltip("Base speed of the player when crouching")]
    [SerializeField]
    private float crouchSpeed = 3f;
    [SerializeField]
    private float maxSlopeAngle = 45f;
    [Header("Stamina Settings")]
    [Tooltip("Enable or disable stamina")]
    [SerializeField]
    private bool useStamina = true;
    [Tooltip("Maximum stamina of player")]
    [SerializeField]
    private float maximumStamina = 100f;
    [Tooltip("How quickly stamina regenerates")]
    [SerializeField]
    private float staminaRegenSpeed = 1f;
    [Tooltip("How much stamina sprinting costs")]
    [SerializeField]
    private float sprintStaminaCost = 1f;

    [Header("Jump Settings")]
    [SerializeField]
    private KeyCode jumpKey = KeyCode.Space;
    [Tooltip("How much force the player jumps with")]
    [SerializeField]
    private float jumpForce = 8f;
    [Tooltip("Multiplier applied to Physics.gravity value")]
    [SerializeField]
    private float gravityMultiplier = 1f;

    [Header("Audio Settings")]
    [Tooltip("Volume modifier of all player sounds")]
    [SerializeField]
    private float volume = 1f;
    [Tooltip("Sound played when player jumps")]
    [SerializeField]
    private AudioClip jumpSound;
    [Tooltip("Sound played when player lands. Plays regardless of whether player jumped")]
    [SerializeField]
    private AudioClip landSound;
    [Tooltip("Sound clips to play when player is moving and grounded")]
    [SerializeField]
    private List<AudioClip> footstepSounds = new List<AudioClip>();
    [Tooltip("Time between each footstep sound. Represents the player's gait")]
    [SerializeField]
    private float stepTime = 0f;

    //Internal variables, should not be modified at runtime
    private bool isGrounded = false;
    private bool isJumping = false;
    private bool isCrouched = false;
    private bool isSprinting = false;

    private Vector3 movement = Vector3.zero;
    private float moveSpeedInternal = 0f;
    private float internalCharacterHeight = 0f;
    private float currentStamina = 0f;

    private float stepCycle = 0f;
    private int stepIndex = 0;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponents<CapsuleCollider>()[0];
        frictionlessCollider = GetComponents<CapsuleCollider>()[1];

        mainCamera = Camera.main.gameObject;

        rb.isKinematic = false;
        rb.useGravity = false;

        playerCollider.radius = characterRadius;
        playerCollider.height = characterHeight;
        internalCharacterHeight = characterHeight;

        //Apply slightly larger frictionless character collider so player doesn't get stuck to walls
        frictionlessCollider.radius = characterRadius * 1.05f;
        frictionlessCollider.height = characterHeight - characterHeight * 0.05f;
        frictionlessCollider.center = new Vector3(0f, characterHeight * 0.05f, 0f);
        frictionlessCollider.material = frictionlessMaterial;

        //Set head position
        headJoint.transform.localPosition = new Vector3(0, characterHeight * 0.90f / 2f, -0.1f);

        //Set rigidbody constraints so player doesn't roll around
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentStamina = maximumStamina;
    }

    private void Update()
    {
        //Rotate camera with mouse
        CameraRotation();

        //Check for player crouch
        CheckCrouch();

        //Check for player sprinting
        CheckSprint();

        //Check for player jumping
        CheckJump();

        //Plays footstep sounds
        FootstepLoop();
    }

    private void FixedUpdate()
    {
        //Check if player is grounded
        CheckGrounded();

        //Calculate player speed
        CalculateSpeed();

        //Calculate player movement
        CalculateMovement();

        //Check if the player can climb a step
        StepCheck();
    }

    private void CameraRotation()
    {
        if (enableCameraMovement)
        {
            //Retrieve mouse input
            float h = horizontalRotationSpeed * Input.GetAxis("Mouse X");
            float v = verticalRotationSpeed * Input.GetAxis("Mouse Y");

            //Rotate player horizontally
            transform.Rotate(0f, h, 0f);
            //Rotate camera vertically
            mainCamera.transform.Rotate((invertY ? 1 : -1) * v, 0f, 0f);

            float verticalAngle = mainCamera.transform.eulerAngles.x;
            //Clamp current camera vertical rotation to verticalRotationLimit
            verticalAngle = Mathf.Clamp(verticalAngle > 180f ? verticalAngle - 360f : verticalAngle, -verticalRotationLimit, verticalRotationLimit);
            mainCamera.transform.localEulerAngles = new Vector3(verticalAngle, 0f, 0f);

            if (!isSprinting)
            {
                currentStamina = Mathf.Clamp(currentStamina + staminaRegenSpeed * Time.deltaTime, 0f, maximumStamina);
            }
        }
    }

    private void StepCheck()
    {
        //Calculate step height beased on whether standing or crouching
        float currentStepHeight = isCrouched ? crouchStepHeight : stepHeight;
        float height = transform.position.y - internalCharacterHeight / 2f + currentStepHeight;
        Vector3 stepTopPosition = new Vector3(transform.position.x, height, transform.position.z) + movement.normalized * characterRadius;
        float stepBasePosition = transform.position.y - internalCharacterHeight / 2f;

        RaycastHit hitInfo;
        if (Physics.Raycast(stepTopPosition, Vector3.down, out hitInfo, currentStepHeight - stepSmooth, layerMask, QueryTriggerInteraction.Ignore))
        {
            //Check whether the hit object is between the step base and step top
            if (hitInfo.point.y <= stepTopPosition.y && hitInfo.point.y >= stepBasePosition && movement.magnitude > 0)
            {
                //Move player to top of step
                rb.position += new Vector3(0f, stepSmooth, 0f);
            }
        }
    }

    //Check if player is grounded
    private void CheckGrounded()
    {
        bool lastFrameGrounded = isGrounded;

        //Check whether player is touching ground
        Collider[] overlapColliders = Physics.OverlapSphere(new Vector3(transform.position.x, transform.position.y - internalCharacterHeight / 2f - 0.1f, transform.position.z),
            characterRadius, layerMask);
        if (overlapColliders.Length > 0)
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }

        //Reset isJumping if the player is returning to the ground
        if(isGrounded && !lastFrameGrounded && isJumping)
        {
            isJumping = false;
        }

        //Play land sound if velocity is high enough
        if(!lastFrameGrounded && isGrounded && rb.velocity.y < Physics.gravity.y / 2f)
        {
            if (landSound != null)
            {
                audioSource.PlayOneShot(landSound);
            }
        }
    }

    //Check if player is crouching
    private void CheckCrouch()
    {
        if(Input.GetKeyDown(crouchKey) && isGrounded)
        {
            Crouch(!isCrouched);
        }
    }

    //Check if player is sprinting
    private void CheckSprint()
    {
        //Start sprinting if user presses key
        if(Input.GetKeyDown(sprintKey) && isGrounded && !isCrouched && (currentStamina > 0f || !useStamina))
        {
            isSprinting = true;
        }
        //Stop sprinting if user lets go of key
        else if(Input.GetKey(sprintKey) == false || !isGrounded || isCrouched || (currentStamina <= 0f && useStamina))
        {
            isSprinting = false;
        }

        if(isSprinting && useStamina)
        {
            currentStamina -= sprintStaminaCost * Time.deltaTime;
        }
    }

    //Check if player is jumping
    private void CheckJump()
    {
        //Check whether player can move and player is grounded
        if (Input.GetKeyDown(jumpKey) && isGrounded)
        {
            isJumping = true;
            Crouch(false);

            //Play jump sound
            if (jumpSound != null)
            {
                audioSource.PlayOneShot(jumpSound, volume);
            }
        }
    }

    //Calculate internal player speed
    private void CalculateSpeed()
    {
        if(isSprinting)
        {
            moveSpeedInternal = sprintSpeed;
        }
        else
        {
            if(isCrouched)
            {
                moveSpeedInternal = crouchSpeed;
            }
            else
            {
                moveSpeedInternal = walkSpeed;
            }
        }
    }

    //Movement mechanics
    private void CalculateMovement()
    {
        movement = Vector3.zero;
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        Vector2 inputXY = new Vector2(horizontalInput, verticalInput);
        if (inputXY.magnitude > 1f)
        {
            inputXY = inputXY.normalized;
        }

        Vector3 movementDirection = inputXY.y * transform.forward + inputXY.x * transform.right;
        //Add horizontal movement
        if (inputXY.magnitude > 0f && enableMovement)
        {
            //Check if player is climbing up climbable slobe 
            if (SlopeCheck(movementDirection))
            {
                movement = movementDirection * moveSpeedInternal;
            }
        }

        //Add vertical movement
        if (isJumping && isGrounded && enableMovement)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        //Finalize movement
        rb.velocity = new Vector3(movement.x, rb.velocity.y, movement.z);
        rb.AddForce(Physics.gravity * gravityMultiplier, ForceMode.Force);
    }

    //Step cycle 
    private void FootstepLoop()
    {
        if (movement.magnitude > 0 && footstepSounds.Count > 0)
        {
            stepCycle += Time.deltaTime;

            //Calculate correct step time based on player's movement speed
            float currentStepTime = isSprinting ? stepTime / 2f : (isCrouched ? stepTime * 2f : stepTime);

            //Play footstep sound if enough time has passed since last sound
            if (stepCycle > currentStepTime)
            {
                //Choose random sound from 
                int index = stepIndex + 1 > footstepSounds.Count - 1 ? 0 : stepIndex + 1; 
                AudioClip footstep = footstepSounds[index];
                audioSource.PlayOneShot(footstep, volume);

                stepCycle = 0f;

                stepIndex = index;
            }
        }
    }

    private bool SlopeCheck(Vector3 moveDirection)
    {
        //Normalize move direction
        Vector3 normalizedMoveDirection = moveDirection;
        if(moveDirection.magnitude > 1)
        {
            normalizedMoveDirection = moveDirection.normalized;
        }

        //Check slope currently under player
        Vector3 currentSlopePoint = Vector3.zero;
        RaycastHit currentSlopeHitInfo;
        if(Physics.Raycast(transform.position, Vector3.down, out currentSlopeHitInfo, internalCharacterHeight, layerMask, QueryTriggerInteraction.Ignore))
        {
            currentSlopePoint = currentSlopeHitInfo.point;
        }

        RaycastHit nextSlopeHitInfo;
        //Check slope in direction player is moving
        if (Physics.Raycast(transform.position + normalizedMoveDirection * 0.1f, Vector3.down, out nextSlopeHitInfo, internalCharacterHeight, layerMask, QueryTriggerInteraction.Ignore))
        {
            //Compare current slope and next slope to determine whether player is moving downward
            bool movingDownward = nextSlopeHitInfo.point.y < currentSlopePoint.y;
            return Slope(nextSlopeHitInfo.normal) < maxSlopeAngle || movingDownward;
        }

        return true;
    }

    //Calculate slope angle from world normal
    private float Slope(Vector3 normal)
    {
        return Vector3.Angle(normal, Vector3.up);
    }

    //Enable or disable crouch
    private void Crouch(bool crouch)
    {
        isCrouched = crouch;
        internalCharacterHeight = crouch ? crouchHeight: characterHeight;
        playerCollider.height = internalCharacterHeight;
        headJoint.transform.localPosition = new Vector3(0f, internalCharacterHeight / 2f * 0.90f, -0.1f);
    }
}
