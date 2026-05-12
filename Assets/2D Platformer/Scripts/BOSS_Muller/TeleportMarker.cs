using UnityEngine;

public class TeleportMarker : MonoBehaviour
{
    public float duration = 2f;
    private float timer;
    private BossController boss;

    public void Setup(BossController bossRef)
    {
        boss = bossRef;
        timer = duration;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        // Optional: Make it blink faster as time runs out
        float blink = Mathf.PingPong(Time.time * 10f, 1);
        GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, blink);

        if (timer <= 0)
        {
            boss.ExecuteTeleport(transform.position);
            Destroy(gameObject);
        }
    }
}