using UnityEngine;

public class CornProjectile : MonoBehaviour
{
    public float spinSpeed = 360f;
    private float lifeTimer = 0f; // NOU: Cât timp a trăit porumbul?

    void Start()
    {
        Destroy(gameObject, 4f);
    }

    void Update()
    {
        transform.Rotate(0, 0, spinSpeed * Time.deltaTime);

        // NOU: Creștem cronometrul în fiecare frame
        lifeTimer += Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            other.GetComponent<Movement>()?.TakeDamage();
            Destroy(gameObject);
        }
        // NOU: Se sparge pe Ground DOAR dacă a trăit mai mult de 0.1 secunde
        else if (other.CompareTag("Ground") && lifeTimer > 0.1f)
        {
            Destroy(gameObject);
        }
    }
}