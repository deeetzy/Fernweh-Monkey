using UnityEngine;
using System.Collections;
using UnityEngine.Audio;

public class MenuAudioManager : MonoBehaviour
{
    public static MenuAudioManager Instance;

    [Header("Mixer Reference")]
    public AudioMixer mainMixer;

    [Header("Fade Settings")]
    public float fadeDuration = 1.5f;

    [Header("Audio Clips")]
    public AudioClip mainMenuMusic;
    public AudioClip oldFilmSound;
    public AudioClip buttonClickClip;

    private AudioSource musicSource;
    private AudioSource atmosphereSource;
    private AudioSource sfxSource;

    void Awake()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        atmosphereSource = gameObject.AddComponent<AudioSource>();
        sfxSource = gameObject.AddComponent<AudioSource>();

        musicSource.loop = true;
        atmosphereSource.loop = true;

        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        AutoAssignMixerGroups();
    }

    void AutoAssignMixerGroups()
    {
        if (mainMixer == null)
        {
            Debug.LogError("MainMixer nu este asignat în LevelAudioManager!");
            return;
        }

        AudioMixerGroup[] musicGroups = mainMixer.FindMatchingGroups("Music");
        AudioMixerGroup[] voicesGroups = mainMixer.FindMatchingGroups("Voices");
        AudioMixerGroup[] sfxGroups = mainMixer.FindMatchingGroups("SFX");

        if (musicGroups.Length > 0) musicSource.outputAudioMixerGroup = musicGroups[0];

        if (voicesGroups.Length > 0)
        {
            atmosphereSource.outputAudioMixerGroup = voicesGroups[0];
        }

        if (sfxGroups.Length > 0) sfxSource.outputAudioMixerGroup = sfxGroups[0];

        Debug.Log("Audio Mixer Groups au fost asignate automat prin cod!");
    }

    void Start()
    {
        float savedMusic = PlayerPrefs.GetFloat("MusicVol", 0.75f);

        mainMixer.SetFloat("musicVol", Mathf.Log10(Mathf.Max(savedMusic, 0.0001f)) * 20);

        float savedVoices = PlayerPrefs.GetFloat("VoicesVol", 0.75f);
        mainMixer.SetFloat("voicesVol", Mathf.Log10(Mathf.Max(savedVoices, 0.0001f)) * 20);

        float savedSFX = PlayerPrefs.GetFloat("SFXVol", 0.75f);
        mainMixer.SetFloat("sfxVol", Mathf.Log10(Mathf.Max(savedSFX, 0.0001f)) * 20);

        if (mainMenuMusic != null)
        {
            musicSource.clip = mainMenuMusic; // Punem melodia în "Audio Clip"
            musicSource.loop = true;          
            musicSource.Play();               
        }

        if (oldFilmSound != null)
        {
            atmosphereSource.clip = oldFilmSound;
            atmosphereSource.loop = true;
            atmosphereSource.Play();
        }
    }

    public void PlayOnClickSound()
    {
        sfxSource.PlayOneShot(buttonClickClip, 0.75f);
    }

    public void StartLoadingFade()
    {
        StartCoroutine(FadeOutMusic());
    }

    private IEnumerator FadeOutMusic()
    {
        float currentVol;
        mainMixer.GetFloat("musicVol", out currentVol); // Luăm volumul actual din Mixer

        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            float newVol = Mathf.Lerp(currentVol, -80f, t / fadeDuration);
            mainMixer.SetFloat("musicVol", newVol);
            yield return null;
        }

        musicSource.Stop();
    }
}