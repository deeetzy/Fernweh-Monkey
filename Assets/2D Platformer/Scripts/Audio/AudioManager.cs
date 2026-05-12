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
    public AudioSource loopsSource; // Pentru Jetpack, Ticking, etc.
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

    // Tabel în care salvăm când a fost redat ultima oară fiecare sunet
    private Dictionary<AudioClip, float> lastPlayTime = new Dictionary<AudioClip, float>();

    [Header("Optimization")]
    public float globalCooldown = 0.05f; // 50 milisecunde între sunete identice

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // CREĂM ȘI ASIGNĂM SURSELE INSTANTANEU
        musicSource = gameObject.AddComponent<AudioSource>();
        sfxSource = gameObject.AddComponent<AudioSource>();
        loopsSource = gameObject.AddComponent<AudioSource>();
        playerSource = gameObject.AddComponent<AudioSource>();
        bossSource = gameObject.AddComponent<AudioSource>();
        combatSource = gameObject.AddComponent<AudioSource>();

        // --- CONECTARE LA MIXER (IMPORTANT!) ---
        AutoAssignGroups();

        // Ierarhia Priorităților (0 e cel mai important)
        musicSource.priority = 0;      // Muzica (Background)
        loopsSource.priority = 5;      // Filmul vechi / Bicicleta
        bossSource.priority = 30;      // Atacurile lui Müller (trebuie să le auzi ca să te ferești!)
        playerSource.priority = 50;    // Mișcările maimuței
        combatSource.priority = 80;    // Impact / Proiectile
        sfxSource.priority = 100;     // UI / Click-uri

        // Configurație de bază ca să fim siguri că nu sunt nule
        musicSource.playOnAwake = false;
        sfxSource.playOnAwake = false;
        loopsSource.playOnAwake = false;
    }

    void AutoAssignGroups()
    {
        if (mainMixer == null) return;

        // Căutăm grupurile după numele lor din Mixer
        AudioMixerGroup[] musicG = mainMixer.FindMatchingGroups("Music");
        AudioMixerGroup[] voicesG = mainMixer.FindMatchingGroups("Voices");
        AudioMixerGroup[] sfxG = mainMixer.FindMatchingGroups("SFX");

        // Le asignăm surselor corespunzătoare
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

    // În LevelAudioManager.cs
    void Start()
    {
        // Aplicăm volumele globale imediat ce începe lupta
        float mVol = PlayerPrefs.GetFloat("MusicVol", 0.75f);
        float vVol = PlayerPrefs.GetFloat("VoicesVol", 0.75f);
        float sVol = PlayerPrefs.GetFloat("SFXVol", 0.75f);

        mainMixer.SetFloat("musicVol", Mathf.Log10(Mathf.Max(mVol, 0.0001f)) * 20);
        mainMixer.SetFloat("voicesVol", Mathf.Log10(Mathf.Max(vVol, 0.0001f)) * 20);
        mainMixer.SetFloat("sfxVol", Mathf.Log10(Mathf.Max(sVol, 0.0001f)) * 20);

        if (backgroundMusic != null)
        {
            musicSource.clip = backgroundMusic; // Punem "discul" în player
            musicSource.loop = true;
            musicSource.Play(); // Acum va cânta!
        }

        if (oldFilmSound != null)
        {
            loopsSource.clip = oldFilmSound;
            loopsSource.loop = true;
            loopsSource.Play();
        }
    }

    // --- FUNCTII DE EXECUTIE ---
    public void SetMenuAtmosphere(bool inMenu)
    {
        Debug.Log("Schimbăm atmosfera: " + (inMenu ? "Muffled" : "Normal"));
        if (inMenu)
        {
            // Trecem la setările de meniu în 0.5 secunde (fade fin)
            muffledSnapshot.TransitionTo(0f);

            // Fâșâitul de film poate rămâne controlat manual dacă nu e în mixer
            loopsSource.volume = 0.3f;
        }
        else
        {
            // Revenim la sunetul clar
            normalSnapshot.TransitionTo(0f);
            loopsSource.volume = 0.1f;
        }
    }

    public void PlayOnClickSound()
    {
        if (buttonClickClip != null)
        {
            // Forțăm sursa să fie gata
            sfxSource.ignoreListenerPause = true;
            sfxSource.priority = 0; // 0 este cea mai mare prioritate în Unity!

            sfxSource.PlayOneShot(buttonClickClip, 1f);
        }
    }

    // Funcția care verifică dacă sunetul are voie să cânte
    private bool CanPlay(AudioClip clip)
    {
        if (clip == null) return false;

        if (lastPlayTime.ContainsKey(clip))
        {
            if (Time.time - lastPlayTime[clip] < globalCooldown)
            {
                return false; // Prea devreme, blochează sunetul
            }
        }

        // Actualizăm timpul pentru acest clip
        lastPlayTime[clip] = Time.time;
        return true;
    }

    public void PlaySFX(AudioClip clip, float volumeScale = 1f, bool randomPitch = false)
    {
        if (!CanPlay(clip)) return;
        sfxSource.pitch = randomPitch ? Random.Range(0.85f, 1.15f) : 1f;
        sfxSource.PlayOneShot(clip, volumeScale); // volumeScale acum e doar un multiplicator local (0-1)
    }

    // Funcție specială pentru maimuță (reduce delay-ul și nu taie muzica)
    public void PlayPlayerSFX(AudioClip clip, float volumeScale = 1f, bool randomPitch = false)
    {
        if (!CanPlay(clip)) return; // BARIERA
        playerSource.pitch = randomPitch ? Random.Range(0.85f, 1.15f) : 1f; // Pitch rapid pentru varietate
        playerSource.PlayOneShot(clip, volumeScale);
    }

    // Funcție pentru lupte
    public void PlayCombatSFX(AudioClip clip, float volumeScale = 1f, bool randomPitch = false)
    {
        if (!CanPlay(clip)) return; // BARIERA
        playerSource.pitch = randomPitch ? Random.Range(0.85f, 1.15f) : 1f; // Pitch rapid pentru varietate
        combatSource.PlayOneShot(clip, volumeScale);
    }

    // Funcție specială pentru Müller
    public void PlayBossSFX(AudioClip clip, float volumeScale = 1f, bool randomPitch = false)
    {
        if (!CanPlay(clip)) return; // BARIERA
        // La boss nu punem mereu pitch random, pentru a păstra "autoritatea" vocii lui
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
        // 1. Oprim muzica de boss (sau facem fade out dacă ai script pentru asta)
        musicSource.Stop();

        // 2. Dezactivăm filtrele (LowPass) dacă vrei ca victoria să se audă clar
        SetMenuAtmosphere(false);

        // 3. Redăm melodia de victorie (VictoryTheme)
        // O poți pune pe musicSource dacă vrei să înlocuiască tot
        musicSource.clip = mullerDefeat;
        musicSource.loop = false; // De obicei victoria nu e loop
        musicSource.Play();
    }
}