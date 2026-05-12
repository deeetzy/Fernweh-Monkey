using UnityEngine;

public class PoliceSpikes : MonoBehaviour
{
    private Rigidbody2D rb;
    private Collider2D col; // NOU: Avem nevoie de collider-ul spinului

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>(); // Îl salvăm la început
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. OPREȘTE-TE CÂND ATINGI PĂMÂNTUL
        if (other.CompareTag("Ground"))
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero; // Oprește viteza
                rb.gravityScale = 0f;             // Taie gravitația
            }

            // --- NOU: Aliniere perfectă pe iarbă ---
            if (col != null)
            {
                // Aflăm exact linia de sus a podelei (tavanul pământului)
                float topOfGround = other.bounds.max.y;

                // Aflăm diferența dintre centrul spinului și talpa lui
                float spikeBottomOffset = transform.position.y - col.bounds.min.y;

                // Teleportăm spinul EXACT pe iarbă, indiferent cât de adânc se îngropase în acel frame
                transform.position = new Vector3(transform.position.x, topOfGround + spikeBottomOffset, transform.position.z);
            }
        }

        // 2. DĂ DAMAGE JUCĂTORULUI CÂND TRECE PRIN EL
        if (other.CompareTag("Player"))
        {
            Movement monkey = other.GetComponent<Movement>();
            if (monkey != null)
            {
                monkey.TakeDamage();
            }
        }
    }
}