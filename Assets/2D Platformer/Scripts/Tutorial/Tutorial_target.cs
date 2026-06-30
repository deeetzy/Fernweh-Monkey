using UnityEngine;
using System.Collections;

public class TutorialTarget : MonoBehaviour
{
    public int health = 3;
    public float flickerDuration = 0.1f;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isFlickering = false;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Banana") && !isFlickering && health > 0)
        {
            health--;

            Destroy(collision.gameObject);

            if (health <= 0)
            {
                StartCoroutine(DeathSequence());
            }
            else
            {
                StartCoroutine(FlickerEffect());
            }
        }
    }

    IEnumerator FlickerEffect()
    {
        isFlickering = true;

        for (int i = 0; i < 2; i++)
        {
            spriteRenderer.color = Color.gray;
            yield return new WaitForSeconds(flickerDuration);
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(flickerDuration);
        }

        spriteRenderer.color = originalColor;
        isFlickering = false;
    }

    IEnumerator DeathSequence()
    {
        isFlickering = true;
        spriteRenderer.color = Color.gray;
        yield return new WaitForSeconds(0.3f);

        gameObject.SetActive(false);
    }
}