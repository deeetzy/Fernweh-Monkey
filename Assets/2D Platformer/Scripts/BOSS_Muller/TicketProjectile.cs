using UnityEngine;
using System.Collections;

public class TicketProjectile : MonoBehaviour
{
    public enum BombState { Falling, Grounded, Parried, Exploding }
    public BombState currentState = BombState.Falling;

    [Header("Settings")]
    public float fuseTime = 3f;
    public float explosionRadius = 3f;
    public float groundLevel = -5.27f;
    private bool hasDealtDamage = false;

    [Header("Parry Settings")]
    public bool isParryable = false;
    public float parrySpeed = 50f;
    public float damageToBoss = 10f;
    public Color parryColor = new Color(1f, 0.4f, 0.9f);

    private Transform boss;
    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private Animator anim;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        GameObject bossObj = GameObject.Find("Boss_PolizaiMuller");
        if (bossObj != null) boss = bossObj.transform;

        if (isParryable) sr.color = parryColor;

        if (rb != null) rb.gravityScale = 2.0f;

        StartCoroutine(BombLogic());
    }

    void Update()
    {
        if (currentState == BombState.Exploding) return;

        if (currentState == BombState.Parried && boss != null && !hasDealtDamage)
        {
            transform.position = Vector3.MoveTowards(transform.position, boss.position + Vector3.up, parrySpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, boss.position + Vector3.up) < 0.8f)
            {
                hasDealtDamage = true;
                boss.GetComponent<BossController>().TakeDamage(damageToBoss, "TicketBomb");
                StartCoroutine(ExecuteExplosionSequence(false));
            }
        }

        if (isParryable && currentState == BombState.Falling)
        {
            GameObject bouncer = GameObject.FindGameObjectWithTag("TutorialBouncer");

            if (bouncer != null && bouncer.GetComponent<BoxCollider2D>().enabled)
            {
                if (rb != null) { rb.gravityScale = 0; rb.linearVelocity = Vector2.zero; }

                transform.position = Vector3.MoveTowards(transform.position, bouncer.transform.position, 12f * Time.deltaTime);

                if (Vector3.Distance(transform.position, bouncer.transform.position) < 0.2f)
                {
                    Parry();
                }
                return; 
            }
        }

        if (currentState == BombState.Falling && transform.position.y <= groundLevel)
        {
            HitGround();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (currentState == BombState.Exploding || currentState == BombState.Parried) return;

        if (collision.CompareTag("Player"))
        {
            Movement playerMove = collision.GetComponent<Movement>();

            if (isParryable)
            {
                if (playerMove != null && playerMove.isDoubleJumping)
                {
                    Parry();
                    Rigidbody2D playerRb = collision.GetComponent<Rigidbody2D>();
                    if (playerRb != null)
                    {
                        playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, 12f);
                    }
                }
                else
                {
                    if (DDA_DataCollector.Instance != null)
                    {
                        DDA_DataCollector.Instance.RecordParry(false);
                    }
                    StartCoroutine(ExecuteExplosionSequence(true));
                }
            }
            else
            {
                StartCoroutine(ExecuteExplosionSequence(true));
            }
        }
    }

    public void Parry()
    {
        if (currentState == BombState.Parried || currentState == BombState.Exploding) return;

        currentState = BombState.Parried;
        bool success = true;
        StopAllCoroutines();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        sr.color = parryColor;
        transform.rotation = Quaternion.identity;

        DDA_DataCollector.Instance.RecordParry(success);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }

    void HitGround()
    {
        if (currentState != BombState.Falling) return;

        currentState = BombState.Grounded;
        transform.position = new Vector3(transform.position.x, groundLevel, transform.position.z);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
        }
        transform.rotation = Quaternion.identity;
    }

    IEnumerator ExecuteExplosionSequence(bool dealPlayerDamage)
    {
        if (currentState == BombState.Exploding) yield break;

        if (rb != null && rb.bodyType != RigidbodyType2D.Static)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
        }

        currentState = BombState.Exploding;
        sr.color = Color.white;

        if (anim != null)
        {
            LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.ticketExplode, 1f);
            anim.Play("Ticket_Explode", 0, 0f);
        }

        if (dealPlayerDamage) ExplodePhysics();

        yield return new WaitForSeconds(0.6f);
        sr.enabled = false;
        Destroy(gameObject, 0.1f);
    }

    void ExplodePhysics()
    {
        Collider2D[] hitObjects = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (Collider2D obj in hitObjects)
        {
            if (obj.CompareTag("Player"))
            {
                obj.GetComponent<Movement>()?.TakeDamage("Ticket");
            }
        }
    }

    IEnumerator BombLogic()
    {
        while (currentState == BombState.Falling) yield return null;
        if (currentState == BombState.Parried || currentState == BombState.Exploding) yield break;

        float timer = 0;
        Color baseColor = isParryable ? parryColor : Color.white;

        while (timer < fuseTime)
        {
            if (currentState != BombState.Grounded) yield break;

            float progress = timer / fuseTime;
            float flashSpeed = Mathf.Lerp(0.25f, 0.05f, progress);

            LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.ticketTicking, 0.4f + (progress * 0.4f));

            sr.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            yield return new WaitForSeconds(flashSpeed);
            sr.color = baseColor;
            yield return new WaitForSeconds(flashSpeed);

            timer += (flashSpeed * 2);
        }

        if (currentState == BombState.Grounded)
            StartCoroutine(ExecuteExplosionSequence(true));
    }
}