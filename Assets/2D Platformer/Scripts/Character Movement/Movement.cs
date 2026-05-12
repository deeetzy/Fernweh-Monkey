using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.Analytics;
using TMPro;

public class Movement : MonoBehaviour
{
    Rigidbody2D rigidbody2d;
    Vector2 move;

    // --- NOU: Enum-ul pentru direcțiile de țintire ---
    public enum AimDirection { Center, Up, Down, Left, Right, UpRight, UpLeft, DownRight, DownLeft }
    [Header("Aiming System")]
    public AimDirection currentAim = AimDirection.Center;
    public float projectileSpeed = 20f;
    public Animator anim;
    private SpriteRenderer spriteRenderer;
    public TextMeshProUGUI healthText;

    [Header("Menus")]
    public GameObject loseMenuObject; // Trage panelul "LoseMenu" aici în Inspector

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
    public int currentLives;
    private bool isInvincible = false;
    bool isStrictlyTakingDamage;
    private bool isDead = false; // Siguranță internă pentru cod

    [Header("Silksong Gravity System")]
    public float baseGravity = 4f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2.5f;

    [Header("isGrounded")]
    public Transform groundCheckPoint;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    private bool isGrounded;
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
    public Vector2 firePointNormalOffset = new Vector2(0.8f, 0.5f); // Poziția normală (Idle/Run)
    public Vector2 firePointCrouchOffset = new Vector2(0.8f, 0.1f); // Poziția când e ghemui (mai jos)
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

        // FORȚĂM motorul fizic să activeze coliziunea între Player (6) și Bule/Boss (7)
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

        // --- LOGICA DE SUNET PENTRU ATERIZARE ---
        if (isGrounded && !wasGrounded)
        {
            // Sunetul se va auzi DOAR la impact, o singură dată!
            LevelAudioManager.Instance.PlayPlayerSFX(LevelAudioManager.Instance.monkeyFall, 0.5f);
        }

        // Salvează starea curentă pentru frame-ul următor (pune asta la finalul Update sau după check)
        wasGrounded = isGrounded;

        if (isDashing) return;

        move = MoveAction.ReadValue<Vector2>();

        // FLIP
        if (Mathf.Abs(move.x) > 0.1f)
        {
            Flip(move.x);
        }

        // --- NOU: Actualizăm direcția în care se uită arma ---
        UpdateAimingState(move.x, move.y);

        // --- NOU: ANIMATOR - Updatează variabilele continue (în fiecare frame) ---
        anim.SetFloat("Speed", Mathf.Abs(move.x)); // Viteza (Run/Idle)
        anim.SetBool("isGrounded", isGrounded); // E pe pământ?
        anim.SetFloat("yVelocity", rigidbody2d.linearVelocity.y); // Sare sau cade?
                                                                  // --- MODIFICAT PENTRU RESETARE LAYER ---
        bool isShooting = FireAction.IsPressed();
        anim.SetBool("isShooting", isShooting);

        // 1. Verificăm dacă suntem în starea de TakeDamage pe Base Layer (Index 0)
        // Asigură-te că animația ta de damage se numește exact "TakeDamage" în Animator
        bool isTakingDamage = anim.GetCurrentAnimatorStateInfo(0).IsName("TakeDamage");

        // 2. Logica de tras: trebuie să apăsăm butonul ȘI să nu fim în damage ȘI să nu fim în dash
        bool canShowBanana = FireAction.IsPressed() && !isStrictlyTakingDamage && !isDashing;

        anim.SetBool("isShooting", canShowBanana);

        // 3. Setăm Weight-ul: dacă suntem loviți, forțăm layer-ul de tras la 0 (invizibil)
        int shootingLayerIndex = anim.GetLayerIndex("ShootingLayer");
        if (canShowBanana)
        {
            anim.SetLayerWeight(shootingLayerIndex, 1f);
        }
        else
        {
            anim.SetLayerWeight(shootingLayerIndex, 0f);
        }

        // Trimitem direcția spre Blend Tree-ul nostru de Aiming
        Vector2 aimDir = GetShootDirection();
        anim.SetFloat("AimX", Mathf.Abs(aimDir.x)); // Absolut pentru că Flip-ul întoarce deja vizual
        anim.SetFloat("AimY", aimDir.y);
        // ------------------------------------------------------------------------
        // 1. Întâi verificăm dacă suntem pe sol

        // 2. Apoi citim input-ul de Crouch
        bool isCrouching = CrouchAction.ReadValue<float>() > 0.1f;

        // 3. Trimitem la Animator ÎNAINTE de orice altceva
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

