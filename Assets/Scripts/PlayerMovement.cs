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
    [SerializeField] private AudioClip grassWalking;
    [SerializeField] private AudioClip woodWalking;

    private Rigidbody2D rb;
    private InputAction moveAction;
    private Vector2 moveInput;
    private float stepTimer;
    private AudioSource moveSource;
    public bool isInBarn;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        moveAction = inputActions.FindAction("Player/Move", throwIfNotFound: true);
        moveSource = gameObject.AddComponent<AudioSource>();
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

        // If player is moving, update walking animation and play sound

        if (moveInput.sqrMagnitude > 0f)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                if (isInBarn)
                {
                    moveSource.PlayOneShot(woodWalking, 1f);
                }
                else
                {
                    moveSource.PlayOneShot(grassWalking, 1f);
                }
                stepTimer = 0.6f;
            }

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
            stepTimer = 0f;
            animator.SetBool("IsWalking", false);
        }

    }

    public void FinishInteraction() 
    {
        animator.SetBool("IsInteracting", false);
    }

    private void FixedUpdate()
    {
        // Wind is folded into the target rather than applied as a separate
        // force: this line overwrites linearVelocity outright, so anything
        // pushing the body from another component would be wiped here.
        // Standing still means drifting with the gust; walking into it costs
        // ground, and walking with it carries you.
        Vector2 wind = WeatherManager.Instance != null
            ? WeatherManager.Instance.WindVelocity
            : Vector2.zero;

        Vector2 targetVelocity = moveInput * moveSpeed + wind;

        // Deceleration is for letting go of the stick. Drifting in wind is not
        // coasting to a stop, so it still accelerates toward the gust.
        bool driving = moveInput.sqrMagnitude > 0f || wind.sqrMagnitude > 0f;
        float rate = driving ? acceleration : deceleration;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);
    }
}
