using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
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

        // --- Top Bar ---
        var topBar = CreatePanel(root, "TopBar", new Vector2(1920, 70), new Vector2(0, 475));
        SetColor(topBar, new Color(0.08f, 0.08f, 0.08f, 0.95f));
        var roundText      = CreateText(topBar.transform, "RoundText",      "Round 1/8", 28, new Vector2(-700, 0));
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

        var benchRow = CreatePanel(benchPanel.transform, "BenchSlotsRow", new Vector2(960, 190), new Vector2(0, -5));
        SetColor(benchRow, Color.clear);
        AddHorizontalLayout(benchRow, 10);
        var benchSlots = new PokemonSlotUI[6];
        for (int i = 0; i < 6; i++)
            benchSlots[i] = CreateSlot(benchRow.transform, $"BenchSlot_{i}", new Vector2(148, 185));

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
        var uiManagerGO = GameObject.Find("UIManager") ?? new GameObject("UIManager");
        Undo.RegisterCreatedObjectUndo(uiManagerGO, "Create UIManager");

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

        EditorUtility.SetDirty(uiManagerGO);
        EditorUtility.SetDirty(canvas.gameObject);

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
        spriteRect.anchoredPosition = new Vector2(0, 15);
        spriteRect.sizeDelta        = new Vector2(size.x - 20, size.y - 50);
        spriteImage.preserveAspect  = true;
        spriteImage.color           = Color.white;
        slotUI.pokemonSprite        = spriteImage;

        // HP text — bottom left
        slotUI.hpText = CreateTMPText(go.transform, "HPText", "", 32,
            new Vector2(-size.x / 4, -size.y / 2 + 18), new Vector2(size.x / 2, 36));
        slotUI.hpText.alignment = TextAlignmentOptions.Left;

        // Attack text — bottom right
        slotUI.attackText = CreateTMPText(go.transform, "AttackText", "", 32,
            new Vector2(size.x / 4, -size.y / 2 + 18), new Vector2(size.x / 2, 36));
        slotUI.attackText.alignment = TextAlignmentOptions.Right;

        // All shop-scene slots show player Pokemon — flip sprites to face right
        slotUI.flipSprite = true;

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
