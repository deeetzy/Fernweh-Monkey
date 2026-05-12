using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

public class BossController : MonoBehaviour
{
    public enum BossPhase { Stage1_Projectiles, Stage2_Dash, Stage3_Chaos, Defeated }
    public BossPhase currentPhase = BossPhase.Stage1_Projectiles;

    private SpriteRenderer bossSR;
    private Animator anim;

    private bool isTransitioning = false; // Blochează Update-ul în timpul animațiilor de tranziție
    private bool isPreparingToTransition = false; // Spune boss-ului să aștepte
    private Coroutine flashCoroutine;

    private StageManager stageManager;

    [Header("Stats")]
    public float maxHealth = 100f;
    public float currentHealth;
    public float teleportThreshold = 0.9f;

    [Header("RL Controlled Variables")]
    public float attackSpeed = 2.0f; // RL can change this!
    public float moveSpeed = 5.0f;   // RL can change this!

    [Header("Stage 1 - Breathalyzer")]
    public GameObject bubblePrefab;
    public float bubbleUp = 0.7f;
    public float bubbleDown = -0.7f;
    private bool phase2Triggered = false; //trigger for teleportation
    private bool isStage1Attacking = false;
    private float timer;

    [Header("Teleport Settings")]
    public GameObject markerPrefab;
    public Transform playerTransform; // Drag the Monkey here
    public float teleportCooldown = 5f;
    private float teleportTimer;
    private bool isTeleporting = false;
    private LineRenderer rope;

    [Header("Stage 2 - Spike Strip")]
    public GameObject spikesPrefab;
    public float arenaWidth = 11.5f;
    public float groundY = -5.3f; // Adjust to your actual floor height
    public float flyHeight = 3.0f;
    public float perspectiveLimitY = 6.0f;
    public float dashCooldown = 2f;
    private float currentDashTimer;
    private bool isDashing = false;
    public GameObject shockwavePrefab;
    private int stage2AttackStep = 0; // 0 = Zbor cu spini, 1 = Stomp, 2 = Alt Stomp
    private bool isStage2Attacking = false;

    [Range(0f, 1f)] public float spikeSpawnChance = 0.6f; // 60% șansă per slot de a genera un spin
    public float spikeWidth = 1.2f; // Cât spațiu ocupă fizic un spin (ajustează dacă modelul e mai mare)
    public int edgePaddingSlots = 2; // Câte sloturi goale lăsăm MEREU la marginile arenei
    public int minGapSlots = 1;      // Câte sloturi goale lăsăm obligatoriu DUPĂ un spin generat

    [Header("Stage 3 Settings")]
    public GameObject k9Prefab;
    public GameObject ticketBombPrefab;
    private bool k9Deployed = false;
    public int maxBombsPerPass = 3;
    private float bombTimer;
    private float movementTargetX;
    private int totalStage3Bombs = 0; // Numără bombele aruncate în Faza 3
    private int bombsSinceLastPurple = 0;
    private K9Unit spawnedK9; // Salvăm scriptul câinelui aici
    private bool tutorialStarted = false;

