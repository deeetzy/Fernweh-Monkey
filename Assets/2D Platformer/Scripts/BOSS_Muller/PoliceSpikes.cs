using UnityEngine;

public class PoliceSpikes : MonoBehaviour
{
    private Rigidbody2D rb;
    private Collider2D col;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ground"))
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero; 
                rb.gravityScale = 0f;        
            }

            if (col != null)
            {
                float topOfGround = other.bounds.max.y;

                float spikeBottomOffset = transform.position.y - col.bounds.min.y;

                transform.position = new Vector3(transform.position.x, topOfGround + spikeBottomOffset, transform.position.z);
            }
        }

        if (other.CompareTag("Player"))
        {
            Movement monkey = other.GetComponent<Movement>();
            if (monkey != null)
            {
                monkey.TakeDamage("Spikes");
            }
        }
    }
}