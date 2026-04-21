using System.Linq;
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
        scaler.screenMatchMode    = CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = 0.5f;

        Transform root = canvas.transform;

        // --- Destroy existing panels so re-running doesn't stack duplicates ---
        string[] managedNames = { "TopBar", "ShopPanel", "BattlePanel", "BenchPanel", "ActionPanel", "StartBattleButton", "Tooltip", "DragGhost", "DragDropManager", "ConfirmReturnPanel", "ConfirmBattlePanel" };
        // Note: all TopBar children (overlay buttons, return button) are destroyed with TopBar.
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
        var roundText      = CreateProgressIconRow(topBar.transform, new Vector2(-600, 0),
                                out Image[] badgeImages, out Image[] starImages, out Image champImage);
        var pokedollarText = CreateCurrencyDisplay(topBar.transform, new Vector2(0, 0));
        var hpText         = CreateHPIconRow(topBar.transform, new Vector2(600, 0), out Image[] heartImages);

        // Top-left: Return to Main Menu button (red background, Return.png icon)
        // Offset from -960 edge: button is 58px wide, centred at -890 → left edge at -919, ~41px from border
        var returnBtn = CreateButton(topBar.transform, "ReturnToMainMenuBtn", "", new Vector2(58, 50), new Vector2(-890, 0));
        SetButtonColor(returnBtn, new Color(0.7f, 0.1f, 0.1f));
        SetButtonIcon(returnBtn, "Assets/Resources/Icons/Return.png");
        returnBtn.gameObject.AddComponent<GlobalOverlayToggle>().target = GlobalOverlayToggle.Target.ReturnToMainMenu;

        // Small overlay buttons — top-right of TopBar.
        var pokedexOverlayBtn  = CreateButton(topBar.transform, "PokedexOverlayBtn",  "", new Vector2(58, 50), new Vector2(860, 0));
        var helperOverlayBtn   = CreateButton(topBar.transform, "HelperOverlayBtn",   "", new Vector2(58, 50), new Vector2(792, 0));
        var settingsOverlayBtn = CreateButton(topBar.transform, "SettingsOverlayBtn", "", new Vector2(58, 50), new Vector2(928, 0));
        SetButtonColor(pokedexOverlayBtn,  new Color(0.18f, 0.28f, 0.58f));
        SetButtonColor(helperOverlayBtn,   new Color(0.6f,  0.5f,  0.0f));
        SetButtonColor(settingsOverlayBtn, new Color(0.25f, 0.25f, 0.35f));
        SetButtonIcon(pokedexOverlayBtn,  "Assets/Resources/Icons/dex.png");
        SetButtonIcon(helperOverlayBtn, "Assets/Resources/Icons/questionmark.png");
        SetButtonIcon(settingsOverlayBtn, "Assets/Resources/Icons/gear.png");
        // Wire at runtime via GlobalOverlayToggle — no direct scene reference needed
        pokedexOverlayBtn.gameObject.AddComponent<GlobalOverlayToggle>().target  = GlobalOverlayToggle.Target.Pokedex;
        helperOverlayBtn.gameObject.AddComponent<GlobalOverlayToggle>().target   = GlobalOverlayToggle.Target.Helper;
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

        var rerollBtn = CreateButton(shopPanel.transform, "RerollButton", "Reroll", new Vector2(180, 60), new Vector2(820, 0));
        // Shift text left to make room for the pokeball cost icon
        var rerollLblTransform = rerollBtn.transform.Find("Label");
        rerollLblTransform.GetComponent<RectTransform>().sizeDelta        = new Vector2(90, 60);
        rerollLblTransform.GetComponent<RectTransform>().anchoredPosition = new Vector2(-30, 0);
        // Pokeball icon represents the 1-pokedollar cost
        {
            var go   = new GameObject("PokeballIcon");
            var rt   = go.AddComponent<RectTransform>();
            var img  = go.AddComponent<Image>();
            go.transform.SetParent(rerollBtn.transform, false);
            rt.sizeDelta        = new Vector2(36, 36);
            rt.anchoredPosition = new Vector2(48, 0);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Icons/pokeball.png");
            if (sprite != null) img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget  = false;
        }

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
        // Align Release and Bait with the slot columns (slot row centre x=-50, width=1250 → left edge=-675, slot width=200+gap=210)
        var releaseBtn = CreateButton(actionPanel.transform, "ReleaseButton", "Release", new Vector2(190, 60), new Vector2(-575, 0));
        releaseBtn.gameObject.AddComponent<ReleaseDropZone>();
        var baitBtn = CreateButton(actionPanel.transform, "BaitButton", "Bait", new Vector2(190, 60), new Vector2(-365, 0));

        // --- Start Battle Button — same row as action panel ---
        var startBattleBtn = CreateButton(actionPanel.transform, "StartBattleButton", "START BATTLE", new Vector2(260, 60), new Vector2(575, 0));
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

        uiManager.badgeImages = badgeImages;
        uiManager.starImages  = starImages;
        uiManager.champImage  = champImage;
        uiManager.heartImages = heartImages;

        uiManager.releaseButton     = releaseBtn;
        uiManager.baitButton        = baitBtn;
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

        // --- Confirm Return Panel ---
        // Full-screen dimmer with a dialog in the centre. Rendered on top (last sibling).
        var confirmRoot = new GameObject("ConfirmReturnPanel");
        Undo.RegisterCreatedObjectUndo(confirmRoot, "Create ConfirmReturnPanel");
        var confirmRootRect = confirmRoot.AddComponent<RectTransform>();
        var confirmRootImg  = confirmRoot.AddComponent<Image>();
        confirmRoot.transform.SetParent(root, false);
        confirmRootRect.anchorMin = Vector2.zero;
        confirmRootRect.anchorMax = Vector2.one;
        confirmRootRect.offsetMin = Vector2.zero;
        confirmRootRect.offsetMax = Vector2.zero;
        confirmRootImg.color      = new Color(0f, 0f, 0f, 0.65f);
        confirmRoot.transform.SetAsLastSibling();

        // Dialog box
        var dialog     = new GameObject("Dialog");
        var dialogRect = dialog.AddComponent<RectTransform>();
        var dialogImg  = dialog.AddComponent<Image>();
        dialog.transform.SetParent(confirmRoot.transform, false);
        dialogRect.sizeDelta        = new Vector2(500, 220);
        dialogRect.anchoredPosition = Vector2.zero;
        dialogImg.color             = new Color(0.12f, 0.12f, 0.18f, 1f);

        // Question text
        var msgGO   = new GameObject("Message");
        var msgRect = msgGO.AddComponent<RectTransform>();
        var msgTMP  = msgGO.AddComponent<TextMeshProUGUI>();
        msgGO.transform.SetParent(dialog.transform, false);
        msgRect.sizeDelta        = new Vector2(440, 100);
        msgRect.anchoredPosition = new Vector2(0, 50);
        msgTMP.text      = "Return to Main Menu?";
        msgTMP.fontSize  = 28;
        msgTMP.alignment = TextAlignmentOptions.Center;
        msgTMP.color     = Color.white;

        // No button — left side
        var noBtn = CreateButton(dialog.transform, "ConfirmNoButton", "No", new Vector2(160, 55), new Vector2(-100, -60));
        SetButtonColor(noBtn, new Color(0.55f, 0.15f, 0.15f));

        // Yes button — right side
        var yesBtn = CreateButton(dialog.transform, "ConfirmYesButton", "Yes", new Vector2(160, 55), new Vector2(100, -60));
        SetButtonColor(yesBtn, new Color(0.15f, 0.55f, 0.15f));

        // Wire via ConfirmReturnPanel script
        var confirmScript = confirmRoot.AddComponent<ConfirmReturnPanel>();
        confirmScript.confirmButton = yesBtn;
        confirmScript.cancelButton  = noBtn;
        // Start() sets Active=false; set it here too so it's hidden in editor
        confirmRoot.SetActive(false);

        // --- Confirm Battle Panel (money warning) ---
        var battleConfirmRoot = new GameObject("ConfirmBattlePanel");
        Undo.RegisterCreatedObjectUndo(battleConfirmRoot, "Create ConfirmBattlePanel");
        var battleConfirmRect = battleConfirmRoot.AddComponent<RectTransform>();
        var battleConfirmImg  = battleConfirmRoot.AddComponent<Image>();
        battleConfirmRoot.transform.SetParent(root, false);
        battleConfirmRect.anchorMin = Vector2.zero;
        battleConfirmRect.anchorMax = Vector2.one;
        battleConfirmRect.offsetMin = Vector2.zero;
        battleConfirmRect.offsetMax = Vector2.zero;
        battleConfirmImg.color      = new Color(0f, 0f, 0f, 0.65f);
        battleConfirmRoot.transform.SetAsLastSibling();

        var battleDialog     = new GameObject("Dialog");
        var battleDialogRect = battleDialog.AddComponent<RectTransform>();
        var battleDialogImg  = battleDialog.AddComponent<Image>();
        battleDialog.transform.SetParent(battleConfirmRoot.transform, false);
        battleDialogRect.sizeDelta        = new Vector2(540, 240);
        battleDialogRect.anchoredPosition = Vector2.zero;
        battleDialogImg.color             = new Color(0.12f, 0.12f, 0.18f, 1f);

        var battleMsgGO   = new GameObject("Message");
        var battleMsgRect = battleMsgGO.AddComponent<RectTransform>();
        var battleMsgTMP  = battleMsgGO.AddComponent<TextMeshProUGUI>();
        battleMsgGO.transform.SetParent(battleDialog.transform, false);
        battleMsgRect.sizeDelta        = new Vector2(480, 120);
        battleMsgRect.anchoredPosition = new Vector2(0, 55);
        battleMsgTMP.text      = "You still have Pokéballs left!\nStart battle anyway?";
        battleMsgTMP.fontSize  = 24;
        battleMsgTMP.alignment = TextAlignmentOptions.Center;
        battleMsgTMP.color     = Color.white;

        var battleNoBtn  = CreateButton(battleDialog.transform, "ConfirmNoButton",  "Keep Shopping", new Vector2(200, 55), new Vector2(-130, -75));
        SetButtonColor(battleNoBtn,  new Color(0.55f, 0.35f, 0.1f));
        var battleYesBtn = CreateButton(battleDialog.transform, "ConfirmYesButton", "Start Battle",  new Vector2(200, 55), new Vector2(100,  -75));
        SetButtonColor(battleYesBtn, new Color(0.15f, 0.55f, 0.15f));

        var battleConfirmScript = battleConfirmRoot.AddComponent<ConfirmBattlePanel>();
        battleConfirmScript.confirmButton = battleYesBtn;
        battleConfirmScript.cancelButton  = battleNoBtn;
        battleConfirmScript.messageText   = battleMsgTMP;
        battleConfirmRoot.SetActive(false);

        uiManager.confirmBattlePanel = battleConfirmScript;

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

    // Creates a horizontal container with a pokeball icon + number text for currency display.
    static TextMeshProUGUI CreateCurrencyDisplay(Transform parent, Vector2 pos)
    {
        // Container
        var container = new GameObject("CurrencyDisplay");
        var rect      = container.AddComponent<RectTransform>();
        container.transform.SetParent(parent, false);
        rect.sizeDelta        = new Vector2(120, 50);
        rect.anchoredPosition = pos;
        var layout = container.AddComponent<HorizontalLayoutGroup>();
        layout.spacing              = 6f;
        layout.childAlignment       = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth      = false;
        layout.childControlHeight     = false;

        // Pokeball icon
        var iconGO   = new GameObject("PokeballIcon");
        var iconRect = iconGO.AddComponent<RectTransform>();
        var iconImg  = iconGO.AddComponent<Image>();
        iconGO.transform.SetParent(container.transform, false);
        iconRect.sizeDelta = new Vector2(38, 38);
        var pokeballSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Icons/pokeball.png");
        if (pokeballSprite != null)
            iconImg.sprite = pokeballSprite;
        iconImg.preserveAspect = true;

        // Number text
        var textGO   = new GameObject("PokedollarText");
        var textRect = textGO.AddComponent<RectTransform>();
        var tmp      = textGO.AddComponent<TextMeshProUGUI>();
        textGO.transform.SetParent(container.transform, false);
        textRect.sizeDelta = new Vector2(70, 50);
        tmp.text      = "5";
        tmp.fontSize  = 36;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color     = Color.white;

        return tmp;
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

    // Hides the text label on a button and fills it with a sprite icon instead (anchor-based, ~80% of button).
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

    // Like SetButtonIcon but uses a fixed pixel size — use this when the sprite has unusual aspect
    // or bleeds outside the button with the anchor method.
    static void SetButtonIconFixed(Button button, string spritePath, Vector2 iconSize)
    {
        var labelTransform = button.transform.Find("Label");
        if (labelTransform != null)
            labelTransform.GetComponent<TextMeshProUGUI>().text = "";

        var go  = new GameObject("Icon");
        var rt  = go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        go.transform.SetParent(button.transform, false);
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = iconSize;
        rt.anchoredPosition = Vector2.zero;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite != null) img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget  = false;
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

    static readonly Color IconLocked = new Color(0.2f, 0.2f, 0.2f, 0.7f);

    // 8 Badges (each a different sprite from the sheet) + 4 Stars + 1 Champ, all greyed out by default.
    // Returns a hidden TMP for UIManager and exposes Image arrays via out params.
    public static TextMeshProUGUI CreateProgressIconRow(Transform parent, Vector2 pos,
        out Image[] badgeImages, out Image[] starImages, out Image champImage)
    {
        var container     = new GameObject("ProgressIconDisplay");
        var containerRect = container.AddComponent<RectTransform>();
        container.transform.SetParent(parent, false);
        containerRect.sizeDelta        = new Vector2(500, 50);
        containerRect.anchoredPosition = pos;
        var layout = container.AddComponent<HorizontalLayoutGroup>();
        layout.spacing               = 4f;
        layout.childAlignment        = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth      = false;
        layout.childControlHeight     = false;

        // Individual badge sprites from the sprite sheet, sorted by name (Badge_0 … Badge_7)
        var allBadgeSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Icons/Badge.png")
            .OfType<Sprite>()
            .OrderBy(s => s.name)
            .ToArray();

        badgeImages = new Image[8];
        for (int i = 0; i < 8; i++)
        {
            var sprite = i < allBadgeSprites.Length ? allBadgeSprites[i] : null;
            badgeImages[i] = CreateRowIcon(container.transform, $"Badge_{i}", new Vector2(32, 32), sprite);
        }

        var starSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Icons/star.png");
        starImages = new Image[4];
        for (int i = 0; i < 4; i++)
            starImages[i] = CreateRowIcon(container.transform, $"Star_{i}", new Vector2(32, 32), starSprite);

        var champSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Icons/champ.png");
        champImage = CreateRowIcon(container.transform, "Champ", new Vector2(32, 32), champSprite);

        return CreateHiddenTMP(parent, "RoundText");
    }

    // 6 Hearts, full (white) by default. Returns a hidden TMP and exposes Image[] via out param.
    public static TextMeshProUGUI CreateHPIconRow(Transform parent, Vector2 pos, out Image[] heartImages)
    {
        var container     = new GameObject("HPIconDisplay");
        var containerRect = container.AddComponent<RectTransform>();
        container.transform.SetParent(parent, false);
        containerRect.sizeDelta        = new Vector2(220, 50);
        containerRect.anchoredPosition = pos;
        var layout = container.AddComponent<HorizontalLayoutGroup>();
        layout.spacing               = 4f;
        layout.childAlignment        = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth      = false;
        layout.childControlHeight     = false;

        var heartSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Icons/heart.png");
        heartImages = new Image[6];
        for (int i = 0; i < 6; i++)
            heartImages[i] = CreateRowIcon(container.transform, $"Heart_{i}", new Vector2(32, 32), heartSprite, locked: false);

        return CreateHiddenTMP(parent, "HPText");
    }

    // Creates a single icon Image inside a layout row.
    static Image CreateRowIcon(Transform parent, string name, Vector2 size, Sprite sprite, bool locked = true)
    {
        var go  = new GameObject(name);
        var rt  = go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        go.transform.SetParent(parent, false);
        rt.sizeDelta       = size;
        if (sprite != null) img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget  = false;
        img.color          = locked ? IconLocked : Color.white;
        return img;
    }

    // Creates an invisible, off-screen TMP that UIManager can write to without it showing.
    static TextMeshProUGUI CreateHiddenTMP(Transform parent, string name)
    {
        var go  = new GameObject(name);
        var rt  = go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        go.transform.SetParent(parent, false);
        rt.sizeDelta        = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-3000, 0);
        tmp.color           = Color.clear;
        tmp.fontSize        = 1;
        return tmp;
    }
}
