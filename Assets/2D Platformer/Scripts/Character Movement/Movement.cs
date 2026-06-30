using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.Analytics;
using TMPro;

public class Movement : MonoBehaviour
{
    Rigidbody2D rigidbody2d;
    Vector2 move;

    public enum AimDirection { Center, Up, Down, Left, Right, UpRight, UpLeft, DownRight, DownLeft }
    [Header("Aiming System")]
    public AimDirection currentAim = AimDirection.Center;
    public float projectileSpeed = 20f;
    public Animator anim;
    private SpriteRenderer spriteRenderer;
    public TextMeshProUGUI healthText;

    [Header("Menus")]
    public GameObject loseMenuObject; 

    [Header("Player movement")]
    public InputAction MoveAction;
    public InputAction JumpAction;
    public InputAction DashAction;
    public InputAction CrouchAction;
    public InputAction FireAction;
    public InputAction AimLockAction;
    public float speed = 5.0f;
    public float jumpForce = 12.0f;

    [Header("Health system")]
    public int maxLives = 3;
    public float currentLives;
    private bool isInvincible = false;
    bool isStrictlyTakingDamage;
    private bool isDead = false;

    [Header("Silksong Gravity System")]
    public float baseGravity = 4f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2.5f;

    [Header("isGrounded")]
    public Transform groundCheckPoint;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    public bool isGrounded;
    private bool wasGrounded;

    [Header("Coyote Time")]
    public float coyoteTime = 0.15f;
    private float coyoteTimeCounter;

    [Header("Jump Buffering")]
    public float jumpBufferTime = 0.15f;
    private float jumpBufferCounter;

    [Header("Dash System")]
    public float dashForce = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    private bool canDash = true;
    private bool isDashing;
    public GameObject poofEffectPrefab;

    [Header("Cuphead Shooting System")]
    public GameObject bananaPrefab;
    public Transform firePoint;
    public Vector2 firePointNormalOffset = new Vector2(0.8f, 0.5f);
    public Vector2 firePointCrouchOffset = new Vector2(0.8f, 0.1f);
    public float shurikenSize = 0.15f;
    public float fireRate = 0.12f;
    private float nextFireTime = 0f;

    [Header("Crouch Settings")]
    public bool isCrouching = false;
    public float crouchScaleY = 0.5f;
    private float standScaleY;
    private BoxCollider2D playerCollider;
    private Vector2 standColliderSize;
    private Vector2 standColliderOffset;

    [Header("Parry System")]
    public float parryRadius = 0.8f;
    public float parryBoostForce = 15f;
    private bool hasParriedInAir = false;
    public bool isDoubleJumping = false;

    void Start()
    {
        MoveAction.Enable();
        JumpAction.Enable();
        DashAction.Enable();
        FireAction.Enable();
        CrouchAction.Enable();
        AimLockAction.Enable();

        rigidbody2d = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rigidbody2d.gravityScale = baseGravity;
        rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        currentLives = maxLives;
        UpdateHealthUI();

        isInvincible = false;
        isDashing = false;
        canDash = true;

        Physics2D.IgnoreLayerCollision(6, 7, false);
    }

    void Awake()
    {
        playerCollider = GetComponent<BoxCollider2D>();
        standScaleY = transform.localScale.y;
        standColliderSize = playerCollider.size;
        standColliderOffset = playerCollider.offset;
    }

    void Update()
    {
        if (isDead) return;
        isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);

        if (isGrounded && !wasGrounded)
        {
            LevelAudioManager.Instance.PlayPlayerSFX(LevelAudioManager.Instance.monkeyFall, 0.5f);
        }

        wasGrounded = isGrounded;

        if (isDashing) return;

        move = MoveAction.ReadValue<Vector2>();

        // FLIP
        if (Mathf.Abs(move.x) > 0.1f)
        {
            Flip(move.x);
        }

        UpdateAimingState(move.x, move.y);
        anim.SetFloat("Speed", Mathf.Abs(move.x));
        anim.SetBool("isGrounded", isGrounded); 
        anim.SetFloat("yVelocity", rigidbody2d.linearVelocity.y);

        bool isShooting = FireAction.IsPressed();
        anim.SetBool("isShooting", isShooting);

        bool isTakingDamage = anim.GetCurrentAnimatorStateInfo(0).IsName("TakeDamage");

        bool canShowBanana = FireAction.IsPressed() && !isStrictlyTakingDamage && !isDashing;

        anim.SetBool("isShooting", canShowBanana);

