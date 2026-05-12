using UnityEngine;
using UnityEngine.InputSystem; // Necesar pentru noul sistem
using System.Collections;

public class StartScreenController : MonoBehaviour
{
    [Header("Grupuri de Canvas")]
    public CanvasGroup PressKey; // Trage aici StartScreenPanel
    public CanvasGroup mainMenuGroup;    // Trage aici MainMenuPanel

    [Header("Setări")]
    public float fadeDuration = 1.0f;    // Durata tranziției în secunde
    private bool isTransitioning = false;

    void Update()
    {
        if (!isTransitioning && (Keyboard.current.anyKey.wasPressedThisFrame || Pointer.current.press.wasPressedThisFrame))
        {
            StartCoroutine(FadeTransition());
        }
    }

    IEnumerator FadeTransition()
    {
        isTransitioning = true;
        float counter = 0f;

        // Activăm panoul de meniu (chiar dacă e încă invizibil/Alpha 0)
        mainMenuGroup.gameObject.SetActive(true);
        mainMenuGroup.alpha = 0;
        mainMenuGroup.interactable = false; // Nu lăsăm click-uri în timpul fade-ului

        while (counter < fadeDuration)
        {
            counter += Time.deltaTime;
            float alphaValue = counter / fadeDuration;

            // Start Screen dispare (1 -> 0)
            if (PressKey != null)
                PressKey.alpha = 1 - alphaValue;

            // Main Menu apare (0 -> 1)
            if (mainMenuGroup != null)
                mainMenuGroup.alpha = alphaValue;

            yield return null;
        }

        // Finalizăm tranziția
        if (PressKey != null) PressKey.gameObject.SetActive(false);

        mainMenuGroup.alpha = 1;
        mainMenuGroup.interactable = true;
        mainMenuGroup.blocksRaycasts = true;

        this.enabled = false;
    }
}