using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

public class OptionsMenu : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioMixer mainMixer;
    public Slider musicSlider;
    public Slider voicesSlider;
    public Slider sfxSlider;

    [Header("DDA Settings")]
    public TextMeshProUGUI ddaStatusText;

    void Start()
    {
        // 1. Încărcăm valorile
        float mVol = PlayerPrefs.GetFloat("MusicVol", 0.75f);
        float vVol = PlayerPrefs.GetFloat("VoicesVol", 0.75f);
        float sVol = PlayerPrefs.GetFloat("SFXVol", 0.75f);

        // 2. Aplicăm valorile vizual pe Slidere
        musicSlider.value = mVol;
        voicesSlider.value = vVol;
        sfxSlider.value = sVol;

        // 3. !!! FOARTE IMPORTANT !!! 
        // Forțăm aplicarea volumului în Mixer chiar la pornire
        SetMusicVolume(mVol);
        SetVoicesVolume(vVol);
        SetSFXVolume(sVol);

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "PolizaiMuller")
        {
            UpdateDDAText();
        }
    }

    public void SetMusicVolume(float value)
    {
        ApplyVolume("musicVol", value);
        PlayerPrefs.SetFloat("MusicVol", value); // Salvăm valoarea
    }

    public void SetVoicesVolume(float value)
    {
        ApplyVolume("voicesVol", value);
        PlayerPrefs.SetFloat("VoicesVol", value); // Salvăm valoarea
    }

    public void SetSFXVolume(float value)
    {
        ApplyVolume("sfxVol", value);
        PlayerPrefs.SetFloat("SFXVol", value); // Salvăm valoarea
    }

    // O funcție ajutătoare ca să nu repetăm codul de Log10 peste tot
    private void ApplyVolume(string parameterName, float value)
    {
        if (value > 0.0001f)
        {
            float dbVolume = Mathf.Log10(value) * 20;
            mainMixer.SetFloat(parameterName, dbVolume);
        }
        else
        {
            mainMixer.SetFloat(parameterName, -80f);
        }
    }

    public void ToggleDDAWithText()
    {
        int currentState = PlayerPrefs.GetInt("DDAEnabled", 1);
        PlayerPrefs.SetInt("DDAEnabled", currentState == 1 ? 0 : 1);
        PlayerPrefs.Save();

        UpdateDDAText();
    }

    private void UpdateDDAText()
    {
        int state = PlayerPrefs.GetInt("DDAEnabled", 1);
        ddaStatusText.text = (state == 1) ? "ON" : "OFF";
    }

    public void CloseOptions()
    {
        PlayerPrefs.Save(); // Salvăm totul la închidere
        gameObject.SetActive(false);
    }
}