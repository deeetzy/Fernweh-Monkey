using UnityEngine;

public class DDA_DataCollector : MonoBehaviour
{
    public static DDA_DataCollector Instance;
    public DDA_Agent ddaAgent;

    [Header("Basic Metrics")]
    public int deathsInCurrentStage = 0;
    public float damageTakenInLastMinute = 0f;
    public int successfulParries = 0;
    public int totalParryOpportunities = 0;
    public float timeSinceStageStart = 0f;

    [Header("Behavioral Data")]
    public float panicJumpCount = 0f;
    public float timeAtEdges = 0f;
    public float airTimeThisSession = 0f;
    public float averageDistanceToBoss = 0f;
    public float dashTimingAccuracy = 0f;

    [Header("Contextual Data")]
    public string lastDamageSource;
    private Vector3 lastDamagePosition;
    private int sameSpotDamageCounter = 0;

    private float sessionStartTime;
    private float lastJumpTime;

    private Transform player;
    private BossController boss;
    private Movement pMove;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        sessionStartTime = Time.time;
    }

    void Start()
    {
        FindEntities();
    }

    void Update()
    {
        if (player == null || boss == null || pMove == null)
        {
            FindEntities();
            if (player == null || boss == null) return;
        }

        if (boss.currentPhase == BossController.BossPhase.Defeated) return;

        timeSinceStageStart = Time.time - sessionStartTime;

        float currentDist = Vector3.Distance(player.position, boss.transform.position);
        averageDistanceToBoss = Mathf.Lerp(averageDistanceToBoss, currentDist, Time.deltaTime);

        if (boss.currentPhase == BossController.BossPhase.Stage2_Dash)
        {
            if (Mathf.Abs(player.position.x) > boss.arenaWidth * 0.8f)
                timeAtEdges += Time.deltaTime;
        }

        if (!pMove.isGrounded)
            airTimeThisSession += Time.deltaTime;

        if (damageTakenInLastMinute > 0f)
        {
            damageTakenInLastMinute -= Time.deltaTime * 0.4f;
            damageTakenInLastMinute = Mathf.Max(0f, damageTakenInLastMinute);
        }

        if (panicJumpCount > 0f)
        {
            panicJumpCount -= Time.deltaTime * 0.3f;
            panicJumpCount = Mathf.Max(0f, panicJumpCount);
        }
    }

    private void FindEntities()
    {
        pMove = Object.FindFirstObjectByType<Movement>();
        if (pMove != null)
        {
            player = pMove.transform;
            if (ddaAgent != null) ddaAgent.player = pMove;
        }

        boss = Object.FindFirstObjectByType<BossController>();
        if (boss != null && ddaAgent != null)
        {
            ddaAgent.boss = boss;
        }
    }

    public void ResetCollectorData()
    {
        damageTakenInLastMinute = 0f;
        panicJumpCount = 0f;
        timeAtEdges = 0f;
        deathsInCurrentStage = 0;

        successfulParries = 0;
        totalParryOpportunities = 0;
        airTimeThisSession = 0f;
        sameSpotDamageCounter = 0;
        sessionStartTime = Time.time;

        Debug.Log("--- [DDA DATA SYSTEM] Toate contoarele au fost curățate la zero! ---");
    }

    public void RecordJump()
    {
        if (Time.time - lastJumpTime < 0.25f)
        {
            panicJumpCount += 1.0f; 
        }
        lastJumpTime = Time.time;
    }

    public void RecordDamage(float amount, string source, Vector3 pos)
    {
        damageTakenInLastMinute += amount;
        lastDamageSource = source;

        if (Vector3.Distance(pos, lastDamagePosition) < 2f) sameSpotDamageCounter++;
        else sameSpotDamageCounter = 0;

        lastDamagePosition = pos;
    }

    public void RecordParry(bool success)
    {
        totalParryOpportunities++;
        if (success) successfulParries++;
    }

    public void RecordDashAccuracy(float distanceToDanger)
    {
        float accuracy = Mathf.InverseLerp(5f, 1.2f, distanceToDanger);
        dashTimingAccuracy = (dashTimingAccuracy + accuracy) / 2f;
    }

    public void RecordDeath()
    {
        deathsInCurrentStage++;
        if (ddaAgent != null)
        {
            ddaAgent.SetReward(-1.0f);
            ddaAgent.EndEpisode();
        }
    }

    public float[] GetObservationVector()
    {
        if (pMove == null || boss == null) return new float[8];

        return new float[] {
            Mathf.Clamp01(boss.currentHealth / boss.maxHealth),
            Mathf.Clamp01((float)pMove.currentLives / pMove.maxLives),
            Mathf.Clamp01(damageTakenInLastMinute / 10f),
            Mathf.Clamp01((float)successfulParries / Mathf.Max(1, totalParryOpportunities)),
            Mathf.Clamp01(deathsInCurrentStage / 10f),
            Mathf.Clamp01(timeAtEdges / 30f),
            Mathf.Clamp01(panicJumpCount / 20f),
            Mathf.Clamp01(dashTimingAccuracy)
        };
    }
}