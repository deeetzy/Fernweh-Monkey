using UnityEngine;

public class HealthCollectible : MonoBehaviour
{
    public int HealthPoints = 1;
    public AudioClip collectedClip;

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController controller = other.GetComponent<PlayerController>();

        if(controller != null && controller.health < controller.maxHealth)
        {
                controller.ChangeHealth(HealthPoints);
                controller.PlaySound(collectedClip);
                Destroy(gameObject);
        }
    }
}