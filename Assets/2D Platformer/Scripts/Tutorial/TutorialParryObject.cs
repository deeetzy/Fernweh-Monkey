using UnityEngine;
using UnityEngine.InputSystem;

public class TutorialParryBall : MonoBehaviour
{
    [Header("Parry Settings")]
    public float parryBounceForce = 12f;
    public Color parryColor = new Color(1f, 0.4f, 0.9f);

    private SpriteRenderer sr;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = parryColor;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Movement playerMove = collision.GetComponent<Movement>();
            bool spacePressedNow = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

            if (playerMove != null && (playerMove.isDoubleJumping || spacePressedNow))
            {
                ExecuteTutorialParry(collision.gameObject, playerMove);
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Movement playerMove = collision.GetComponent<Movement>();
            bool spacePressedNow = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

            if (spacePressedNow || (playerMove != null && playerMove.isDoubleJumping))
            {
                ExecuteTutorialParry(collision.gameObject, playerMove);
            }
        }
    }

    private void ExecuteTutorialParry(GameObject player, Movement playerMove)
    {
        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, parryBounceForce);
        }

        if (playerMove != null)
        {
            playerMove.isDoubleJumping = false;  

            System.Reflection.FieldInfo field = typeof(Movement).GetField("hasParriedInAir", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(playerMove, false);
        }

        if (LevelAudioManager.Instance != null)
        {
            LevelAudioManager.Instance.PlaySFX(LevelAudioManager.Instance.monkeyParry);
        }
    }
}