using UnityEngine;
using System.Collections;

public class PigeonEnemy : MonoBehaviour
{
    private enum PigeonState { Flying, Warning, Diving, Rising }
    private PigeonState currentState = PigeonState.Flying;

    [Header("Setări Zbor")]
    public float flyingSpeed = 8f;
    public float diveSpeed = 18f;
    public float triggerDistance = 14f;
    public float warningTime = 0.6f;

    private Transform player;
    private Vector3 memorizedTarget;
    private Vector3 diveDirection;
    private Vector3 riseDirection;

    private SpriteRenderer sr;
    private Vector3 flightDirection; // NOU: În ce direcție zboară inițial?

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        sr = GetComponentInChildren<SpriteRenderer>();

        // NOU: Unde ne aflăm față de maimuță?
        if (transform.position.x < player.position.x)
        {
            // Am apărut în stânga, zburăm spre dreapta
            flightDirection = Vector3.right;
            if (sr != null) sr.flipX = true; // Întoarce sprite-ul (dacă desenul original se uită la stânga)
        }
        else
        {
            // Am apărut în dreapta, zburăm spre stânga
            flightDirection = Vector3.left;
            if (sr != null) sr.flipX = false;
        }
    }

    void Update()
    {
        switch (currentState)
        {
            case PigeonState.Flying:
                // Zboară în direcția corectă calculată la Start
                transform.Translate(flightDirection * flyingSpeed * Time.deltaTime, Space.World);

                // NOU: Verificăm DOAR distanța absolută, indiferent din ce parte vine
                if (player != null && Mathf.Abs(transform.position.x - player.position.x) < triggerDistance)
                {
                    StartCoroutine(WarningRoutine());
                }
                break;

            case PigeonState.Warning:
                break;

            case PigeonState.Diving:
                transform.Translate(diveDirection * diveSpeed * Time.deltaTime, Space.World);
                if (transform.position.y <= memorizedTarget.y) currentState = PigeonState.Rising;
                break;

            case PigeonState.Rising:
                transform.Translate(riseDirection * diveSpeed * Time.deltaTime, Space.World);
                break;
        }

        // Curățenie lărgită: Îi distrugem dacă se duc prea departe în ORICE direcție
        if (Mathf.Abs(transform.position.x - Camera.main.transform.position.x) > 40f || transform.position.y > 15f)
        {
            Destroy(gameObject);
        }
    }

    IEnumerator WarningRoutine()
    {
        currentState = PigeonState.Warning;
        if (sr != null) sr.color = Color.red;
        yield return new WaitForSeconds(warningTime);
        if (sr != null) sr.color = Color.white;

        if (player != null)
        {
            memorizedTarget = player.position;
            memorizedTarget.y -= 0.5f;
            diveDirection = (memorizedTarget - transform.position).normalized;
            riseDirection = new Vector3(diveDirection.x, -diveDirection.y, 0).normalized;

            // Întoarcem pasărea să se uite pe direcția picajului
            if (sr != null) sr.flipX = (diveDirection.x > 0);
        }

        currentState = PigeonState.Diving;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) other.GetComponent<Movement>()?.TakeDamage();
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player")) collision.gameObject.GetComponent<Movement>()?.TakeDamage();
    }
}