        int shootingLayerIndex = anim.GetLayerIndex("ShootingLayer");
        if (canShowBanana)
        {
            anim.SetLayerWeight(shootingLayerIndex, 1f);
        }
        else
        {
            anim.SetLayerWeight(shootingLayerIndex, 0f);
        }

        Vector2 aimDir = GetShootDirection();
        anim.SetFloat("AimX", Mathf.Abs(aimDir.x));
        anim.SetFloat("AimY", aimDir.y);

        bool isCrouching = CrouchAction.ReadValue<float>() > 0.1f;

        anim.SetBool("isGrounded", isGrounded);
        anim.SetBool("isCrouching", isCrouching);

        // SILKSONG GRAVITY
        if (rigidbody2d.linearVelocity.y < 0)
        {
            rigidbody2d.gravityScale = baseGravity * fallMultiplier;
        }
        else if (rigidbody2d.linearVelocity.y > 0 && !JumpAction.IsPressed())
        {
            rigidbody2d.gravityScale = baseGravity * lowJumpMultiplier;
        }
        else
        {
            rigidbody2d.gravityScale = baseGravity;
        }

        // CROUCH
        HandleCrouch();

        //FirPoint
        UpdateFirePointPosition();

        // COYOTE TIME
        if (isGrounded)
        {
            isDoubleJumping = false;
            hasParriedInAir = false;
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // JUMP BUFFERING
        if (JumpAction.triggered)
        {
            if (DDA_DataCollector.Instance != null) DDA_DataCollector.Instance.RecordJump();

            if (coyoteTimeCounter > 0f)
            {
                jumpBufferCounter = jumpBufferTime;
            }
            else if(!isGrounded && !hasParriedInAir)
            {
                PerformParryAction();
            }
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        // JUMP
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            Jump();
            jumpBufferCounter = 0f;
        }

        // DASH
        if (DashAction.triggered && canDash)
        {
            StartCoroutine(Dash());
        }

        // CUPHEAD SHOOTING
        if (FireAction.IsPressed() && Time.time >= nextFireTime)
        {
            ThrowBanana();
            nextFireTime = Time.time + fireRate;
        }
    }

    void FixedUpdate()
    {
        if (isDashing) return;

        bool isAimLocked = AimLockAction.IsPressed();
        float currentMaxSpeed = isAimLocked ? 0f : (isCrouching ? speed * 0.5f : speed);
        rigidbody2d.linearVelocity = new Vector2(move.x * currentMaxSpeed, rigidbody2d.linearVelocity.y);
    }

    void UpdateFirePointPosition()
    {
        if (firePoint == null) return;

        if (isCrouching)
        {
            firePoint.localPosition = firePointCrouchOffset;
        }
        else
        {
            firePoint.localPosition = firePointNormalOffset;
        }
    }

    void UpdateAimingState(float h, float v)
    {
        float threshold = 0.5f; 

        if (Mathf.Abs(h) < threshold && Mathf.Abs(v) < threshold)
        {
            currentAim = AimDirection.Center;
            return;
        }

        if (h > threshold)
        {
            if (v > threshold) currentAim = AimDirection.UpRight;
            else if (v < -threshold) currentAim = AimDirection.DownRight;
            else currentAim = AimDirection.Right;
        }
        else if (h < -threshold)
        {
            if (v > threshold) currentAim = AimDirection.UpLeft;
            else if (v < -threshold) currentAim = AimDirection.DownLeft;
            else currentAim = AimDirection.Left;
        }
        else 
        {
            if (v > threshold) currentAim = AimDirection.Up;
            else if (v < -threshold) currentAim = AimDirection.Down;
        }
    }

    Vector2 GetShootDirection()
    {
        bool isAimLocked = AimLockAction.IsPressed();

        Vector2 forwardDir = new Vector2(Mathf.Sign(transform.localScale.x), 0);
        Vector2 dir = forwardDir;

        switch (currentAim)
        {
            case AimDirection.Up: dir = Vector2.up; break;

            case AimDirection.Down:
                if (isAimLocked) dir = Vector2.down;
                else dir = forwardDir;
                break;

            case AimDirection.Left: dir = Vector2.left; break;
            case AimDirection.Right: dir = Vector2.right; break;
            case AimDirection.UpRight: dir = new Vector2(1f, 1f); break;
            case AimDirection.UpLeft: dir = new Vector2(-1f, 1f); break;

            case AimDirection.DownRight:
                if (isAimLocked) dir = new Vector2(1f, -1f);
                else dir = Vector2.right;
                break;

            case AimDirection.DownLeft:
                if (isAimLocked) dir = new Vector2(-1f, -1f);
                else dir = Vector2.left;
                break;

            case AimDirection.Center:
                dir = forwardDir;
                break;
        }

        return dir.normalized;
    }

