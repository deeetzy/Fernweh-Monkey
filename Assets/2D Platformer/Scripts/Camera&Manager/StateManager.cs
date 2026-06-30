using UnityEngine;
using System.Collections;

public class StageManager : MonoBehaviour
{
    [Header("Referinte Personaje")]
    public GameObject monkey;
    public GameObject bikeProp;
    public BossController mullerScript;
    public GameObject bossSplashScreen;

    [Header("Setari Lume")]
    public GameObject invisibleWalls;
    public GameObject invisibleWalls1;
    public float groundY = -5.3f;
    public MonoBehaviour monkeyControlScript;

    [Header("Ending UI")]
    public GameObject gameEndScreen;
    public CanvasGroup endScreenCanvasGroup;

    private Animator monkeyAnim;

    void Start()
    {
        monkeyAnim = monkey.GetComponent<Animator>();

        if (invisibleWalls != null) invisibleWalls.SetActive(false);
        if (monkeyControlScript != null) monkeyControlScript.enabled = false;

        StartCoroutine(FullIntroSequence());
    }

    IEnumerator FullIntroSequence()
    {
        if (monkeyControlScript != null) monkeyControlScript.enabled = false;

        LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.bikeSound, 0.4f);
        monkeyAnim.Play("Monkey_BikeRide", 0, 0f);
        while (monkey.transform.position.x < -3f)
        {
            monkey.transform.position += Vector3.right * 4f * Time.deltaTime;
            yield return null;
        }

        mullerScript.StartWhistleSequence();

        if (bossSplashScreen != null)
        {
            bossSplashScreen.SetActive(true);
            Transform textTransform = bossSplashScreen.transform;

            Vector3 targetPos = new Vector3(0f, 0f, 0f);
            Vector3 startPos = new Vector3(3.5f, 0f, 0f);

            float t = 0;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                textTransform.localPosition = Vector3.Lerp(startPos, targetPos, t / 0.5f);
                yield return null;
            }
            textTransform.localPosition = targetPos;
        }

        LevelAudioManager.Instance.StopLoop();
        LevelAudioManager.Instance.PlaySFX(LevelAudioManager.Instance.bikeRing);

        monkeyAnim.Play("Monkey_BikeIntro", 0, 0f);

        if (bikeProp != null)
        {
            bikeProp.SetActive(true);
            bikeProp.transform.position = new Vector3(monkey.transform.position.x, groundY, 0);
        }
        yield return new WaitForSeconds(2.5f);

        if (bossSplashScreen != null)
        {
            bossSplashScreen.SetActive(false);
        }

        yield return new WaitForSeconds(1.0f);

        if (bossSplashScreen != null)
        {
            bossSplashScreen.SetActive(false);
        }

        if (invisibleWalls != null) invisibleWalls.SetActive(true);
        monkeyAnim.Play("Monkey_Idle", 0, 0f);
        if (monkeyControlScript != null) monkeyControlScript.enabled = true;
    }

    public void OnBossDefeated()
    {
        if (invisibleWalls1 != null) invisibleWalls1.SetActive(false);
        if (monkeyControlScript != null) monkeyControlScript.enabled = false;

        Movement playerMovement = monkey.GetComponent<Movement>();
        if (playerMovement != null)
        {
            Physics2D.IgnoreLayerCollision(6, 7, true);

            System.Reflection.FieldInfo invincibleField = typeof(Movement).GetField("isInvincible", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (invincibleField != null) invincibleField.SetValue(playerMovement, true);
        }

        Debug.Log("[STAGE CLEANUP] Toate pericolele au fost eliminate. Maimuța este în siguranță!");

        StartCoroutine(EndingSequence());
    }

    IEnumerator EndingSequence()
    {
        monkeyAnim.SetBool("isGrounded", true);
        monkeyAnim.Play("Monkey_Idle", 0, 0f);
        yield return new WaitForSeconds(1f);

        Rigidbody2D monkeyRb = monkey.GetComponent<Rigidbody2D>();

        if (monkeyRb != null && monkey.transform.position.y > groundY + 0.1f)
        {
            monkeyRb.bodyType = RigidbodyType2D.Dynamic;
            monkeyRb.gravityScale = 3f;

            while (monkey.transform.position.y > groundY + 0.05f)
            {
                yield return null;
            }

            monkeyRb.bodyType = RigidbodyType2D.Kinematic;
            monkeyRb.linearVelocity = Vector2.zero;
            monkey.transform.position = new Vector3(monkey.transform.position.x, groundY, 0);

            LevelAudioManager.Instance.PlayPlayerSFX(LevelAudioManager.Instance.monkeyFall, 0.5f);
            yield return new WaitForSeconds(0.5f);
        }

        monkeyAnim.Play("Monkey_Run", 0, 0f);

        float targetX = bikeProp.transform.position.x;
        float scaleX = Mathf.Abs(monkey.transform.localScale.x);

        float lookDir = targetX > monkey.transform.position.x ? scaleX : -scaleX;
        monkey.transform.localScale = new Vector3(lookDir, monkey.transform.localScale.y, monkey.transform.localScale.z);

        while (Mathf.Abs(monkey.transform.position.x - targetX) > 0.1f)
        {
            float direction = targetX > monkey.transform.position.x ? 1 : -1;
            monkey.transform.position += new Vector3(direction * 2f * Time.deltaTime, 0, 0);
            yield return null;
        }

        monkey.transform.position = new Vector3(targetX, groundY, 0);
        monkeyAnim.Play("Monkey_BikeOutro", 0, 0f);
        yield return new WaitForSeconds(0.5f);
        LevelAudioManager.Instance.PlaySFX(LevelAudioManager.Instance.bikeRing);

        if (bikeProp != null) bikeProp.SetActive(false);

        monkey.transform.localScale = new Vector3(scaleX, monkey.transform.localScale.y, monkey.transform.localScale.z);
        monkeyAnim.Play("Monkey_BikeRide", 0, 0f);
        LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.bikeSound, 0.5f);

        float exitTimer = 0;
        while (monkey.transform.position.x < 15f && exitTimer < 5f)
        {
            exitTimer += Time.deltaTime;
            monkey.transform.position += Vector3.right * 5f * Time.deltaTime;
            yield return null;
        }
        LevelAudioManager.Instance.StopLoop();
        DDA_BulletproofExporter.ExportEvent("VICTORIE_BOSS");

        int activeSlot = PlayerPrefs.GetInt("ActiveSlot", 0);
        PlayerPrefs.SetInt("SaveSlot_" + activeSlot, 2);
        PlayerPrefs.Save();

        if (gameEndScreen != null)
        {
            gameEndScreen.SetActive(true);
            if (endScreenCanvasGroup != null) StartCoroutine(FadeInEnd(endScreenCanvasGroup));
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MenuSceneName");
        }
    }

    IEnumerator FadeInEnd(CanvasGroup cg)
    {
        cg.alpha = 0;
        while (cg.alpha < 1)
        {
            cg.alpha += Time.deltaTime;
            yield return null;
        }
    }
}