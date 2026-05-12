using UnityEngine;
using System.Collections;

public class FlockSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject pigeonPrefab;
    public GameObject seagullPrefab;

    [Header("Setări Ritm")]
    public float spawnRate = 5f;
    public int minBirds = 2;
    public int maxBirds = 4;

    [Header("Setări Stol")]
    public float imprastiereY = 1f; // Pune asta pe 0 în Unity dacă vrei să vină pe același nivel!

    private GameObject currentBirdPrefab;
    private bool isSpawning = false;
    private Camera cam;
    private Coroutine activeRoutine;

    void Start()
    {
        cam = Camera.main;
        StartPhase(pigeonPrefab);
    }

    void StartPhase(GameObject birdType)
    {
        currentBirdPrefab = birdType;
        isSpawning = true;

        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(SpawnLoop());
    }

    // Funcția chemată de perete
    public void SwitchToSeagulls(float delay)
    {
        // REPARAȚIA 1: Oprim instant cronometrul vechi ca să nu mai vină porumbei "întârziați"
        if (activeRoutine != null) StopCoroutine(activeRoutine);

        StartCoroutine(TransitionSequence(delay));
    }

    IEnumerator TransitionSequence(float delay)
    {
        isSpawning = false;
        Debug.Log("Perete distrus! Pauză absolută de " + delay + " secunde...");

        yield return new WaitForSeconds(delay);

        StartPhase(seagullPrefab);
    }

    public void StopEverything()
    {
        isSpawning = false;
        if (activeRoutine != null) StopCoroutine(activeRoutine);
    }

    IEnumerator SpawnLoop()
    {
        while (isSpawning)
        {
            yield return new WaitForSeconds(spawnRate);

            // Verificare de siguranță: dacă s-a oprit în timpul așteptării, nu spawna!
            if (isSpawning)
            {
                SpawnFlock();
            }
        }
    }

    void SpawnFlock()
    {
        if (currentBirdPrefab == null) return;

        int count = Random.Range(minBirds, maxBirds + 1);
        float sideX = (Random.value > 0.5f) ? -0.1f : 1.1f;
        Vector3 spawnBase = cam.ViewportToWorldPoint(new Vector3(sideX, Random.Range(0.4f, 0.9f), 10));

        for (int i = 0; i < count; i++)
        {
            // REPARAȚIA 2: Controlezi cât de mult se împrăștie pe verticală
            Vector3 offset = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-imprastiereY, imprastiereY), 0);
            Instantiate(currentBirdPrefab, spawnBase + offset, Quaternion.identity);
        }
    }
}