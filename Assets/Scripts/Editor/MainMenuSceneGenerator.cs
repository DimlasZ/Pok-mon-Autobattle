using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using TMPro;

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
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
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
            new Vector2(0, 260), new Vector2(1200, 120));
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color     = Color.white;

        // ── Main Buttons ──────────────────────────────────────────────────
        var playBtn     = CreateButton(root, "PlayButton",     "Play Now",  new Vector2(320, 75), new Vector2(0,  150));
        var pokedexBtn  = CreateButton(root, "PokedexButton",  "Pokédex",   new Vector2(320, 75), new Vector2(0,   50));
        var settingsBtn = CreateButton(root, "SettingsButton", "Settings",  new Vector2(320, 75), new Vector2(0,  -50));
        var quitBtn     = CreateButton(root, "QuitButton",     "Quit",      new Vector2(320, 75), new Vector2(0, -150));

        SetButtonColor(playBtn,    new Color(0.18f, 0.58f, 0.18f));
        SetButtonColor(pokedexBtn, new Color(0.18f, 0.28f, 0.68f));
        SetButtonColor(quitBtn,    new Color(0.58f, 0.12f, 0.12f));
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
        overlayScaler.screenMatchMode      = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        overlayScaler.matchWidthOrHeight   = 0.5f;

        Transform overlayRoot = overlayCanvasGO.transform;

        // ── Settings Panel (overlay, hidden) ─────────────────────────────
        var settingsPanelGO = BuildSettingsPanel(overlayRoot,
            out Slider musicSlider, out Slider sfxSlider, out Slider weatherSlider,
            out TMP_Dropdown resDropdown,
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

        // ── GlobalOverlayManager (DontDestroyOnLoad singleton) ───────────
        var overlayMgr = overlayCanvasGO.AddComponent<GlobalOverlayManager>();
        overlayMgr.settingsPanel     = settingsPanelGO;
        overlayMgr.pokedexPanel      = pokedexPanelGO;
        overlayMgr.musicSlider       = musicSlider;
        overlayMgr.sfxSlider         = sfxSlider;
        overlayMgr.weatherSlider     = weatherSlider;
        overlayMgr.resolutionDropdown = resDropdown;

        // Wire close buttons via GlobalOverlayToggle (toggle = close when panel is open)
        AddOverlayToggle(closeSettingsBtn.gameObject, GlobalOverlayToggle.Target.Settings);
        AddOverlayToggle(closePokedexBtn.gameObject,  GlobalOverlayToggle.Target.Pokedex);

        // Wire main-menu settings/pokédex buttons the same way
        AddOverlayToggle(settingsBtn.gameObject, GlobalOverlayToggle.Target.Settings);
        AddOverlayToggle(pokedexBtn.gameObject,  GlobalOverlayToggle.Target.Pokedex);

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
        controller.playButton = playBtn;
        controller.quitButton = quitBtn;

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

        // ── GameManager bootstrap ─────────────────────────────────────────
        // Ensure a GameManager exists in the scene so it persists into subsequent scenes.
        var gmGO = new GameObject("GameManager");
        gmGO.transform.SetParent(null);
        var gm = gmGO.AddComponent<GameManager>();
        gm.mainMenuSceneName = "MainMenuScene";
        gm.shopSceneName     = "ShopScene";
        gm.battleSceneName   = "BattleScene";
        gm.winsToVictory     = 13;
        gmGO.AddComponent<SceneTransitionManager>();

        // ── Populate PokemonDatabase ──────────────────────────────────────
        PopulatePokemonDatabase();

        // ── Save ──────────────────────────────────────────────────────────
        EditorUtility.SetDirty(canvasGO);
        EditorUtility.SetDirty(overlayCanvasGO);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenuScene.unity");

        Debug.Log("Main Menu Scene saved to Assets/Scenes/MainMenuScene.unity — add it as Scene 0 in Build Settings.");
    }

    // ================================================================
    // SETTINGS PANEL
    // ================================================================

    static GameObject BuildSettingsPanel(Transform root,
        out Slider musicSlider, out Slider sfxSlider, out Slider weatherSlider,
        out TMP_Dropdown resDropdown, out Button closeBtn)
    {
        var panel     = CreatePanel(root, "SettingsPanel", new Vector2(760, 520), Vector2.zero);
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
        CreateLabel(panel.transform, "ResLabel", "Resolution", new Vector2(-230, 120));
        resDropdown = CreateDropdown(panel.transform, "ResDropdown", new Vector2(310, 45), new Vector2(120, 120));

        // Volume rows
        musicSlider   = CreateSliderRow(panel.transform, "Music",   new Vector2(0,  40));
        sfxSlider     = CreateSliderRow(panel.transform, "SFX",     new Vector2(0, -40));
        weatherSlider = CreateSliderRow(panel.transform, "Weather", new Vector2(0,-120));

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
}
