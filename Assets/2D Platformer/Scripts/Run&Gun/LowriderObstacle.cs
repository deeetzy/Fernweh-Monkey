using UnityEngine;

public class LowriderObstacle : MonoBehaviour
{
    public enum RideHeight { HighBounce, LowBounce }

    [Header("Setări Lowrider")]
    public RideHeight carType;
    public float speed = 10f; // Viteza mașinii

    [Header("Referințe Vizuale & Gameplay")]
    public Transform carBody; // Partea vizuală a mașinii (sprite-ul)
    public GameObject pinkOrnament; // Obiectul roz pentru Parry

    void Start()
    {
        SetupCar();
    }

    void Update()
    {
        // 1. Mașina fuge mereu spre stânga (spre maimuță)
        transform.Translate(Vector3.left * speed * Time.deltaTime);

        // 2. Curățenie: Distrugem mașina dacă a ieșit mult în afara ecranului
        if (transform.position.x < -20f)
        {
            Destroy(gameObject);
        }
    }

    void SetupCar()
    {
        if (carType == RideHeight.HighBounce)
        {
            carBody.localPosition = new Vector3(0, 1.3f, 0); // Se ridică caroseria
            if (pinkOrnament != null) pinkOrnament.SetActive(false);
        }
        else if (carType == RideHeight.LowBounce)
        {
            carBody.localPosition = new Vector3(0, 0f, 0); // Rămâne la nivelul roților

            if (pinkOrnament != null)
            {
                pinkOrnament.SetActive(true);
                // Ornamentul stă pe capotă
                pinkOrnament.transform.localPosition = new Vector3(0.3f, 1.8f, 0);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Dacă lovește jucătorul, apelăm funcția ta de damage
        if (other.CompareTag("Player"))
        {
            other.GetComponent<Movement>()?.TakeDamage("BOSS");
        }
    }
}