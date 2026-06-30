using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Audio;

public class LevelAudioManager : MonoBehaviour
{
    public static LevelAudioManager Instance;

    [Header("Mixer Reference")]
    public AudioMixer mainMixer;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;
    public AudioSource loopsSource; 
    private AudioSource playerSource;
    private AudioSource bossSource;  
    private AudioSource combatSource;

    [Header("Monkey (Maimuta)")]
    public AudioClip monkeyJump;
    public AudioClip monkeyDash;
    public AudioClip monkeyTakeDmg;
    public AudioClip monkeyParry;
    public AudioClip monkeyFall;
    public AudioClip monkeyDefeat;

    [Header("Muller (Boss)")]
    public AudioClip mullerWhistle;
    public AudioClip mullerWrite;
    public AudioClip mullerThrowSpike;
    public AudioClip mullerHarpoon;
    public AudioClip mullerHarpoonFlight;
    public AudioClip mullerHarpoonFall;
    public AudioClip mullerJetpackIgnite;
    public AudioClip mullerJetpackFlight;
    public AudioClip mullerJetpackStop;
    public AudioClip mullerBubbleSpawn;
    public AudioClip mullerDuckAngry;
    public AudioClip mullerTrainAngry;
    public AudioClip mullerObosit;
    public AudioClip mullerDefeat;
    public AudioClip mullerIdle;
    public AudioClip mullerTransition1Bonk;

    [Header("Items & Enemies")]
    public AudioClip bananaThrow;
    public AudioClip bananaConnect;
    public AudioClip bubblePop;
    public AudioClip bikeRing;
    public AudioClip bikeSound;
    public AudioClip k9UnitBark;
    public AudioClip k9UnitBite;
    public AudioClip k9UnitRun;
    public AudioClip ticketTicking;
    public AudioClip ticketExplode;

    [Header("Ambient")]
    public AudioClip backgroundMusic;
    public AudioClip oldFilmSound;
    public AudioClip buttonClickClip;

    [Header("Mixer Snapshots")]
    public AudioMixerSnapshot normalSnapshot;  // Snapshot-ul pentru luptă (sunet clar)
    public AudioMixerSnapshot muffledSnapshot; // Snapshot-ul pentru pauză (sunet înfundat)

    private Dictionary<AudioClip, float> lastPlayTime = new Dictionary<AudioClip, float>();

    [Header("Optimization")]
    public float globalCooldown = 0.05f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        musicSource = gameObject.AddComponent<AudioSource>();
        sfxSource = gameObject.AddComponent<AudioSource>();
        loopsSource = gameObject.AddComponent<AudioSource>();
        playerSource = gameObject.AddComponent<AudioSource>();
        bossSource = gameObject.AddComponent<AudioSource>();
        combatSource = gameObject.AddComponent<AudioSource>();

        AutoAssignGroups();

        // Ierarhia Priorităților (0 e cel mai important)
        musicSource.priority = 0;      // Muzica (Background)
        loopsSource.priority = 5;      // Filmul vechi / Bicicleta
        bossSource.priority = 30;      // Atacurile lui Müller (trebuie să le auzi ca să te ferești!)
        playerSource.priority = 50;    // Mișcările maimuței
        combatSource.priority = 80;    // Impact / Proiectile
        sfxSource.priority = 100;     // UI / Click-uri

