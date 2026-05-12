using UnityEngine;
using System.Collections;

public class SleepingCat : MonoBehaviour
{
    [Header("Setări Viață")]
    public int health = 2;

    private SpriteRenderer sr;

    void TakeDamage()
    {
        health--;

        // Feedback vizual: clipește roșu când e lovit
        StartCoroutine(FlashRed());

        if (health <= 0)
        {
            Die();
        }
    }

    IEnumerator FlashRed()
    {
        if (sr != null)
        {
            sr.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            sr.color = Color.white;
        }
    }

    void Die()
    {
        StopAllCoroutines();
        // Aici poți pune un sunet sau particule de praf
        Debug.Log("Pisica a fugit!");
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. Dacă Maimuța sare fix pe pisică, maimuța ia damage
        if (other.CompareTag("Player"))
        {
            other.GetComponent<Movement>()?.TakeDamage();
        }
        // 2. Dacă Maimuța o nimerește cu glonțul (folosind tag-ul tău!)
        else if (other.CompareTag("Banana"))
        {
            Debug.Log("Miau! Pisica a fugit!");

            // Distrugem glonțul ca să nu treacă prin ea
            Destroy(other.gameObject);

            // Aici poți adăuga un efect de particule sau un sunet de "Meow"

            // Distrugem pisica (fuge de pe ecran)
            TakeDamage();
        }
    }
}