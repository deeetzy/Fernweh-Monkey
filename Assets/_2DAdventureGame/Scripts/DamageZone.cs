using UnityEngine;

public class DamageZone : MonoBehaviour
{
    public int HealthPoints = -1;

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerController controller = other.GetComponent<PlayerController>();

        if (controller != null)
        {
            controller.ChangeHealth(HealthPoints);
        }
    }
}