        musicSource.playOnAwake = false;
        sfxSource.playOnAwake = false;
        loopsSource.playOnAwake = false;
    }

    void AutoAssignGroups()
    {
        if (mainMixer == null) return;

        AudioMixerGroup[] musicG = mainMixer.FindMatchingGroups("Music");
        AudioMixerGroup[] voicesG = mainMixer.FindMatchingGroups("Voices");
        AudioMixerGroup[] sfxG = mainMixer.FindMatchingGroups("SFX");

        if (musicG.Length > 0) musicSource.outputAudioMixerGroup = musicG[0];
        if (sfxG.Length > 0)
        {
            sfxSource.outputAudioMixerGroup = sfxG[0];
            combatSource.outputAudioMixerGroup = sfxG[0];
            loopsSource.outputAudioMixerGroup = sfxG[0];
        }
        if (voicesG.Length > 0)
        {
            playerSource.outputAudioMixerGroup = voicesG[0];
            bossSource.outputAudioMixerGroup = voicesG[0];
        }
    }

    void Start()
    {
        float mVol = PlayerPrefs.GetFloat("MusicVol", 0.75f);
        float vVol = PlayerPrefs.GetFloat("VoicesVol", 0.75f);
        float sVol = PlayerPrefs.GetFloat("SFXVol", 0.75f);

        mainMixer.SetFloat("musicVol", Mathf.Log10(Mathf.Max(mVol, 0.0001f)) * 20);
        mainMixer.SetFloat("voicesVol", Mathf.Log10(Mathf.Max(vVol, 0.0001f)) * 20);
        mainMixer.SetFloat("sfxVol", Mathf.Log10(Mathf.Max(sVol, 0.0001f)) * 20);

        if (backgroundMusic != null)
        {
            musicSource.clip = backgroundMusic; 
            musicSource.loop = true;
            musicSource.Play();
        }

        if (oldFilmSound != null)
        {
            loopsSource.clip = oldFilmSound;
            loopsSource.loop = true;
            loopsSource.Play();
        }
    }

    public void SetMenuAtmosphere(bool inMenu)
    {
        Debug.Log("Schimbăm atmosfera: " + (inMenu ? "Muffled" : "Normal"));
        if (inMenu)
        {
            muffledSnapshot.TransitionTo(0f);

            loopsSource.volume = 0.3f;
        }
        else
        {
            normalSnapshot.TransitionTo(0f);
            loopsSource.volume = 0.1f;
        }
    }

    public void PlayOnClickSound()
    {
        if (buttonClickClip != null)
        {
            sfxSource.ignoreListenerPause = true;
            sfxSource.priority = 0;

            sfxSource.PlayOneShot(buttonClickClip, 1f);
        }
    }

    private bool CanPlay(AudioClip clip)
    {
        if (clip == null) return false;

        if (lastPlayTime.ContainsKey(clip))
        {
            if (Time.time - lastPlayTime[clip] < globalCooldown)
            {
                return false; 
            }
        }

        lastPlayTime[clip] = Time.time;
        return true;
    }

    public void PlaySFX(AudioClip clip, float volumeScale = 1f, bool randomPitch = false)
    {
        if (!CanPlay(clip)) return;
        sfxSource.pitch = randomPitch ? Random.Range(0.85f, 1.15f) : 1f;
        sfxSource.PlayOneShot(clip, volumeScale);
    }

    public void PlayPlayerSFX(AudioClip clip, float volumeScale = 1f, bool randomPitch = false)
    {
        if (!CanPlay(clip)) return;
        playerSource.pitch = randomPitch ? Random.Range(0.85f, 1.15f) : 1f; 
        playerSource.PlayOneShot(clip, volumeScale);
    }

    public void PlayCombatSFX(AudioClip clip, float volumeScale = 1f, bool randomPitch = false)
    {
        if (!CanPlay(clip)) return;
        playerSource.pitch = randomPitch ? Random.Range(0.85f, 1.15f) : 1f; 
        combatSource.PlayOneShot(clip, volumeScale);
    }

    public void PlayBossSFX(AudioClip clip, float volumeScale = 1f, bool randomPitch = false)
    {
        if (!CanPlay(clip)) return; 
        bossSource.PlayOneShot(clip, volumeScale);
    }

    public void StartLoop(AudioClip clip, float volume = 1f)
    {
        if (loopsSource.clip == clip && loopsSource.isPlaying) return;
        loopsSource.clip = clip;
        loopsSource.loop = true;
        loopsSource.volume = volume;
        loopsSource.Play();
    }

    public void StopLoop()
    {
        if (loopsSource != null) loopsSource.Stop();
    }

    public void PlayVictoryMusic()
    {
        musicSource.Stop();
        SetMenuAtmosphere(false);

        musicSource.clip = mullerDefeat;
        musicSource.loop = false; 
        musicSource.Play();
    }
}