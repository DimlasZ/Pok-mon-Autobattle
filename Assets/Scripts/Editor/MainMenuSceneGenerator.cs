using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

// Generates the Main Menu scene from scratch and saves it to Assets/Scenes/MainMenuScene.unity.
// Also creates/updates Assets/Resources/PokemonDatabase.asset with all PokemonData in the project.
//
// Run via: Pokemon -> Generate Main Menu Scene
// Any unsaved changes in your open scene will be prompted before the new scene is created.

public class MainMenuSceneGenerator
{
    [MenuItem("Pokemon/Generate Main Menu Scene")]
    public static void Generate()
    {
        // Prompt to save any dirty scenes first
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // ── Create & open a new empty scene ───────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera (required — empty scene has none) ──────────────────────
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        var cam = cameraGO.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = new Color(0.1f, 0.1f, 0.1f, 1f);
        cam.orthographic     = true;
        cam.depth            = -1;
        cameraGO.AddComponent<AudioListener>();

        // ── EventSystem (required for all UI interaction) ─────────────────
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        // Use the correct input module depending on project Input settings
#if ENABLE_INPUT_SYSTEM
        esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif

        // ── AudioManager (persists into ShopScene / BattleScene) ─────────
        var audioGO = new GameObject("AudioManager");
        var audioMgr = audioGO.AddComponent<AudioManager>();

        // Auto-assign mixer groups from the project's AudioMixer
        var mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/Audio/NewAudioMixer.mixer");
        if (mixer != null)
        {
            audioMgr.musicGroup   = FindMixerGroup(mixer, "Music");
            audioMgr.sfxGroup     = FindMixerGroup(mixer, "Sound Effects");
            audioMgr.weatherGroup = FindMixerGroup(mixer, "Weather");
            EditorUtility.SetDirty(audioGO);
        }
        else
            Debug.LogWarning("MainMenuSceneGenerator: AudioMixer not found at Assets/Audio/NewAudioMixer.mixer — assign mixer groups manually.");

        // ── Canvas ────────────────────────────────────────────────────────
        var canvasGO = new GameObject("Canvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        canvasGO.AddComponent<GraphicRaycaster>();

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight  = 0.5f;

        Transform root = canvasGO.transform;

        // ── Background image ──────────────────────────────────────────────
        // Ensure the PNG is imported as Sprite
        const string bgPath = "Assets/Resources/Backgrounds/Mainmenu.png";
        var importer = AssetImporter.GetAtPath(bgPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType  = TextureImporterType.Sprite;
            importer.spritePivot  = new Vector2(0.5f, 0.5f);
            importer.SaveAndReimport();
        }
        var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(bgPath);

        var bgGO   = new GameObject("Background");
        var bgRect = bgGO.AddComponent<RectTransform>();
        var bgImg  = bgGO.AddComponent<Image>();
        bgGO.transform.SetParent(root, false);
        bgRect.anchorMin        = Vector2.zero;
        bgRect.anchorMax        = Vector2.one;
        bgRect.offsetMin        = Vector2.zero;
        bgRect.offsetMax        = Vector2.zero;
        bgImg.sprite            = bgSprite;
        bgImg.color             = Color.white;
        bgImg.type              = Image.Type.Simple;
        bgImg.preserveAspect    = false;   // stretch to fill screen

        // ── Dark overlay (ensures readability on any background) ──────────
        var overlayGO  = new GameObject("DarkOverlay");
        var overlayRect = overlayGO.AddComponent<RectTransform>();
        var overlayImg  = overlayGO.AddComponent<Image>();
        overlayGO.transform.SetParent(root, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayImg.color      = new Color(0f, 0f, 0f, 0.45f);
        overlayImg.raycastTarget = false;

        // ── Title ─────────────────────────────────────────────────────────
        var titleTMP = CreateTMP(root, "TitleText", "Pokémon Auto Battler", 72,
            new Vector2(0, 400), new Vector2(1200, 120));
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color     = Color.white;

        // ── Main Buttons ──────────────────────────────────────────────────
        // Button layout (top → bottom): Continue (if save), Play Now, Pokédex, Hall of Fame, Settings, Quit
        // Continue is hidden at scene-open; MainMenuController shows it at runtime if a save exists.
        var continueBtn      = CreateButton(root, "ContinueButton",      "Continue",      new Vector2(320, 75), new Vector2(0,  250));
        var playBtn          = CreateButton(root, "PlayButton",          "Play Now",      new Vector2(320, 75), new Vector2(0,  163));
        var multiplayerBtn   = CreateButton(root, "MultiplayerButton",   "Multiplayer",   new Vector2(320, 75), new Vector2(0,   76));
        var pokedexBtn       = CreateButton(root, "PokedexButton",       "Pokédex",       new Vector2(320, 75), new Vector2(0,  -12));
        var hallOfFameBtn    = CreateButton(root, "HallOfFameButton",    "Hall of Fame",  new Vector2(320, 75), new Vector2(0, -100));
        var settingsBtn      = CreateButton(root, "SettingsButton",      "Settings",      new Vector2(320, 75), new Vector2(0, -187));
        var quitBtn          = CreateButton(root, "QuitButton",          "Quit",          new Vector2(320, 75), new Vector2(0, -275));

        SetButtonColor(playBtn,        new Color(0.18f, 0.58f, 0.18f));
        SetButtonColor(continueBtn,    new Color(0.10f, 0.45f, 0.45f));
        SetButtonColor(multiplayerBtn, new Color(0.45f, 0.15f, 0.55f));
        SetButtonColor(pokedexBtn,     new Color(0.18f, 0.28f, 0.68f));
        SetButtonColor(hallOfFameBtn,  new Color(0.55f, 0.40f, 0.05f));
        SetButtonColor(quitBtn,        new Color(0.58f, 0.12f, 0.12f));
        // settings keeps default grey

        // ── GlobalOverlayCanvas (root-level, persists across scenes via DontDestroyOnLoad) ──
        // Panels live here so Settings & Pokédex survive scene transitions.
        var overlayCanvasGO = new GameObject("GlobalOverlayCanvas");
        var overlayCanvas   = overlayCanvasGO.AddComponent<Canvas>();
        overlayCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 10;   // renders on top of all scene canvases
        overlayCanvasGO.AddComponent<GraphicRaycaster>();
        var overlayScaler = overlayCanvasGO.AddComponent<CanvasScaler>();
        overlayScaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        overlayScaler.referenceResolution  = new Vector2(1920, 1080);
        overlayScaler.screenMatchMode      = CanvasScaler.ScreenMatchMode.Expand;
        overlayScaler.matchWidthOrHeight   = 0.5f;

        Transform overlayRoot = overlayCanvasGO.transform;

        // ── Settings Panel (overlay, hidden) ─────────────────────────────
        var settingsPanelGO = BuildSettingsPanel(overlayRoot,
            out Slider musicSlider, out Slider sfxSlider, out Slider weatherSlider,
            out TMP_Dropdown resDropdown,
            out Toggle windowedToggle,
            out Button closeSettingsBtn);
        settingsPanelGO.SetActive(false);

        // ── Pokédex Panel (overlay, hidden) ───────────────────────────────
        var pokedexPanelGO = BuildPokedexPanel(overlayRoot,
            out RectTransform cardContainer,
            out GameObject    detailPanel,
            out Image         detailSprite,
            out TextMeshProUGUI detailName,
            out TextMeshProUGUI detailTypes,
            out TextMeshProUGUI detailStats,
            out TextMeshProUGUI detailAbility,
            out RectTransform evolutionContainer,
            out Image         detailTypeIcon,
            out TMP_InputField pokedexSearch,
            out TMP_Dropdown   pokedexTypeDropdown,
            out TMP_Dropdown   pokedexTierDropdown,
            out Button closePokedexBtn);
        pokedexPanelGO.SetActive(false);

        // ── Helper Panel (overlay, hidden) ────────────────────────────────
        var helperPanelGO = BuildHelperPanel(overlayRoot, out Button closeHelperBtn);
        helperPanelGO.SetActive(false);

        // ── GlobalOverlayManager (DontDestroyOnLoad singleton) ───────────
        var overlayMgr = overlayCanvasGO.AddComponent<GlobalOverlayManager>();
        overlayMgr.settingsPanel     = settingsPanelGO;
        overlayMgr.pokedexPanel      = pokedexPanelGO;
        overlayMgr.helperPanel       = helperPanelGO;
        overlayMgr.musicSlider       = musicSlider;
        overlayMgr.sfxSlider         = sfxSlider;
        overlayMgr.weatherSlider     = weatherSlider;
        overlayMgr.resolutionDropdown = resDropdown;
        overlayMgr.windowedToggle    = windowedToggle;

        // Wire close buttons via GlobalOverlayToggle (toggle = close when panel is open)
        AddOverlayToggle(closeSettingsBtn.gameObject, GlobalOverlayToggle.Target.Settings);
        AddOverlayToggle(closePokedexBtn.gameObject,  GlobalOverlayToggle.Target.Pokedex);
        AddOverlayToggle(closeHelperBtn.gameObject,   GlobalOverlayToggle.Target.Helper);

        // Wire main-menu buttons
        AddOverlayToggle(settingsBtn.gameObject,   GlobalOverlayToggle.Target.Settings);
        AddOverlayToggle(pokedexBtn.gameObject,    GlobalOverlayToggle.Target.Pokedex);
        AddOverlayToggle(hallOfFameBtn.gameObject, GlobalOverlayToggle.Target.HallOfFame);

        // ── PokedexPanel component ────────────────────────────────────────
        var pokedexComp = pokedexPanelGO.AddComponent<PokedexPanel>();
        pokedexComp.cardContainer      = cardContainer;
        pokedexComp.detailPanel        = detailPanel;
        pokedexComp.detailSprite       = detailSprite;
        pokedexComp.detailName         = detailName;
        pokedexComp.detailTypes        = detailTypes;
        pokedexComp.detailStats        = detailStats;
        pokedexComp.detailAbility      = detailAbility;
        pokedexComp.evolutionContainer = evolutionContainer;
        pokedexComp.detailTypeIcon     = detailTypeIcon;
        pokedexComp.searchInput        = pokedexSearch;
        pokedexComp.typeDropdown       = pokedexTypeDropdown;
        pokedexComp.tierDropdown       = pokedexTierDropdown;

        // ── MainMenuController (play button only) ─────────────────────────
        var controllerGO = new GameObject("MainMenuController");
        controllerGO.transform.SetParent(root, false);
        var controller = controllerGO.AddComponent<MainMenuController>();
        controller.playButton     = playBtn;
        controller.continueButton = continueBtn;
        controller.quitButton     = quitBtn;
        controller.multiplayerButton = multiplayerBtn;
        // hallOfFameBtn, pokedexBtn, settingsBtn are wired via GlobalOverlayToggle above

        // ── Progress Overlay (gym badges / Elite 4 / champ / lives) ──────
        // Sprites are loaded from Resources at runtime — no assignments needed here.
        var progressGO = new GameObject("ProgressOverlay");
        progressGO.transform.SetParent(overlayRoot, false);

        // Fullscreen stretch
        var progressRT = progressGO.AddComponent<RectTransform>();
        progressRT.anchorMin        = Vector2.zero;
        progressRT.anchorMax        = Vector2.one;
        progressRT.offsetMin        = Vector2.zero;
        progressRT.offsetMax        = Vector2.zero;

        var progressUI = progressGO.AddComponent<ProgressOverlayUI>();
        overlayMgr.progressOverlay = progressUI;

        // ── Tier Upgrade Overlay ──────────────────────────────────────────
        var tierUpgradeGO = new GameObject("TierUpgradeOverlay");
        tierUpgradeGO.transform.SetParent(overlayRoot, false);
        var tierUpgradeRT = tierUpgradeGO.AddComponent<RectTransform>();
        tierUpgradeRT.anchorMin        = Vector2.zero;
        tierUpgradeRT.anchorMax        = Vector2.one;
        tierUpgradeRT.offsetMin        = Vector2.zero;
        tierUpgradeRT.offsetMax        = Vector2.zero;
        var tierUpgradeUI = tierUpgradeGO.AddComponent<TierUpgradeOverlayUI>();
        overlayMgr.tierUpgradeOverlay = tierUpgradeUI;

        // ── Game Over Overlay ─────────────────────────────────────────────
        var gameOverGO = new GameObject("GameOverOverlay");
        gameOverGO.transform.SetParent(overlayRoot, false);
        var gameOverRT = gameOverGO.AddComponent<RectTransform>();
        gameOverRT.anchorMin        = Vector2.zero;
        gameOverRT.anchorMax        = Vector2.one;
        gameOverRT.offsetMin        = Vector2.zero;
        gameOverRT.offsetMax        = Vector2.zero;
        var gameOverUI = gameOverGO.AddComponent<GameOverOverlayUI>();
        overlayMgr.gameOverOverlay = gameOverUI;

        // ── Victory Overlay ───────────────────────────────────────────────
        var victoryGO = new GameObject("VictoryOverlay");
        victoryGO.transform.SetParent(overlayRoot, false);
        var victoryRT = victoryGO.AddComponent<RectTransform>();
        victoryRT.anchorMin        = Vector2.zero;
        victoryRT.anchorMax        = Vector2.one;
        victoryRT.offsetMin        = Vector2.zero;
        victoryRT.offsetMax        = Vector2.zero;
        var victoryUI = victoryGO.AddComponent<VictoryOverlayUI>();
        overlayMgr.victoryOverlay = victoryUI;

        // ── Hall of Fame Panel ────────────────────────────────────────────
        var hofGO = new GameObject("HallOfFamePanel");
        hofGO.transform.SetParent(overlayRoot, false);
        var hofRT = hofGO.AddComponent<RectTransform>();
        hofRT.anchorMin        = new Vector2(0.5f, 0.5f);
        hofRT.anchorMax        = new Vector2(0.5f, 0.5f);
        hofRT.pivot            = new Vector2(0.5f, 0.5f);
        hofRT.anchoredPosition = Vector2.zero;
        hofRT.sizeDelta        = new Vector2(1100f, 800f);
        var hofPanel = hofGO.AddComponent<HallOfFamePanel>();
        overlayMgr.hallOfFamePanel = hofPanel;

        // ── Multiplayer Lobby Panel ───────────────────────────────────────
        var lobbyPanelGO = BuildLobbyPanel(overlayRoot,
            out Button mpHostBtn, out Button mpJoinBtn,
            out Button mpHostCancelBtn, out Button mpJoinCancelBtn, out Button mpBackBtn,
            out Button mpJoinConfirmBtn,
            out TextMeshProUGUI mpRoomCodeLabel, out TextMeshProUGUI mpHostStatusLabel,
            out TMP_InputField mpCodeInput, out TextMeshProUGUI mpJoinStatusLabel,
            out GameObject mpIdlePanel, out GameObject mpHostPanel, out GameObject mpJoinPanel);
        lobbyPanelGO.SetActive(false);

        var lobbyUI = lobbyPanelGO.AddComponent<MultiplayerLobbyUI>();
        lobbyUI.idlePanel        = mpIdlePanel;
        lobbyUI.hostPanel        = mpHostPanel;
        lobbyUI.joinPanel        = mpJoinPanel;
        lobbyUI.hostButton       = mpHostBtn;
        lobbyUI.joinButton       = mpJoinBtn;
        lobbyUI.hostCancelButton = mpHostCancelBtn;
        lobbyUI.joinCancelButton = mpJoinCancelBtn;
        lobbyUI.backButton       = mpBackBtn;
        lobbyUI.joinConfirmButton   = mpJoinConfirmBtn;
        lobbyUI.roomCodeLabel    = mpRoomCodeLabel;
        lobbyUI.hostStatusLabel  = mpHostStatusLabel;
        lobbyUI.codeInputField   = mpCodeInput;
        lobbyUI.joinStatusLabel  = mpJoinStatusLabel;

        // Wire multiplayer button to show lobby panel
        multiplayerBtn.onClick.AddListener(() => lobbyPanelGO.SetActive(true));

        // ── MultiplayerNetworkManager bootstrap ───────────────────────────
        var mpGO = new GameObject("MultiplayerNetworkManager");
        mpGO.transform.SetParent(null);
        mpGO.AddComponent<MultiplayerNetworkManager>();

        // ── Netcode NetworkManager ────────────────────────────────────────
        // Unity requires exactly one NetworkManager in the scene (DontDestroyOnLoad).
        var netGO        = new GameObject("NetworkManager");
        netGO.transform.SetParent(null);
        var netManager   = netGO.AddComponent<NetworkManager>();
        var transport    = netGO.AddComponent<UnityTransport>();
        netGO.AddComponent<MultiplayerBattleSync>();
        netManager.NetworkConfig.NetworkTransport = transport;

        // ── GameManager bootstrap ─────────────────────────────────────────
        // Ensure a GameManager exists in the scene so it persists into subsequent scenes.
        var gmGO = new GameObject("GameManager");
        gmGO.transform.SetParent(null);
        var gm = gmGO.AddComponent<GameManager>();
        gm.mainMenuSceneName = "MainMenuScene";
        gm.shopSceneName     = "ShopScene"; // matches Assets/Scenes/ShopScene.unity
        gm.battleSceneName   = "BattleScene";
        gm.winsToVictory     = 13;
        gmGO.AddComponent<SceneTransitionManager>();

        // ── Populate PokemonDatabase ──────────────────────────────────────
        PopulatePokemonDatabase();

        // ── Save ──────────────────────────────────────────────────────────
        EditorUtility.SetDirty(canvasGO);
        EditorUtility.SetDirty(overlayCanvasGO);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenuScene.unity");

        // ── Register all game scenes in Build Settings ────────────────────
        EnsureSceneInBuildSettings("Assets/Scenes/MainMenuScene.unity");
        EnsureSceneInBuildSettings("Assets/Scenes/ShopScene.unity");
        EnsureSceneInBuildSettings("Assets/Scenes/BattleScene.unity");

        Debug.Log("Main Menu Scene saved. All scenes registered in Build Settings.");
    }

    // ================================================================
    // SETTINGS PANEL
    // ================================================================

    static GameObject BuildSettingsPanel(Transform root,
        out Slider musicSlider, out Slider sfxSlider, out Slider weatherSlider,
        out TMP_Dropdown resDropdown, out Toggle windowedToggle, out Button closeBtn)
    {
        var panel     = CreatePanel(root, "SettingsPanel", new Vector2(760, 560), Vector2.zero);
        SetColor(panel, new Color(0.08f, 0.08f, 0.12f, 0.97f));
        panel.AddComponent<Outline>().effectColor = new Color(0.4f, 0.4f, 0.6f, 0.8f);

        // Title
        var title = CreateTMP(panel.transform, "Title", "Settings", 42,
            new Vector2(0, 220), new Vector2(600, 70));
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;

        // Close button
        closeBtn = CreateButton(panel.transform, "CloseButton", "", new Vector2(50, 50), new Vector2(340, 220));
        SetButtonColor(closeBtn, new Color(0.6f, 0.1f, 0.1f));
        SetButtonIcon(closeBtn, "Assets/Resources/Icons/X.png");

        // Divider
        var divider     = new GameObject("Divider");
        var divRect     = divider.AddComponent<RectTransform>();
        var divImg      = divider.AddComponent<Image>();
        divider.transform.SetParent(panel.transform, false);
        divRect.sizeDelta        = new Vector2(680, 2);
        divRect.anchoredPosition = new Vector2(0, 178);
        divImg.color             = new Color(0.4f, 0.4f, 0.6f, 0.5f);

        // Resolution row
        CreateLabel(panel.transform, "ResLabel", "Resolution", new Vector2(-230, 130));
        resDropdown = CreateDropdown(panel.transform, "ResDropdown", new Vector2(310, 45), new Vector2(120, 130));

        // Windowed toggle row
        CreateLabel(panel.transform, "WindowedLabel", "Windowed", new Vector2(-230, 70));
        windowedToggle = CreateToggle(panel.transform, "WindowedToggle", new Vector2(120, 70));

        // Volume rows
        musicSlider   = CreateSliderRow(panel.transform, "Music",   new Vector2(0,   0));
        sfxSlider     = CreateSliderRow(panel.transform, "SFX",     new Vector2(0, -70));
        weatherSlider = CreateSliderRow(panel.transform, "Weather", new Vector2(0,-140));

        return panel;
    }

    static Slider CreateSliderRow(Transform parent, string label, Vector2 pos)
    {
        CreateLabel(parent, $"{label}Label", label, new Vector2(pos.x - 230, pos.y));

        var sliderGO = new GameObject($"{label}Slider");
        var slRect   = sliderGO.AddComponent<RectTransform>();
        var slider   = sliderGO.AddComponent<Slider>();
        sliderGO.transform.SetParent(parent, false);
        slRect.sizeDelta        = new Vector2(380, 30);
        slRect.anchoredPosition = new Vector2(pos.x + 90, pos.y);
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value    = 1f;

        // Track background
        var trackBG    = new GameObject("Background");
        var trackRect  = trackBG.AddComponent<RectTransform>();
        var trackImg   = trackBG.AddComponent<Image>();
        trackBG.transform.SetParent(sliderGO.transform, false);
        trackRect.anchorMin        = new Vector2(0, 0.25f);
        trackRect.anchorMax        = new Vector2(1, 0.75f);
        trackRect.offsetMin        = Vector2.zero;
        trackRect.offsetMax        = Vector2.zero;
        trackImg.color             = new Color(0.2f, 0.2f, 0.2f, 1f);
        slider.targetGraphic       = trackImg;

        // Fill area
        var fillArea   = new GameObject("Fill Area");
        var fillRect   = fillArea.AddComponent<RectTransform>();
        fillArea.transform.SetParent(sliderGO.transform, false);
        fillRect.anchorMin  = new Vector2(0, 0.25f);
        fillRect.anchorMax  = new Vector2(1, 0.75f);
        fillRect.offsetMin  = new Vector2(5, 0);
        fillRect.offsetMax  = new Vector2(-5, 0);

        var fill     = new GameObject("Fill");
        var fillFR   = fill.AddComponent<RectTransform>();
        var fillImg  = fill.AddComponent<Image>();
        fill.transform.SetParent(fillArea.transform, false);
        fillFR.anchorMin  = Vector2.zero;
        fillFR.anchorMax  = new Vector2(1, 1);
        fillFR.offsetMin  = Vector2.zero;
        fillFR.offsetMax  = Vector2.zero;
        fillImg.color     = new Color(0.2f, 0.7f, 0.3f, 1f);
        slider.fillRect   = fillFR;

        // Handle area
        var handleArea   = new GameObject("Handle Slide Area");
        var handleARect  = handleArea.AddComponent<RectTransform>();
        handleArea.transform.SetParent(sliderGO.transform, false);
        handleARect.anchorMin = Vector2.zero;
        handleARect.anchorMax = Vector2.one;
        handleARect.offsetMin = new Vector2(10, 0);
        handleARect.offsetMax = new Vector2(-10, 0);

        var handle      = new GameObject("Handle");
        var handleRect  = handle.AddComponent<RectTransform>();
        var handleImg   = handle.AddComponent<Image>();
        handle.transform.SetParent(handleArea.transform, false);
        handleRect.sizeDelta = new Vector2(24, 24);
        handleImg.color      = new Color(0.9f, 0.9f, 0.9f, 1f);
        slider.handleRect    = handleRect;

        return slider;
    }

    static TMP_InputField CreateInputField(Transform parent, string name, string placeholder,
        Vector2 size, Vector2 pos)
    {
        var go    = new GameObject(name);
        var rect  = go.AddComponent<RectTransform>();
        var img   = go.AddComponent<Image>();
        var field = go.AddComponent<TMP_InputField>();
        go.transform.SetParent(parent, false);
        rect.sizeDelta        = size;
        rect.anchoredPosition = pos;
        img.color = new Color(0.12f, 0.12f, 0.20f, 1f);

        // Text Area — clips the text so it doesn't overflow the field
        var areaGO   = new GameObject("Text Area");
        var areaRect = areaGO.AddComponent<RectTransform>();
        areaGO.AddComponent<RectMask2D>();
        areaGO.transform.SetParent(go.transform, false);
        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = new Vector2(8, 2);
        areaRect.offsetMax = new Vector2(-8, -2);

        // Placeholder
        var phGO   = new GameObject("Placeholder");
        var phRect = phGO.AddComponent<RectTransform>();
        var phTMP  = phGO.AddComponent<TextMeshProUGUI>();
        phGO.transform.SetParent(areaGO.transform, false);
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;
        phTMP.text       = placeholder;
        phTMP.fontSize   = 18;
        phTMP.color      = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        phTMP.fontStyle  = FontStyles.Italic;
        phTMP.alignment  = TextAlignmentOptions.MidlineLeft;

        // Input text
        var txtGO   = new GameObject("Text");
        var txtRect = txtGO.AddComponent<RectTransform>();
        var txtTMP  = txtGO.AddComponent<TextMeshProUGUI>();
        txtGO.transform.SetParent(areaGO.transform, false);
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
        txtTMP.fontSize   = 18;
        txtTMP.color      = Color.white;
        txtTMP.alignment  = TextAlignmentOptions.MidlineLeft;

        field.textViewport  = areaRect;
        field.textComponent = txtTMP;
        field.placeholder   = phTMP;

        return field;
    }

    static TMP_Dropdown CreateDropdown(Transform parent, string name, Vector2 size, Vector2 pos)
    {
        var go       = new GameObject(name);
        var rect     = go.AddComponent<RectTransform>();
        var img      = go.AddComponent<Image>();
        var dropdown = go.AddComponent<TMP_Dropdown>();
        go.transform.SetParent(parent, false);
        rect.sizeDelta        = size;
        rect.anchoredPosition = pos;
        img.color = new Color(0.2f, 0.2f, 0.3f, 1f);

        // Label
        var labelGO   = new GameObject("Label");
        var labelRect = labelGO.AddComponent<RectTransform>();
        var labelTMP  = labelGO.AddComponent<TextMeshProUGUI>();
        labelGO.transform.SetParent(go.transform, false);
        labelRect.anchorMin  = Vector2.zero;
        labelRect.anchorMax  = Vector2.one;
        labelRect.offsetMin  = new Vector2(10, 6);
        labelRect.offsetMax  = new Vector2(-30, -7);
        labelTMP.text        = "Option A";
        labelTMP.fontSize    = 18;
        labelTMP.color       = Color.white;
        labelTMP.alignment   = TextAlignmentOptions.Left;
        dropdown.captionText = labelTMP;

        // Arrow (simple text placeholder)
        var arrowGO   = new GameObject("Arrow");
        var arrowRect = arrowGO.AddComponent<RectTransform>();
        var arrowTMP  = arrowGO.AddComponent<TextMeshProUGUI>();
        arrowGO.transform.SetParent(go.transform, false);
        arrowRect.anchorMin        = new Vector2(1, 0);
        arrowRect.anchorMax        = Vector2.one;
        arrowRect.offsetMin        = new Vector2(-30, 0);
        arrowRect.offsetMax        = Vector2.zero;
        arrowTMP.text              = "▼";
        arrowTMP.fontSize          = 16;
        arrowTMP.color             = Color.white;
        arrowTMP.alignment         = TextAlignmentOptions.Center;

        // Template (required by TMP_Dropdown but hidden)
        var templateGO   = new GameObject("Template");
        var templateRect = templateGO.AddComponent<RectTransform>();
        var templateImg  = templateGO.AddComponent<Image>();
        var templateSR   = templateGO.AddComponent<ScrollRect>();
        templateGO.transform.SetParent(go.transform, false);
        templateRect.anchorMin        = new Vector2(0, 0);
        templateRect.anchorMax        = new Vector2(1, 0);
        templateRect.pivot            = new Vector2(0.5f, 1);
        templateRect.sizeDelta        = new Vector2(0, 150);
        templateRect.anchoredPosition = new Vector2(0, 2);
        templateImg.color             = new Color(0.15f, 0.15f, 0.2f, 1f);
        templateGO.SetActive(false);
        dropdown.template = templateRect;

        // Viewport inside template
        var vpGO   = new GameObject("Viewport");
        var vpRect = vpGO.AddComponent<RectTransform>();
        vpGO.AddComponent<Image>();
        vpGO.AddComponent<Mask>().showMaskGraphic = false;
        vpGO.transform.SetParent(templateGO.transform, false);
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = Vector2.zero;
        templateSR.viewport = vpRect;

        // Content inside viewport
        var contentGO   = new GameObject("Content");
        var contentRect = contentGO.AddComponent<RectTransform>();
        contentGO.transform.SetParent(vpGO.transform, false);
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot     = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 28);
        templateSR.content = contentRect;

        // Item inside content
        var itemGO   = new GameObject("Item");
        var itemRect = itemGO.AddComponent<RectTransform>();
        var itemImg  = itemGO.AddComponent<Image>();
        var itemToggle = itemGO.AddComponent<Toggle>();
        itemGO.transform.SetParent(contentGO.transform, false);
        itemRect.anchorMin  = new Vector2(0, 0.5f);
        itemRect.anchorMax  = new Vector2(1, 0.5f);
        itemRect.sizeDelta  = new Vector2(0, 28);
        itemImg.color       = Color.clear;
        dropdown.itemText   = null; // set below

        var itemLabelGO   = new GameObject("Item Label");
        var itemLabelRect = itemLabelGO.AddComponent<RectTransform>();
        var itemLabelTMP  = itemLabelGO.AddComponent<TextMeshProUGUI>();
        itemLabelGO.transform.SetParent(itemGO.transform, false);
        itemLabelRect.anchorMin = Vector2.zero;
        itemLabelRect.anchorMax = Vector2.one;
        itemLabelRect.offsetMin = new Vector2(10, 0);
        itemLabelRect.offsetMax = Vector2.zero;
        itemLabelTMP.text       = "Option";
        itemLabelTMP.fontSize   = 16;
        itemLabelTMP.color      = Color.white;
        itemLabelTMP.alignment  = TextAlignmentOptions.Left;
        dropdown.itemText = itemLabelTMP;

        itemToggle.targetGraphic = itemImg;

        return dropdown;
    }

    static Toggle CreateToggle(Transform parent, string name, Vector2 pos)
    {
        var go     = new GameObject(name);
        var rect   = go.AddComponent<RectTransform>();
        var toggle = go.AddComponent<Toggle>();
        go.transform.SetParent(parent, false);
        rect.sizeDelta        = new Vector2(40, 40);
        rect.anchoredPosition = pos;

        // Background (unchecked visual)
        var bgGO   = new GameObject("Background");
        var bgRect = bgGO.AddComponent<RectTransform>();
        var bgImg  = bgGO.AddComponent<Image>();
        bgGO.transform.SetParent(go.transform, false);
        bgRect.anchorMin        = Vector2.zero;
        bgRect.anchorMax        = Vector2.one;
        bgRect.offsetMin        = Vector2.zero;
        bgRect.offsetMax        = Vector2.zero;
        bgImg.color             = new Color(0.2f, 0.2f, 0.3f, 1f);
        toggle.targetGraphic    = bgImg;

        // Checkmark (checked visual)
        var checkGO   = new GameObject("Checkmark");
        var checkRect = checkGO.AddComponent<RectTransform>();
        var checkImg  = checkGO.AddComponent<Image>();
        checkGO.transform.SetParent(bgGO.transform, false);
        checkRect.anchorMin        = new Vector2(0.15f, 0.15f);
        checkRect.anchorMax        = new Vector2(0.85f, 0.85f);
        checkRect.offsetMin        = Vector2.zero;
        checkRect.offsetMax        = Vector2.zero;
        checkImg.color             = new Color(0.3f, 0.8f, 0.3f, 1f);
        toggle.graphic             = checkImg;

        return toggle;
    }


    // ================================================================
    // POKÉDEX PANEL
    // ================================================================

    static GameObject BuildPokedexPanel(Transform root,
        out RectTransform   cardContainer,
        out GameObject      detailPanel,
        out Image           detailSprite,
        out TextMeshProUGUI detailName,
        out TextMeshProUGUI detailTypes,
        out TextMeshProUGUI detailStats,
        out TextMeshProUGUI detailAbility,
        out RectTransform   evolutionContainer,
        out Image           detailTypeIcon,
        out TMP_InputField  searchInput,
        out TMP_Dropdown    typeDropdown,
        out TMP_Dropdown    tierDropdown,
        out Button          closeBtn)
    {
        var panel = CreatePanel(root, "PokedexPanel", new Vector2(1660, 880), Vector2.zero);
        SetColor(panel, new Color(0.06f, 0.06f, 0.10f, 0.97f));
        panel.AddComponent<Outline>().effectColor = new Color(0.3f, 0.3f, 0.6f, 0.8f);

        // Title — centered across the full panel width
        var title = CreateTMP(panel.transform, "Title", "Pokédex", 52,
            new Vector2(0, 392), new Vector2(1400, 80));
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;

        // Close button
        closeBtn = CreateButton(panel.transform, "CloseButton", "", new Vector2(55, 55), new Vector2(790, 392));
        SetButtonColor(closeBtn, new Color(0.6f, 0.1f, 0.1f));
        SetButtonIcon(closeBtn, "Assets/Resources/Icons/X.png");

        // ── Filter bar (above the scroll view) ────────────────────────────
        // Scroll view spans x: -795 to -45 (width 750, center -420)
        // Search: 420px, Type: 180px, Tier: 140px — with 5px gaps
        searchInput  = CreateInputField(panel.transform, "SearchInput",
            "Search name or ability...", new Vector2(420, 40), new Vector2(-585, 310));
        typeDropdown = CreateDropdown(panel.transform, "TypeDropdown",
            new Vector2(180, 40), new Vector2(-280, 310));
        tierDropdown = CreateDropdown(panel.transform, "TierDropdown",
            new Vector2(140, 40), new Vector2(-115, 310));

        // Give them placeholder captions so they're recognisable before runtime populates them
        if (typeDropdown.captionText != null) typeDropdown.captionText.text = "All Types";
        if (tierDropdown.captionText != null) tierDropdown.captionText.text = "All Tiers";

        // ── Left side: ScrollView with grid (slightly shorter to fit filter bar) ──
        var (_, content) = CreateScrollView(panel.transform, "PokedexScrollView",
            new Vector2(750, 720), new Vector2(-420, -65));
        cardContainer = content;

        // ── Right side: Detail panel ──────────────────────────────────────
        var rightPanel = CreatePanel(panel.transform, "DetailPanel", new Vector2(780, 770), new Vector2(370, -40));
        SetColor(rightPanel, new Color(0.10f, 0.10f, 0.18f, 1f));

        // Sprite (top portion of detail)
        var spriteGO    = new GameObject("DetailSprite");
        var spriteRect  = spriteGO.AddComponent<RectTransform>();
        detailSprite    = spriteGO.AddComponent<Image>();
        spriteGO.transform.SetParent(rightPanel.transform, false);
        spriteRect.anchoredPosition = new Vector2(0, 230);
        spriteRect.sizeDelta        = new Vector2(280, 220);
        detailSprite.preserveAspect = true;
        detailSprite.color          = Color.clear;

        // Evolution chain container (right of sprite, grows downward from top)
        var evoGO  = new GameObject("EvolutionContainer");
        evolutionContainer = evoGO.AddComponent<RectTransform>();
        evoGO.transform.SetParent(rightPanel.transform, false);
        // Anchor + pivot at top-right so the chain always starts from the top-right corner
        evolutionContainer.anchorMin        = new Vector2(1f, 1f);
        evolutionContainer.anchorMax        = new Vector2(1f, 1f);
        evolutionContainer.pivot            = new Vector2(0.5f, 1f);
        evolutionContainer.sizeDelta        = new Vector2(240f, 300f);
        evolutionContainer.anchoredPosition = new Vector2(0f, -5f);

        // Type icon — top-left, mirroring evolution container
        var typeIconGO   = new GameObject("DetailTypeIcon");
        var typeIconRect = typeIconGO.AddComponent<RectTransform>();
        detailTypeIcon   = typeIconGO.AddComponent<Image>();
        typeIconGO.transform.SetParent(rightPanel.transform, false);
        typeIconRect.anchorMin        = new Vector2(0f, 1f);
        typeIconRect.anchorMax        = new Vector2(0f, 1f);
        typeIconRect.pivot            = new Vector2(0f, 1f);
        typeIconRect.sizeDelta        = new Vector2(48f, 48f);
        typeIconRect.anchoredPosition = new Vector2(10f, -5f);
        detailTypeIcon.preserveAspect = true;
        detailTypeIcon.color          = Color.white;

        // Name
        detailName = CreateTMP(rightPanel.transform, "DetailName", "", 32,
            new Vector2(0, 70), new Vector2(720, 55));
        detailName.alignment = TextAlignmentOptions.Center;

        // Types
        detailTypes = CreateTMP(rightPanel.transform, "DetailTypes", "", 24,
            new Vector2(0, 20), new Vector2(720, 38));
        detailTypes.alignment = TextAlignmentOptions.Center;
        detailTypes.color     = new Color(0.7f, 0.85f, 1f);

        // Divider
        var div     = new GameObject("Divider");
        var divR    = div.AddComponent<RectTransform>();
        var divI    = div.AddComponent<Image>();
        div.transform.SetParent(rightPanel.transform, false);
        divR.sizeDelta        = new Vector2(680, 2);
        divR.anchoredPosition = new Vector2(0, -20);
        divI.color            = new Color(0.3f, 0.3f, 0.5f, 0.6f);

        // Stats
        detailStats = CreateTMP(rightPanel.transform, "DetailStats", "", 26,
            new Vector2(0, -130), new Vector2(520, 200));
        detailStats.alignment  = TextAlignmentOptions.Center;
        detailStats.lineSpacing = 4;

        // Divider between stats and ability
        var div2  = new GameObject("DividerAbility");
        var div2R = div2.AddComponent<RectTransform>();
        var div2I = div2.AddComponent<Image>();
        div2.transform.SetParent(rightPanel.transform, false);
        div2R.sizeDelta        = new Vector2(680, 2);
        div2R.anchoredPosition = new Vector2(0, -245);
        div2I.color            = new Color(0.3f, 0.3f, 0.5f, 0.6f);

        // Ability
        detailAbility = CreateTMP(rightPanel.transform, "DetailAbility", "", 22,
            new Vector2(0, -310), new Vector2(700, 140));
        detailAbility.alignment       = TextAlignmentOptions.Center;
        detailAbility.color           = new Color(0.85f, 0.85f, 0.85f);
        detailAbility.textWrappingMode = TextWrappingModes.Normal;
        detailAbility.overflowMode    = TextOverflowModes.Overflow;

        detailPanel = rightPanel;
        detailPanel.SetActive(false);

        return panel;
    }

    static (ScrollRect scrollRect, RectTransform content) CreateScrollView(
        Transform parent, string name, Vector2 size, Vector2 pos)
    {
        var scrollGO = new GameObject(name);
        var scrollR  = scrollGO.AddComponent<RectTransform>();
        var scrollI  = scrollGO.AddComponent<Image>();
        var scrollC  = scrollGO.AddComponent<ScrollRect>();
        scrollGO.transform.SetParent(parent, false);
        scrollR.sizeDelta        = size;
        scrollR.anchoredPosition = pos;
        scrollI.color = new Color(0.08f, 0.08f, 0.14f, 0.9f);

        // Viewport
        var vpGO   = new GameObject("Viewport");
        var vpRect = vpGO.AddComponent<RectTransform>();
        var vpImg  = vpGO.AddComponent<Image>();
        vpGO.AddComponent<Mask>().showMaskGraphic = false;
        vpGO.transform.SetParent(scrollGO.transform, false);
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = Vector2.zero;
        vpImg.color      = Color.white;

        // Content
        var contentGO   = new GameObject("Content");
        var contentRect = contentGO.AddComponent<RectTransform>();
        contentGO.transform.SetParent(vpGO.transform, false);
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot     = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);

        var grid             = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(137, 160);
        grid.spacing         = new Vector2(10, 10);
        grid.padding         = new RectOffset(12, 12, 12, 12);
        grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment  = TextAnchor.UpperLeft;
        grid.constraint      = GridLayoutGroup.Constraint.Flexible;

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollC.content          = contentRect;
        scrollC.viewport         = vpRect;
        scrollC.horizontal       = false;
        scrollC.vertical         = true;
        scrollC.scrollSensitivity = 35;
        scrollC.movementType     = ScrollRect.MovementType.Clamped;

        return (scrollC, contentRect);
    }

