using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Avem nevoie de asta pentru Slider

public class LoseMenu : MonoBehaviour
{
    public Slider progressBar; // Trage Slider-ul aici în Inspector

    public void ShowProgress(float currentBossHP, float maxBossHP)
    {
        // Oprim muzica de tot (cum ai avut tu)
        LevelAudioManager.Instance.musicSource.Stop();

        // EXTRĂ: Întărim fâșâitul de film pentru atmosferă
        LevelAudioManager.Instance.loopsSource.volume = 0.3f;

        // Punem sunetul de înfrângere
        LevelAudioManager.Instance.PlaySFX(LevelAudioManager.Instance.monkeyDefeat);

        float progress = 1f - (currentBossHP / maxBossHP);
        progressBar.value = progress;
    }

    public void Restart()
    {
        LevelAudioManager.Instance.PlayOnClickSound();

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        // Pune aici numele exact al scenei tale de meniu
        SceneManager.LoadScene("MENU");
    }
}