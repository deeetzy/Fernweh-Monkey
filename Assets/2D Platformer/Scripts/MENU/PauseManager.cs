using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // 1. Adaugă neapărat acest namespace
using UnityEngine.EventSystems;

public class PauseManager : MonoBehaviour
{
    public static bool isPaused = false;
    public GameObject pauseMenuUI;
    public GameObject optionsMenuUI;
    public GameObject loseMenuUI;

    // 2. Definim acțiunea de pauză pentru noul sistem
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
        // VERIFICARE CRITICĂ: Dacă ecranul de moarte e activ, blocăm orice comandă de pauză/resume
        if (loseMenuUI != null && loseMenuUI.activeSelf)
        {
            return; // Ieșim imediat, restul codului de mai jos nu se execută
        }

        if (PauseAction.triggered)
        {
            // Logica ta existentă de priorități...
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

        // Punem muzica pe pauză
        LevelAudioManager.Instance.SetMenuAtmosphere(true);

        // RESETARE VIZUALĂ BUTOANE:
        // Căutăm toate componentele Animator din interiorul meniului de pauză
        Animator[] childAnimators = pauseMenuUI.GetComponentsInChildren<Animator>();
        foreach (Animator anim in childAnimators)
        {
            // Resetăm parametrii (dacă ai Trigger-e sau Bools pentru Hover)
            anim.ResetTrigger("Highlighted"); // Schimbă numele dacă ai altul în Animator
            anim.ResetTrigger("Normal");

            // Forțăm starea "Normal" să ruleze de la început (frame 0)
            // "Normal" trebuie să fie numele stării tale de bază din Animator
            anim.Play("Normal", 0, 0f);
            anim.Update(0f);
        }

        // Deselectăm orice obiect pentru a preveni "Stuck Highlight"
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

        // 1. Ascundem meniul de pauză
        pauseMenuUI.SetActive(false);

        // 2. Afișăm opțiunile
        optionsMenuUI.SetActive(true);
    }

    public void CloseOptions()
    {
        // 1. Închidem opțiunile
        optionsMenuUI.SetActive(false);

        // 2. Revenim la meniul de pauză
        pauseMenuUI.SetActive(true);
    }

    public void Restart()
    {
        LevelAudioManager.Instance.PlayOnClickSound();

        // --- REPARARE BUG MUFFLED ---
        // Revenim la sunetul clar imediat, înainte de restart
        if (LevelAudioManager.Instance != null)
        {
            LevelAudioManager.Instance.SetMenuAtmosphere(false);
        }

        Time.timeScale = 1f; // Ne asigurăm că timpul revine la normal
        isPaused = false;    // Resetăm variabila de stare

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        // Pune aici numele exact al scenei tale de meniu
        SceneManager.LoadScene("MENU");
    }
}