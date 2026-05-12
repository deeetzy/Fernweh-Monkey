using UnityEngine;

public class Shockwave : MonoBehaviour
{
    public float speed = 12f;
    public int direction = 1; // 1 pentru Dreapta, -1 pentru Stânga
    public float lifetime = 3f;

    void Start()
    {
        // Se distruge singură după câteva secunde ca să nu iasă din ecran la infinit
        Destroy(gameObject, lifetime);

        // Orientăm sprite-ul în direcția de mers
        transform.localScale = new Vector3(direction * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    void Update()
    {
        transform.Translate(Vector2.right * direction * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            collision.GetComponent<Movement>()?.TakeDamage();
        }
    }
}