using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.EventSystems;
using System.IO;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuButtonsPanel;
    public GameObject optionsPanel;
    public GameObject saveSelectionPanel;
    public GameObject nameInputPanel;

    [Header("UI Elements")]
    public TextMeshProUGUI[] slotTexts;
    public TMP_InputField nameInputField;

    [Header("Loading Screen")]
    public GameObject loadingScreen;
    public CanvasGroup loadingCanvasGroup;

    [Header("Input Actions")]
    public InputAction EscAction;
    public InputAction SubmitAction;

    private int selectedSlotToCreate;

    // --- FLAG-ul DE SECURITATE ---
    private bool isExitingPanel = false;

    [System.Serializable]
    public class SaveData
    {
        public string playerName;
        public int lastCompletedStage;
        public float completionPercent;
        public string lastSaveDate;
    }

    void OnEnable()
    {
        EscAction.Enable();
        SubmitAction.Enable();
    }

    void OnDisable()
    {
        EscAction.Disable();
        SubmitAction.Disable();
    }

    void Update()
    {
        // 1. Verificăm Esc
        if (EscAction.triggered)
        {
            if (nameInputPanel.activeSelf)
            {
                BackFromNameInput();
                return;
            }
            if (saveSelectionPanel.activeSelf)
            {
                CloseSaveSelection();
                return;
            }
            if (optionsPanel.activeSelf)
            {
                CloseOptions();
                return;
            }
        }

        // 2. Verificăm Enter (Submit)
        // Adăugăm condiția !isExitingPanel ca să fim siguri că nu pornește dacă tocmai am ieșit cu Esc
        if (SubmitAction.triggered && nameInputPanel.activeSelf && !isExitingPanel)
        {
            OnNameInputSubmit(nameInputField.text);
        }
    }

    public void BackFromNameInput()
    {
        isExitingPanel = true; // „Încuiem” ușa
        StopAllCoroutines();

        nameInputPanel.SetActive(false);
        saveSelectionPanel.SetActive(true);
        nameInputField.text = "";

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        // „Descuiem” ușa după o fracțiune de secundă (0.1s)
        Invoke("ResetExitFlag", 0.1f);
    }

    private void ResetExitFlag()
    {
        isExitingPanel = false;
    }

    private string GetSavePath(int index)
    {
        return Path.Combine(Application.dataPath, "Saves", "save_" + index + ".json");
    }

    public void RefreshUI()
    {
        for (int i = 0; i < 3; i++)
        {
            string filePath = GetSavePath(i);
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                slotTexts[i].text = data.playerName.ToUpper();
            }
            else
            {
                slotTexts[i].text = "< EMPTY >";
            }
        }
    }

    public void SelectSlot(int slotIndex)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        string filePath = GetSavePath(slotIndex);

        if (File.Exists(filePath))
        {
            PlayerPrefs.SetInt("ActiveSlot", slotIndex);
            saveSelectionPanel.SetActive(false);
            StartCoroutine(LoadLevelAsync("PolizaiMuller"));
        }
        else
        {
            selectedSlotToCreate = slotIndex;
            saveSelectionPanel.SetActive(false);
            nameInputPanel.SetActive(true);
            nameInputField.text = "";
            nameInputField.ActivateInputField();
        }
    }

    public void OnNameInputSubmit(string pName)
    {
        // DACĂ tocmai ieșim din panel, ignorăm comanda
        if (isExitingPanel) return;

        if (string.IsNullOrWhiteSpace(pName)) pName = "Monkey";

        SaveData newData = new SaveData();
        newData.playerName = pName;
        newData.lastCompletedStage = 1;
        newData.lastSaveDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        string json = JsonUtility.ToJson(newData, true);
        string filePath = GetSavePath(selectedSlotToCreate);

        string directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        File.WriteAllText(filePath, json);
        PlayerPrefs.SetInt("ActiveSlot", selectedSlotToCreate);

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
        nameInputPanel.SetActive(false);
        StartCoroutine(LoadLevelAsync("PolizaiMuller"));
    }

    // --- RESTUL FUNCȚIILOR RĂMÂN NESCHIMBATE ---
    public void DeleteSlot(int slotIndex)
    {
        string filePath = GetSavePath(slotIndex);
        if (File.Exists(filePath))
        {
            string directory = Path.GetDirectoryName(filePath);
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string newPath = Path.Combine(directory, "archived_" + slotIndex + "_" + timestamp + ".deleted");
            File.Move(filePath, newPath);
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
        RefreshUI();
    }

    public void OpenSaveSelection() { mainMenuButtonsPanel.SetActive(false); saveSelectionPanel.SetActive(true); RefreshUI(); }
    public void CloseSaveSelection() { saveSelectionPanel.SetActive(false); mainMenuButtonsPanel.SetActive(true); }

    IEnumerator LoadLevelAsync(string sceneName)
    {
        // 1. Apelăm fade-ul muzicii folosind scriptul tău existent
        MenuAudioManager audioMan = Object.FindFirstObjectByType<MenuAudioManager>();
        if (audioMan != null)
        {
            audioMan.StartLoadingFade();
        }

        // 2. Ne asigurăm că toate panourile de meniu sunt închise înainte de loading
        saveSelectionPanel.SetActive(false);
        if (nameInputPanel != null) nameInputPanel.SetActive(false);
        if (mainMenuButtonsPanel != null) mainMenuButtonsPanel.SetActive(false);

        // 3. Afișăm ecranul de loading
        loadingScreen.SetActive(true);
        loadingCanvasGroup.alpha = 1;

        // 4. Pornim încărcarea scenei în fundal
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        float timer = 0;
        // 5. Așteptăm cele 5 secunde (pentru animație/atmosferă) și încărcarea tehnică
        while (timer < 5f || operation.progress < 0.9f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 6. Activăm scena nouă
        operation.allowSceneActivation = true;

        // 7. Așteptăm până când scena este gata complet
        while (!operation.isDone)
        {
            yield return null;
        }

        // 8. Dezactivăm ecranul de loading (după ce am trecut în scena nouă)
        loadingScreen.SetActive(false);
    }

    public void OpenOptions() { mainMenuButtonsPanel.SetActive(false); optionsPanel.SetActive(true); }
    public void CloseOptions() { optionsPanel.SetActive(false); mainMenuButtonsPanel.SetActive(true); }
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit(); 
#endif
    }
}