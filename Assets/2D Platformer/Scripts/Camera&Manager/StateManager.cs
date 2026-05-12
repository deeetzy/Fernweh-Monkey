using UnityEngine;
using System.Collections;
using UnityEngine.UIElements;

public class StageManager : MonoBehaviour
{
    [Header("Referinte Personaje")]
    public GameObject monkey;
    public GameObject bikeProp;
    public BossController mullerScript;
    public GameObject bossSplashScreen;

    [Header("Setari Lume")]
    public GameObject invisibleWalls;
    public GameObject invisibleWalls1; // Trage aici obiectul cu peretii arenei
    public float groundY = -5.3f;
    public MonoBehaviour monkeyControlScript;

    [Header("Ending UI")]
    public GameObject gameEndScreen; // Trage aici noul Panel din ierarhie
    public CanvasGroup endScreenCanvasGroup; // Optional pentru fade

    private Animator monkeyAnim;

    void Start()
    {
        monkeyAnim = monkey.GetComponent<Animator>();

        // La inceput, peretii sunt dezactivati pentru a permite intrarea lina
        if (invisibleWalls != null) invisibleWalls.SetActive(false);

        if (monkeyControlScript != null) monkeyControlScript.enabled = false;

        StartCoroutine(FullIntroSequence());
    }

    IEnumerator FullIntroSequence()
    {
        // 1. Dezactivăm controlul maimuței (blindăm intrarea)
        if (monkeyControlScript != null) monkeyControlScript.enabled = false;

        // 2. Maimuța intră în cadru
        LevelAudioManager.Instance.StartLoop(LevelAudioManager.Instance.bikeSound, 0.4f);
        monkeyAnim.Play("Monkey_BikeRide", 0, 0f);
        while (monkey.transform.position.x < -3f)
        {
            monkey.transform.position += Vector3.right * 4f * Time.deltaTime;
            yield return null;
        }

        // 3. Müller începe să scrie amenda (Whistle + Write animation)
        mullerScript.StartWhistleSequence();

        if (bossSplashScreen != null)
        {
            bossSplashScreen.SetActive(true);

            // Folosim Transform (care funcționează și pe UI și pe obiecte de joc)
            Transform textTransform = bossSplashScreen.transform;

            // Valorile de X depind de rezoluția ta; ajustează-le dacă e nevoie
            Vector3 targetPos = new Vector3(0f, 0f, 0f); // Poziția "pe ecran"
            Vector3 startPos = new Vector3(3.5f, 0f, 0f); // Poziția "afară din ecran"

            float t = 0;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                // Folosim localPosition pentru a fi siguri că nu e afectat de poziția Canvas-ului
                textTransform.localPosition = Vector3.Lerp(startPos, targetPos, t / 0.5f);
                yield return null;
            }
            textTransform.localPosition = targetPos;
        }

        LevelAudioManager.Instance.StopLoop();
        // --- SUNET: Un clin-clin de salut/oprire ---
        LevelAudioManager.Instance.PlaySFX(LevelAudioManager.Instance.bikeRing);

        // 5. Maimuța se dă jos de pe bicicletă în timp ce Müller scrie
        monkeyAnim.Play("Monkey_BikeIntro", 0, 0f);

        // Punem bicicleta pe jos
        if (bikeProp != null)
        {
            bikeProp.SetActive(true);
            bikeProp.transform.position = new Vector3(monkey.transform.position.x, groundY, 0);
        }
        yield return new WaitForSeconds(2.5f);
        // DISPARIȚIE SIMPLĂ
        if (bossSplashScreen != null)
        {
            bossSplashScreen.SetActive(false);
        }

        // Așteptăm să treacă timpul de intro (ex: cât durează scrisul amenzii)
        yield return new WaitForSeconds(1.0f);

        // 6. DISPARE TEXTUL și începe lupta
        if (bossSplashScreen != null)
        {
            bossSplashScreen.SetActive(false);
        }

