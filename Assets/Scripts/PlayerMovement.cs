using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;

    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float deceleration = 60f;

    private Rigidbody2D rb;
    private InputAction moveAction;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        moveAction = inputActions.FindAction("Player/Move", throwIfNotFound: true);
    }

    private void OnEnable()
    {
        moveAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
    }

    private void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        // If player is moving, update walking animation

        if (moveInput.sqrMagnitude > 0f)
        {
            animator.SetBool("IsWalking", true);

            if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
            {
                // moving left or right
                animator.SetInteger("Direction", moveInput.x > 0 ? 1 : 3);
            }
            if (Mathf.Abs(moveInput.y) > Mathf.Abs(moveInput.x))
            {
                // moving up or down
                animator.SetInteger("Direction", moveInput.y > 0 ? 0 : 2);
            }
        }
        else
        {
            animator.SetBool("IsWalking", false);
        }

    }

    public void FinishInteraction() 
    {
        animator.SetBool("IsInteracting", false);
    }

    private void FixedUpdate()
    {
        Vector2 targetVelocity = moveInput * moveSpeed;
        float rate = moveInput.sqrMagnitude > 0f ? acceleration : deceleration;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);
    }
}