    // ================================================================
    // MULTIPLAYER LOBBY PANEL
    // ================================================================

    static GameObject BuildLobbyPanel(Transform root,
        out Button hostBtn, out Button joinBtn,
        out Button hostCancelBtn, out Button joinCancelBtn, out Button backBtn,
        out Button joinConfirmBtn,
        out TextMeshProUGUI roomCodeLabel, out TextMeshProUGUI hostStatusLabel,
        out TMP_InputField codeInput, out TextMeshProUGUI joinStatusLabel,
        out GameObject idlePanel, out GameObject hostPanel, out GameObject joinPanel)
    {
        var panel = CreatePanel(root, "MultiplayerLobbyPanel", new Vector2(600, 500), Vector2.zero);
        SetColor(panel, new Color(0.07f, 0.05f, 0.10f, 0.97f));
        panel.AddComponent<Outline>().effectColor = new Color(0.5f, 0.2f, 0.7f, 0.8f);

        // Title
        var title = CreateTMP(panel.transform, "Title", "Multiplayer", 46,
            new Vector2(0, 210), new Vector2(520, 70));
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;

        // Back button (top-right)
        backBtn = CreateButton(panel.transform, "BackButton", "", new Vector2(50, 50), new Vector2(260, 210));
        SetButtonColor(backBtn, new Color(0.5f, 0.1f, 0.1f));
        SetButtonIcon(backBtn, "Assets/Resources/Icons/X.png");

        // ── Idle panel: Host / Join ────────────────────────────────────────
        var idleGO = new GameObject("IdlePanel");
        idleGO.AddComponent<RectTransform>();
        idleGO.transform.SetParent(panel.transform, false);

        hostBtn = CreateButton(idleGO.transform, "HostButton", "Host Game",
            new Vector2(280, 70), new Vector2(0, 50));
        SetButtonColor(hostBtn, new Color(0.18f, 0.55f, 0.18f));

        joinBtn = CreateButton(idleGO.transform, "JoinButton", "Join Game",
            new Vector2(280, 70), new Vector2(0, -40));
        SetButtonColor(joinBtn, new Color(0.18f, 0.28f, 0.65f));

        idlePanel = idleGO;

        // ── Host panel: room code display ─────────────────────────────────
        var hostGO = new GameObject("HostPanel");
        hostGO.AddComponent<RectTransform>();
        hostGO.transform.SetParent(panel.transform, false);

        roomCodeLabel = CreateTMP(hostGO.transform, "RoomCodeLabel", "Room Code\n----", 36,
            new Vector2(0, 60), new Vector2(500, 120));
        roomCodeLabel.alignment = TextAlignmentOptions.Center;

        hostStatusLabel = CreateTMP(hostGO.transform, "HostStatusLabel", "Waiting for opponent...", 22,
            new Vector2(0, -40), new Vector2(500, 40));
        hostStatusLabel.alignment = TextAlignmentOptions.Center;
        hostStatusLabel.color     = Color.yellow;

        hostCancelBtn = CreateButton(hostGO.transform, "HostCancelButton", "Cancel",
            new Vector2(200, 55), new Vector2(0, -120));
        SetButtonColor(hostCancelBtn, new Color(0.55f, 0.12f, 0.12f));

        hostPanel = hostGO;
        hostGO.SetActive(false);

        // ── Join panel: code input ─────────────────────────────────────────
        var joinGO = new GameObject("JoinPanel");
        joinGO.AddComponent<RectTransform>();
        joinGO.transform.SetParent(panel.transform, false);

        var joinPrompt = CreateTMP(joinGO.transform, "JoinPrompt", "Enter room code:", 26,
            new Vector2(0, 80), new Vector2(480, 40));
        joinPrompt.alignment = TextAlignmentOptions.Center;

        codeInput = CreateInputField(joinGO.transform, "CodeInputField", "e.g. XK47",
            new Vector2(220, 55), new Vector2(0, 20));

        joinConfirmBtn = CreateButton(joinGO.transform, "JoinConfirmButton", "Join",
            new Vector2(200, 55), new Vector2(0, -50));
        SetButtonColor(joinConfirmBtn, new Color(0.18f, 0.55f, 0.18f));

        joinStatusLabel = CreateTMP(joinGO.transform, "JoinStatusLabel", "", 20,
            new Vector2(0, -115), new Vector2(480, 36));
        joinStatusLabel.alignment = TextAlignmentOptions.Center;

        joinCancelBtn = CreateButton(joinGO.transform, "JoinCancelButton", "Cancel",
            new Vector2(200, 55), new Vector2(0, -160));
        SetButtonColor(joinCancelBtn, new Color(0.55f, 0.12f, 0.12f));

        joinPanel = joinGO;
        joinGO.SetActive(false);

        return panel;
    }

