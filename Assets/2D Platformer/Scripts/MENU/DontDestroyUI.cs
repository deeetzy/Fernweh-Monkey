using UnityEngine;

public class DontDestroyUI : MonoBehaviour
{
    void Awake()
    {
        // Această linie spune Unity: "Nu șterge acest Canvas când schimbi scena"
        DontDestroyOnLoad(gameObject);
    }
}