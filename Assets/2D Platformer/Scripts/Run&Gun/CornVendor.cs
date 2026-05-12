using UnityEngine;
using System.Collections;

public class CornVendor : MonoBehaviour
{
    [Header("Setări Viață")]
    public int health = 3; // Câte gloanțe rezistă

    [Header("Setări Atac")]
    public GameObject cornPrefab;
    public Transform throwPoint;
    public float throwInterval = 2.5f;
    public float detectionRange = 12f; // Distanța maximă de la care te vede

    [Header("Setări Forță Aruncare")]
    public float verticalForce = 8f;   // Forța pe verticală (înălțimea arcului)
    public float horizontalForceBase = 5f; // Cât de tare împinge spre jucător

    private Transform player;
    private SpriteRenderer sr;

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        sr = GetComponentInChildren<SpriteRenderer>();
        StartCoroutine(ThrowRoutine());
    }

    IEnumerator ThrowRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(throwInterval);

            if (player != null)
            {
                // Măsurăm distanța dintre vânzător și maimuță
                float distanceToPlayer = Vector2.Distance(transform.position, player.position);

                // Aruncă DOAR dacă maimuța e în raza de acțiune
                if (distanceToPlayer <= detectionRange)
                {
                    ThrowCorn();
                }
            }
        }
    }

    void ThrowCorn()
    {
        if (cornPrefab == null || throwPoint == null) return;

        GameObject corn = Instantiate(cornPrefab, throwPoint.position, Quaternion.identity);
        Rigidbody2D rb = corn.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            float directionX = (player.position.x < transform.position.x) ? -1f : 1f;
            if (sr != null) sr.flipX = (directionX > 0);

            // 1. Calculăm distanța Orizontală (X) și Verticală (Y) separat
            float distanceX = Mathf.Abs(player.position.x - transform.position.x);
            float distanceY = player.position.y - transform.position.y; // Cât de sus ești față de el?

            // 2. Puterea orizontală rămâne la fel
            float horizontalPower = Mathf.Clamp(distanceX * 0.8f, horizontalForceBase, 15f);

            // 3. Puterea verticală de BAZĂ
            float baseVertical = Mathf.Clamp(distanceX * 0.5f, 3f, verticalForce);

            // 4. SECRETUL: Dacă ești mai sus de el (distanceY pozitiv), adaugă un "boost" la forța verticală!
            float extraHeightBoost = (distanceY > 0) ? (distanceY * 1.1f) : 0f;

            // Puterea finală e baza + boost-ul de înălțime
            Vector2 finalForce = new Vector2(directionX * horizontalPower, baseVertical + extraHeightBoost);

            rb.AddForce(finalForce, ForceMode2D.Impulse);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Verifică tag-ul glonțului tău (asigură-te că glonțul are tag-ul "PlayerBullet")
        if (other.CompareTag("Banana"))
        {
            TakeDamage();
            // Distrugem glonțul la impact ca să nu treacă prin el
            Destroy(other.gameObject);
        }
    }

    void TakeDamage()
    {
        health--;

        // Feedback vizual: clipește roșu când e lovit
        StartCoroutine(FlashRed());

        if (health <= 0)
        {
            Die();
        }
    }

    IEnumerator FlashRed()
    {
        if (sr != null)
        {
            sr.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            sr.color = Color.white;
        }
    }

    void Die()
    {
        StopAllCoroutines();

        // Căutăm scriptul de mișcare pe părinte (Taraba) și îl oprim
        MovingTaraba movingScript = GetComponentInParent<MovingTaraba>();
        if (movingScript != null)
        {
            movingScript.isMoving = false;
        }

        Debug.Log("Vânzătorul a fugit, taraba s-a oprit!");
        Destroy(gameObject);
    }
}