    void PerformParryAction()
    {
        isDoubleJumping = true;
        if (hasParriedInAir) return;

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, parryRadius);
        bool hitSomething = false;
        TicketProjectile targetTicket = null;

        foreach (Collider2D hit in hitColliders)
        {
            if (hit.CompareTag("Parryable"))
            {
                LevelAudioManager.Instance.PlayPlayerSFX(LevelAudioManager.Instance.monkeyParry, 0.5f);

                targetTicket = hit.GetComponent<TicketProjectile>();
                if (targetTicket != null)
                {
                    hitSomething = true;
                    break;
                }
            }
        }

        anim.SetBool("isSuccessfulParry", hitSomething);
        anim.SetTrigger("Parry");
        anim.Update(0); 

        if (hitSomething)
        {
            hasParriedInAir = false; 
            rigidbody2d.linearVelocity = new Vector2(rigidbody2d.linearVelocity.x, parryBoostForce);

            targetTicket.Parry();
        }
        else
        {
            hasParriedInAir = true; 
        }
    }

    IEnumerator HitStop(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    IEnumerator FlashEffect()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.white;
            yield return new WaitForSeconds(0.05f);
            sr.color = Color.gray;
            yield return new WaitForSeconds(0.05f);
            sr.color = Color.white;
        }
    }

    void Flip(float horizontalInput)
    {
        float direction = (horizontalInput > 0) ? 1 : -1;
        transform.localScale = new Vector3(direction * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    void Jump()
    {

        rigidbody2d.linearVelocity = new Vector2(rigidbody2d.linearVelocity.x, jumpForce);
        isGrounded = false;
        coyoteTimeCounter = 0f;

        LevelAudioManager.Instance.PlayPlayerSFX(LevelAudioManager.Instance.monkeyJump, 0.4f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Boss") && !isInvincible)
        {
            TakeDamage("Müller_Melee");
        }
        else if (other.CompareTag("Parryable") && !isInvincible)
        {
            TakeDamage("Ticket_Bomb");
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Car") && !isInvincible)
        {
            TakeDamage("Car_Obstacle");
        }

        if (collision.gameObject.GetComponent<MovingTaraba>() != null)
        {
            if (collision.contacts.Length > 0 && collision.contacts[0].normal.y > 0.5f)
            {
                transform.SetParent(collision.transform);
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (this.gameObject.activeInHierarchy && transform.parent == collision.transform)
        {
            transform.SetParent(null);
        }
    }

    private void OnDisable()
    {
        transform.SetParent(null);
    }

    void HandleCrouch()
    {
        bool isDown = CrouchAction.IsPressed();

        if (isDown && !isCrouching)
        {
            StartCrouch();
        }
        else if (!isDown && isCrouching)
        {
            StopCrouch();
        }
    }

    void StartCrouch()
    {
        isCrouching = true;
        anim.SetBool("isCrouching", true);
        anim.Update(0);
        playerCollider.size = new Vector2(standColliderSize.x, standColliderSize.y * 0.5f);
        playerCollider.offset = new Vector2(standColliderOffset.x, standColliderOffset.y - (standColliderSize.y * 0.25f));
    }

    void StopCrouch()
    {
        isCrouching = false;
        anim.SetBool("isCrouching", false);
        playerCollider.size = standColliderSize;
        playerCollider.offset = standColliderOffset;
    }

    public void TakeDamage(string damageSource)
    {
        if (currentLives <= 0 || isInvincible) return;

        if (DDA_DataCollector.Instance != null)
            DDA_DataCollector.Instance.RecordDamage(1, damageSource, transform.position);

        anim.SetTrigger("TakeDamage");

        LevelAudioManager.Instance.PlayPlayerSFX(LevelAudioManager.Instance.monkeyTakeDmg, 0.6f);

        StartCoroutine(DamageVisualLock());

        currentLives = Mathf.Max(0, currentLives - 1);
        UpdateHealthUI();
        Debug.Log("MONKEY HIT! Lives left: " + currentLives);

        if (currentLives <= 0)
        {
            GameOver();
        }
        else
        {
            StartCoroutine(BecomeInvincible());
        }
    }

    void UpdateHealthUI()
    {
        if (healthText != null)
        {
            healthText.text = "HP. " + currentLives;
        }
    }

    private IEnumerator DamageVisualLock()
    {
        isStrictlyTakingDamage = true;

        int shootingLayerIndex = anim.GetLayerIndex("ShootingLayer");
        anim.SetLayerWeight(shootingLayerIndex, 0f);

        yield return new WaitForSeconds(0.3f);

        isStrictlyTakingDamage = false;
    }

    void GameOver()
    {
        if (isDead) return; 
        isDead = true; 

        Debug.Log("GAME OVER: The Monkey was arrested!");
        DDA_BulletproofExporter.ExportEvent("MOARTE");

        LevelAudioManager.Instance.PlayPlayerSFX(LevelAudioManager.Instance.monkeyDefeat, 0.8f);

        anim.Play("Monkey_Die", 0, 0f);
        anim.SetBool("isDead", true);
        int shootingLayerIndex = anim.GetLayerIndex("ShootingLayer");
        if (shootingLayerIndex != -1) anim.SetLayerWeight(shootingLayerIndex, 0f);

        this.enabled = false;

        rigidbody2d.linearVelocity = new Vector2(0, rigidbody2d.linearVelocity.y);

        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        StartCoroutine(ShowLoseScreenWithDelay());
    }

    IEnumerator ShowLoseScreenWithDelay()
    {
        if (Unity.MLAgents.Academy.Instance.IsCommunicatorOn)
        {
            yield return new WaitForSeconds(0.1f);

            if (DDA_DataCollector.Instance != null)
            {
                DDA_DataCollector.Instance.RecordDeath();
            }

            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            yield break;
        }

        yield return new WaitForSeconds(2.0f);

        if (loseMenuObject != null)
        {
            loseMenuObject.SetActive(true);
            Time.timeScale = 0f; 

            BossController bossScript = Object.FindFirstObjectByType<BossController>();
            if (bossScript != null)
            {
                loseMenuObject.GetComponent<LoseMenu>().ShowProgress(bossScript.currentHealth, bossScript.maxHealth);
            }
        }
    }

    private IEnumerator BecomeInvincible()
    {
        isInvincible = true;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();

        float timer = 0f;
        float duration = 1.5f;

        while (timer < duration)
        {
            if (sr != null)
            {
                float alpha = Mathf.PingPong(Time.time * 10f, 0.4f) + 0.3f;
                sr.color = new Color(1, 1, 1, alpha);
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (sr != null) sr.color = Color.white;
        isInvincible = false;
    }

    private IEnumerator Dash()
    {
        canDash = false;
        isDashing = true;
        isInvincible = true;

        BossController boss = Object.FindFirstObjectByType<BossController>();
        if (boss != null && DDA_DataCollector.Instance != null)
        {
            float dist = Vector3.Distance(transform.position, boss.transform.position);
            DDA_DataCollector.Instance.RecordDashAccuracy(dist);
        }

        anim.SetTrigger("Dash");

        LevelAudioManager.Instance.PlayPlayerSFX(LevelAudioManager.Instance.monkeyDash, 0.5f);
        Instantiate(poofEffectPrefab, transform.position, Quaternion.identity);
        spriteRenderer.enabled = false;
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("separat"), true);

        float originalGravity = rigidbody2d.gravityScale;
        rigidbody2d.gravityScale = 0f;

        Physics2D.IgnoreLayerCollision(6, 7, true);

        float dashDirection = move.x != 0 ? move.x : transform.localScale.x;
        rigidbody2d.linearVelocity = new Vector2(dashDirection * dashForce, 0f);

        yield return new WaitForSeconds(dashDuration);

        rigidbody2d.linearVelocity = Vector2.zero;
        spriteRenderer.enabled = true;
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("separat"), false);
        rigidbody2d.gravityScale = originalGravity;

        Instantiate(poofEffectPrefab, transform.position, Quaternion.identity);
        yield return new WaitForSeconds(0.1f);

        isInvincible = false;
        Physics2D.IgnoreLayerCollision(6, 7, false);
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    void ThrowBanana()
    {
        LevelAudioManager.Instance.PlayPlayerSFX(LevelAudioManager.Instance.bananaThrow, 0.2f);

        GameObject banana = Instantiate(bananaPrefab, firePoint.position, firePoint.rotation);
        Rigidbody2D bananarb = banana.GetComponent<Rigidbody2D>();

        Vector2 shootDirection = GetShootDirection();

        if (bananarb != null)
        {
            bananarb.linearVelocity = shootDirection * projectileSpeed;
        }

        float angle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;
        banana.transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
    }
}