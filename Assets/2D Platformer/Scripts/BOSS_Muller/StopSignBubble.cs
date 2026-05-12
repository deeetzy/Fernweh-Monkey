using UnityEngine;

public class StopSignBubble : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float moveDirection = -1f;

    void Update()
    {
        // Move strictly horizontal
        transform.position += Vector3.left * moveDirection * moveSpeed * Time.deltaTime;

        // Cleanup if it goes off-screen
        if (transform.position.x < -15f) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            LevelAudioManager.Instance.PlayBossSFX(LevelAudioManager.Instance.bubblePop, 0.6f);
            other.GetComponent<Movement>()?.TakeDamage();
            Destroy(gameObject);
        }
    }
}