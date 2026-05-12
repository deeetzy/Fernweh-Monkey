using UnityEngine;
using System.Collections;

public class BlockadeWall : MonoBehaviour
{
    [Header("Stats Perete")]
    public float health = 50f;
    public GameObject explosionEffect; // Opțional: un efect de explozie când moare

    [Header("Setări Spawner")]
    public GameObject carPrefab; // Prefab-ul de Lowrider creat anterior
    public Transform spawnPoint; // Locul de unde "ies" mașinile
    public float minSpawnDelay = 1.5f;
    public float maxSpawnDelay = 3.0f;

    public FlockSpawner birdManager;

    private bool isDestroyed = false;

    void Start()
    {
        // Începe să scuipe mașini imediat ce peretele apare pe ecran
        StartCoroutine(SpawnCarRoutine());
    }

    IEnumerator SpawnCarRoutine()
    {
        while (!isDestroyed)
        {
            // Așteaptă un timp random între mașini
            float delay = Random.Range(minSpawnDelay, maxSpawnDelay);
            yield return new WaitForSeconds(delay);

            if (!isDestroyed)
            {
                SpawnCar();
            }
        }
    }

    void SpawnCar()
    {
        GameObject car = Instantiate(carPrefab, spawnPoint.position, Quaternion.identity);

        // Configurăm mașina să fie High sau Low la întâmplare
        LowriderObstacle script = car.GetComponent<LowriderObstacle>();
        if (script != null)
        {
            script.carType = (Random.value > 0.5f) ?
                LowriderObstacle.RideHeight.HighBounce :
                LowriderObstacle.RideHeight.LowBounce;
        }
    }

    // Funcția apelată de gloanțele maimuței
    public void TakeDamage(float damage)
    {
        if (isDestroyed) return;

        health -= damage;
        Debug.Log("Perete lovit! Viață rămasă: " + health);

        // Feedback vizual (se face roșu scurt)
        StartCoroutine(FlashRed());

        if (health <= 0)
        {
            DestroyWall();
        }
    }

    // Dacă collider-ul bananei este setat pe IsTrigger
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Banana"))
        {
            TakeDamage(1f); // Scade viața peretelui
            Destroy(other.gameObject); // Distruge banana la impact
        }
    }

    // Dacă collider-ul bananei NU este pe IsTrigger (coliziune fizică plină)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Banana"))
        {
            TakeDamage(1f); // Scade viața peretelui
            Destroy(collision.gameObject); // Distruge banana la impact
        }
    }

    IEnumerator FlashRed()
    {
        GetComponent<SpriteRenderer>().color = Color.red;
        yield return new WaitForSeconds(0.1f);
        GetComponent<SpriteRenderer>().color = Color.white;
    }

    void DestroyWall()
    {
        isDestroyed = true;
        Debug.Log("Peretele a fost distrus! Trecem la Istanbul...");

        if (explosionEffect != null) Instantiate(explosionEffect, transform.position, Quaternion.identity);

        // Oprim raidurile de pe fundal (din Zona 1)
        AirRaidManager manager = Object.FindFirstObjectByType<AirRaidManager>();
        if (manager != null) manager.StopRaids();

        // --- NOU: Dăm semnalul Managerului de Păsări! ---
        if (birdManager != null)
        {
            birdManager.SwitchToSeagulls(4f); // 4 secunde pauză, apoi începe Faza 2 cu pescăruși
        }

        Destroy(gameObject, 0.2f);
    }
}