        // Arena se închide și maimuța primește controlul
        if (invisibleWalls != null) invisibleWalls.SetActive(true);
        monkeyAnim.Play("Monkey_Idle", 0, 0f);
        if (monkeyControlScript != null) monkeyControlScript.enabled = true;
    }

    public void OnBossDefeated()
    {
        if (invisibleWalls1 != null) invisibleWalls1.SetActive(false);

        // BLOCĂM CONTROLUL: Dezactivăm scriptul de input al jucătorului
        if (monkeyControlScript != null) monkeyControlScript.enabled = false;

        StartCoroutine(EndingSequence());
    }

    IEnumerator EndingSequence()
    {
        // 1. OPRIM ANIMAȚIA DE RUN
        monkeyAnim.SetBool("isGrounded", true);
        monkeyAnim.Play("Monkey_Idle", 0, 0f);
        yield return new WaitForSeconds(1f);

        Rigidbody2D monkeyRb = monkey.GetComponent<Rigidbody2D>();

        if (monkeyRb != null && monkey.transform.position.y > groundY + 0.1f)
        {
            Debug.Log("Maimuța cade natural spre sol...");

            // Ne asigurăm că Rigidbody nu este Kinematic și are gravitație
            monkeyRb.bodyType = RigidbodyType2D.Dynamic;
            monkeyRb.gravityScale = 3f; // O cădere puțin mai rapidă/grea pentru "feel"

            // Așteptăm până când viteza pe Y este aproape zero și poziția e aproape de groundY
            // SAU poți folosi verificarea ta de isGrounded dacă scriptul permite
            while (monkey.transform.position.y > groundY + 0.05f)
            {
                yield return null;
            }

            // Fixăm poziția și înghețăm fizica pentru a începe mersul controlat
            monkeyRb.bodyType = RigidbodyType2D.Kinematic;
            monkeyRb.linearVelocity = Vector2.zero;
            monkey.transform.position = new Vector3(monkey.transform.position.x, groundY, 0);

            // Sunet de aterizare
            LevelAudioManager.Instance.PlayPlayerSFX(LevelAudioManager.Instance.monkeyFall, 0.5f);
            yield return new WaitForSeconds(0.5f);
        }

        // --- MERS SPRE BICICLETĂ (Ignorăm Y pentru precizie) ---
        monkeyAnim.Play("Monkey_Run", 0, 0f); // Punem animația de mers

        float targetX = bikeProp.transform.position.x;
        float scaleX = Mathf.Abs(monkey.transform.localScale.x);

        // Orientare
        float lookDir = targetX > monkey.transform.position.x ? scaleX : -scaleX;
        monkey.transform.localScale = new Vector3(lookDir, monkey.transform.localScale.y, monkey.transform.localScale.z);

        // Mergem spre bicicletă până când diferența de X e mică
        while (Mathf.Abs(monkey.transform.position.x - targetX) > 0.1f)
        {
            float direction = targetX > monkey.transform.position.x ? 1 : -1;
            monkey.transform.position += new Vector3(direction * 2f * Time.deltaTime, 0, 0);
            yield return null;
        }

        // --- URCARE PE BICICLETĂ ---
        monkey.transform.position = new Vector3(targetX, groundY, 0); // O poziționăm fix
        monkeyAnim.Play("Monkey_BikeOutro", 0, 0f); // Animația de urcare
        yield return new WaitForSeconds(0.5f);
        LevelAudioManager.Instance.PlaySFX(LevelAudioManager.Instance.bikeRing);

        if (bikeProp != null) bikeProp.SetActive(false); // Dispare obiectul static de jos

        // --- PLECARE SPRE DREAPTA (THE END) ---
        // Forțăm privirea spre DREAPTA înainte de plecare
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
        // 1. Aflăm ce slot folosim acum
        int activeSlot = PlayerPrefs.GetInt("ActiveSlot", 0);

        // 2. Salvăm faptul că acest slot a ajuns la stadiul următor
        // De exemplu, dacă am bătut Stage 1, salvăm valoarea 2
        PlayerPrefs.SetInt("SaveSlot_" + activeSlot, 2);

        // 3. Forțăm scrierea pe disc
        PlayerPrefs.Save();

        if (gameEndScreen != null)
        {
            gameEndScreen.SetActive(true);
            // Dacă vrei un fade in rapid:
            if (endScreenCanvasGroup != null) StartCoroutine(FadeInEnd(endScreenCanvasGroup));
        }
        else
        {
            // Soluție de rezervă: dacă nu avem UI în scenă, mergem forțat înapoi în meniu
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