using UnityEngine;

public class BananaShuriken : MonoBehaviour
{
    public float speed = 15f;
    public float rotationSpeed = 500f;
    public float damage = 50f;
    public float lifeTime = 2f;

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Boss"))
        {
            BossController boss = other.GetComponent<BossController>();
            if (boss != null)
            {
                boss.TakeDamage(damage, "Banana");
            }
            Destroy(gameObject);
        }

        if (other.CompareTag("Dog") || other.CompareTag("Ground"))
        {

            Destroy(gameObject);
        }

        if (other.CompareTag("Blockade"))
        {
            other.GetComponent<BlockadeWall>()?.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}