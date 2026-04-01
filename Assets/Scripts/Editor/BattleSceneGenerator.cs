using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEditor.Events;

// Generates the Battle Scene UI layout.
// Run via: Pokemon -> Generate Battle Scene UI
// Make sure you are in the BattleScene before running this!

public class BattleSceneGenerator
{
    [MenuItem("Pokemon/Generate Battle Scene UI")]
    public static void GenerateBattleSceneUI()
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
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        Transform root = canvas.transform;

        // --- Destroy existing panels so re-running doesn't stack duplicates ---
        string[] managedNames = { "Background", "TopBar", "PlayerPanel", "EnemyPanel", "LogPanel", "ResultText", "PlaybackPanel", "ContinueButton", "Tooltip", "ShopPanel", "BattlePanel", "BenchPanel", "ActionPanel", "StartBattleButton" };
        foreach (string n in managedNames)
        {
            var found = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in root)
                if (child.name == n) found.Add(child);
            foreach (var t in found)
                Undo.DestroyObjectImmediate(t.gameObject);
        }

        // Remove UIManager if accidentally added by shop generator
        var strayUIManager = GameObject.Find("UIManager");
        if (strayUIManager != null)
            Undo.DestroyObjectImmediate(strayUIManager);

        // --- Background ---
        var bg = CreatePanel(root, "Background", new Vector2(1920, 1080), Vector2.zero);
        SetColor(bg, new Color(0.05f, 0.05f, 0.15f, 1f));

        // --- Top Bar: round and HP ---
        var topBar = CreatePanel(root, "TopBar", new Vector2(1920, 70), new Vector2(0, 475));
        SetColor(topBar, new Color(0.08f, 0.08f, 0.08f, 0.95f));
        CreateLabel(topBar.transform, "HPLabel", "HP: 3/3", 28, new Vector2(700, 0));

        // --- Player Team Panel (LEFT side) ---
        var playerPanel = CreatePanel(root, "PlayerPanel", new Vector2(900, 320), new Vector2(-480, 40));
        SetColor(playerPanel, new Color(0.05f, 0.15f, 0.25f, 0.8f));
        CreateLabel(playerPanel.transform, "PlayerLabel", "YOUR TEAM", 22, new Vector2(-360, 125));

        var playerRow = CreatePanel(playerPanel.transform, "PlayerSlotsRow", new Vector2(660, 230), new Vector2(0, -10));
        SetColor(playerRow, Color.clear);
        AddHorizontalLayout(playerRow, 15);

        // Create player slots in reverse visual order so slot 0 is rightmost (front fighter = closest to enemy)
        var playerSlots = new PokemonSlotUI[3];
        for (int i = 2; i >= 0; i--)
            playerSlots[i] = CreateSlot(playerRow.transform, $"PlayerSlot_{i}", new Vector2(210, 220), flipSlot: true);

        // --- Enemy Team Panel (RIGHT side) ---
        var enemyPanel = CreatePanel(root, "EnemyPanel", new Vector2(900, 320), new Vector2(480, 40));
        SetColor(enemyPanel, new Color(0.25f, 0.05f, 0.05f, 0.8f));
        CreateLabel(enemyPanel.transform, "EnemyLabel", "ENEMY TEAM", 22, new Vector2(-360, 125));

        var enemyRow = CreatePanel(enemyPanel.transform, "EnemySlotsRow", new Vector2(660, 230), new Vector2(0, -10));
        SetColor(enemyRow, Color.clear);
        AddHorizontalLayout(enemyRow, 15);

        // Enemy slots in normal order — slot 0 is leftmost (front fighter = closest to player)
        var enemySlots = new PokemonSlotUI[3];
        for (int i = 0; i < 3; i++)
            enemySlots[i] = CreateSlot(enemyRow.transform, $"EnemySlot_{i}", new Vector2(210, 220), flipSlot: false);

        // --- Battle Log ---
        var logPanel = CreatePanel(root, "LogPanel", new Vector2(900, 70), new Vector2(0, -300));
        SetColor(logPanel, new Color(0.1f, 0.1f, 0.1f, 0.9f));
        var battleLogText = CreateTMPLabel(logPanel.transform, "BattleLogText", "Battle starting...", 22, Vector2.zero, new Vector2(880, 60));

        // --- Result Banner (hidden by default) ---
        var resultText = CreateTMPLabel(root, "ResultText", "VICTORY!", 72, new Vector2(0, 40), new Vector2(700, 120));
        resultText.fontStyle = FontStyles.Bold;
        resultText.gameObject.SetActive(false);

        // --- Playback Buttons ---
        var playbackPanel = CreatePanel(root, "PlaybackPanel", new Vector2(560, 60), new Vector2(-330, -440));
        SetColor(playbackPanel, Color.clear);
        AddHorizontalLayout(playbackPanel, 10);

        var stepBtn    = CreateButtonInLayout(playbackPanel.transform, "StepButton",    "Step",     new Vector2(170, 55));
        var autoBtn    = CreateButtonInLayout(playbackPanel.transform, "AutoButton",    "Auto",     new Vector2(170, 55));
        var speedUpBtn = CreateButtonInLayout(playbackPanel.transform, "SpeedUpButton", "Speed Up", new Vector2(170, 55));

        // --- Continue Button (hidden until battle ends) ---
        var continueBtn = CreateButton(root, "ContinueButton", "Continue", new Vector2(260, 60), new Vector2(600, -440));
        SetButtonColor(continueBtn, new Color(0.2f, 0.6f, 0.2f));
        continueBtn.gameObject.SetActive(false);

        // --- BattleSceneManager ---
        var bsmGO = GameObject.Find("BattleSceneManager");
        if (bsmGO == null)
        {
            bsmGO = new GameObject("BattleSceneManager");
            Undo.RegisterCreatedObjectUndo(bsmGO, "Create BattleSceneManager");
        }

        var bsm = bsmGO.GetComponent<BattleSceneManager>() ?? bsmGO.AddComponent<BattleSceneManager>();

        bsm.playerSlots    = playerSlots;
        bsm.enemySlots     = enemySlots;
        bsm.battleLogText  = battleLogText;
        bsm.resultText     = resultText;
        bsm.stepButton     = stepBtn;
        bsm.autoButton     = autoBtn;
        bsm.speedUpButton  = speedUpBtn;
        bsm.continueButton = continueBtn;

        // Mark slots as non-interactive (battle scene slots are display only)
        SetupDisplaySlots(playerSlots);
        SetupDisplaySlots(enemySlots);

        EditorUtility.SetDirty(bsmGO);
        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.SaveOpenScenes();

        // --- Tooltip Panel ---
        UIGeneratorHelpers.CreateTooltip(root);

        Debug.Log("Battle Scene UI generated!");
    }

    // Battle slots are display only — no click behaviour needed
    static void SetupDisplaySlots(PokemonSlotUI[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].slotIndex = i;
            var button = slots[i].GetComponent<Button>();
            if (button != null) button.interactable = false;
            EditorUtility.SetDirty(slots[i]);
        }
    }

    // -------------------------------------------------------
    // SLOT CREATION
    // -------------------------------------------------------

    static PokemonSlotUI CreateSlot(Transform parent, string name, Vector2 size, bool flipSlot)
    {
        var go     = new GameObject(name);
        var rect   = go.AddComponent<RectTransform>();
        var image  = go.AddComponent<Image>();
        var button = go.AddComponent<Button>();
        var slotUI = go.AddComponent<PokemonSlotUI>();
        go.transform.SetParent(parent, false);
        rect.sizeDelta = size;
        image.color    = new Color(0.2f, 0.2f, 0.2f, 1f);

        slotUI.flipSlot = flipSlot;

        var highlight        = CreateChildImage(go.transform, "Highlight", size, Color.clear);
        var highlightOutline = highlight.AddComponent<Outline>();
        highlightOutline.effectColor    = new Color(1f, 0.9f, 0f, 1f);
        highlightOutline.effectDistance = new Vector2(4, 4);
        slotUI.highlight = highlight.GetComponent<Image>();

        var spriteGO    = new GameObject("PokemonSprite");
        var spriteRect  = spriteGO.AddComponent<RectTransform>();
        var spriteImage = spriteGO.AddComponent<Image>();
        spriteGO.transform.SetParent(go.transform, false);
        spriteRect.anchoredPosition         = new Vector2(18f, 25f);
        spriteRect.sizeDelta                = new Vector2(180f, 120f);
        spriteGO.transform.localEulerAngles = new Vector3(0f, -180f, 0f);
        spriteImage.preserveAspect          = true;
        spriteImage.color                   = Color.white;
        slotUI.pokemonSprite                = spriteImage;

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
        slotUI.attackText = CreateTMPLabel(go.transform, "AttackText", "", 23,
            new Vector2(-40.3f, 21.3f), new Vector2(third, 36));
        slotUI.attackText.alignment        = TextAlignmentOptions.Left;
        slotUI.attackText.fontStyle        = FontStyles.Bold;
        slotUI.attackText.characterSpacing = -10;
        slotUI.attackText.color            = new Color(36/255f, 36/255f, 36/255f);

        // Speed
        slotUI.speedText = CreateTMPLabel(go.transform, "SpeedText", "", 23,
            new Vector2(-65.5f, -18f), new Vector2(third, 36));
        slotUI.speedText.alignment        = TextAlignmentOptions.Center;
        slotUI.speedText.fontStyle        = FontStyles.Bold;
        slotUI.speedText.characterSpacing = -10;
        slotUI.speedText.color            = new Color(36/255f, 36/255f, 36/255f);

        // HP — bottom right in layout (appears on the left when slot is flipped for player)
        slotUI.hpText = CreateTMPLabel(go.transform, "HPText", "", 23,
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

    static void CreateLabel(Transform parent, string name, string text, int fontSize, Vector2 pos)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        go.transform.SetParent(parent, false);
        rect.sizeDelta        = new Vector2(400, 50);
        rect.anchoredPosition = pos;
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
    }

    static TextMeshProUGUI CreateTMPLabel(Transform parent, string name, string text, int fontSize, Vector2 pos, Vector2 size)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        go.transform.SetParent(parent, false);
        rect.sizeDelta        = size;
        rect.anchoredPosition = pos;
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
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

        var labelGO  = new GameObject("Label");
        var lRect    = labelGO.AddComponent<RectTransform>();
        var lTMP     = labelGO.AddComponent<TextMeshProUGUI>();
        labelGO.transform.SetParent(go.transform, false);
        lRect.sizeDelta        = size;
        lRect.anchoredPosition = Vector2.zero;
        lTMP.text      = label;
        lTMP.fontSize  = 18;
        lTMP.alignment = TextAlignmentOptions.Center;
        lTMP.color     = Color.white;

        return button;
    }

    static Button CreateButtonInLayout(Transform parent, string name, string label, Vector2 size)
    {
        var btn  = CreateButton(parent, name, label, size, Vector2.zero);
        var rect = btn.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        return btn;
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

    static void AddHorizontalLayout(GameObject go, float spacing)
    {
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing              = spacing;
        layout.childAlignment       = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth      = false;
        layout.childControlHeight     = false;
    }
}
