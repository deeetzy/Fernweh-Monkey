using UnityEngine;
using System.Collections;

public class K9Unit : MonoBehaviour
{
    public enum K9State { Entering, Barking, Patrol, Charging, Drifting, Outro }
    public K9State currentState = K9State.Entering;

    [Header("Movement Settings")]
    public float patrolSpeed = 5f;
    public float chargeSpeed = 10f;
    public float entranceSpeed = 2.5f;
    public float targetWaitX = 9.5f;
    public float arenaLimit = 11.5f;
    public float dynamicStunDuration = 1.8f;

    public float ddaSpeedMultiplier = 1.0f;

    [Header("Detection")]
    public float detectionRange = 10f;
    public float rearDetectionMinDist = 3.5f;
    public float chargeCooldown = 1.5f;
    public float overshootTolerance = 1.0f;
    private float currentCooldownTimer;

    public float groundY = -5.3f;
    private int direction = -1;
    private Transform player;
    private SpriteRenderer sr;
    private Animator anim;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        transform.position = new Vector3(transform.position.x, groundY, 0);

        transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        anim.Play("Dog_Walk");
    }

    void Update()
    {
        if (currentState == K9State.Outro) return;
        if (currentCooldownTimer > 0) currentCooldownTimer -= Time.deltaTime;

        switch (currentState)
        {
            case K9State.Entering:
                LevelAudioManager.Instance.StopLoop();
                EntranceLogic();
                break;
            case K9State.Patrol:
                LevelAudioManager.Instance.StopLoop();
                PatrolLogic();
                CheckForPlayer();
                break;
            case K9State.Charging:
                LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.k9UnitRun, 0.4f);
                ChargeLogic();
                break;
        }
    }

    public void WakeUp()
    {
        if (currentState == K9State.Barking)
        {
            sr.color = Color.white;
            currentState = K9State.Patrol;
            anim.Play("Dog_Walk");
        }
    }

    void EntranceLogic()
    {
        transform.Translate(Vector2.left * entranceSpeed * Time.deltaTime);
        if (transform.position.x <= targetWaitX)
        {
            transform.position = new Vector3(targetWaitX, groundY, 0);
            currentState = K9State.Barking;

            anim.Play("Dog_Intro");
            LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.k9UnitBark, 0.6f);
            StartCoroutine(WaitInIdleAfterIntro());
        }
    }

    IEnumerator WaitInIdleAfterIntro()
    {
        yield return new WaitForSeconds(1.5f);
        if (currentState == K9State.Barking)
        {
            anim.Play("Dog_Idle"); 
        }
    }

    void PatrolLogic()
    {
        float speed = patrolSpeed * ddaSpeedMultiplier;
        transform.Translate(Vector2.right * speed * direction * Time.deltaTime);

        if (!anim.GetCurrentAnimatorStateInfo(0).IsName("Dog_Walk"))
        {
            anim.Play("Dog_Walk");
        }

        if (transform.position.x > arenaLimit && direction > 0)
        {
            direction = -1;
            Flip();
        }
        else if (transform.position.x < -arenaLimit && direction < 0)
        {
            direction = 1;
            Flip();
        }
    }

    void CheckForPlayer()
    {
        if (player == null || currentCooldownTimer > 0) return;

        float dist = Vector2.Distance(transform.position, player.position);
        float dirToPlayer = player.position.x - transform.position.x;
        bool isFacingPlayer = (dirToPlayer > 0 && direction > 0) || (dirToPlayer < 0 && direction < 0);

        if ((isFacingPlayer && dist < detectionRange) || (!isFacingPlayer && dist > rearDetectionMinDist && dist < detectionRange))
        {
            if (!isFacingPlayer) { direction *= -1; Flip(); }
            StartCoroutine(PrepareCharge());
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("K9 a atins: " + other.gameObject.name + " cu tag-ul: " + other.tag);

        if (other.CompareTag("Player"))
        {
            LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.k9UnitBite, 0.8f);

            Movement playerMovement = other.GetComponent<Movement>();
            if (playerMovement != null)
            {
                playerMovement.TakeDamage("K9Unit");
                Debug.Log("Damage trimis către Player!");
            }

            if (currentState == K9State.Charging)
            {
                LevelAudioManager.Instance.StopLoop();
                currentState = K9State.Patrol;
                currentCooldownTimer = chargeCooldown;
                anim.Play("Dog_Walk");
            }
        }
    }

    IEnumerator PrepareCharge()
    {
        currentState = K9State.Barking;
        anim.Play("Dog_Intro");
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.k9UnitBark, 0.7f);
        yield return new WaitForSeconds(0.4f);

        currentState = K9State.Charging;
        anim.Play("Dog_Run");
    }

    void ChargeLogic()
    {
        float speed = chargeSpeed * ddaSpeedMultiplier;
        transform.Translate(Vector2.right * speed * direction * Time.deltaTime);
        bool hitWall = Mathf.Abs(transform.position.x) > arenaLimit;

        bool passedPlayer = false;
        if (player != null)
        {
            if (direction > 0 && transform.position.x > player.position.x + overshootTolerance) passedPlayer = true;
            else if (direction < 0 && transform.position.x < player.position.x - overshootTolerance) passedPlayer = true;
        }

        if (hitWall)
        {
            float clampedX = Mathf.Clamp(transform.position.x, -arenaLimit, arenaLimit);
            transform.position = new Vector3(clampedX, transform.position.y, 0);
            StartCoroutine(StunAfterCharge());
        }
        else if (passedPlayer)
        {
            StartCoroutine(DriftStopSequence());
        }
    }

    IEnumerator DriftStopSequence()
    {
        currentState = K9State.Drifting;
        LevelAudioManager.Instance.StopLoop();
        anim.Play("Dog_Stop");

        float currentSpeed = chargeSpeed;
        while (currentSpeed > 0.5f)
        {
            currentSpeed = Mathf.Lerp(currentSpeed, 0, Time.deltaTime * 3f);
            float speed = currentSpeed * ddaSpeedMultiplier;
            transform.Translate(Vector2.right * speed * direction * Time.deltaTime);

            if (Mathf.Abs(transform.position.x) > arenaLimit) break;
            yield return null;
        }

        yield return new WaitForSeconds(0.4f); 
        direction *= -1;
        Flip();

        currentState = K9State.Patrol;
        anim.Play("Dog_Walk");
        currentCooldownTimer = chargeCooldown;
    }

    IEnumerator StunAfterCharge()
    {
        currentState = K9State.Barking;
        anim.Play("Dog_Idle");

        yield return new WaitForSeconds(dynamicStunDuration);
        direction *= -1;
        Flip();
        currentState = K9State.Patrol;
        anim.Play("Dog_Walk");
        currentCooldownTimer = chargeCooldown + 1f;
    }

    void Flip()
    {
        transform.localScale = new Vector3(direction * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    public void DeactivateK9()
    {
        StopAllCoroutines();
        currentState = K9State.Outro;
        LevelAudioManager.Instance.StopLoop();

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.bodyType = RigidbodyType2D.Static; }

        anim.Play("Dog_Outro");

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        this.enabled = false;
    }
}