using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections; 

public class ReturnToMenu : MonoBehaviour
{
    [Header("Transition Settings")]
    public GameObject loadingScreen; 
    public float minLoadingTime = 3f; 

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        DDA_BulletproofExporter.ExportEvent("IESIRE_MENIU");

        if (LevelAudioManager.Instance != null)
        {
            LevelAudioManager.Instance.PlayOnClickSound();
            LevelAudioManager.Instance.SetMenuAtmosphere(false);
        }

        StartCoroutine(LoadMenuRoutine());
    }

    private IEnumerator LoadMenuRoutine()
    {
        if (LevelAudioManager.Instance != null)
        {
            LevelAudioManager.Instance.musicSource.Stop();

            LevelAudioManager.Instance.PlayBossSFX(null);

            AudioSource[] allSources = LevelAudioManager.Instance.GetComponents<AudioSource>();
            foreach (AudioSource source in allSources)
            {
                if (source != LevelAudioManager.Instance.loopsSource)
                {
                    source.Stop();
                }
            }

            LevelAudioManager.Instance.loopsSource.volume = 0.3f;
        }

        Movement player = Object.FindFirstObjectByType<Movement>();
        if (player != null)
        {
            player.enabled = false;
            player.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        if (loadingScreen != null)
        {
            loadingScreen.SetActive(true);
            loadingScreen.transform.SetAsLastSibling();
        }

        if (LevelAudioManager.Instance != null)
        {
            LevelAudioManager.Instance.SetMenuAtmosphere(false);
        }

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