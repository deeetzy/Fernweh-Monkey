using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class PhysicsButtonFinish : MonoBehaviour
{
    [Header("UI Loading Screen (Din scena Tutorial)")]
    public GameObject loadingScreen;
    public CanvasGroup loadingCanvasGroup;

    private Animator anim;
    private bool isTransitioning = false;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isTransitioning || !collision.CompareTag("Player")) return;

        Debug.Log("[TUTORIAL COMPLETED] Maimuța a atins FINISH! Trecem la Main Menu...");
        StartCoroutine(LoadMainMenuAsync());
    }

    IEnumerator LoadMainMenuAsync()
    {
        isTransitioning = true;

        if (anim != null) anim.Play("Pressed");

        if (loadingScreen != null)
        {
            loadingScreen.SetActive(true);
            if (loadingCanvasGroup != null) loadingCanvasGroup.alpha = 1f;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync("MENU");
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