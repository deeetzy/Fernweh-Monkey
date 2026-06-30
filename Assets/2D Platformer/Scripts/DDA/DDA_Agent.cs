using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class DDA_Agent : Agent
{
    [Header("References")]
    public BossController boss;
    public Movement player;

    [Header("DDA Fixed States")]
    public float targetDifficulty = 1.0f;
    public float smoothDifficulty = 1.0f;

    [Header("DDA Tuning Knobs")]
    [Tooltip("Durata în secunde a ferestrei de analiză ofensivă. Recomandat: 12")]
    public float windowDuration = 12.0f;

    [Tooltip("Cât de puțin urcă dificultatea per condiție îndeplinită. Recomandat: 0.02")]
    public float difficultyIncreaseStep = 0.02f;

    [Tooltip("Cât de mult scade dificultatea când jucătorul încasează hit-uri. Recomandat: 0.05")]
    public float difficultyDecreaseStep = 0.05f;

    [Tooltip("Viteza de tranziție fluidă a manetei. Recomandat: 0.02")]
    public float transitionSpeed = 0.02f;

    [Header("Dynamic Stage Damage Settings")]
    [Tooltip("Damage necesar în Stagiul 1 și 2 (Banane). Recomandat: 27")]
    public float stage1And2RequiredDamage = 27f;

    [Tooltip("Damage necesar în Stagiul 3 (Bombe). Recomandat: 55 (reprezintă minimum 2 bombe nimerite)")]
    public float stage3RequiredDamage = 55f;

    [Header("Defensivă de Rezistență (Fereastră Lungă)")]
    [Tooltip("Câte secunde fără niciun hit sunt necesare pentru un upgrade defensiv.")]
    public float requiredNoHitTime = 60f;
    public float noHitTimer = 0f;

    [Header("Internal Timers")]
    private float windowTimer = 0f;
    private float gracePeriodTimer = 3.0f;

    private float snapDamage = 0f;
    private float snapPanicJumps = 0f;
    private int snapParries = 0;
    private float snapBossHealth = 0f;

    private float lastFrameDamage = 0f;
    private Movement pMove; 

    public override void OnEpisodeBegin()
    {
        targetDifficulty = 1.0f;
        smoothDifficulty = 1.0f;
        gracePeriodTimer = 3.0f;
        windowTimer = 0f;
        noHitTimer = 0f;
        lastFrameDamage = 0f;

        if (DDA_DataCollector.Instance != null)
        {
            DDA_DataCollector.Instance.ResetCollectorData();
        }

        ResetWindowSnapshots();
        ApplyDDA(1.0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (DDA_DataCollector.Instance == null) return;

        if (actions.DiscreteActions.Length > 0) { int dummy = actions.DiscreteActions[0]; }

        if (player == null || boss == null)
        {
            FindReferencesInScene();
        }

        //PROTECȚIE MAIN MENU
        if (player == null || boss == null)
        {
            targetDifficulty = 1.0f;
            smoothDifficulty = 1.0f;
            return;
        }

        //VERIFICARE BUTON ON/OFF
        int isDDAEnabled = PlayerPrefs.GetInt("DDAEnabled", 1);
        if (isDDAEnabled == 0)
        {
            targetDifficulty = 1.0f;
            smoothDifficulty = 1.0f;
            ApplyDDA(smoothDifficulty);
            return;
        }

        //PERIOADA DE GRAȚIE LA PORNIRE
        if (gracePeriodTimer > 0f)
        {
            gracePeriodTimer -= Time.deltaTime;
            targetDifficulty = 1.0f;
            smoothDifficulty = 1.0f;
            ApplyDDA(smoothDifficulty);
            ResetWindowSnapshots();
            return;
        }

        float currentDamagePool = DDA_DataCollector.Instance.damageTakenInLastMinute;

        if (currentDamagePool > lastFrameDamage)
        {
            noHitTimer = 0f; //Reset instant la hit
        }
        else
        {
            noHitTimer += Time.deltaTime;

            if (noHitTimer >= requiredNoHitTime)
            {
                targetDifficulty += difficultyIncreaseStep;
                Debug.Log($"[DDA RECOMPENSĂ] {requiredNoHitTime} secunde perfecte! +{difficultyIncreaseStep} la dificultate.");
                noHitTimer = 0f;
            }
        }
        lastFrameDamage = currentDamagePool;
        windowTimer += Time.deltaTime;

        if (windowTimer >= windowDuration)
        {
            EvaluatePlayerPerformanceTrend();
            windowTimer = 0f;
            ResetWindowSnapshots();
        }

        smoothDifficulty = Mathf.MoveTowards(smoothDifficulty, targetDifficulty, Time.deltaTime * transitionSpeed);
        smoothDifficulty = Mathf.Clamp(smoothDifficulty, 0.75f, 1.25f);

        ApplyDDA(smoothDifficulty);
    }

    private void ResetWindowSnapshots()
    {
        if (DDA_DataCollector.Instance == null) return;

        snapDamage = DDA_DataCollector.Instance.damageTakenInLastMinute;
        snapPanicJumps = DDA_DataCollector.Instance.panicJumpCount;
        snapParries = DDA_DataCollector.Instance.successfulParries;

        if (boss != null)
        {
            snapBossHealth = boss.currentHealth;
        }
    }

    private void EvaluatePlayerPerformanceTrend()
    {
        if (DDA_DataCollector.Instance == null || boss == null) return;

        float deltaDamage = DDA_DataCollector.Instance.damageTakenInLastMinute - snapDamage;
        float damageDealtToBoss = snapBossHealth - boss.currentHealth;

        if (deltaDamage < 0) deltaDamage = DDA_DataCollector.Instance.damageTakenInLastMinute;
        if (damageDealtToBoss < 0) damageDealtToBoss = 0;

        float activeDamageThreshold = stage1And2RequiredDamage;

        if (boss.currentPhase == BossController.BossPhase.Stage3_Chaos)
        {
            activeDamageThreshold = stage3RequiredDamage; 
        }

        //CONDIȚIA OFENSIVĂ
        if (damageDealtToBoss >= activeDamageThreshold)
        {
            targetDifficulty += difficultyIncreaseStep;
            Debug.Log($"[DDA OFENSIV] Etapă validată! Damage dat: {damageDealtToBoss:F0}/{activeDamageThreshold}. (+{difficultyIncreaseStep})");
        }

        //ONDIȚIA DE AJUTOR
        if (deltaDamage > 0f)
        {
            targetDifficulty -= difficultyDecreaseStep;
            Debug.Log($"[DDA URGENȚĂ] Jucător lovit în fereastră. (-{difficultyDecreaseStep})");
        }

        targetDifficulty = Mathf.Clamp(targetDifficulty, 0.75f, 1.25f);
    }

    private void FindReferencesInScene()
    {
        pMove = Object.FindFirstObjectByType<Movement>();
        if (pMove != null) player = pMove;
        boss = Object.FindFirstObjectByType<BossController>();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (DDA_DataCollector.Instance == null) { sensor.AddObservation(new float[8]); return; }
        foreach (float obs in DDA_DataCollector.Instance.GetObservationVector()) { sensor.AddObservation(obs); }
    }

    public void ResetDifficultyOnPlayerRestart()
    {
        if (targetDifficulty > 1.1f)
        {
            Debug.Log($"[DDA BUTTON RESET] Resetare executată! Dificultatea era {targetDifficulty:F2}. O aducem la 1.0f.");
            targetDifficulty = 1.0f;
            smoothDifficulty = 1.0f;
        }
        else
        {
            targetDifficulty = Mathf.Clamp(targetDifficulty, 0.75f, 1.25f);
            smoothDifficulty = targetDifficulty;
        }

        gracePeriodTimer = 3.0f;
        windowTimer = 0f;
        noHitTimer = 0f;
        lastFrameDamage = 0f;

        ResetWindowSnapshots();
        ApplyDDA(smoothDifficulty);
    }

    private void ApplyDDA(float multiplier)
    {
        if (player == null || boss == null) FindReferencesInScene();
        if (player != null) player.dashCooldown = 1.0f * multiplier;
        if (boss != null && boss.currentPhase != BossController.BossPhase.Defeated)
        {
            boss.ApplyDDAMultiplier(multiplier);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut) { }
}