using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BossController : MonoBehaviour
{
    public enum BossPhase { Stage1_Projectiles, Stage2_Dash, Stage3_Chaos, Defeated }
    public BossPhase currentPhase = BossPhase.Stage1_Projectiles;

    private SpriteRenderer bossSR;
    private Animator anim;

    private bool isTransitioning = false; 
    private bool isPreparingToTransition = false; 
    private Coroutine flashCoroutine;

    private StageManager stageManager;

    [Header("DDA Integration")]
    public float baseAttackSpeed = 2.0f; 
    public float currentDDA = 1.0f; 
    private float lockedDDAForCurrentAttack = 1.0f; 

    [Header("Stats")]
    public float maxHealth = 100f;
    public float currentHealth;
    public float teleportThreshold = 0.9f;

    [Header("RL Controlled Variables")]
    public float attackSpeed = 2.0f;
    public float moveSpeed = 5.0f; 

    [Header("Stage 1 - Breathalyzer")]
    public GameObject bubblePrefab;
    public float bubbleUp = 0.7f;
    public float bubbleDown = -0.7f;
    private bool phase2Triggered = false; 
    private bool isStage1Attacking = false;
    private float timer;

    [Header("Teleport Settings")]
    public GameObject markerPrefab;
    public Transform playerTransform; 
    public float teleportCooldown = 5f;
    private float teleportTimer;
    private bool isTeleporting = false;
    private LineRenderer rope;

    [Header("Stage 2 - Spike Strip")]
    public GameObject spikesPrefab;
    public float arenaWidth = 11.5f;
    public float groundY = -5.3f; 
    public float flyHeight = 3.0f;
    public float perspectiveLimitY = 6.0f;
    public float dashCooldown = 2f;
    private float currentDashTimer;
    private bool isDashing = false;
    public GameObject shockwavePrefab;
    private int stage2AttackStep = 0;
    private bool isStage2Attacking = false;

    [Range(0f, 1f)] public float spikeSpawnChance = 0.6f;
    public float spikeWidth = 1.2f;
    public int edgePaddingSlots = 2;
    public int minGapSlots = 1;

    [Header("Stage 3 Settings")]
    public GameObject k9Prefab;
    public GameObject ticketBombPrefab;
    private bool k9Deployed = false;
    public int maxBombsPerPass = 3;
    private float bombTimer;
    private float movementTargetX;
    private int totalStage3Bombs = 0;
    private int bombsSinceLastPurple = 0;
    private K9Unit spawnedK9;
    private bool tutorialStarted = false;

    void Start()
    {
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;

        currentHealth = maxHealth;
        timer = attackSpeed;
        transform.position = new Vector3(7.5f, transform.position.y, 0);
        bossSR = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        rope = GetComponent<LineRenderer>();
        rope.enabled = false;

        stageManager = Object.FindFirstObjectByType<StageManager>();
        isTransitioning = true;
    }

    public void StartWhistleSequence()
    {
        StartCoroutine(HandleIntroSequence());
    }

    IEnumerator HandleIntroSequence()
    {
        isTransitioning = true;
        LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.mullerWrite, 0.5f);
        yield return new WaitForSeconds(4.5f);
        isTransitioning = false;
    }

    public void PlayWhistleSound()
    {
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerWhistle);
    }

    void Update()
    {
        UpdatePhases();
        if (currentPhase != BossPhase.Defeated)
        {
            ApplyPerspectiveScale();
        }

        if (isTransitioning) return;

        switch (currentPhase)
        {
            case BossPhase.Stage1_Projectiles:
                Stage1_Behavior();
                break;
            case BossPhase.Stage2_Dash:
                Stage2_Behavior();
                break;
            case BossPhase.Stage3_Chaos:
                Stage3_Behavior();
                break;
        }
    }

    void UpdatePhases()
    {
        if (isTransitioning) return;

        float healthPercent = currentHealth / maxHealth;
        BossPhase targetPhase = currentPhase;

        if (healthPercent >= 0.66f) targetPhase = BossPhase.Stage1_Projectiles;
        else if (healthPercent >= 0.33f) targetPhase = BossPhase.Stage2_Dash;
        else if (healthPercent > 0f) targetPhase = BossPhase.Stage3_Chaos;
        else targetPhase = BossPhase.Defeated;

        if (targetPhase != currentPhase)
        {
            if (isStage1Attacking || isStage2Attacking || isDashing || isTeleporting)
            {
                isPreparingToTransition = true;
                return;
            }

            isTransitioning = true;
            isPreparingToTransition = false;

            if (currentPhase == BossPhase.Stage1_Projectiles && targetPhase == BossPhase.Stage2_Dash)
            {
                StartCoroutine(PhaseTransitionToStage2());
            }
            else if (currentPhase == BossPhase.Stage2_Dash && targetPhase == BossPhase.Stage3_Chaos)
            {
                StartCoroutine(PhaseTransitionSweepAndStun());
            }
            else
            {
                currentPhase = targetPhase;
                isTransitioning = false;
            }
        }
    }

    float GetDifficultyFactor()
    {
        float phaseMinHP = 0f;
        float phaseMaxHP = 1f;

        if (currentPhase == BossPhase.Stage1_Projectiles) { phaseMinHP = 0.66f; phaseMaxHP = 1.0f; }
        else if (currentPhase == BossPhase.Stage2_Dash) { phaseMinHP = 0.33f; phaseMaxHP = 0.66f; }
        else if (currentPhase == BossPhase.Stage3_Chaos) { phaseMinHP = 0.0f; phaseMaxHP = 0.33f; }

        float healthPercent = currentHealth / maxHealth;
        float factor = 1f - Mathf.InverseLerp(phaseMinHP, phaseMaxHP, healthPercent);
        float finalFactor = (factor + currentDDA) / 2f;
        return Mathf.Clamp01(finalFactor);
    }

    public void ApplyDDAMultiplier(float multiplier)
    {
        currentDDA = Mathf.Clamp(multiplier, 0.5f, 1.5f);
        float calculatedAttackSpeed = baseAttackSpeed / currentDDA;
        attackSpeed = calculatedAttackSpeed;
    }

    void ApplyPerspectiveScale()
    {
        float minScale = 1.35f;
        float maxScale = 1.75f;
        float heightFactor = Mathf.InverseLerp(groundY, perspectiveLimitY, transform.position.y);
        float targetScale = Mathf.Lerp(maxScale, minScale, heightFactor);
        float flipDir = transform.localScale.x > 0 ? 1 : -1;

        transform.localScale = new Vector3(targetScale * flipDir, targetScale, 1f);
    }

    void Stage1_Behavior()
    {
        HandleLookingAtPlayer();

        if (!isStage1Attacking && !isTeleporting && !isPreparingToTransition)
        {
            StartCoroutine(Stage1AttackSequence());
        }
    }

    // 

    IEnumerator Stage1AttackSequence()
    {
        isStage1Attacking = true;
        yield return new WaitForSeconds(attackSpeed);
        if (isTeleporting) yield break;

        lockedDDAForCurrentAttack = currentDDA;
        anim.SetTrigger("Shoot");

        for (int i = 0; i < 4; i++)
        {
            if (isTeleporting) yield break;

            SpawnBubble();
            float df = GetDifficultyFactor();
            float dynamicFireRate = Mathf.Lerp(1.6f, 0.8f, df);
            float finalFireRate = dynamicFireRate / lockedDDAForCurrentAttack;
            yield return new WaitForSeconds(Mathf.Clamp(finalFireRate, 0.5f, 2.0f));
        }

        yield return new WaitForSeconds(0.5f);

        if (currentHealth / maxHealth <= teleportThreshold)
        {
            if (!phase2Triggered)
            {
                anim.SetTrigger("Grapple");
                phase2Triggered = true;
            }
            StartTeleportProcess();
        }

        isStage1Attacking = false;
    }

    void SpawnBubble()
    {
        anim.Play("Muller_BlowBubble", 0, 0f);
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerBubbleSpawn, 0.6f);
        float spawnY = (Random.value > 0.5f) ? bubbleUp : bubbleDown;

        Vector3 spawnPos = new Vector3(transform.position.x - (transform.localScale.x > 0 ? 1f : -1f), spawnY, 0);
        GameObject bubble = Instantiate(bubblePrefab, spawnPos, Quaternion.identity);

        float df = GetDifficultyFactor();
        float dynamicSpeed = Mathf.Lerp(4.5f, 8.5f, df);

        StopSignBubble bubbleScript = bubble.GetComponent<StopSignBubble>();
        if (bubbleScript != null)
        {
            float finalSpeed = dynamicSpeed * lockedDDAForCurrentAttack;
            bubbleScript.moveSpeed = Mathf.Max(3.5f, finalSpeed);
            bubbleScript.moveDirection = (transform.localScale.x > 0) ? 1f : -1f;
        }
    }

    void HandleLookingAtPlayer()
    {
        if (isTeleporting || isDashing) return;

        if (playerTransform != null)
        {
            float direction = (playerTransform.position.x < transform.position.x) ? 1 : -1;
            float targetScale = Mathf.Abs(transform.localScale.x);
            transform.localScale = new Vector3(direction * targetScale, transform.localScale.y, transform.localScale.z);
        }
    }

    void StartTeleportProcess()
    {
        isTeleporting = true;
        List<float> possibleSpots = new List<float> { -arenaWidth, 0f, arenaWidth };

        float currentX = transform.position.x;
        float closestSpot = possibleSpots[0];
        foreach (float s in possibleSpots)
        {
            if (Mathf.Abs(currentX - s) < 1f) closestSpot = s;
        }
        possibleSpots.Remove(closestSpot);

        float targetX = possibleSpots[Random.Range(0, possibleSpots.Count)];
        Vector3 targetPos = new Vector3(targetX, groundY, 0);

        GameObject marker = Instantiate(markerPrefab, targetPos, Quaternion.identity);
        marker.GetComponent<TeleportMarker>().Setup(this);
    }

    public void PlayHarpoonShotSound() => LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerHarpoon, 0.8f);
    public void PlayHarpoonFlightSound() => LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerHarpoonFlight, 0.7f);
    public void PlayHarpoonFallSound() => LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerHarpoonFall, 0.5f);
    public void PlayAngryDuckSound() => LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerDuckAngry, 0.95f);

    public void ExecuteTeleport(Vector3 newPos)
    {
        StopAllCoroutines();
        isStage1Attacking = false;

        anim.ResetTrigger("Shoot");
        anim.ResetTrigger("Grapple");
        anim.Play("Muller_Bloated", 0, 0f);

        StartCoroutine(GrapplingMovement(newPos));
    }

    IEnumerator GrapplingMovement(Vector3 destination)
    {
        isTeleporting = true;

        float flyDirection = (destination.x < transform.position.x) ? 1 : -1;
        float currentScaleSize = Mathf.Abs(transform.localScale.x);
        transform.localScale = new Vector3(flyDirection * currentScaleSize, transform.localScale.y, transform.localScale.z);

        anim.Play("Muller_GrapplingShot", 0, 0f);
        rope.enabled = true;
        rope.positionCount = 2;
        Vector3 ceilingDest = new Vector3(destination.x, 5.5f, 0);

        rope.SetPosition(0, transform.position + new Vector3(0, 3f, 0));
        rope.SetPosition(1, ceilingDest);

        yield return new WaitForSeconds(0.6f);

        anim.Play("Muller_GrapplingJump", 0, 0f);

        while (Vector3.Distance(transform.position, ceilingDest) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, ceilingDest, 15f * Time.deltaTime);
            rope.SetPosition(0, transform.position + new Vector3(0, 3f, 0));
            rope.SetPosition(1, ceilingDest);
            yield return null;
        }

        anim.Play("Muller_GrapplingFall", 0, 0f);
        rope.enabled = false;

        while (Vector3.Distance(transform.position, destination) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, 25f * Time.deltaTime);
            yield return null;
        }

        transform.position = destination;
        anim.SetTrigger("Land");
        yield return new WaitForSeconds(0.1f);
        isTeleporting = false;
    }

    IEnumerator PhaseTransitionToStage2()
    {
        isTransitioning = true;
        isTeleporting = true;
        isStage1Attacking = false;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        HandleLookingAtPlayer();
        anim.Play("Muller_Bloated");
        yield return new WaitForSeconds(1.5f);

        anim.Play("Muller_GrapplingShot", 0, 0f);

        float targetX = (transform.position.x > 0) ? -8f : 8f;
        Vector3 ceilingDest = new Vector3(targetX, 5.5f, 0);
        Vector3 floorDest = new Vector3(targetX, groundY, 0);

        float flyDirection = (ceilingDest.x < transform.position.x) ? 1 : -1;
        float currentScaleSize = Mathf.Abs(transform.localScale.x);
        transform.localScale = new Vector3(flyDirection * currentScaleSize, transform.localScale.y, transform.localScale.z);

        rope.enabled = true;
        rope.positionCount = 2;
        rope.SetPosition(0, transform.position + new Vector3(0, 3f, 0));
        rope.SetPosition(1, ceilingDest);

        yield return new WaitForSeconds(0.6f);

        anim.Play("Muller_TransitionJump", 0, 0f);

        float setupTimer = 0;
        while (Vector3.Distance(transform.position, ceilingDest) > 0.5f && setupTimer < 3f)
        {
            setupTimer += Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, ceilingDest, 18f * Time.deltaTime);
            rope.SetPosition(0, transform.position + new Vector3(0, 3f, 0));
            rope.SetPosition(1, ceilingDest);
            yield return null;
        }

        transform.position = ceilingDest;
        rope.enabled = false;
        anim.Play("Muller_TransitionFall", 0, 0f);
        yield return new WaitForSeconds(0.3f);

        setupTimer = 0;
        while (Vector3.Distance(transform.position, floorDest) > 0.1f && setupTimer < 3f)
        {
            setupTimer += Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, floorDest, 15f * Time.deltaTime);
            yield return null;
        }

        transform.position = floorDest;
        anim.Play("Muller_TransitionFinal", 0, 0f);

        yield return new WaitForSeconds(2.0f);
        float directionToPlayer = (playerTransform.position.x < transform.position.x) ? 1 : -1;
        float scaleSize = Mathf.Abs(transform.localScale.x);
        transform.localScale = new Vector3(directionToPlayer * scaleSize, transform.localScale.y, transform.localScale.z);

        yield return new WaitForSeconds(1.5f);

        anim.Play("Muller_JetpackIgnite", 0, 0f);
        float timerIgnite = 0;
        while (timerIgnite < 1.5f)
        {
            float latestDir = (playerTransform.position.x < transform.position.x) ? 1 : -1;
            transform.localScale = new Vector3(latestDir * scaleSize, transform.localScale.y, transform.localScale.z);
            timerIgnite += Time.deltaTime;
            yield return null;
        }

        if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;

        currentPhase = BossPhase.Stage2_Dash;
        isTeleporting = false;
        isTransitioning = false;

        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        currentDashTimer = 2.5f;
        stage2AttackStep = 0;
    }

    public void PlayBonkSound() => LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerTransition1Bonk, 0.8f);

    void Stage2_Behavior()
    {
        if (isDashing) return;

        currentDashTimer -= Time.deltaTime;

        if (currentDashTimer <= 0 && !isPreparingToTransition)
        {
            if (stage2AttackStep == 0) StartCoroutine(JetpackSpikeRun());
            else if (stage2AttackStep == 1 || stage2AttackStep == 2) StartCoroutine(TargetedStompAttack());
            else if (stage2AttackStep == 3) StartCoroutine(GroundSweepDash());
            else if (stage2AttackStep == 4 || stage2AttackStep == 5) StartCoroutine(TargetedStompAttack());

            stage2AttackStep++;
            if (stage2AttackStep > 5)
            {
                stage2AttackStep = 0;
                currentDashTimer = 5.0f;
                anim.Play("Muller_MadDown", 0, 0f);
            }
        }
    }

    private List<Vector3> CalculateSpikePositions()
    {
        List<Vector3> positions = new List<Vector3>();
        float safeSpikeWidth = Mathf.Max(0.8f, spikeWidth);
        int padding = Mathf.Max(0, edgePaddingSlots);
        int gap = Mathf.Max(1, minGapSlots);
        float safeChance = Mathf.Clamp01(spikeSpawnChance);

        float randomOffset = Random.Range(-safeSpikeWidth * 0.3f, safeSpikeWidth * 0.3f);
        float totalWidth = arenaWidth * 2f;
        int totalSlots = Mathf.FloorToInt(totalWidth / safeSpikeWidth);

        if (totalSlots <= padding * 2) return positions;

        float startX = -arenaWidth + (safeSpikeWidth / 2f) + randomOffset;
        int currentSlot = padding;
        int skipSlotsRemaining = 0;

        while (currentSlot < totalSlots - padding)
        {
            if (skipSlotsRemaining > 0)
            {
                skipSlotsRemaining--;
            }
            else
            {
                if (Random.value <= safeChance)
                {
                    float spawnX = startX + (currentSlot * safeSpikeWidth);
                    if (spawnX > -arenaWidth + 0.5f && spawnX < arenaWidth - 0.5f)
                    {
                        positions.Add(new Vector3(spawnX, flyHeight, 0));
                        skipSlotsRemaining = gap;
                    }
                }
            }
            currentSlot++;
        }
        return positions;
    }

    IEnumerator JetpackSpikeRun()
    {
        isDashing = true;
        lockedDDAForCurrentAttack = currentDDA;

        anim.Play("Muller_JetpackIgnite", 0, 0f);
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerJetpackIgnite, 0.8f);

        float speed = 15f * Mathf.Clamp(lockedDDAForCurrentAttack, 0.7f, 1.3f);

        float startX = (transform.position.x > 0) ? arenaWidth : -arenaWidth;
        float targetX = -startX;
        Vector3 startPosHover = new Vector3(startX, flyHeight, 0);

        float dirStart = (startX < transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(dirStart * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        float setupTimer = 0f;
        while (Vector3.Distance(transform.position, startPosHover) > 0.2f && setupTimer < 2.0f)
        {
            setupTimer += Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, startPosHover, 25f * Time.deltaTime);
            yield return null;
        }
        transform.position = startPosHover;

        anim.Play("Muller_FlyingLoop", 0, 0f);
        LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.mullerJetpackFlight, 0.4f);

        List<Vector3> spikePositions = CalculateSpikePositions();
        if (targetX < startX) spikePositions.Reverse();

        float dirDash = (targetX < transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(dirDash * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        if (spikePositions != null && spikePositions.Count > 0)
        {
            foreach (Vector3 pos in spikePositions)
            {
                float spikeTimer = 0f;
                bool reachedX = false;

                while (!reachedX && spikeTimer < 2.0f)
                {
                    spikeTimer += Time.deltaTime;
                    transform.position = Vector3.MoveTowards(transform.position, new Vector3(targetX, flyHeight, 0), speed * Time.deltaTime);

                    if (targetX > startX)
                    {
                        if (transform.position.x >= pos.x) reachedX = true;
                    }
                    else 
                    {
                        if (transform.position.x <= pos.x) reachedX = true;
                    }
                    yield return null;
                }

                anim.Play("Muller_DropSpike", 0, 0f);
                LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerThrowSpike, 0.7f);
                SpawnFallingSpike(new Vector3(pos.x, flyHeight, 0));
            }
        }

        float finalRunTimer = 0f;
        while (Mathf.Abs(transform.position.x - targetX) > 0.2f && finalRunTimer < 3.0f)
        {
            finalRunTimer += Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(targetX, flyHeight, 0), speed * Time.deltaTime);
            yield return null;
        }

        transform.position = new Vector3(targetX, flyHeight, 0);
        LevelAudioManager.Instance.StopLoop();
        isDashing = false;
        currentDashTimer = 1.5f;
    }

    IEnumerator TargetedStompAttack()
    {
        isDashing = true;

        float[] stompSpots = { -arenaWidth * 0.6f, 0f, arenaWidth * 0.6f };
        float targetX = stompSpots[Random.Range(0, stompSpots.Length)];
        Vector3 hoverPos = new Vector3(targetX, flyHeight, 0);

        float dirHover = (targetX < transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(dirHover * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        while (Vector3.Distance(transform.position, hoverPos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, hoverPos, 13f * Time.deltaTime);
            yield return null;
        }
        anim.Play("Muller_DropPrepare", 0, 0f);
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerJetpackIgnite, 0.7f);

        float df = GetDifficultyFactor();
        float dynamicPrepTime = Mathf.Lerp(1.8f, 0.7f, df);
        float prepTime = dynamicPrepTime / currentDDA;

        while (prepTime > 0)
        {
            transform.position = hoverPos + (Vector3)Random.insideUnitCircle * (0.2f * (1f - prepTime));
            prepTime -= Time.deltaTime;
            yield return null;
        }
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerHarpoonFall, 0.6f);

        anim.Play("Muller_StompImpact", 0, 0f);
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerTransition1Bonk, 1f);
        Vector3 landPos = new Vector3(targetX, groundY, 0);
        float fallSpeed = 28f;
        while (Vector3.Distance(transform.position, landPos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, landPos, fallSpeed * Time.deltaTime);
            yield return null;
        }

        if (shockwavePrefab != null)
        {
            GameObject leftWave = Instantiate(shockwavePrefab, transform.position + new Vector3(-1f, 0, 0), Quaternion.identity);
            leftWave.GetComponent<Shockwave>().direction = -1;

            GameObject rightWave = Instantiate(shockwavePrefab, transform.position + new Vector3(1f, 0, 0), Quaternion.identity);
            rightWave.GetComponent<Shockwave>().direction = 1;
        }

        float groundTime;
        if (stage2AttackStep <= 2)
        {
            groundTime = 0.8f;
        }
        else
        {
            groundTime = Mathf.Lerp(5.0f, 2.5f, df);
            anim.Play("Muller_MadDown", 0, 0f);
        }

        HandleLookingAtPlayer();
        isDashing = false;
        currentDashTimer = groundTime;
    }

    IEnumerator GroundSweepDash()
    {
        isDashing = true;
        float df = GetDifficultyFactor();

        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerJetpackIgnite, 0.9f);

        float startX = (transform.position.x > 0) ? arenaWidth : -arenaWidth;
        float targetX = -startX;

        anim.Play("Muller_JetpackIgnite", 0, 0f);

        Vector3 airEdgePos = new Vector3(startX, flyHeight, 0);
        while (Vector3.Distance(transform.position, airEdgePos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, airEdgePos, 25f * Time.deltaTime);
            yield return null;
        }

        Vector3 groundPos = new Vector3(transform.position.x, groundY, 0);
        while (Vector3.Distance(transform.position, groundPos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, groundPos, 20f * Time.deltaTime);
            yield return null;
        }

        HandleLookingAtPlayer();

        float prepTime = 1f;
        while (prepTime > 0)
        {
            transform.position = groundPos + (Vector3)Random.insideUnitCircle * 0.05f;
            prepTime -= Time.deltaTime;
            yield return null;
        }

        anim.Play("Muller_FlyingLoop", 0, 0f);
        LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.mullerJetpackFlight, 0.6f);
        float dir = (targetX < transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(dir * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        GameObject[] spikesOnGround = GameObject.FindGameObjectsWithTag("Spike");

        while (Mathf.Abs(transform.position.x - targetX) > 0.1f)
        {
            float dynamicSweepSpeed = Mathf.Lerp(22f, 35f, df);
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(targetX, groundY, 0), (dynamicSweepSpeed * currentDDA) * Time.deltaTime);

            if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) < 1.8f)
            {
                playerTransform.GetComponent<Movement>()?.TakeDamage("BOSS");
            }

            foreach (GameObject spike in spikesOnGround)
            {
                if (spike != null && Vector3.Distance(transform.position, spike.transform.position) < 2.5f)
                {
                    Destroy(spike);
                }
            }
            yield return null;
        }

        foreach (GameObject spike in spikesOnGround) { if (spike != null) Destroy(spike); }
        LevelAudioManager.Instance.StopLoop();
        anim.Play("Muller_Bloated", 0, 0f);

        float groundTime = Mathf.Lerp(4.0f, 1.5f, df);

        HandleLookingAtPlayer();
        isDashing = false;
        currentDashTimer = groundTime;
    }

    void SpawnFallingSpike(Vector3 spawnPos)
    {
        GameObject spike = Instantiate(spikesPrefab, spawnPos, Quaternion.identity);
        Rigidbody2D rb = spike.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 4.5f;
            rb.linearVelocity = new Vector2(0, -2f);
        }
    }

    void Stage3_Behavior()
    {
        if (isDashing || k9Deployed || tutorialStarted) return;

        tutorialStarted = true;

        Vector3 spawnPos = new Vector3(arenaWidth + 4f, groundY, 0);
        GameObject k9 = Instantiate(k9Prefab, spawnPos, Quaternion.identity);
        spawnedK9 = k9.GetComponent<K9Unit>();

        StartCoroutine(MillerFloatMode());
    }

    IEnumerator MillerFloatMode()
    {
        float flyHeight = 2.5f;
        moveSpeed = 6.0f;

        HandleLookingAtPlayer();
        anim.SetTrigger("Jetpack");
        LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.mullerJetpackFlight, 0.6f);

        Vector3 startTutorialPos = new Vector3(arenaWidth - 1f, flyHeight, 0);
        while (Vector3.Distance(transform.position, startTutorialPos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, startTutorialPos, 15f * Time.deltaTime);
            yield return null;
        }

        yield return new WaitForSeconds(1.0f);

        Vector3 leftTutorialTarget = new Vector3(-arenaWidth + 1f, flyHeight, 0);
        bool b1 = false, b2 = false, b3 = false;
        GameObject tutorialPinkBomb = null;

        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        while (transform.position.x > leftTutorialTarget.x + 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, leftTutorialTarget, moveSpeed * Time.deltaTime);

            if (!b1 && transform.position.x < arenaWidth * 0.5f)
            {
                b1 = true;
                LaunchTutorialBomb(false);
            }
            if (!b2 && transform.position.x < 0f)
            {
                b2 = true;
                LaunchTutorialBomb(false);
            }
            if (!b3 && transform.position.x <= leftTutorialTarget.x + 0.5f)
            {
                b3 = true;
                tutorialPinkBomb = LaunchTutorialBomb(true);
            }
            yield return null;
        }

        if (tutorialPinkBomb != null)
        {
            yield return StartCoroutine(TutorialBombBounce(tutorialPinkBomb));
        }

        k9Deployed = true;
        int direction = (transform.position.x > 0) ? -1 : 1;
        float df = GetDifficultyFactor();
        spawnedK9.dynamicStunDuration = Mathf.Lerp(1.8f, 0.7f, df);

        yield return StartCoroutine(TutorialBombBounce(tutorialPinkBomb));

        while (currentHealth > 0)
        {
            float targetX = arenaWidth * direction;

            if (spawnedK9 != null)
            {
                spawnedK9.ddaSpeedMultiplier = currentDDA;
                spawnedK9.patrolSpeed = Mathf.Lerp(5f, 7f, df);
                spawnedK9.chargeSpeed = Mathf.Lerp(10f, 14f, df);
                spawnedK9.dynamicStunDuration = Mathf.Lerp(1.8f, 0.6f, df) / currentDDA;
            }

            int bombsThisPass = Random.Range(1, maxBombsPerPass + 1);
            float[] dropZones = new float[bombsThisPass];
            float startDropX = -arenaWidth + 1f;
            float endDropX = arenaWidth - 1f;
            float segmentLength = (endDropX - startDropX) / bombsThisPass;

            for (int i = 0; i < bombsThisPass; i++)
            {
                float segStart = startDropX + (i * segmentLength);
                float segEnd = segStart + segmentLength;
                float padding = segmentLength * 0.2f;
                dropZones[i] = Random.Range(segStart + padding, segEnd - padding);
            }

            if (direction == -1) System.Array.Reverse(dropZones);

            int bombsDropped = 0;
            float scaleDir = (direction == -1) ? 1 : -1;
            transform.localScale = new Vector3(scaleDir * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

            while (Mathf.Abs(transform.position.x - targetX) > 0.1f)
            {
                float newX = Mathf.MoveTowards(transform.position.x, targetX, moveSpeed * Time.deltaTime);
                float newY = flyHeight + Mathf.Sin(Time.time * 4f) * 0.2f;
                transform.position = new Vector3(newX, newY, 0);

                if (bombsDropped < bombsThisPass)
                {
                    bool reachedZone = (direction == 1 && transform.position.x >= dropZones[bombsDropped]) ||
                                       (direction == -1 && transform.position.x <= dropZones[bombsDropped]);

                    if (reachedZone)
                    {
                        anim.SetTrigger("TossTicket");
                        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerThrowSpike, 0.6f);
                        GameObject droppedBomb = Instantiate(ticketBombPrefab, transform.position, Quaternion.identity);
                        bombsDropped++;
                        totalStage3Bombs++;
                        bombsSinceLastPurple++;

                        TicketProjectile bombScript = droppedBomb.GetComponent<TicketProjectile>();
                        if (bombScript != null)
                        {
                            if (bombsSinceLastPurple >= 5)
                            {
                                bombScript.isParryable = true;
                                droppedBomb.tag = "Parryable";
                                bombsSinceLastPurple = 0;
                            }
                            else
                            {
                                bombScript.isParryable = false;
                                droppedBomb.tag = "Untagged";
                            }
                        }
                    }
                }
                yield return null;
            }

            direction *= -1;
            float dynamicPassPause = Mathf.Lerp(2.5f, 1.0f, df) / currentDDA;
            yield return new WaitForSeconds(dynamicPassPause);
        }
        LevelAudioManager.Instance.StopLoop();
        BossDefeated();
    }

    GameObject LaunchTutorialBomb(bool isPink)
    {
        anim.SetTrigger("TossTicket");
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerThrowSpike, 0.6f);
        GameObject bomb = Instantiate(ticketBombPrefab, transform.position, Quaternion.identity);
        totalStage3Bombs++;

        if (isPink)
        {
            TicketProjectile script = bomb.GetComponent<TicketProjectile>();
            script.isParryable = true;
            bomb.tag = "Parryable";
        }
        return bomb;
    }

    IEnumerator TutorialBombBounce(GameObject bomb)
    {
        float targetHeight = groundY + 1.5f;
        while (bomb != null)
        {
            if (bomb.transform.position.y <= targetHeight)
            {
                bomb.GetComponent<TicketProjectile>().Parry();
                break;
            }
            yield return null;
        }

        while (bomb != null) yield return null;
        yield return new WaitForSeconds(0.5f);

        GameObject bouncer = GameObject.FindGameObjectWithTag("TutorialBouncer");
        if (bouncer != null)
        {
            BoxCollider2D col = bouncer.GetComponent<BoxCollider2D>();
            if (col != null) col.enabled = false;
        }

        if (spawnedK9 != null) spawnedK9.WakeUp();
        k9Deployed = true;
    }

    public void PlayMullerLoseSound() => LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerDefeat, 1f);

    void BossDefeated()
    {
        currentPhase = BossPhase.Defeated;
        StopAllCoroutines();

        if (LevelAudioManager.Instance != null) LevelAudioManager.Instance.PlayVictoryMusic();

        bossSR.color = Color.white;
        moveSpeed = 0;

        BoxCollider2D bossCollider = GetComponent<BoxCollider2D>();
        if (bossCollider != null) bossCollider.enabled = false;

        if (spawnedK9 != null) spawnedK9.DeactivateK9();
        else
        {
            GameObject k9 = GameObject.FindGameObjectWithTag("K9");
            if (k9 != null) k9.GetComponent<K9Unit>()?.DeactivateK9();
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        if (stageManager != null) stageManager.OnBossDefeated();
        StartCoroutine(DramaticDeathSequence());
    }

    IEnumerator DramaticDeathSequence()
    {
        HandleLookingAtPlayer();
        anim.Play("Muller_Defeated1", 0, 0f);
        yield return new WaitForSeconds(1.2f);

        float fallSpeed = 0f;
        float gravity = 15f;
        anim.Play("Muller_Defeated2", 0, 0f);

        float stopY = groundY;
        while (transform.position.y > stopY)
        {
            fallSpeed += gravity * Time.deltaTime;
            transform.position -= new Vector3(0, fallSpeed * Time.deltaTime, 0);
            yield return null;
        }

        transform.position = new Vector3(transform.position.x, stopY, transform.position.z);
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Static;

        anim.Play("Muller_Defeated3", 0, 0f);
    }

    public void TakeDamage(float amount, string damageSource = "Banana")
    {
        if (currentPhase == BossPhase.Defeated || isTransitioning || isPreparingToTransition) return;

        if (currentPhase == BossPhase.Stage3_Chaos && damageSource == "Banana") return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        Debug.Log($"Boss Health: {currentHealth}");

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);

        if (damageSource == "TicketBomb" || damageSource == "Bomb")
        {
            flashCoroutine = StartCoroutine(HeavyBombDamageFlash());
        }
        else
        {
            flashCoroutine = StartCoroutine(CupheadDamageFlash());
        }

        if (currentHealth <= 0)
        {
            currentPhase = BossPhase.Defeated;
            BossDefeated();
        }
    }

    IEnumerator CupheadDamageFlash()
    {
        for (int i = 0; i < 3; i++)
        {
            bossSR.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            yield return new WaitForSeconds(0.05f);
            bossSR.color = Color.white;
            yield return new WaitForSeconds(0.05f);
        }
        flashCoroutine = null;
    }

    IEnumerator HeavyBombDamageFlash()
    {
        bossSR.color = new Color(1f, 0.2f, 0.2f, 1f);

        Vector3 originalScale = transform.localScale;
        transform.localScale = originalScale * 1.15f;

        yield return new WaitForSeconds(0.15f); 

        for (int i = 0; i < 4; i++)
        {
            bossSR.color = Color.gray; // Întunecat
            yield return new WaitForSeconds(0.04f);
            bossSR.color = new Color(1f, 0.4f, 0.4f, 1f); // Roșu aprins
            yield return new WaitForSeconds(0.04f);
        }

        transform.localScale = originalScale;
        bossSR.color = Color.white;
        flashCoroutine = null;
    }

    IEnumerator PhaseTransitionSweepAndStun()
    {
        isTransitioning = true;
        isDashing = true;

        HandleLookingAtPlayer();
        anim.Play("Muller_Bloated");
        yield return new WaitForSeconds(1.5f);

        anim.Play("Muller_FlyingLoop", 0, 0f);
        LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.mullerJetpackFlight, 0.6f);

        float startSideX = (transform.position.x > 0) ? arenaWidth : -arenaWidth;
        float targetSideX = -startSideX;
        Vector3 startCorner = new Vector3(startSideX, groundY, 0);

        float dirToCorner = (startCorner.x < transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(dirToCorner * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        while (Vector3.Distance(transform.position, startCorner) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, startCorner, 25f * Time.deltaTime);
            yield return null;
        }

        yield return new WaitForSeconds(0.2f);

        float dashDir = (targetSideX < transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(dashDir * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        while (Mathf.Abs(transform.position.x - targetSideX) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(targetSideX, groundY, 0), 25f * Time.deltaTime);

            GameObject[] spikes = GameObject.FindGameObjectsWithTag("Spike");
            foreach (GameObject spike in spikes)
            {
                if (spike != null && Vector3.Distance(transform.position, spike.transform.position) < 3.5f)
                {
                    Destroy(spike);
                }
            }

            if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) < 1.8f)
                playerTransform.GetComponent<Movement>()?.TakeDamage("BOSS");

            yield return null;
        }
        LevelAudioManager.Instance.StopLoop();

        float awayX = transform.position.x + (transform.position.x > 0 ? -3.5f : 3.5f);
        Vector3 safeSpot = new Vector3(awayX, groundY, 0);
        while (Vector3.Distance(transform.position, safeSpot) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, safeSpot, 8f * Time.deltaTime);
            yield return null;
        }

        float lookDir = (playerTransform.position.x < transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(lookDir * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        yield return new WaitForSeconds(1.0f);

        anim.Play("Muller_JetpackIgnite", 0, 0f);
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerJetpackIgnite, 0.9f);
        yield return new WaitForSeconds(1.2f);

        Vector3 failAirPos = new Vector3(transform.position.x, groundY + 3.0f, 0);
        float liftTimer = 0;
        while (Vector3.Distance(transform.position, failAirPos) > 0.1f && liftTimer < 2.0f)
        {
            liftTimer += Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, failAirPos, 2.5f * Time.deltaTime);
            yield return null;
        }

        anim.Play("Muller_JetpackExplode", 0, 0f);
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerTrainAngry, 1f);
        yield return new WaitForSeconds(0.8f);

        Vector3 crashPos = new Vector3(transform.position.x, groundY, 0);
        while (Vector3.Distance(transform.position, crashPos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, crashPos, 6f * Time.deltaTime);
            yield return null;
        }

        yield return new WaitForSeconds(0.8f);
        anim.Play("Muller_K9Unit", 0, 0f);
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerWhistle, 1f);
        yield return new WaitForSeconds(2.5f);

        anim.Play("Muller_JetpackIgnite");
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerJetpackIgnite, 0.9f);
        yield return new WaitForSeconds(2.0f);

        currentPhase = BossPhase.Stage3_Chaos;
        isDashing = false;
        isTransitioning = false;
    }
}