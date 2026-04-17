using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Persistent overlay manager — survives scene loads via DontDestroyOnLoad.
// Hosts the Settings panel and Pokédex panel so both are accessible from every scene.
// Created once in MainMenuScene by MainMenuSceneGenerator; shop and future scenes just call Toggle*().

public class GlobalOverlayManager : MonoBehaviour
{
    public static GlobalOverlayManager Instance { get; private set; }

    [Header("Panels")]
    public GameObject settingsPanel;
    public GameObject pokedexPanel;

    [Header("Progress Overlay")]
    public ProgressOverlayUI progressOverlay;

    [Header("Audio Sliders")]
    public Slider musicSlider;
    public Slider sfxSlider;
    public Slider weatherSlider;

    [Header("Resolution")]
    public TMP_Dropdown resolutionDropdown;

    private Resolution[] _resolutions;

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitVolumeSliders();
        InitResolutionDropdown();
    }

    // -------------------------------------------------------
    // TOGGLE API — call from any scene
    // -------------------------------------------------------

    public void ToggleSettings()
    {
        if (settingsPanel == null) return;
        bool open = !settingsPanel.activeSelf;
        settingsPanel.SetActive(open);
        if (open && pokedexPanel != null) pokedexPanel.SetActive(false);
    }

    public void TogglePokedex()
    {
        if (pokedexPanel == null) return;
        bool open = !pokedexPanel.activeSelf;
        pokedexPanel.SetActive(open);
        if (open && settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void CloseAll()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pokedexPanel  != null) pokedexPanel.SetActive(false);
    }

    // -------------------------------------------------------
    // VOLUME
    // -------------------------------------------------------

    void InitVolumeSliders()
    {
        if (musicSlider != null)
        {
            musicSlider.value = PlayerPrefs.GetFloat("Vol_Music", 1f);
            musicSlider.onValueChanged.AddListener(v =>
            {
                AudioManager.Instance?.SetMusicVolume(v);
                PlayerPrefs.SetFloat("Vol_Music", v);
            });
            AudioManager.Instance?.SetMusicVolume(musicSlider.value);
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = PlayerPrefs.GetFloat("Vol_SFX", 1f);
            sfxSlider.onValueChanged.AddListener(v =>
            {
                AudioManager.Instance?.SetSFXVolume(v);
                PlayerPrefs.SetFloat("Vol_SFX", v);
            });
            AudioManager.Instance?.SetSFXVolume(sfxSlider.value);
        }

        if (weatherSlider != null)
        {
            weatherSlider.value = PlayerPrefs.GetFloat("Vol_Weather", 1f);
            weatherSlider.onValueChanged.AddListener(v =>
            {
                AudioManager.Instance?.SetWeatherVolume(v);
                PlayerPrefs.SetFloat("Vol_Weather", v);
            });
            AudioManager.Instance?.SetWeatherVolume(weatherSlider.value);
        }
    }

    // -------------------------------------------------------
    // RESOLUTION
    // -------------------------------------------------------

    void InitResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        _resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        var options      = new System.Collections.Generic.List<string>();
        int currentIndex = 0;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            var r = _resolutions[i];
            options.Add($"{r.width} x {r.height} @ {(int)r.refreshRateRatio.value}Hz");
            if (r.width == Screen.currentResolution.width &&
                r.height == Screen.currentResolution.height)
                currentIndex = i;
        }

        if (options.Count == 0)
            options.Add($"{Screen.width} x {Screen.height}");

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentIndex;
        resolutionDropdown.RefreshShownValue();
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    void OnResolutionChanged(int index)
    {
        if (_resolutions == null || index >= _resolutions.Length) return;
        var r = _resolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreen);
    }
}