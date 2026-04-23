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
    public GameObject helperPanel;

    [Header("Progress Overlay")]
    public ProgressOverlayUI progressOverlay;

    [Header("Victory & Hall of Fame")]
    public VictoryOverlayUI  victoryOverlay;
    public HallOfFamePanel   hallOfFamePanel;

    [Header("Game Over")]
    public GameOverOverlayUI gameOverOverlay;

    [Header("Tier Upgrade")]
    public TierUpgradeOverlayUI tierUpgradeOverlay;

    [Header("Multiplayer")]
    public GameObject multiplayerLobbyPanel;

    [Header("Audio Sliders")]
    public Slider musicSlider;
    public Slider sfxSlider;
    public Slider weatherSlider;

    [Header("Resolution")]
    public TMP_Dropdown resolutionDropdown;

    [Header("Windowed Mode")]
    public Toggle windowedToggle;

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
        InitWindowedToggle();
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
        if (open && helperPanel   != null) helperPanel.SetActive(false);
    }

    public void ToggleHelper()
    {
        if (helperPanel == null) return;
        bool open = !helperPanel.activeSelf;
        helperPanel.SetActive(open);
        if (open && settingsPanel != null) settingsPanel.SetActive(false);
        if (open && pokedexPanel  != null) pokedexPanel.SetActive(false);
    }

    public void ToggleHallOfFame()
    {
        if (hallOfFamePanel == null) return;
        bool open = !hallOfFamePanel.gameObject.activeSelf;
        hallOfFamePanel.gameObject.SetActive(open);
        if (open && settingsPanel != null) settingsPanel.SetActive(false);
        if (open && pokedexPanel  != null) pokedexPanel.SetActive(false);
        if (open && helperPanel   != null) helperPanel.SetActive(false);
    }

    public void CloseAll()
    {
        if (settingsPanel   != null) settingsPanel.SetActive(false);
        if (pokedexPanel    != null) pokedexPanel.SetActive(false);
        if (helperPanel     != null) helperPanel.SetActive(false);
        if (hallOfFamePanel != null) hallOfFamePanel.gameObject.SetActive(false);
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

        // Popular resolutions in priority order
        var popular = new (int w, int h)[]
        {
            (1920, 1080), (2560, 1440), (3840, 2160),
            (1280, 720),  (1600, 900),  (1366, 768),
            (1440, 900),  (1680, 1050), (2560, 1080), (3440, 1440),
        };

        // Build deduplicated list from what the monitor actually supports
        var allRes = Screen.resolutions;
        var seen   = new System.Collections.Generic.HashSet<(int, int)>();
        var unique = new System.Collections.Generic.List<Resolution>();
        foreach (var r in allRes)
            if (seen.Add((r.width, r.height)))
                unique.Add(r);

        // Keep only popular ones that are supported, in priority order
        _resolutions = System.Array.Empty<Resolution>();
        var filtered = new System.Collections.Generic.List<Resolution>();
        foreach (var p in popular)
            foreach (var r in unique)
                if (r.width == p.w && r.height == p.h) { filtered.Add(r); break; }

        // Fallback: if monitor supports none of the popular ones, use all unique
        if (filtered.Count == 0) filtered = unique;

        _resolutions = filtered.ToArray();

        resolutionDropdown.ClearOptions();
        var options      = new System.Collections.Generic.List<string>();
        int currentIndex = 0;
        for (int i = 0; i < _resolutions.Length; i++)
        {
            var r = _resolutions[i];
            options.Add($"{r.width} x {r.height}");
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

    // -------------------------------------------------------
    // WINDOWED MODE
    // -------------------------------------------------------

    void InitWindowedToggle()
    {
        if (windowedToggle == null) return;
        bool windowed = PlayerPrefs.GetInt("Windowed", 0) == 1;
        windowedToggle.isOn = windowed;
        ApplyWindowedMode(windowed);
        windowedToggle.onValueChanged.AddListener(OnWindowedChanged);
    }

    void OnWindowedChanged(bool windowed)
    {
        PlayerPrefs.SetInt("Windowed", windowed ? 1 : 0);
        ApplyWindowedMode(windowed);
    }

    static void ApplyWindowedMode(bool windowed)
    {
        Screen.SetResolution(Screen.width, Screen.height,
            windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
    }
}