    // ================================================================
    // POKEMON DATABASE
    // ================================================================

    static void PopulatePokemonDatabase()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        const string dbPath = "Assets/Resources/PokemonDatabase.asset";
        var db = AssetDatabase.LoadAssetAtPath<PokemonDatabase>(dbPath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<PokemonDatabase>();
            AssetDatabase.CreateAsset(db, dbPath);
        }

        var guids = AssetDatabase.FindAssets("t:PokemonData");
        db.allPokemon = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<PokemonData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null)
            .OrderBy(p => p.id)
            .ToArray();

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log($"PokemonDatabase updated — {db.allPokemon.Length} Pokémon indexed.");
    }

    // ================================================================
    // HELPERS
    // ================================================================

    static GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 pos)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        go.AddComponent<Image>();
        go.transform.SetParent(parent, false);
        rect.sizeDelta        = size;
        rect.anchoredPosition = pos;
        return go;
    }

    static TextMeshProUGUI CreateTMP(Transform parent, string name, string text,
        int fontSize, Vector2 pos, Vector2 size)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        go.transform.SetParent(parent, false);
        rect.sizeDelta        = size;
        rect.anchoredPosition = pos;
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    static void CreateLabel(Transform parent, string name, string text, Vector2 pos)
    {
        var tmp = CreateTMP(parent, name, text, 22, pos, new Vector2(220, 40));
        tmp.alignment = TextAlignmentOptions.Left;
    }

    static Button CreateButton(Transform parent, string name, string label,
        Vector2 size, Vector2 pos)
    {
        var go  = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        var img  = go.AddComponent<Image>();
        var btn  = go.AddComponent<Button>();
        go.transform.SetParent(parent, false);
        rect.sizeDelta        = size;
        rect.anchoredPosition = pos;
        img.color = new Color(0.25f, 0.25f, 0.25f, 1f);

        var labelGO   = new GameObject("Label");
        var labelRect = labelGO.AddComponent<RectTransform>();
        var labelTMP  = labelGO.AddComponent<TextMeshProUGUI>();
        labelGO.transform.SetParent(go.transform, false);
        labelRect.sizeDelta        = size;
        labelRect.anchoredPosition = Vector2.zero;
        labelTMP.text      = label;
        labelTMP.fontSize  = 26;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.color     = Color.white;

        return btn;
    }

    // Hides the text label on a button and fills it with a sprite icon instead.
    static void SetButtonIcon(Button button, string spritePath)
    {
        var labelTransform = button.transform.Find("Label");
        if (labelTransform != null)
            labelTransform.GetComponent<TextMeshProUGUI>().text = "";

        var go   = new GameObject("Icon");
        var rt   = go.AddComponent<RectTransform>();
        var img  = go.AddComponent<Image>();
        go.transform.SetParent(button.transform, false);
        rt.anchorMin = new Vector2(0.1f, 0.1f);
        rt.anchorMax = new Vector2(0.9f, 0.9f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite != null) img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget  = false;
    }

    static void AddOverlayToggle(GameObject go, GlobalOverlayToggle.Target target)
    {
        var tog = go.AddComponent<GlobalOverlayToggle>();
        tog.target = target;
    }

    static void SetColor(GameObject go, Color color)
    {
        var img = go.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    static void SetButtonColor(Button btn, Color color)
    {
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    static UnityEngine.Audio.AudioMixerGroup FindMixerGroup(
        UnityEngine.Audio.AudioMixer mixer, string groupName)
    {
        var groups = mixer.FindMatchingGroups(groupName);
        return groups != null && groups.Length > 0 ? groups[0] : null;
    }

    static void EnsureSceneInBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"Added to Build Settings: {scenePath}");
    }

    // ================================================================
    // HELPER PANEL
    // ================================================================

    static readonly string[] HelperPageTitles = new[]
    {
        "How to Win",
        "Bait & Release",
        "Weather"
    };

    static readonly string[] HelperPageTexts = new[]
    {
        // Page 1 — Goal
        "Your goal is to earn <b>8 Gym Badges</b> by defeating Gym Leaders.\n\n" +
        "Each badge you collect unlocks a harder opponent and expands the tier of " +
        "Pokémon available in the shop.\n\n" +
        "Once all 8 badges are yours, you face the <b>Elite Four</b>. " +
        "Each Elite Four member is followed by a shop break so you can adjust your team.\n\n" +
        "Defeat all four and you earn the right to challenge the <b>Champion</b>. " +
        "Defeat the Champion to complete your run and enter the Hall of Fame.",

        // Page 2 — Bait & Release
        "<b>Bait</b> — Select a Pokémon in the shop, then click the Bait button to lock that slot. " +
        "A baited slot is marked and will not be replaced when you reroll — " +
        "useful when you see a Pokémon you want but can't afford yet.\n\n" +
        "<b>Release</b> — Select a Pokémon on your battle row or bench, then press the Release button. " +
        "The Pokémon is gone immediately — no refund is given, so only release when " +
        "you're sure you no longer need it.",

        // Page 3 — Weather
        "At the start of certain battles a random <b>weather condition</b> is set. " +
        "Weather affects all Pokémon on the field for the entire battle.\n\n\n" +
        "<b>Sun</b> — Fire-type moves deal bonus damage. " +
        "Water-type moves deal reduced damage.\n\n" +
        "<b>Rain</b> — Water-type moves deal bonus damage. " +
        "Fire-type moves deal reduced damage.\n\n" +
        "<b>Sandstorm</b> — Ground types are unaffected. " +
        "All others lose HP each round unless they have a protective ability."
    };

    static GameObject BuildHelperPanel(Transform root, out Button closeBtn)
    {
        var panel = CreatePanel(root, "HelperPanel", new Vector2(1200, 780), Vector2.zero);
        SetColor(panel, new Color(0.06f, 0.08f, 0.06f, 0.97f));
        panel.AddComponent<Outline>().effectColor = new Color(0.2f, 0.45f, 0.2f, 0.8f);

        // Title
        var title = CreateTMP(panel.transform, "Title", "How to Play", 46,
            new Vector2(0, 340), new Vector2(1100, 70));
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;

        // Close button
        closeBtn = CreateButton(panel.transform, "CloseHelperBtn", "X",
            new Vector2(60, 60), new Vector2(560, 340));
        SetButtonColor(closeBtn, new Color(0.55f, 0.12f, 0.12f));

        // Page indicator  (e.g. "1 / 3")
        var pageIndicatorTMP = CreateTMP(panel.transform, "PageIndicator", "1 / 3", 22,
            new Vector2(0, -320), new Vector2(300, 40));
        pageIndicatorTMP.alignment = TextAlignmentOptions.Center;

        // Prev / Next buttons
        var prevBtn = CreateButton(panel.transform, "PrevBtn", "<", new Vector2(80, 60), new Vector2(-560, -320));
        var nextBtn = CreateButton(panel.transform, "NextBtn", ">", new Vector2(80, 60), new Vector2( 560, -320));
        SetButtonColor(prevBtn, new Color(0.2f, 0.35f, 0.2f));
        SetButtonColor(nextBtn, new Color(0.2f, 0.35f, 0.2f));

        // Build one child page GameObject per page
        var pages = new GameObject[HelperPageTitles.Length];
        for (int i = 0; i < HelperPageTitles.Length; i++)
        {
            var page = CreatePanel(panel.transform, $"Page_{i}", new Vector2(1100, 560), new Vector2(0, -10));
            page.GetComponent<Image>().color = Color.clear;

            var pageTitleTMP = CreateTMP(page.transform, "PageTitle", HelperPageTitles[i], 32,
                new Vector2(0, 248), new Vector2(1060, 50));
            pageTitleTMP.fontStyle = FontStyles.Bold;
            pageTitleTMP.alignment = TextAlignmentOptions.Center;

            var bodyTMP = CreateTMP(page.transform, "Body", HelperPageTexts[i], 20,
                new Vector2(0, 80), new Vector2(1020, 280));
            bodyTMP.alignment          = TextAlignmentOptions.TopLeft;
            bodyTMP.textWrappingMode = TextWrappingModes.Normal;
            bodyTMP.wordSpacing        = 0;
            bodyTMP.lineSpacing        = 0;

            var imgGO   = new GameObject("PageImage");
            var imgRect = imgGO.AddComponent<RectTransform>();
            var imgComp = imgGO.AddComponent<Image>();
            imgGO.transform.SetParent(page.transform, false);
            imgRect.sizeDelta        = new Vector2(600, 180);
            imgRect.anchoredPosition = new Vector2(0, -170);
            var pageSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/Icons/helper{i + 1}.png");
            if (pageSprite != null) { imgComp.sprite = pageSprite; imgComp.preserveAspect = true; }
            else imgComp.color = new Color(1, 1, 1, 0);

            pages[i] = page;
        }

        // Wire HelperPanelUI component
        var helperUI = panel.AddComponent<HelperPanelUI>();
        helperUI.pages         = pages;
        helperUI.pageIndicator = pageIndicatorTMP;
        helperUI.prevButton    = prevBtn.GetComponent<Button>();
        helperUI.nextButton    = nextBtn.GetComponent<Button>();

        return panel;
    }
}
