using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; 
using UnityEngine.EventSystems;

public class PauseManager : MonoBehaviour
{
    public static bool isPaused = false;
    public GameObject pauseMenuUI;
    public GameObject optionsMenuUI;
    public GameObject loseMenuUI;

    public InputAction PauseAction;

    void OnEnable()
    {
        PauseAction.Enable();
    }

    void OnDisable()
    {
        PauseAction.Disable();
    }

    void Update()
    {
        if (loseMenuUI != null && loseMenuUI.activeSelf)
        {
            return; 
        }

        if (PauseAction.triggered)
        {
            if (optionsMenuUI.activeSelf)
            {
                CloseOptions();
            }
            else if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Resume()
    {
        LevelAudioManager.Instance.PlayOnClickSound();
        LevelAudioManager.Instance.SetMenuAtmosphere(false);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        pauseMenuUI.SetActive(false);
        optionsMenuUI.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
    }

    void Pause()
    {
        pauseMenuUI.SetActive(true);
        LevelAudioManager.Instance.SetMenuAtmosphere(true);

        Animator[] childAnimators = pauseMenuUI.GetComponentsInChildren<Animator>();
        foreach (Animator anim in childAnimators)
        {
            anim.ResetTrigger("Highlighted"); 
            anim.ResetTrigger("Normal");

            anim.Play("Normal", 0, 0f);
            anim.Update(0f);
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        Time.timeScale = 0f;
        isPaused = true;
    }

    public void OpenOptions()
    {
        LevelAudioManager.Instance.PlayOnClickSound();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        pauseMenuUI.SetActive(false);
        optionsMenuUI.SetActive(true);
    }

    public void CloseOptions()
    {
        optionsMenuUI.SetActive(false);

        pauseMenuUI.SetActive(true);
    }

    public void Restart()
    {
        LevelAudioManager.Instance.PlayOnClickSound();
        DDA_DataCollector.Instance.deathsInCurrentStage++;

        if (LevelAudioManager.Instance != null)
        {
            LevelAudioManager.Instance.SetMenuAtmosphere(false);
        }

        Time.timeScale = 1f; 
        isPaused = false;  

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        DDA_BulletproofExporter.ExportEvent("IESIRE_MENIU");
        SceneManager.LoadScene("MENU");
    }
}