using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Windows;
public class PlayerController : MonoBehaviour, IDamageable
{
    Rigidbody2D rb;
    PlayerContext playerContext;
    HandleInputs handleInputs;

    [Header("Componentes")]
    [Header("Vida")]
    [SerializeField] private float maxHealth;

    [Header("Movimiento")]
    [SerializeField] private float walkingSpeed;
    [SerializeField] private float runningSpeed;
    private float currentSpeed;
    [SerializeField] private float smoothFactor;

    [Header("Salto")]
    [SerializeField] private float jumpForce;
    [SerializeField] private float maxCoyoteTime;
    [SerializeField] private float maxJumpBufferTime;

    [Header("Dash")]
    [SerializeField] private float dashSpeed;
    [SerializeField] private float dashDuration;
    [SerializeField] private float dashCooldown;

    [Header("Detecci�n de Suelo")]
    [SerializeField] private Transform GroundCheck;
    [SerializeField] private LayerMask groundLayer;

    private bool isGrounded;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float currentHealth;
    private float gravity;
    private bool canDash = true;
    public bool isDashing;
    private float dashTimeLeft;
    private Vector2 dashDirection;
    private SpriteRenderer spriteRenderer;
    private PlayerAnimation playerAnimation;

    public float MaxHealth { get => maxHealth; set { } }
    public float CurrentHealth { get => currentHealth; set { } }

    public Action OnTakeDamage { get; set; }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        handleInputs = GetComponent<HandleInputs>();
        playerContext = GetComponent<PlayerContext>();
        playerAnimation = GetComponent<PlayerAnimation>();

        currentHealth = maxHealth;
        gravity = rb.gravityScale;
    }

    private void Update()
    {
        Move();
        WaitForJump();
        WaitForDash();
        RefreshTimers();
    }

    private void FixedUpdate()
    {
        IsDashing();
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        OnTakeDamage?.Invoke();
        if (currentHealth <= 0)
        {
            Debug.Log("Player has died.");
            gameObject.SetActive(false);
        }
    }

    private void Move()
    {
        if (isDashing) return;
        if (handleInputs.IsRunning()) { currentSpeed = runningSpeed; } else { currentSpeed = walkingSpeed; }
        if (handleInputs.IsAttacking()) { rb.velocity = Vector2.zero;   
                                            StartCoroutine(playerAnimation.WaitForCurrentAnimation()); }

        FaceDirection();
        Vector2 move = playerContext.HandleInputs.GetMoveVector2();

        Vector2 targetVelocity = new Vector2(move.x * currentSpeed, rb.velocity.y);
        rb.velocity = Vector2.Lerp(rb.velocity, targetVelocity, smoothFactor * Time.fixedDeltaTime);
    }

    private void FaceDirection()
    {
        Vector2 move = playerContext.HandleInputs.GetMoveVector2();
        if (move.x > 0)
            spriteRenderer.flipX = false;
        else if (move.x < 0)
            spriteRenderer.flipX = true;
    }

    private void RefreshTimers()
    {
        isGrounded = Physics2D.OverlapCircle(GroundCheck.position, 0.2f, groundLayer);

        // Actualiza el contador de coyote time
        if (isGrounded)
            coyoteTimer = maxCoyoteTime;
        else
            coyoteTimer -= Time.deltaTime;

        // Actualiza el contador de buffer de salto
        if (playerContext.HandleInputs.IsJumping())
            jumpBufferTimer = maxJumpBufferTime;
        else
            jumpBufferTimer -= Time.deltaTime;
    }

    private void WaitForJump()
    {
        // Si el jugador presiona el bot�n de salto y est� en coyote time o tocando el suelo
        if (jumpBufferTimer > 0f && (isGrounded || coyoteTimer > 0f))
        {
            Vector2 currentVelocity = rb.velocity;
            // Aplica la fuerza de salto en el eje Y
            rb.velocity = new Vector2(currentVelocity.x, jumpForce);

            // Reinicia los contadores
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }
    }


    private void IsDashing()
    {
        if (isDashing)
        {
            rb.velocity = dashDirection * dashSpeed;
            dashTimeLeft -= Time.fixedDeltaTime;

            if (dashTimeLeft <= 0f)
            {
                EndDash();
            }
        }
    }
    private void WaitForDash()
    {
        if (!isDashing && playerContext.HandleInputs.IsDashing() && canDash)
        {
            canDash = false;
            isDashing = true;
            dashTimeLeft = dashDuration;

            Vector2 input = playerContext.HandleInputs.GetMoveVector2();

            if (input == Vector2.zero)
                input = new Vector2(rb.transform.localScale.x >= 0 ? 1 : -1, 0);

            dashDirection = input.normalized;
            rb.gravityScale = 0f;
        }
    }
    private void EndDash()
    {
        isDashing = false;
        rb.gravityScale = gravity;
        StartCoroutine(CooldownRoutine(dashCooldown));
    }

    private IEnumerator CooldownRoutine(float cooldown)
    {
        yield return new WaitForSeconds(cooldown);
        canDash = true;
        Debug.Log("Dash listo");
    }

    // cuando "agarra" items que tienen la interfaz IPickUp
    void OnTriggerEnter2D(Collider2D other)
    {
        IPickUp pickup = other.gameObject.GetComponent<IPickUp>();

        if (pickup != null)
        {
            pickup.PickUp();
            Debug.Log($"<color=green>el pickeable se agarro</color>");
        }

    }
}

