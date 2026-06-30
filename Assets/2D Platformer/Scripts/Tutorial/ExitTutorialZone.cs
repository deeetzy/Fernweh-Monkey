using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class ExitTutorialZone : MonoBehaviour
{
    [Header("UI Loading Screen")]
    public GameObject loadingScreen;
    public CanvasGroup loadingCanvasGroup;

    private bool isTransitioning = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && !isTransitioning)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            Debug.Log("[TUTORIAL] Jucătorul a ajuns la final. Mouse-ul a fost activat!");
        }
    }

    public void FinishTutorialAndReturnToMenu()
    {
        if (isTransitioning) return;

        StartCoroutine(LoadMainMenuAsync());
    }

    IEnumerator LoadMainMenuAsync()
    {
        isTransitioning = true;

        if (loadingScreen != null)
        {
            loadingScreen.SetActive(true);
            if (loadingCanvasGroup != null) loadingCanvasGroup.alpha = 1f;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync("MainMenu");
        operation.allowSceneActivation = false;

        float timer = 0f;
        while (timer < 5f || operation.progress < 0.9f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        operation.allowSceneActivation = true;
    }
}