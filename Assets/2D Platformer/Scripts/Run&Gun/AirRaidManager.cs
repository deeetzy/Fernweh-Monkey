using UnityEngine;
using System.Collections;

public class AirRaidManager : MonoBehaviour
{
    [Header("Setări Atac")]
    public GameObject flockSpawnerPrefab; // Trage aici Prefab-ul cu norul (FlockSpawner)
    public float minDelay = 5f;  // Timp minim între atacuri
    public float maxDelay = 12f; // Timp maxim între atacuri

    [Header("Locație de spawn")]
    public float heightOffScreen = 6f; // Cât de sus să apară față de centrul ecranului
    public float widthOffScreen = 15f; // Cât de mult în stânga/dreapta ecranului să fie

    void Start()
    {
        // Începe bucla de atacuri
        StartCoroutine(RaidRoutine());
    }

    IEnumerator RaidRoutine()
    {
        while (true)
        {
            // Așteaptă un timp random
            float waitTime = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(waitTime);

            SpawnFlockOffScreen();
        }
    }

    void SpawnFlockOffScreen()
    {
        if (flockSpawnerPrefab == null) return;

        // Găsim poziția curentă a camerei
        Vector3 cameraPos = Camera.main.transform.position;

        // Dăm cu zarul: 0 = Stânga, 1 = Dreapta
        int side = Random.Range(0, 2);

        float spawnX = (side == 0) ? cameraPos.x - widthOffScreen : cameraPos.x + widthOffScreen;
        float spawnY = cameraPos.y + heightOffScreen;

        Vector3 spawnPosition = new Vector3(spawnX, spawnY, 0);

        // Spawnăm norul de porumbei!
        Instantiate(flockSpawnerPrefab, spawnPosition, Quaternion.identity);
    }

    public void StopRaids()
    {
        StopAllCoroutines(); // Oprește timerul de atac
        Debug.Log("Atacurile aeriene au fost oprite!");
    }
}