using UnityEngine;

public class BazaarGate : MonoBehaviour
{
    public FlockSpawner birdManager;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (birdManager != null)
            {
                // Am actualizat numele funcției aici:
                birdManager.StopEverything();
            }
        }
    }
}