    void Start()
    {
        currentHealth = maxHealth;
        timer = attackSpeed;
        transform.position = new Vector3(7.5f, transform.position.y, 0);
        bossSR = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>(); // Inițializare animator
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
        // 2. Scrisul (Write) - Putem folosi StartLoop dacă scrie mult timp
        LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.mullerWrite, 0.5f);
        yield return new WaitForSeconds(4.5f); // Timpul animației de Intro (Whistle + Write)
        isTransitioning = false;
    }

    public void PlayWhistleSound()
    {
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerWhistle);
    }

    void Update()
    {
        // 1. Check for Phase Transitions based on Health
        UpdatePhases();
        float healthPercent = currentHealth / maxHealth;
        if (currentPhase != BossPhase.Defeated)
        {
            ApplyPerspectiveScale();
        }

        if (isTransitioning) return;

        // 2. Run the logic for the current phase
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
            // Așteptăm să termine orice acțiune curentă
            if (isStage1Attacking || isStage2Attacking || isDashing || isTeleporting)
            {
                isPreparingToTransition = true; // Müller știe că trebuie să se oprească după ce termină atacul
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

    void ApplyPerspectiveScale()
    {
        // Aceste valori pot fi scoase ca variabile publice dacă vrei să le reglezi din Inspector
        float minScale = 1.35f;
        float maxScale = 1.75f;

        // Calculăm factorul de înălțime
        // groundY e podeaua, flyHeight e punctul maxim de sus
        float heightFactor = Mathf.InverseLerp(groundY, perspectiveLimitY, transform.position.y);

        // Calculăm mărimea țintă
        float targetScale = Mathf.Lerp(maxScale, minScale, heightFactor);

        // Păstrăm direcția în care privește boss-ul
        float flipDir = transform.localScale.x > 0 ? 1 : -1;

        // Aplicăm scara
        transform.localScale = new Vector3(targetScale * flipDir, targetScale, 1f);
    }

    // --- PHASE BEHAVIORS ---
    // --- PHASE 1 ---
    void Stage1_Behavior()
    {
        HandleLookingAtPlayer();

        // Adăugăm "&& !isPreparingToTransition" aici:
        if (!isStage1Attacking && !isTeleporting && !isPreparingToTransition)
        {
            StartCoroutine(Stage1AttackSequence());
        }
    }

    IEnumerator Stage1AttackSequence()
    {
        isStage1Attacking = true;

        // 1. PAUZĂ (Cooldown între acțiuni - controlat de RL prin attackSpeed)
        yield return new WaitForSeconds(attackSpeed);
        if (isTeleporting) yield break;

        // 2. AVERTIZARE: Scoate arma!
        anim.SetTrigger("Shoot");
        Color baseColor = bossSR.color;

        // 3. TRAGE 4 BULE (Burst Fire)
        for (int i = 0; i < 4; i++)
        {
            if (isTeleporting) yield break;

            SpawnBubble();
            yield return new WaitForSeconds(0.85f); // Timpul dintre gloanțe (fire rate)
        }

        // O mică pauză după ce a tras ca să nu sară instantaneu cu grappling hook-ul
        yield return new WaitForSeconds(0.5f);

        // 4. GRAPPLING HOOK (Fostul Teleport)
        // Verificăm dacă i-am dat destul damage ca să înceapă să se miște
        if (currentHealth / maxHealth <= teleportThreshold)
        {
            if (!phase2Triggered)
            {
                anim.SetTrigger("Grapple");
                Debug.Log("BOSS: Health below 90%! Initiating Grappling Hook Phase.");
                phase2Triggered = true;
            }

            StartTeleportProcess(); // Asta va seta isTeleporting = true

            // Corutina se termină aici. 
            // isStage1Attacking devine false, dar Faza 1 nu va reîncepe până când 
            // marker-ul nu apelează ExecuteTeleport() și face isTeleporting = false!
        }

        isStage1Attacking = false;
    }

    void SpawnBubble()
    {
        anim.Play("Muller_BlowBubble", 0, 0f);
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerBubbleSpawn, 0.6f);
        float spawnY;
        if (Random.value > 0.5f)
        {
            spawnY = bubbleUp;
        }
        else
        {
            spawnY = bubbleDown;
        }
        Vector3 spawnPos = new Vector3(transform.position.x - 1f, spawnY, 0);
        GameObject bubble = Instantiate(bubblePrefab, spawnPos, Quaternion.identity);

        float dir = (transform.localScale.x > 0) ? 1f : -1f;
        bubble.GetComponent<StopSignBubble>().moveDirection = dir;

        Debug.Log("BOSS: Blowing Stop-Sign Bubble!");
    }

    void HandleLookingAtPlayer()
    {
        if (isTeleporting || isDashing) return; // Don't flip while mid-teleport

        if (playerTransform != null)
        {
            // Check if player is to the left or right of the boss
            float direction = (playerTransform.position.x < transform.position.x) ? 1 : -1;

            // Apply scale (assuming your boss's default 'Forward' is Left)
            // If your boss faces Right by default, swap the 'direction' logic
            float targetScale = Mathf.Abs(transform.localScale.x);
            transform.localScale = new Vector3(direction * targetScale, transform.localScale.y, transform.localScale.z);
        }
    }

    void StartTeleportProcess()
    {
        isTeleporting = true;

        List<float> possibleSpots = new List<float> { -arenaWidth, 0f, arenaWidth };

        // Găsim punctul cel mai apropiat de unde stă Müller acum și îl scoatem din listă
        float currentX = transform.position.x;
        float closestSpot = possibleSpots[0];
        foreach (float s in possibleSpots)
        {
            if (Mathf.Abs(currentX - s) < 1f) closestSpot = s;
        }
        possibleSpots.Remove(closestSpot);

        // Alegem dintre punctele rămase (Müller se va muta garantat)
        float targetX = possibleSpots[Random.Range(0, possibleSpots.Count)];
        Vector3 targetPos = new Vector3(targetX, groundY, 0); // Folosește groundY fix

        GameObject marker = Instantiate(markerPrefab, targetPos, Quaternion.identity);
        marker.GetComponent<TeleportMarker>().Setup(this);
    }

    public void PlayHarpoonShotSound()
    {
        // Sunet de impact pe canalul de Boss
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerHarpoon, 0.8f);
    }

    public void PlayHarpoonFlightSound()
    {
        // Sunet de impact pe canalul de Boss
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerHarpoonFlight, 0.7f);
    }

    public void PlayHarpoonFallSound()
    {
        // Sunet de impact pe canalul de Boss
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerHarpoonFall, 0.5f);
    }

    public void PlayAngryDuckSound()
    {
        // Sunet de impact pe canalul de Boss
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerDuckAngry, 0.95f);
    }

    public void ExecuteTeleport(Vector3 newPos)
    {
        // 1. Oprim forțat corutina de atac cu bule ca să nu mai trimită triggere
        StopAllCoroutines();
        isStage1Attacking = false;

        // 2. CURĂȚĂM Animatorul de orice trigger "Shoot" care a rămas blocat în coadă
        anim.ResetTrigger("Shoot");
        anim.ResetTrigger("Grapple"); // Îl resetăm și pe ăsta ca să fim siguri

        // 3. Îl forțăm să intre în Idle (Bloated) pentru un cadru, ca să ucidem orice altă animație
        anim.Play("Muller_Bloated", 0, 0f);

        // 4. Pornim mișcarea nouă
        StartCoroutine(GrapplingMovement(newPos));
    }

    IEnumerator GrapplingMovement(Vector3 destination)
    {
        isTeleporting = true;

        // --- LOGICA DE PRIVIRE ---
        float flyDirection = (destination.x < transform.position.x) ? 1 : -1;
        float currentScaleSize = Mathf.Abs(transform.localScale.x);
        transform.localScale = new Vector3(flyDirection * currentScaleSize, transform.localScale.y, transform.localScale.z);

        // 1. SHOT (La sol)
        // Folosim Play în loc de SetTrigger pentru a forța animația să apară MEREU
        anim.Play("Muller_GrapplingShot", 0, 0f);
        // ACTIVĂM SFOARA
        rope.enabled = true;
        rope.positionCount = 2;
        Vector3 ceilingPos = new Vector3(transform.position.x, 5.5f, 0);
        Vector3 ceilingDest = new Vector3(destination.x, ceilingPos.y, 0);

        rope.SetPosition(0, transform.position + new Vector3(0, 3f, 0));
        rope.SetPosition(1, ceilingDest);

        // Așteptăm să vedem brațul/arma (ajustează timpul dacă e prea lung/scurt)
        yield return new WaitForSeconds(0.6f);

        // 2. JUMP (Decolarea)
        anim.Play("Muller_GrapplingJump", 0, 0f);

        // 3. TRANVERSAREA
        while (Vector3.Distance(transform.position, ceilingDest) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, ceilingDest, 15f * Time.deltaTime);

            // Sfoara rămâne prinsă de punctul fix de pe tavan în timp ce Müller alunecă
            rope.SetPosition(0, transform.position + new Vector3(0, 3f, 0));
            rope.SetPosition(1, ceilingDest);

            yield return null;
        }

        // 4. FALL (Căderea)
        anim.Play("Muller_GrapplingFall", 0, 0f);
        rope.enabled = false;

        while (Vector3.Distance(transform.position, destination) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, 25f * Time.deltaTime);
            yield return null;
        }

        // 5. IMPACT
        transform.position = destination;
        anim.SetTrigger("Land");
        yield return new WaitForSeconds(0.1f);
        isTeleporting = false;
    }

    IEnumerator PhaseTransitionToStage2()
    {
        Debug.Log("!!! TRANZITIE PORNIȚA !!!");
        isTransitioning = true;

        // Dezactivăm orice altceva ar putea mișca boss-ul
        isTeleporting = true;
        isStage1Attacking = false;

        // IMPORTANT: Facem boss-ul imun la fizică pe timpul zborului
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        HandleLookingAtPlayer();
        anim.Play("Muller_Bloated");
        yield return new WaitForSeconds(1.5f);

        // 1. SHOT
        anim.Play("Muller_GrapplingShot", 0, 0f);

        float targetX = (transform.position.x > 0) ? -8f : 8f;
        Vector3 ceilingDest = new Vector3(targetX, 5.5f, 0);
        Vector3 floorDest = new Vector3(targetX, groundY, 0);

        float flyDirection = (ceilingDest.x < transform.position.x) ? 1 : -1;
        float currentScaleSize = Mathf.Abs(transform.localScale.x);
        transform.localScale = new Vector3(flyDirection * currentScaleSize, transform.localScale.y, transform.localScale.z);

        rope.enabled = true;
        rope.positionCount = 2;
        Vector3 handPos = transform.position + new Vector3(0, 3f, 0);
        rope.SetPosition(0, handPos);
        rope.SetPosition(1, ceilingDest);

        yield return new WaitForSeconds(0.6f);

        // 2. JUMP (Zborul pe diagonală)
        anim.Play("Muller_TransitionJump", 0, 0f);
        Debug.Log("Muller ar trebui să zboare acum spre: " + ceilingDest);

        // Folosim un timer de siguranță ca să nu înghețe jocul niciodată
        float timer = 0;
        while (Vector3.Distance(transform.position, ceilingDest) > 0.5f && timer < 3f)
        {
            timer += Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, ceilingDest, 18f * Time.deltaTime);
            rope.SetPosition(0, transform.position + new Vector3(0, 3f, 0));
            rope.SetPosition(1, ceilingDest);
            yield return null;
        }

        // 3. FALL
        Debug.Log("Muller a ajuns la perete, acum cade.");
        transform.position = ceilingDest;
        rope.enabled = false;
        anim.Play("Muller_TransitionFall", 0, 0f);
        yield return new WaitForSeconds(0.3f);

        timer = 0;
        while (Vector3.Distance(transform.position, floorDest) > 0.1f && timer < 3f)
        {
            timer += Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, floorDest, 15f * Time.deltaTime);
            yield return null;
        }

        // 4. FINAL
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

        // --- REPORNIRE LOGICĂ JOC ---
        if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic; // Resetăm fizica la loc

        currentPhase = BossPhase.Stage2_Dash; // ABIA ACUM SCHIMBĂM FAZA
        isTeleporting = false;
        isTransitioning = false;

        Debug.Log("!!! TRANZITIE TERMINATĂ !!!");
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
    }

    // Apelată automat de evenimentul din animație
    public void PlayBonkSound()
    {
        // Sunet de impact pe canalul de Boss
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerTransition1Bonk, 0.8f);
    }

    // --- PHASE 2 ---
    void Stage2_Behavior()
    {
        if (isDashing) return; // Dacă deja dă dash, nu facem altceva

        currentDashTimer -= Time.deltaTime;

        if (currentDashTimer <= 0 && !isPreparingToTransition)
        {
            if (stage2AttackStep == 0)
            {
                StartCoroutine(JetpackSpikeRun());
            }
            else if (stage2AttackStep == 1 || stage2AttackStep == 2)
            {
                StartCoroutine(TargetedStompAttack());
            }
            else if (stage2AttackStep == 3)
            {
                StartCoroutine(GroundSweepDash());
            }
            else if (stage2AttackStep == 4 || stage2AttackStep == 5)
            {
                StartCoroutine(TargetedStompAttack());
            }

            stage2AttackStep++;
            if (stage2AttackStep > 5) stage2AttackStep = 0; // Resetăm combo-ul
        }
    }

    private System.Collections.Generic.List<Vector3> CalculateSpikePositions()
    {
        System.Collections.Generic.List<Vector3> positions = new System.Collections.Generic.List<Vector3>();

        float safeSpikeWidth = Mathf.Max(0.8f, spikeWidth);
        int padding = Mathf.Max(0, edgePaddingSlots); // Am scos limita minimă de 1. Acum poți pune 0 din Inspector!
        int gap = Mathf.Max(1, minGapSlots);
        float safeChance = Mathf.Clamp01(spikeSpawnChance);

        // Adăugăm o ușoară variație la pornire ca grila să nu fie mereu în același loc
        float randomOffset = Random.Range(-safeSpikeWidth * 0.3f, safeSpikeWidth * 0.3f);

        float totalWidth = arenaWidth * 2f;
        int totalSlots = Mathf.FloorToInt(totalWidth / safeSpikeWidth);

        if (totalSlots <= padding * 2) return positions;

        // StartX include acum acel Offset randomizat
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

                    // Siguranță finală: nu-l lăsăm să iasă în afara arenei vizuale
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

    // ATACUL 1: Zbor din margine în margine
    IEnumerator JetpackSpikeRun()
    {
        isDashing = true;

        anim.Play("Muller_JetpackIgnite", 0, 0f);
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerJetpackIgnite, 0.8f);
        float speed = 15f; // Viteza redusă pentru a vedea clar ploaia de spini

        // 1. Zboară la cea mai apropiată margine
        float startX = (transform.position.x > 0) ? arenaWidth : -arenaWidth;
        float targetX = -startX;
        Vector3 startPosHover = new Vector3(startX, flyHeight, 0);

        float dirStart = (startX < transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(dirStart * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        while (Vector3.Distance(transform.position, startPosHover) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, startPosHover, 25f * Time.deltaTime); // Aici se grăbește spre colț
            yield return null;
        }
        anim.Play("Muller_FlyingLoop", 0, 0f);
        LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.mullerJetpackFlight, 0.4f);

        // 2. GENERĂM HARTA SPINILOR
        System.Collections.Generic.List<Vector3> spikePositions = CalculateSpikePositions();
        if (targetX < startX) spikePositions.Reverse();

        // 3. CURSA CU SPINI (Mergem fix din X în X)
        float dirDash = (targetX < transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(dirDash * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        foreach (Vector3 pos in spikePositions)
        {
            // Zboară strict până ajunge EXACT la locația următorului spin de pe listă
            while (Mathf.Abs(transform.position.x - pos.x) > 0.1f)
            {
                transform.position = Vector3.MoveTowards(transform.position, new Vector3(targetX, flyHeight, 0), speed * Time.deltaTime);
                yield return null;
            }
            anim.Play("Muller_DropSpike", 0, 0f);
            LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerThrowSpike, 0.7f);

            // A ajuns la țintă! Dă drumul spinului. Sincronizare perfectă.
            SpawnFallingSpike(new Vector3(pos.x, flyHeight, 0));
        }
        anim.Play("Muller_FlyingLoop", 0, 0f);
        LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.mullerJetpackFlight, 0.4f);

        // După ce a aruncat toți spinii, continuă zborul până la capătul arenei
        while (Mathf.Abs(transform.position.x - targetX) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(targetX, flyHeight, 0), speed * Time.deltaTime);
            yield return null;
        }
        LevelAudioManager.Instance.StopLoop();
        transform.position = new Vector3(targetX, flyHeight, 0);

        isDashing = false;
        currentDashTimer = 1.5f; // Pauză ca să aterizeze spinii
    }

    // ATACUL 2: Stomp în locații diverse
    IEnumerator TargetedStompAttack()
    {
        isDashing = true;

        // 1. ALEGEM LOCAȚIA (Stânga, Centru sau Dreapta)
        float[] stompSpots = { -arenaWidth * 0.6f, 0f, arenaWidth * 0.6f };
        float targetX = stompSpots[Random.Range(0, stompSpots.Length)];
        Vector3 hoverPos = new Vector3(targetX, flyHeight, 0);

        // Se uită unde merge
        float dirHover = (targetX < transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(dirHover * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        // Zboară la locația aleasă
        while (Vector3.Distance(transform.position, hoverPos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, hoverPos, 20f * Time.deltaTime);
            yield return null;
        }
        anim.Play("Muller_DropPrepare", 0, 0f);
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerJetpackIgnite, 0.7f);
        // 2. TELEGRAPH (Se încarcă și tremură)
        float prepTime = 0.6f;
        while (prepTime > 0)
        {
            transform.position = hoverPos + (Vector3)Random.insideUnitCircle * 0.1f;
            prepTime -= Time.deltaTime;
            yield return null;
        }
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerHarpoonFall, 0.6f);
        // 3. THE STOMP
        anim.Play("Muller_StompImpact", 0, 0f);
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerTransition1Bonk, 1f);
        Vector3 landPos = new Vector3(targetX, groundY, 0);
        while (Vector3.Distance(transform.position, landPos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, landPos, 35f * Time.deltaTime);
            yield return null;
        }

        // 4. UNDE DE ȘOC
        if (shockwavePrefab != null)
        {
            GameObject leftWave = Instantiate(shockwavePrefab, transform.position + new Vector3(-1f, 0, 0), Quaternion.identity);
            leftWave.GetComponent<Shockwave>().direction = -1;

            GameObject rightWave = Instantiate(shockwavePrefab, transform.position + new Vector3(1f, 0, 0), Quaternion.identity);
            rightWave.GetComponent<Shockwave>().direction = 1;
        }

        // 5. VULNERABILITATE (Stă pe jos)
        float healthPercent = currentHealth / maxHealth;
        // La ultimul pas din combo (stomp-ul 2), stă pe jos mai mult ca să poată fi lovit bine
        float groundTime = (stage2AttackStep == 2) ? Mathf.Max(2.5f, 4.0f * healthPercent) : 1.0f;

        HandleLookingAtPlayer();
        isDashing = false;
        currentDashTimer = groundTime;
    }

    // ATACUL 3: Dash si colectare de spini
    IEnumerator GroundSweepDash()
    {
        isDashing = true;

        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerJetpackIgnite, 0.9f);

        // 1. DUTE LA MARGINE MAI ÎNTÂI (Evităm să înceapă din mijloc)
        float startX = (transform.position.x > 0) ? arenaWidth : -arenaWidth;
        float targetX = -startX;

        anim.Play("Muller_JetpackIgnite", 0, 0f);

        Vector3 airEdgePos = new Vector3(startX, flyHeight, 0);
        while (Vector3.Distance(transform.position, airEdgePos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, airEdgePos, 25f * Time.deltaTime);
            yield return null;
        }

        // 2. Coboară la nivelul solului (dacă nu e deja acolo)
        Vector3 groundPos = new Vector3(transform.position.x, groundY, 0);
        while (Vector3.Distance(transform.position, groundPos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, groundPos, 20f * Time.deltaTime);
            yield return null;
        }

        // 3. AVERTIZARE (Telegraph) - Se încarcă cu albastru
        HandleLookingAtPlayer();

        // Tremură pe sol o jumătate de secundă
        float prepTime = 0.5f;
        while (prepTime > 0)
        {
            transform.position = groundPos + (Vector3)Random.insideUnitCircle * 0.05f;
            prepTime -= Time.deltaTime;
            yield return null;
        }

        // 4. THE DASH (Curăță spinii)
        anim.Play("Muller_FlyingLoop", 0, 0f);
        LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.mullerJetpackFlight, 0.6f);
        float dir = (targetX < transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(dir * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        // Găsim toți spinii de pe ecran ÎNAINTE să pornim (optimizare)
        GameObject[] spikesOnGround = GameObject.FindGameObjectsWithTag("Spike");

        while (Mathf.Abs(transform.position.x - targetX) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(targetX, groundY, 0), 25f * Time.deltaTime);

            // A. Dă damage jucătorului dacă se atinge de boss
            if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) < 1.8f)
            {
                playerTransform.GetComponent<Movement>()?.TakeDamage();
            }

            // B. Cât timp face dash, distruge spinii din fața lui
            foreach (GameObject spike in spikesOnGround)
            {
                if (spike != null && Vector3.Distance(transform.position, spike.transform.position) < 2.5f)
                {
                    Destroy(spike);
                }
            }
            yield return null;
        }

        // Siguranță: Distrugem orice a rămas
        foreach (GameObject spike in spikesOnGround) { if (spike != null) Destroy(spike); }
        LevelAudioManager.Instance.StopLoop();
        // 5. VULNERABILITATE
        anim.Play("Muller_Bloated", 0, 0f);

        float healthPercent = currentHealth / maxHealth;
        float groundTime = Mathf.Max(1.0f, 2.5f * healthPercent);

        HandleLookingAtPlayer();

        isDashing = false;
        currentDashTimer = groundTime;
    }

    void SpawnFallingSpike(Vector3 spawnPos)
    {
        // Spawnează spinul fix la coordonata calculată de algoritm
        GameObject spike = Instantiate(spikesPrefab, spawnPos, Quaternion.identity);

        // Îi redăm fizica greoaie ca să cadă rapid!
        Rigidbody2D rb = spike.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 4.5f; // Cădere grea
            rb.linearVelocity = new Vector2(0, -2f); // Împingere inițială în jos
        }
    }

    // --- PHASE 3 ---
    void Stage3_Behavior()
    {
        // Verificăm tutorialStarted ca să nu intre de 2 ori
        if (isDashing || k9Deployed || tutorialStarted) return;

        tutorialStarted = true; // Oprim poarta imediat!

        Vector3 spawnPos = new Vector3(arenaWidth + 4f, groundY, 0);
        GameObject k9 = Instantiate(k9Prefab, spawnPos, Quaternion.identity);
        spawnedK9 = k9.GetComponent<K9Unit>();

        StartCoroutine(MillerFloatMode()); // Pornește zborul o singură dată
    }

    IEnumerator MillerFloatMode()
    {
        float flyHeight = 2.5f;
        moveSpeed = 6.0f;

        HandleLookingAtPlayer();
        anim.SetTrigger("Jetpack");
        LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.mullerJetpackFlight, 0.6f);
        // --- PARTEA 1: TUTORIALUL ---
        // 1. Propulsare în aer: Se forțează poziția în DREAPTA la început
        Vector3 startTutorialPos = new Vector3(arenaWidth - 1f, flyHeight, 0);
        while (Vector3.Distance(transform.position, startTutorialPos) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, startTutorialPos, 15f * Time.deltaTime);
            yield return null;
        }

        yield return new WaitForSeconds(1.0f);

        // 2. Zbor spre STÂNGA pentru tutorial
        Vector3 leftTutorialTarget = new Vector3(-arenaWidth + 1f, flyHeight, 0);
        bool b1 = false, b2 = false, b3 = false;
        GameObject tutorialPinkBomb = null;

        // Müller privește spre stânga
        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        while (transform.position.x > leftTutorialTarget.x + 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, leftTutorialTarget, moveSpeed * Time.deltaTime);

            // Aruncă prima bombă (roșie) pe la început
            if (!b1 && transform.position.x < arenaWidth * 0.5f)
            {
                b1 = true;
                LaunchTutorialBomb(false);
            }
            // Aruncă a doua bombă (roșie) la mijloc
            if (!b2 && transform.position.x < 0f)
            {
                b2 = true;
                LaunchTutorialBomb(false);
            }
            // Aruncă ultima bombă (ROZ) când ajunge maxim în STÂNGA (lângă stâlp)
            if (!b3 && transform.position.x <= leftTutorialTarget.x + 0.5f)
            {
                b3 = true;
                tutorialPinkBomb = LaunchTutorialBomb(true);
            }
            yield return null;
        }

        // Așteptăm ca biletul roz să ricoșeze și să finalizeze tutorialul
        if (tutorialPinkBomb != null)
        {
            yield return StartCoroutine(TutorialBombBounce(tutorialPinkBomb));
        }

        // --- PARTEA 2: FAZA 3 CHAOS (BUCLA INFINITĂ) ---
        // k9Deployed este acum setat pe true (făcut în TutorialBombBounce sau manual aici)
        k9Deployed = true;
        int direction = (transform.position.x > 0) ? -1 : 1;

        yield return StartCoroutine(TutorialBombBounce(tutorialPinkBomb));

        while (currentHealth > 0)
        {
            float targetX = arenaWidth * direction;

            // Metoda segmentelor (Codul tău original)
            int bombsThisPass = Random.Range(1, maxBombsPerPass + 1);
            float[] dropZones = new float[bombsThisPass];
            float startX = -arenaWidth + 1f;
            float endX = arenaWidth - 1f;
            float segmentLength = (endX - startX) / bombsThisPass;

            for (int i = 0; i < bombsThisPass; i++)
            {
                float segStart = startX + (i * segmentLength);
                float segEnd = segStart + segmentLength;
                float padding = segmentLength * 0.2f;
                dropZones[i] = Random.Range(segStart + padding, segEnd - padding);
            }

            if (direction == -1) System.Array.Reverse(dropZones);

            int bombsDropped = 0;
            float scaleDir = (direction == -1) ? 1 : -1;
            transform.localScale = new Vector3(scaleDir * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

            // Execuție trecere Chaos
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
                            // Regula 1 din 5 este roz
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
            yield return new WaitForSeconds(1.2f);
        }
        LevelAudioManager.Instance.StopLoop();
        // FAZA DE DEFEAT (Când currentHealth <= 0)
        BossDefeated();
    }

    // Helper pentru tutorial
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

        // Așteptăm ca Müller să fie lovit (bomba dispare)
        while (bomb != null)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        // --- CURĂȚENIE DUPĂ TUTORIAL ---
        // Găsim stâlpul și îi dezactivăm collider-ul ca să nu mai încurce
        GameObject bouncer = GameObject.FindGameObjectWithTag("TutorialBouncer");
        if (bouncer != null)
        {
            BoxCollider2D col = bouncer.GetComponent<BoxCollider2D>();
            if (col != null) col.enabled = false;

            // Opțional: Poți chiar să distrugi obiectul dacă e doar un trigger invizibil
            // Destroy(bouncer); 
        }

        // Trezim câinele și deblocăm faza Chaos
        if (spawnedK9 != null)
        {
            spawnedK9.WakeUp();
        }
        k9Deployed = true;
    }

    public void PlayMullerLoseSound()
    {
        // Sunet de impact pe canalul de Boss
        LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.mullerDefeat, 1f);
    }

    void BossDefeated()
    {
        currentPhase = BossPhase.Defeated;
        StopAllCoroutines();

        if (LevelAudioManager.Instance != null)
        {
            LevelAudioManager.Instance.PlayVictoryMusic();
        }

        bossSR.color = Color.white; // Reparăm culoarea roșie
        moveSpeed = 0;

        BoxCollider2D bossCollider = GetComponent<BoxCollider2D>();
        if (bossCollider != null)
        {
            bossCollider.enabled = false;
        }

        if (spawnedK9 != null)
        {
            // Îi spunem câinelui să se oprească (vom crea această funcție în scriptul K9Unit)
            spawnedK9.DeactivateK9();
        }
        else
        {
            // Siguranță: dacă referința e null, căutăm după tag și dezactivăm
            GameObject k9 = GameObject.FindGameObjectWithTag("K9");
            if (k9 != null) k9.GetComponent<K9Unit>()?.DeactivateK9();
        }

        // Îl înghețăm în aer pentru prima animație
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        if (stageManager != null)
        {
            stageManager.OnBossDefeated(); // Pornim Outro-ul maimuței
        }
        StartCoroutine(DramaticDeathSequence());
    }

    IEnumerator DramaticDeathSequence()
    {
        HandleLookingAtPlayer();
        // PASUL 1: Lovitura în aer
        anim.Play("Muller_Defeated1", 0, 0f);
        yield return new WaitForSeconds(1.2f); // Pauza în aer

        // PASUL 2: Începe căderea controlată
        float fallSpeed = 0f;
        float gravity = 15f; // Puterea gravitației manuale
        anim.Play("Muller_Defeated2", 0, 0f);

        // Cădem până când atingem nivelul solului (groundY)
        // Ajustăm groundY cu o mică valoare (ex: +1.2f) pentru ca picioarele să stea pe sol
        float stopY = groundY;

        while (transform.position.y > stopY)
        {
            fallSpeed += gravity * Time.deltaTime;
            transform.position -= new Vector3(0, fallSpeed * Time.deltaTime, 0);
            yield return null;
        }

        // PASUL 3: A ajuns la sol - Forțăm poziția pe podea
        transform.position = new Vector3(transform.position.x, stopY, transform.position.z);

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Static;

        anim.Play("Muller_Defeated3", 0, 0f);
    }

    public void TakeDamage(float amount, string damageSource = "Banana")
    {
        // 1. Feedback vizual: Flash-ul apare MEREU când e lovit de banană sau bilet
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(CupheadDamageFlash());

        // 2. Logica de Damage: Verificăm dacă are voie să ia damage
        if (currentPhase == BossPhase.Defeated || isTransitioning || isPreparingToTransition)
        {
            return;
        }

        // În Faza 3, doar biletul roz (TicketBomb) dă damage, dar banana tot face flash-ul
        if (currentPhase == BossPhase.Stage3_Chaos && damageSource == "Banana")
        {
            return;
        }

        // Scădem viața
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        Debug.Log($"Boss Health: {currentHealth}");

        if (currentHealth <= 0)
        {
            currentPhase = BossPhase.Defeated;
            BossDefeated();
        }
    }

    IEnumerator CupheadDamageFlash()
    {
        // Efectul de "pâlpâire" albă (Cuphead style)
        // Trecem rapid între alb și culoarea normală de câteva ori
        for (int i = 0; i < 3; i++)
        {
            // Facem boss-ul aproape alb (un gri foarte deschis păstrează detaliile)
            bossSR.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            yield return new WaitForSeconds(0.05f);

            // Revenim la normal
            bossSR.color = Color.white;
            yield return new WaitForSeconds(0.05f);
        }

        flashCoroutine = null;
    }

    IEnumerator PhaseTransitionSweepAndStun()
    {
        isTransitioning = true;
        isDashing = true;

        HandleLookingAtPlayer();
        anim.Play("Muller_Bloated"); // O animație de "obosit" sau Idle
        yield return new WaitForSeconds(1.5f);

        anim.Play("Muller_FlyingLoop", 0, 0f);
        LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.mullerJetpackFlight, 0.6f);
        // --- 1. POZIȚIONARE PENTRU CURĂȚENIE (Mersul în colț) ---
        // Decidem care e cel mai apropiat colț sau un colț fix
        float startSideX = (transform.position.x > 0) ? arenaWidth : -arenaWidth;
        float targetSideX = -startSideX;

        // A. Mai întâi zboară/merge în colțul de start
        Vector3 startCorner = new Vector3(startSideX, groundY, 0);

        // Îl întoarcem spre colțul unde merge
        float dirToCorner = (startCorner.x < transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(dirToCorner * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        while (Vector3.Distance(transform.position, startCorner) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, startCorner, 25f * Time.deltaTime);
            yield return null;
        }

        // B. S-a poziționat. Acum se întoarce spre restul arenei pentru Dash
        yield return new WaitForSeconds(0.2f); // Mică pauză de pregătire

        float dashDir = (targetSideX < transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(dashDir * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        // --- 2. DASH-ul PROPRIU-ZIS (Mătură toată arena) ---
        // Acum pornește din colț, deci va lua garantat TOȚI spinii
        while (Mathf.Abs(transform.position.x - targetSideX) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(targetSideX, groundY, 0), 35f * Time.deltaTime);

            // Scanăm arena în fiecare cadru (logica de "aspirator")
            GameObject[] spikes = GameObject.FindGameObjectsWithTag("Spike");
            foreach (GameObject spike in spikes)
            {
                if (spike != null && Vector3.Distance(transform.position, spike.transform.position) < 3.5f)
                {
                    Destroy(spike);
                }
            }

            if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) < 1.8f)
                playerTransform.GetComponent<Movement>()?.TakeDamage();

            yield return null;
        }
        LevelAudioManager.Instance.StopLoop();
        // --- REPOZIȚIONARE ȘI SHOWREEL (LENT) ---
        float awayX = transform.position.x + (transform.position.x > 0 ? -3.5f : 3.5f);
        Vector3 safeSpot = new Vector3(awayX, groundY, 0);
        while (Vector3.Distance(transform.position, safeSpot) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, safeSpot, 8f * Time.deltaTime);
            yield return null;
        }

        // Întoarcere spre player
        float lookDir = (playerTransform.position.x < transform.position.x) ? 1 : -1;
        transform.localScale = new Vector3(lookDir * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        yield return new WaitForSeconds(1.0f);

        // Secvența de explozie (LENTĂ)
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
        yield return new WaitForSeconds(2.0f); // Timp pentru flăcări/sunet de pornire

        currentPhase = BossPhase.Stage3_Chaos;
        isDashing = false;
        isTransitioning = false;
    }
}