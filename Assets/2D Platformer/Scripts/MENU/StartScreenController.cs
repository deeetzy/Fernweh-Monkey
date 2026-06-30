using UnityEngine;
using UnityEngine.InputSystem; 
using System.Collections;

public class StartScreenController : MonoBehaviour
{
    [Header("Grupuri de Canvas")]
    public CanvasGroup PressKey; 
    public CanvasGroup mainMenuGroup;   

    [Header("Setări")]
    public float fadeDuration = 1.0f;   
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

        mainMenuGroup.gameObject.SetActive(true);
        mainMenuGroup.alpha = 0;
        mainMenuGroup.interactable = false;

        while (counter < fadeDuration)
        {
            counter += Time.deltaTime;
            float alphaValue = counter / fadeDuration;

            if (PressKey != null)
                PressKey.alpha = 1 - alphaValue;

            if (mainMenuGroup != null)
                mainMenuGroup.alpha = alphaValue;

            yield return null;
        }

        if (PressKey != null) PressKey.gameObject.SetActive(false);

        mainMenuGroup.alpha = 1;
        mainMenuGroup.interactable = true;
        mainMenuGroup.blocksRaycasts = true;

        this.enabled = false;
    }
}