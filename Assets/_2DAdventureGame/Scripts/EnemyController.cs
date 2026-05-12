using UnityEngine;

public class EnemyController : MonoBehaviour
{
    //public var
    public float speed = 3.0f;
    public bool vertical;
    public float changeTime = 3.0f;
    public bool isBroken { get { return broken; } }

    //effects
    public ParticleSystem smokeParticleEffect;
    public ParticleSystem fixedParticleEffect;

    //private var
    Rigidbody2D rigidbody2d;
    Animator animator;
    float timer = 0;
    int direction = -1;
    bool broken = true;

    //Audio
    AudioSource audioSource;
    public AudioClip EnemyFixed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rigidbody2d = GetComponent<Rigidbody2D>();
        timer = changeTime;
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        fixedParticleEffect.Stop();
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        if(timer < 0)
        {
            int j = Random.Range(0, 2);
            if (j == 1) direction = 1;
            else direction = -1;

            int i = Random.Range(0, 2);
            if (i == 1) vertical = true;
            else vertical = false;

            timer = changeTime;
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!broken) return;


        Vector2 position = rigidbody2d.position;

        if (vertical)
        {
            position.y = position.y + speed * direction * Time.deltaTime;
            animator.SetFloat("Move X", 0);
            animator.SetFloat("Move Y", direction);
        }
        else
        {
            position.x = position.x + speed * direction * Time.deltaTime;
            animator.SetFloat("Move Y", 0);
            animator.SetFloat("Move X", direction);
        }

        rigidbody2d.MovePosition(position);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.gameObject.GetComponent<PlayerController>();

        if (player != null)
        {
            player.ChangeHealth(-1);
        }
    }

    public void Fix()
    {
        broken = false;
        rigidbody2d.simulated = false;
        animator.SetTrigger("Fixed");
        audioSource.Stop();
        audioSource.PlayOneShot(EnemyFixed);
        smokeParticleEffect.Stop();
        fixedParticleEffect.Play();
    }
}
