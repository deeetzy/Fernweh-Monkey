using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections; // Necesar pentru Corutine

public class ReturnToMenu : MonoBehaviour
{
    [Header("Transition Settings")]
    public GameObject loadingScreen; // Trage Prefab-ul de Loading Screen aici în Inspector
    public float minLoadingTime = 3f; // Câte secunde să stea maimuța pe ecran

    public void LoadMenu()
    {
        // 1. Resetăm starea jocului
        Time.timeScale = 1f;

        if (LevelAudioManager.Instance != null)
        {
            LevelAudioManager.Instance.PlayOnClickSound();
            LevelAudioManager.Instance.SetMenuAtmosphere(false);
        }

        // 2. Pornim procesul de încărcare asincron
        StartCoroutine(LoadMenuRoutine());
    }

    private IEnumerator LoadMenuRoutine()
    {
        // --- CURĂȚENIE AUDIO SELECTIVĂ ---
        if (LevelAudioManager.Instance != null)
        {
            // 1. Oprim muzica de fundal imediat
            LevelAudioManager.Instance.musicSource.Stop();

            // 2. Oprim sursele de luptă/voci ca să nu mai auzim pumni sau replici
            // Folosim Stop() direct pe sursele create în LevelAudioManager
            // Acestea sunt variabilele pe care le-ai definit ca AddComponent în Awake-ul de acolo

            // Căutăm sursele prin acces direct (dacă sunt publice) sau prin referință
            // Notă: playerSource, bossSource și combatSource sunt cele care fac zgomot în luptă

            // Dacă variabilele din LevelAudioManager sunt private, va trebui să le pui 
            // LevelAudioManager.Instance.StopAllCombatSounds(); (vezi mai jos cum facem asta)

            // Pentru moment, forțăm oprirea pe sursele principale:
            LevelAudioManager.Instance.PlayBossSFX(null); // O metodă de a reseta dacă e nevoie

            // O variantă sigură pentru a opri sursele AddComponent-uite din LevelAudioManager:
            AudioSource[] allSources = LevelAudioManager.Instance.GetComponents<AudioSource>();
            foreach (AudioSource source in allSources)
            {
                // OPRIM TOT, MAI PUȚIN loopsSource (unde stă fâșâitul)
                if (source != LevelAudioManager.Instance.loopsSource)
                {
                    source.Stop();
                }
            }

            // Ne asigurăm că fâșâitul se aude bine (poate îi creștem puțin volumul pentru loading)
            LevelAudioManager.Instance.loopsSource.volume = 0.3f;
        }

        // --- PROTECȚIE ÎMPOTRIVA DAMAGE-ULUI ---
        Movement player = Object.FindFirstObjectByType<Movement>();
        if (player != null)
        {
            player.enabled = false;
            player.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        // --- VIZUAL LOADING ---
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(true);
            loadingScreen.transform.SetAsLastSibling();
        }

        if (LevelAudioManager.Instance != null)
        {
            LevelAudioManager.Instance.SetMenuAtmosphere(false);
        }

        // --- ÎNCĂRCARE SCENĂ ---
        AsyncOperation operation = SceneManager.LoadSceneAsync("MENU");
        operation.allowSceneActivation = false;

        float timer = 0;
        while (timer < minLoadingTime || operation.progress < 0.9f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        operation.allowSceneActivation = true;
    }
}