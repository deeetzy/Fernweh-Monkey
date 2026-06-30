using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoseMenu : MonoBehaviour
{
    public Slider progressBar; 

    public void ShowProgress(float currentBossHP, float maxBossHP)
    {
        LevelAudioManager.Instance.musicSource.Stop();
        LevelAudioManager.Instance.loopsSource.volume = 0.3f;
        LevelAudioManager.Instance.PlaySFX(LevelAudioManager.Instance.monkeyDefeat);

        float progress = 1f - (currentBossHP / maxBossHP);
        progressBar.value = progress;
    }

    public void Restart()
    {
        LevelAudioManager.Instance.PlayOnClickSound();
        DDA_Agent ddaAgent = Object.FindFirstObjectByType<DDA_Agent>();

        if (ddaAgent != null)
        {
            ddaAgent.ResetDifficultyOnPlayerRestart();
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMenu()
    {
        DDA_BulletproofExporter.ExportEvent("IESIRE_MENIU");
        Time.timeScale = 1f;
        SceneManager.LoadScene("MENU");
    }
}