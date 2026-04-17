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
    public AudioMixerGroup weatherGroup;

    private AudioSource _sfxSource;
    private AudioSource _musicSource;
    private AudioSource _weatherSource;
    private AudioClip   _buttonClip;

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

        _weatherSource = gameObject.AddComponent<AudioSource>();
        _weatherSource.playOnAwake = false;
        _weatherSource.loop = true;
        _weatherSource.outputAudioMixerGroup = weatherGroup;

        _buttonClip = Resources.Load<AudioClip>("Audio/Sounds/Button");
    }

    // Set music volume (0.0 - 1.0)
    public void SetMusicVolume(float volume)
    {
        if (musicGroup != null)
            musicGroup.audioMixer.SetFloat("MusicVolume", Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20);
    }

    // Set SFX volume (0.0 - 1.0)
    public void SetSFXVolume(float volume)
    {
        if (sfxGroup != null)
            sfxGroup.audioMixer.SetFloat("SFXVolume", Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20);
    }

    // Set weather ambient volume (0.0 - 1.0)
    public void SetWeatherVolume(float volume)
    {
        if (weatherGroup != null)
            weatherGroup.audioMixer.SetFloat("WeatherVolume", Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20);
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
        if (_musicSource == null) return;
        if (_musicSource.clip == clip && _musicSource.isPlaying) return; // already playing this track
        _musicSource.loop = true;
        _musicSource.clip = clip;
        _musicSource.Play();
    }

    // Picks a random track whose name starts with the given prefix and plays it.
    public void PlayRandomMusic(string prefix)
    {
        var all = Resources.LoadAll<AudioClip>("Audio/Music");
        var matches = System.Array.FindAll(all, c => c.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase));
        if (matches.Length == 0)
        {
            Debug.LogWarning($"AudioManager: No music found with prefix '{prefix}'");
            return;
        }
        var clip = matches[Random.Range(0, matches.Length)];
        if (_musicSource == null) return;
        if (_musicSource.clip == clip && _musicSource.isPlaying) return;
        _musicSource.loop = true;
        _musicSource.clip = clip;
        _musicSource.Play();
    }

    // Plays the UI button click sound instantly (pre-cached)
    public void PlayButtonSound()
    {
        if (_sfxSource == null || _buttonClip == null) return;
        _sfxSource.PlayOneShot(_buttonClip);
    }

    // Plays a sound from Resources/Audio/ by path (no extension)
    public void PlaySound(string resourcePath)
    {
        if (_sfxSource == null) return;
        var clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null)
        {
            Debug.LogWarning($"AudioManager: No sound found at Resources/{resourcePath}");
            return;
        }
        _sfxSource.PlayOneShot(clip);
    }

    // Starts a looping ambient weather sound (e.g. "rain", "sandstorm").
    // Pass null or empty string to stop any playing weather sound.
    public void PlayWeatherSound(string weatherName)
    {
        if (string.IsNullOrEmpty(weatherName))
        {
            StopWeatherSound();
            return;
        }

        var clip = Resources.Load<AudioClip>($"Audio/Sounds/{weatherName}");
        if (clip == null)
        {
            Debug.LogWarning($"AudioManager: No weather sound at Resources/Audio/Sounds/{weatherName}");
            StopWeatherSound();
            return;
        }

        if (_weatherSource.clip == clip && _weatherSource.isPlaying) return; // already playing
        _weatherSource.clip = clip;
        _weatherSource.Play();
    }

    // Plays a music track once (no loop) — used for loss/victory stings.
    public void PlayMusicOnce(string trackName)
    {
        var clip = Resources.Load<AudioClip>($"Audio/Music/{trackName}");
        if (clip == null)
        {
            Debug.LogWarning($"AudioManager: No music found at Resources/Audio/Music/{trackName}");
            return;
        }
        _musicSource.loop = false;
        _musicSource.clip = clip;
        _musicSource.Play();
    }

    public void StopMusic()
    {
        _musicSource.Stop();
        _musicSource.clip = null;
    }

    public void StopWeatherSound()
    {
        _weatherSource.Stop();
        _weatherSource.clip = null;
    }

    // Plays the cry for the given Pokédex ID (1.ogg, 2.ogg, etc.)
    public void PlayCry(int pokedexId)
    {
        if (_sfxSource == null) return;
        var clip = Resources.Load<AudioClip>($"Audio/Cries/{pokedexId}");
        if (clip == null)
        {
            Debug.LogWarning($"AudioManager: No cry found for ID {pokedexId} (Resources/Audio/Cries/{pokedexId}.ogg)");
            return;
        }
        _sfxSource.PlayOneShot(clip);
    }
}
