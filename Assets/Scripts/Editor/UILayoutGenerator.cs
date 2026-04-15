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
        string[] managedNames = { "TopBar", "ShopPanel", "BattlePanel", "BenchPanel", "ActionPanel", "StartBattleButton", "Tooltip", "DragGhost", "DragDropManager" };
        // Note: SettingsOverlayBtn and PokedexOverlayBtn are children of TopBar, so they're destroyed with it.
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

        // Small overlay buttons — top-right of TopBar. Icons assigned later.
        var pokedexOverlayBtn  = CreateButton(topBar.transform, "PokedexOverlayBtn",  "DEX", new Vector2(58, 50), new Vector2(860, 0));
        var settingsOverlayBtn = CreateButton(topBar.transform, "SettingsOverlayBtn", "SET", new Vector2(58, 50), new Vector2(928, 0));
        SetButtonColor(pokedexOverlayBtn,  new Color(0.18f, 0.28f, 0.58f));
        SetButtonColor(settingsOverlayBtn, new Color(0.25f, 0.25f, 0.35f));
        // Scale button label text down to fit
        pokedexOverlayBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize  = 16;
        settingsOverlayBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 16;
        // Wire at runtime via GlobalOverlayToggle — no direct scene reference needed
        pokedexOverlayBtn.gameObject.AddComponent<GlobalOverlayToggle>().target  = GlobalOverlayToggle.Target.Pokedex;
        settingsOverlayBtn.gameObject.AddComponent<GlobalOverlayToggle>().target = GlobalOverlayToggle.Target.Settings;

        // --- Shop Panel ---
        var shopPanel = CreatePanel(root, "ShopPanel", new Vector2(1920, 220), new Vector2(0, 265));
        SetColor(shopPanel, new Color(0.1f, 0.1f, 0.25f, 0.95f));
        CreateLabel(shopPanel.transform, "ShopLabel", "SHOP", new Vector2(-820, 75));

        // Shop row: max 6 slots — same width/position as battle row so columns align
        var shopRow = CreatePanel(shopPanel.transform, "ShopSlotsRow", new Vector2(1250, 190), new Vector2(-50, -5));
        SetColor(shopRow, Color.clear);
        AddHorizontalLayout(shopRow, 10, TextAnchor.MiddleLeft);
        var shopSlots = new PokemonSlotUI[ShopManager.MaxShopSize];
        for (int i = 0; i < ShopManager.MaxShopSize; i++)
            shopSlots[i] = UIGeneratorHelpers.CreateSlot(shopRow.transform, $"ShopSlot_{i}", new Vector2(200, 185));

        var rerollBtn = CreateButton(shopPanel.transform, "RerollButton", "Reroll  P$1", new Vector2(180, 60), new Vector2(820, 0));

        // --- Battle Panel ---
        var battlePanel = CreatePanel(root, "BattlePanel", new Vector2(1920, 220), new Vector2(0, 20));
        SetColor(battlePanel, new Color(0.25f, 0.08f, 0.08f, 0.95f));
        CreateLabel(battlePanel.transform, "BattleLabel", "BATTLE TEAM", new Vector2(-820, 75));

        // Battle row: max 6 slots (1x6 horizontal, UIManager shows/hides based on round)
        var battleRow = CreatePanel(battlePanel.transform, "BattleSlotsRow", new Vector2(1250, 190), new Vector2(-50, -5));
        SetColor(battleRow, Color.clear);
        AddHorizontalLayout(battleRow, 10, TextAnchor.MiddleLeft);
        var battleSlots = new PokemonSlotUI[ShopManager.MaxBattleSize];
        for (int i = 0; i < ShopManager.MaxBattleSize; i++)
            battleSlots[i] = UIGeneratorHelpers.CreateSlot(battleRow.transform, $"BattleSlot_{i}", new Vector2(200, 185));

        // --- Bench Panel ---
        var benchPanel = CreatePanel(root, "BenchPanel", new Vector2(1920, 220), new Vector2(0, -220));
        SetColor(benchPanel, new Color(0.08f, 0.18f, 0.08f, 0.95f));
        CreateLabel(benchPanel.transform, "BenchLabel", "BENCH", new Vector2(-820, 75));

        // Bench: up to 4 slots — same width/position as battle row so columns align
        var benchRow = CreatePanel(benchPanel.transform, "BenchSlotsRow", new Vector2(1250, 190), new Vector2(-50, -5));
        SetColor(benchRow, Color.clear);
        AddHorizontalLayout(benchRow, 10, TextAnchor.MiddleLeft);
        var benchSlots = new PokemonSlotUI[ShopManager.MaxBenchSize];
        for (int i = 0; i < ShopManager.MaxBenchSize; i++)
            benchSlots[i] = UIGeneratorHelpers.CreateSlot(benchRow.transform, $"BenchSlot_{i}", new Vector2(200, 185));

        // --- Action Buttons Panel ---
        var actionPanel = CreatePanel(root, "ActionPanel", new Vector2(1920, 80), new Vector2(0, -430));
        SetColor(actionPanel, new Color(0.08f, 0.08f, 0.08f, 0.95f));
        var releaseBtn = CreateButton(actionPanel.transform, "ReleaseButton", "Release", new Vector2(180, 60), new Vector2(-300, 0));
        // Add ReleaseDropZone so Pokemon can also be dragged onto this button to release them
        releaseBtn.gameObject.AddComponent<ReleaseDropZone>();

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

        uiManager.releaseButton     = releaseBtn;
        uiManager.rerollButton      = rerollBtn;
        uiManager.startBattleButton = startBattleBtn;
        uiManager.releaseDropZone   = releaseBtn.GetComponent<ReleaseDropZone>();

        // --- Set slot sources and wire clicks ---
        SetupSlots(shopSlots,   ShopManager.SelectionSource.Shop);
        SetupSlots(battleSlots, ShopManager.SelectionSource.Battle);
        SetupSlots(benchSlots,  ShopManager.SelectionSource.Bench);

        // --- Tooltip Panel ---
        UIGeneratorHelpers.CreateTooltip(root);

        // --- Drag Ghost Image ---
        // A sprite that follows the cursor while dragging a Pokemon.
        // raycastTarget = false so it doesn't block OnDrop events on target slots.
        var ghostGO   = new GameObject("DragGhost");
        var ghostRect = ghostGO.AddComponent<RectTransform>();
        var ghostImg  = ghostGO.AddComponent<Image>();
        ghostGO.transform.SetParent(root, false);
        ghostRect.sizeDelta                = new Vector2(120, 90);
        ghostRect.pivot                    = new Vector2(0.5f, 0.5f);
        ghostGO.transform.localEulerAngles = new Vector3(0f, 180f, 0f); // Match slot sprite flip
        ghostImg.raycastTarget             = false;
        ghostImg.preserveAspect    = true;
        ghostImg.color             = new Color(1f, 1f, 1f, 0.85f);
        ghostGO.SetActive(false);
        ghostGO.transform.SetAsLastSibling(); // Renders on top of everything

        // --- DragDropManager ---
        var strayDDM = GameObject.Find("DragDropManager");
        if (strayDDM != null) Undo.DestroyObjectImmediate(strayDDM);

        var ddmGO = new GameObject("DragDropManager");
        Undo.RegisterCreatedObjectUndo(ddmGO, "Create DragDropManager");
        var ddm = ddmGO.AddComponent<DragDropManager>();
        ddm.ghostImage = ghostImg;

        EditorUtility.SetDirty(ddmGO);
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

    static void AddHorizontalLayout(GameObject go, float spacing, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var layout          = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing      = spacing;
        layout.childAlignment         = alignment;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth      = false;
        layout.childControlHeight     = false;
    }
}
