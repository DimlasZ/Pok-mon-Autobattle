using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEditor.Events;

// Generates the entire UI layout automatically.
// Run via: Pokemon -> Generate UI Layout

public class UILayoutGenerator
{
    [MenuItem("Pokemon/Generate Shop Scene UI")]
    public static void GenerateLayout()
    {
        // --- Canvas ---
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            var cGO = new GameObject("Canvas");
            Undo.RegisterCreatedObjectUndo(cGO, "Create Canvas");
            canvas = cGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cGO.AddComponent<CanvasScaler>();
            cGO.AddComponent<GraphicRaycaster>();
        }

        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode    = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Transform root = canvas.transform;

        // --- Destroy existing panels so re-running doesn't stack duplicates ---
        string[] managedNames = { "TopBar", "ShopPanel", "BattlePanel", "BenchPanel", "ActionPanel", "StartBattleButton", "Tooltip" };
        foreach (string n in managedNames)
        {
            // Destroy ALL children with this name (handles accidental duplicates)
            var found = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in root)
                if (child.name == n) found.Add(child);
            foreach (var t in found)
                Undo.DestroyObjectImmediate(t.gameObject);
        }

        // --- Top Bar ---
        var topBar = CreatePanel(root, "TopBar", new Vector2(1920, 70), new Vector2(0, 475));
        SetColor(topBar, new Color(0.08f, 0.08f, 0.08f, 0.95f));
        var roundText      = CreateText(topBar.transform, "RoundText",      "Wins 0/10", 28, new Vector2(-700, 0));
        var pokedollarText = CreateText(topBar.transform, "PokedollarText", "P$5",       36, new Vector2(0, 0));
        var hpText         = CreateText(topBar.transform, "HPText",         "HP: 3/3",   28, new Vector2(700, 0));

        // --- Shop Panel ---
        var shopPanel = CreatePanel(root, "ShopPanel", new Vector2(1920, 220), new Vector2(0, 265));
        SetColor(shopPanel, new Color(0.1f, 0.1f, 0.25f, 0.95f));
        CreateLabel(shopPanel.transform, "ShopLabel", "SHOP", new Vector2(-820, 75));

        var shopRow = CreatePanel(shopPanel.transform, "ShopSlotsRow", new Vector2(630, 190), new Vector2(-150, -5));
        SetColor(shopRow, Color.clear);
        AddHorizontalLayout(shopRow, 10);
        var shopSlots = new PokemonSlotUI[3];
        for (int i = 0; i < 3; i++)
            shopSlots[i] = CreateSlot(shopRow.transform, $"ShopSlot_{i}", new Vector2(200, 185));

        var rerollBtn = CreateButton(shopPanel.transform, "RerollButton", "Reroll  P$1", new Vector2(180, 60), new Vector2(750, 0));

        // --- Battle Panel ---
        var battlePanel = CreatePanel(root, "BattlePanel", new Vector2(1920, 220), new Vector2(0, 20));
        SetColor(battlePanel, new Color(0.25f, 0.08f, 0.08f, 0.95f));
        CreateLabel(battlePanel.transform, "BattleLabel", "BATTLE TEAM", new Vector2(-820, 75));

        var battleRow = CreatePanel(battlePanel.transform, "BattleSlotsRow", new Vector2(630, 190), new Vector2(-150, -5));
        SetColor(battleRow, Color.clear);
        AddHorizontalLayout(battleRow, 10);
        var battleSlots = new PokemonSlotUI[3];
        for (int i = 0; i < 3; i++)
            battleSlots[i] = CreateSlot(battleRow.transform, $"BattleSlot_{i}", new Vector2(200, 185));

        // --- Bench Panel ---
        var benchPanel = CreatePanel(root, "BenchPanel", new Vector2(1920, 220), new Vector2(0, -220));
        SetColor(benchPanel, new Color(0.08f, 0.18f, 0.08f, 0.95f));
        CreateLabel(benchPanel.transform, "BenchLabel", "BENCH", new Vector2(-820, 75));

        var benchRow = CreatePanel(benchPanel.transform, "BenchSlotsRow", new Vector2(1250, 190), new Vector2(0, -5));
        SetColor(benchRow, Color.clear);
        AddHorizontalLayout(benchRow, 10);
        var benchSlots = new PokemonSlotUI[6];
        for (int i = 0; i < 6; i++)
            benchSlots[i] = CreateSlot(benchRow.transform, $"BenchSlot_{i}", new Vector2(200, 185));

        // --- Action Buttons Panel ---
        var actionPanel = CreatePanel(root, "ActionPanel", new Vector2(1920, 80), new Vector2(0, -430));
        SetColor(actionPanel, new Color(0.08f, 0.08f, 0.08f, 0.95f));
        var buyBtn          = CreateButton(actionPanel.transform, "BuyButton",          "Buy  P$1",   new Vector2(180, 60), new Vector2(-500, 0));
        var sellBtn         = CreateButton(actionPanel.transform, "SellButton",         "Release",    new Vector2(180, 60), new Vector2(-300, 0));
        var moveToBattleBtn = CreateButton(actionPanel.transform, "MoveToBattleButton", "To Battle",  new Vector2(180, 60), new Vector2(-100, 0));
        var moveToBenchBtn  = CreateButton(actionPanel.transform, "MoveToBenchButton",  "To Bench",   new Vector2(180, 60), new Vector2(100,  0));

        // --- Start Battle Button ---
        var startBattleBtn = CreateButton(root, "StartBattleButton", "START BATTLE", new Vector2(260, 70), new Vector2(800, -450));
        SetButtonColor(startBattleBtn, new Color(0.8f, 0.2f, 0.2f));

        // --- UIManager ---
        var uiManagerGO = GameObject.Find("UIManager");
        if (uiManagerGO == null)
        {
            uiManagerGO = new GameObject("UIManager");
            Undo.RegisterCreatedObjectUndo(uiManagerGO, "Create UIManager");
        }

        var uiManager = uiManagerGO.GetComponent<UIManager>() ?? uiManagerGO.AddComponent<UIManager>();

        uiManager.shopSlots   = shopSlots;
        uiManager.battleSlots = battleSlots;
        uiManager.benchSlots  = benchSlots;

        uiManager.pokedollarText = pokedollarText;
        uiManager.roundText      = roundText;
        uiManager.playerHPText   = hpText;

        uiManager.buyButton          = buyBtn;
        uiManager.sellButton         = sellBtn;
        uiManager.moveToBattleButton = moveToBattleBtn;
        uiManager.moveToBenchButton  = moveToBenchBtn;
        uiManager.rerollButton       = rerollBtn;
        uiManager.startBattleButton  = startBattleBtn;

        // --- Set slot sources and wire clicks ---
        SetupSlots(shopSlots,   ShopManager.SelectionSource.Shop);
        SetupSlots(battleSlots, ShopManager.SelectionSource.Battle);
        SetupSlots(benchSlots,  ShopManager.SelectionSource.Bench);

        // --- Tooltip Panel ---
        UIGeneratorHelpers.CreateTooltip(root);

        EditorUtility.SetDirty(uiManagerGO);
        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("UI Layout generated! Check your scene.");
    }

    // -------------------------------------------------------
    // SLOT SETUP
    // -------------------------------------------------------

    static void SetupSlots(PokemonSlotUI[] slots, ShopManager.SelectionSource source)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].source    = source;
            slots[i].slotIndex = i;

            // Wire the button click to OnClicked()
            var button = slots[i].GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(button.onClick, slots[i].OnClicked);
            EditorUtility.SetDirty(slots[i]);
        }
    }

    // -------------------------------------------------------
    // SLOT CREATION
    // Creates one Pokemon slot with all child elements
    // -------------------------------------------------------

    static PokemonSlotUI CreateSlot(Transform parent, string name, Vector2 size)
    {
        // Root button
        var go     = new GameObject(name);
        var rect   = go.AddComponent<RectTransform>();
        var image  = go.AddComponent<Image>();
        var button = go.AddComponent<Button>();
        var slotUI = go.AddComponent<PokemonSlotUI>();

        go.transform.SetParent(parent, false);
        rect.sizeDelta = size;
        image.color    = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Highlight (yellow border shown when selected)
        var highlight      = CreateChildImage(go.transform, "Highlight", size, Color.clear);
        var highlightOutline = highlight.AddComponent<Outline>();
        highlightOutline.effectColor    = new Color(1f, 0.9f, 0f, 1f);
        highlightOutline.effectDistance = new Vector2(4, 4);
        slotUI.highlight = highlight.GetComponent<Image>();

        // Pokemon sprite (centered, leaving room at bottom for stats)
        var spriteGO    = new GameObject("PokemonSprite");
        var spriteRect  = spriteGO.AddComponent<RectTransform>();
        var spriteImage = spriteGO.AddComponent<Image>();
        spriteGO.transform.SetParent(go.transform, false);
        spriteRect.anchoredPosition          = new Vector2(18f, 25f);
        spriteRect.sizeDelta                 = new Vector2(180f, 120f);
        spriteGO.transform.localEulerAngles  = new Vector3(0f, -180f, 0f);
        spriteImage.preserveAspect           = true;
        spriteImage.color                    = Color.white;
        slotUI.pokemonSprite                 = spriteImage;

        // Load HP bar sprites
        Sprite hpBarBoxSprite   = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/BattleBoxOverlay.png");
        Sprite hpBarTrackSprite = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/UI/BattleUI_sheet3.png"))
            if (a is Sprite s && s.name == "HPBarTrack") { hpBarTrackSprite = s; break; }

        // HP Bar Box (sprite frame)
        var hpBox  = new GameObject("HPBarBox");
        var hpBoxR = hpBox.AddComponent<RectTransform>();
        var hpBoxI = hpBox.AddComponent<Image>();
        hpBox.transform.SetParent(go.transform, false);
        hpBoxR.anchoredPosition = new Vector2(0f, -20.8f);
        hpBoxR.sizeDelta        = new Vector2(186f, 124f);
        if (hpBarBoxSprite != null) { hpBoxI.sprite = hpBarBoxSprite; hpBoxI.type = Image.Type.Sliced; }
        else hpBoxI.color = new Color(0.1f, 0.1f, 0.3f, 0.9f);
        slotUI.hpBarBox = hpBoxI;

        // Speed icon (child of HPBarBox)
        Sprite speedIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/flash.png");
        var speedIconGO   = new GameObject("SpeedImage");
        var speedIconRect = speedIconGO.AddComponent<RectTransform>();
        var speedIconImg  = speedIconGO.AddComponent<Image>();
        speedIconGO.transform.SetParent(hpBox.transform, false);
        speedIconRect.anchoredPosition = new Vector2(-80f, 5f);
        speedIconRect.sizeDelta        = new Vector2(15f, 15f);
        if (speedIconSprite != null) speedIconImg.sprite = speedIconSprite;
        speedIconImg.color = Color.white;

        // HP Bar Track (child of box)
        var barBg  = new GameObject("HealthBarBG");
        var barBgR = barBg.AddComponent<RectTransform>();
        var barBgI = barBg.AddComponent<Image>();
        barBg.transform.SetParent(hpBox.transform, false);
        barBgR.anchoredPosition = new Vector2(39f, -37.6f);
        barBgR.sizeDelta        = new Vector2(86f, 5f);
        if (hpBarTrackSprite != null) { barBgI.sprite = hpBarTrackSprite; barBgI.type = Image.Type.Sliced; }
        else barBgI.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // HP Fill (child of track)
        var barFill  = new GameObject("HealthBarFill");
        var barFillR = barFill.AddComponent<RectTransform>();
        var barFillI = barFill.AddComponent<Image>();
        barFill.transform.SetParent(barBg.transform, false);
        barFillR.anchorMin   = new Vector2(0f, 0f);
        barFillR.anchorMax   = new Vector2(1f, 1f);
        barFillR.offsetMin   = Vector2.zero;
        barFillR.offsetMax   = Vector2.zero;
        barFillI.color       = new Color(0.18f, 0.78f, 0.18f);
        slotUI.healthBarFill = barFillI;

        float third = size.x / 3f;

        // Attack
        slotUI.attackText = CreateTMPText(go.transform, "AttackText", "", 23,
            new Vector2(-40.3f, 21.3f), new Vector2(third, 36));
        slotUI.attackText.alignment        = TextAlignmentOptions.Left;
        slotUI.attackText.fontStyle        = FontStyles.Bold;
        slotUI.attackText.characterSpacing = -10;
        slotUI.attackText.color            = new Color(36/255f, 36/255f, 36/255f);

        // Speed
        slotUI.speedText = CreateTMPText(go.transform, "SpeedText", "", 23,
            new Vector2(-65.5f, -18f), new Vector2(third, 36));
        slotUI.speedText.alignment        = TextAlignmentOptions.Center;
        slotUI.speedText.fontStyle        = FontStyles.Bold;
        slotUI.speedText.characterSpacing = -10;
        slotUI.speedText.color            = new Color(36/255f, 36/255f, 36/255f);

        // HP — bottom right in layout (appears on the left when slot is flipped for player)
        slotUI.hpText = CreateTMPText(go.transform, "HPText", "", 23,
            new Vector2(-67.5f, -60.5f), new Vector2(third, 36));
        slotUI.hpText.alignment        = TextAlignmentOptions.Right;
        slotUI.hpText.fontStyle        = FontStyles.Bold;
        slotUI.hpText.characterSpacing = -10;
        slotUI.hpText.color            = new Color(36/255f, 36/255f, 36/255f);

        return slotUI;
    }

    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

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

    static GameObject CreateChildImage(Transform parent, string name, Vector2 size, Color color)
    {
        var go    = new GameObject(name);
        var rect  = go.AddComponent<RectTransform>();
        var image = go.AddComponent<Image>();
        go.transform.SetParent(parent, false);
        rect.sizeDelta        = size;
        rect.anchoredPosition = Vector2.zero;
        image.color           = color;
        return go;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, Vector2 pos)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        go.transform.SetParent(parent, false);
        rect.sizeDelta        = new Vector2(400, 50);
        rect.anchoredPosition = pos;
        tmp.text              = text;
        tmp.fontSize          = fontSize;
        tmp.alignment         = TextAlignmentOptions.Center;
        tmp.color             = Color.white;
        return tmp;
    }

    static void CreateLabel(Transform parent, string name, string text, Vector2 pos)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        go.transform.SetParent(parent, false);
        rect.sizeDelta        = new Vector2(250, 40);
        rect.anchoredPosition = pos;
        tmp.text              = text;
        tmp.fontSize          = 22;
        tmp.fontStyle         = FontStyles.Bold;
        tmp.alignment         = TextAlignmentOptions.Left;
        tmp.color             = new Color(0.8f, 0.8f, 0.8f, 1f);
    }

    static TextMeshProUGUI CreateTMPText(Transform parent, string name, string text, int fontSize, Vector2 pos, Vector2 size)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        go.transform.SetParent(parent, false);
        rect.sizeDelta        = size;
        rect.anchoredPosition = pos;
        tmp.text              = text;
        tmp.fontSize          = fontSize;
        tmp.alignment         = TextAlignmentOptions.Center;
        tmp.color             = Color.white;
        return tmp;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 pos)
    {
        var go     = new GameObject(name);
        var rect   = go.AddComponent<RectTransform>();
        var image  = go.AddComponent<Image>();
        var button = go.AddComponent<Button>();
        go.transform.SetParent(parent, false);
        rect.sizeDelta        = size;
        rect.anchoredPosition = pos;
        image.color           = new Color(0.25f, 0.25f, 0.25f, 1f);

        // Label inside button
        var labelGO   = new GameObject("Label");
        var labelRect = labelGO.AddComponent<RectTransform>();
        var labelTMP  = labelGO.AddComponent<TextMeshProUGUI>();
        labelGO.transform.SetParent(go.transform, false);
        labelRect.sizeDelta        = size;
        labelRect.anchoredPosition = Vector2.zero;
        labelTMP.text              = label;
        labelTMP.fontSize          = 18;
        labelTMP.alignment         = TextAlignmentOptions.Center;
        labelTMP.color             = Color.white;

        return button;
    }

    static void SetColor(GameObject go, Color color)
    {
        var image = go.GetComponent<Image>();
        if (image != null) image.color = color;
    }

    static void SetButtonColor(Button button, Color color)
    {
        var image = button.GetComponent<Image>();
        if (image != null) image.color = color;
    }

    static void AddHorizontalLayout(GameObject go, float spacing)
    {
        var layout          = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing      = spacing;
        layout.childAlignment         = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth      = false;
        layout.childControlHeight     = false;
    }
}