        // Dacă ținem apăsat butonul de Aim Lock, viteza orizontală devine 0
        bool isAimLocked = AimLockAction.IsPressed();
        float currentMaxSpeed = isAimLocked ? 0f : (isCrouching ? speed * 0.5f : speed);
        rigidbody2d.linearVelocity = new Vector2(move.x * currentMaxSpeed, rigidbody2d.linearVelocity.y);
    }

    void UpdateFirePointPosition()
    {
        if (firePoint == null) return;

        // Dacă maimuța este ghemuită, folosim offset-ul de crouch, altfel cel normal
        if (isCrouching)
        {
            firePoint.localPosition = firePointCrouchOffset;
        }
        else
        {
            firePoint.localPosition = firePointNormalOffset;
        }
    }

    // --- NOU: Funcția care citește combinația de taste ---
    void UpdateAimingState(float h, float v)
    {
        float threshold = 0.5f; // Limita pentru a detecta apăsarea clară a tastei

        if (Mathf.Abs(h) < threshold && Mathf.Abs(v) < threshold)
        {
            currentAim = AimDirection.Center;
            return;
        }

        if (h > threshold) // Apasă Dreapta
        {
            if (v > threshold) currentAim = AimDirection.UpRight;
            else if (v < -threshold) currentAim = AimDirection.DownRight;
            else currentAim = AimDirection.Right;
        }
        else if (h < -threshold) // Apasă Stânga
        {
            if (v > threshold) currentAim = AimDirection.UpLeft;
            else if (v < -threshold) currentAim = AimDirection.DownLeft;
            else currentAim = AimDirection.Left;
        }
        else // Apasă Doar Vertical
        {
            if (v > threshold) currentAim = AimDirection.Up;
            else if (v < -threshold) currentAim = AimDirection.Down;
        }
    }

    // --- NOU: Funcția care calculează vectorul corect de zbor ---
    Vector2 GetShootDirection()
    {
        // 1. Verificăm dacă ții apăsat butonul de Aim Lock (Ctrl)
        bool isAimLocked = AimLockAction.IsPressed();

        // 2. Definim direcția implicită: drept în față (stânga sau dreapta)
        Vector2 forwardDir = new Vector2(Mathf.Sign(transform.localScale.x), 0);
        Vector2 dir = forwardDir;

        switch (currentAim)
        {
            case AimDirection.Up: dir = Vector2.up; break;

            case AimDirection.Down:
                // Trage în jos DOAR dacă ții apăsat Ctrl. 
                // Altfel, vei sta aplecat (Crouch) și vei trage drept în față.
                if (isAimLocked) dir = Vector2.down;
                else dir = forwardDir;
                break;

            case AimDirection.Left: dir = Vector2.left; break;
            case AimDirection.Right: dir = Vector2.right; break;
            case AimDirection.UpRight: dir = new Vector2(1f, 1f); break;
            case AimDirection.UpLeft: dir = new Vector2(-1f, 1f); break;

            case AimDirection.DownRight:
                // Trage pe diagonală jos DOAR dacă ții apăsat Ctrl.
                // Dacă doar alergi spre dreapta și apeși și în jos, gloanțele merg drept.
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

        return dir.normalized; // Returnăm mereu vectorul normalizat pentru viteză constantă
    }

    void PerformParryAction()
    {
        if (hasParriedInAir) return;

        // 1. Scanăm zona pentru obiecte parabile
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

        // 2. Trimitem datele către Animator ÎNAINTE de Trigger
        anim.SetBool("isSuccessfulParry", hitSomething);
        anim.SetTrigger("Parry");
        anim.Update(0); // Forțăm tranziția instantanee

        // 3. Executăm logica fizică
        if (hitSomething)
        {
            hasParriedInAir = false; // Resetăm pentru chained parries
            rigidbody2d.linearVelocity = new Vector2(rigidbody2d.linearVelocity.x, parryBoostForce);

            // Spunem biletului să plece spre boss
            targetTicket.Parry();

            // OPȚIONAL: Micul flash verde de care vorbeam
            // StartCoroutine(DebugColor(Color.green));
        }
        else
        {
            hasParriedInAir = true; // A ratat, blocăm alte încercări
                                    // StartCoroutine(DebugColor(Color.red));
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
            sr.color = Color.white; // Flash alb
            yield return new WaitForSeconds(0.05f);
            sr.color = Color.gray; // Întoarcere rapidă spre normal
            yield return new WaitForSeconds(0.05f);
            sr.color = Color.white; // Normal
        }
    }

    void Flip(float horizontalInput)
    {
        float direction = (horizontalInput > 0) ? 1 : -1;
        // Modificăm doar X-ul pentru întoarcere, lăsăm restul cum e
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
            TakeDamage();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Car") && !isInvincible)
        {
            TakeDamage();
        }

        // Verificăm dacă suntem pe o platformă mobilă
        if (collision.gameObject.GetComponent<MovingTaraba>() != null)
        {
            // Verificăm dacă picioarele maimuței sunt deasupra (normal.y > 0.5)
            if (collision.contacts.Length > 0 && collision.contacts[0].normal.y > 0.5f)
            {
                transform.SetParent(collision.transform);
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        // VERIFICARE DE SIGURANȚĂ:
        // Verificăm dacă scriptul este încă activ și dacă obiectul nu este în curs de distrugere
        if (this.gameObject.activeInHierarchy && transform.parent == collision.transform)
        {
            transform.SetParent(null);
        }
    }

    // Adăugăm și această funcție pentru siguranță maximă
    private void OnDisable()
    {
        // Dacă jucătorul moare sau este dezactivat, îl scoatem de sub părinte 
        // pentru a evita erorile de ierarhie
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
        // ȘTERGE linia cu transform.localScale de aici!
        playerCollider.size = standColliderSize;
        playerCollider.offset = standColliderOffset;
    }

    public void TakeDamage()
    {
        if (currentLives <= 0 || isInvincible) return;

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

        // Forțăm Weight 0 imediat
        int shootingLayerIndex = anim.GetLayerIndex("ShootingLayer");
        anim.SetLayerWeight(shootingLayerIndex, 0f);

        // Așteptăm cât durează animația ta de damage (ex: 0.3 secunde)
        yield return new WaitForSeconds(0.3f);

        isStrictlyTakingDamage = false;
    }

    void GameOver()
    {
        if (isDead) return; // Siguranță să nu apelăm de două ori
        isDead = true; // Blochează Update-ul imediat

        Debug.Log("GAME OVER: The Monkey was arrested!");

        LevelAudioManager.Instance.PlayPlayerSFX(LevelAudioManager.Instance.monkeyDefeat, 0.8f);

        // 1. Activăm animația de moarte
        anim.Play("Monkey_Die", 0, 0f);
        anim.SetBool("isDead", true);

        // 2. STINGEM ShootingLayer-ul (ca să nu mai stea cu banana în mână când moare)
        int shootingLayerIndex = anim.GetLayerIndex("ShootingLayer");
        if (shootingLayerIndex != -1) anim.SetLayerWeight(shootingLayerIndex, 0f);

        // 3. Oprim scriptul de mișcare (deja aveai asta)
        this.enabled = false;

        rigidbody2d.linearVelocity = new Vector2(0, rigidbody2d.linearVelocity.y);

        // Ignorăm coliziunile cu inamicii (Layer-ul 7 de obicei e inamic, ajustează dacă e cazul)
        // Sau pur și simplu îi punem un tag de "Dead" și verificăm în inamici
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast"); // Nu mai este detectată de inamici

        StartCoroutine(ShowLoseScreenWithDelay());
    }

    IEnumerator ShowLoseScreenWithDelay()
    {
        Debug.Log("S-a apelat GameOver!");
        yield return new WaitForSecondsRealtime(1.5f); // Așteptăm 1.5 secunde

        if (loseMenuObject != null)
        {
            loseMenuObject.SetActive(true);
            Time.timeScale = 0f; // Înghețăm jocul

            // Trimitem datele către Slider
            BossController boss = Object.FindFirstObjectByType<BossController>();
            if (boss != null)
            {
                loseMenuObject.GetComponent<LoseMenu>().ShowProgress(boss.currentHealth, boss.maxHealth);
            }
        }
    }

    private IEnumerator BecomeInvincible()
    {
        isInvincible = true;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();

        // În loc de o transparență fixă, facem un efect de "pulse" (ca în Silksong)
        float timer = 0f;
        float duration = 1.5f;

        while (timer < duration)
        {
            // Oscilăm transparența între 0.3 și 0.7 pentru un efect de "fantomă"
            if (sr != null)
            {
                float alpha = Mathf.PingPong(Time.time * 10f, 0.4f) + 0.3f;
                sr.color = new Color(1, 1, 1, alpha);
            }

            timer += Time.deltaTime;
            yield return null; // Așteptăm următorul cadru
        }

        if (sr != null) sr.color = Color.white; // Revenim la normal
        isInvincible = false;
    }

    private IEnumerator Dash()
    {
        canDash = false;
        isDashing = true;
        isInvincible = true;

        anim.SetTrigger("Dash");

        LevelAudioManager.Instance.PlayPlayerSFX(LevelAudioManager.Instance.monkeyDash, 0.5f);
        // 1. START DASH: Poof + Dispare
        Instantiate(poofEffectPrefab, transform.position, Quaternion.identity);
        spriteRenderer.enabled = false; // Maimuța devine invizibilă

        float originalGravity = rigidbody2d.gravityScale;
        rigidbody2d.gravityScale = 0f;

        Physics2D.IgnoreLayerCollision(6, 7, true);

        float dashDirection = move.x != 0 ? move.x : transform.localScale.x;
        rigidbody2d.linearVelocity = new Vector2(dashDirection * dashForce, 0f);

        yield return new WaitForSeconds(dashDuration);

        rigidbody2d.linearVelocity = Vector2.zero;
        spriteRenderer.enabled = true; // Maimuța reapare
        rigidbody2d.gravityScale = originalGravity;

        Instantiate(poofEffectPrefab, transform.position, Quaternion.identity);
        yield return new WaitForSeconds(0.1f);

        isInvincible = false; // DEZACTIVĂM
        Physics2D.IgnoreLayerCollision(6, 7, false);
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // --- MODIFICAT: Acum aruncă în funcție de vectorul de țintire ---
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

        // Rotim banana ca să se uite vizual în direcția corectă (sus/jos)
        // Calculăm unghiul în grade pe baza vectorului de direcție
        float angle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;
        banana.transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
    }
}