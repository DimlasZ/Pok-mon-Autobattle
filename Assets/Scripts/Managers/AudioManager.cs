using UnityEngine;
using UnityEngine.Audio;

// AudioManager handles all sound playback.
// Loads Pokemon cries at runtime from Resources/Audio/Cries/{id}.ogg
// No manual assignment needed — cries are looked up by Pokédex ID.

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer Groups")]
    public AudioMixerGroup musicGroup;
    public AudioMixerGroup sfxGroup;

    private AudioSource _sfxSource;
    private AudioSource _musicSource;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.outputAudioMixerGroup = sfxGroup;

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.outputAudioMixerGroup = musicGroup;
    }

    // Set music volume (0.0 - 1.0)
    public void SetMusicVolume(float volume)
    {
        musicGroup?.audioMixer.SetFloat("MusicVolume", Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20);
    }

    // Set SFX volume (0.0 - 1.0)
    public void SetSFXVolume(float volume)
    {
        sfxGroup?.audioMixer.SetFloat("SFXVolume", Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20);
    }

    // Plays looping background music from Resources/Audio/Music/
    public void PlayMusic(string trackName)
    {
        var clip = Resources.Load<AudioClip>($"Audio/Music/{trackName}");
        if (clip == null)
        {
            Debug.LogWarning($"AudioManager: No music found at Resources/Audio/Music/{trackName}");
            return;
        }
        if (_musicSource.clip == clip) return; // already playing this track
        _musicSource.clip = clip;
        _musicSource.Play();
    }

    // Plays a sound from Resources/Audio/ by path (no extension)
    public void PlaySound(string resourcePath)
    {
        var clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null)
        {
            Debug.LogWarning($"AudioManager: No sound found at Resources/{resourcePath}");
            return;
        }
        _sfxSource.PlayOneShot(clip);
    }

    // Plays the cry for the given Pokédex ID (1.ogg, 2.ogg, etc.)
    public void PlayCry(int pokedexId)
    {
        var clip = Resources.Load<AudioClip>($"Audio/Cries/{pokedexId}");
        if (clip == null)
        {
            Debug.LogWarning($"AudioManager: No cry found for ID {pokedexId} (Resources/Audio/Cries/{pokedexId}.ogg)");
            return;
        }
        _sfxSource.PlayOneShot(clip);
    }
}
