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
        string[] managedNames = { "Background", "TopBar", "PlayerPanel", "EnemyPanel", "LogPanel", "PlaybackPanel", "Tooltip", "ShopPanel", "BattlePanel", "BenchPanel", "ActionPanel", "StartBattleButton" };
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
        var hpLabel = CreateTMPLabel(topBar.transform, "HPLabel", "HP: 3/3", 28, new Vector2(700, 0), new Vector2(300, 50));

        // Slot size for 2x3 grid (3 per row, 2 rows per side)
        var slotSize = new Vector2(190, 175);
        float rowW   = 3 * slotSize.x + 2 * 10;  // 590
        float frontOffset = 120f; // front row shifts this many px toward the center gap

        // --- Player Team Panel (LEFT side) ---
        var playerPanel = CreatePanel(root, "PlayerPanel", new Vector2(920, 400), new Vector2(-490, 20));
        SetColor(playerPanel, new Color(0.05f, 0.15f, 0.25f, 0.8f));

        // Front row — slightly offset right (toward enemy center)
        var playerFrontRow = CreatePanel(playerPanel.transform, "PlayerFrontRow", new Vector2(rowW, slotSize.y), new Vector2(frontOffset, 85));
        SetColor(playerFrontRow, Color.clear);
        AddHorizontalLayout(playerFrontRow, 10);

        // Back row — centered
        var playerBackRow = CreatePanel(playerPanel.transform, "PlayerBackRow", new Vector2(rowW, slotSize.y), new Vector2(100f, -100));
        SetColor(playerBackRow, Color.clear);
        AddHorizontalLayout(playerBackRow, 10);

        // Slots 0-2 = front row (added right-to-left so slot 0 is rightmost = closest to enemy)
        // Slots 3-5 = back row (same right-to-left order)
        var playerSlots = new PokemonSlotUI[ShopManager.MaxBattleSize];
        for (int i = 2; i >= 0; i--)
            playerSlots[i] = UIGeneratorHelpers.CreateSlot(playerFrontRow.transform, $"PlayerSlot_{i}", slotSize);
        for (int i = 5; i >= 3; i--)
            playerSlots[i] = UIGeneratorHelpers.CreateSlot(playerBackRow.transform, $"PlayerSlot_{i}", slotSize);

        // --- Enemy Team Panel (RIGHT side) ---
        var enemyPanel = CreatePanel(root, "EnemyPanel", new Vector2(920, 400), new Vector2(490, 20));
        SetColor(enemyPanel, new Color(0.25f, 0.05f, 0.05f, 0.8f));

        // Front row — slightly offset left (toward player center)
        var enemyFrontRow = CreatePanel(enemyPanel.transform, "EnemyFrontRow", new Vector2(rowW, slotSize.y), new Vector2(-frontOffset, 85));
        SetColor(enemyFrontRow, Color.clear);
        AddHorizontalLayout(enemyFrontRow, 10);

        // Back row — centered
        var enemyBackRow = CreatePanel(enemyPanel.transform, "EnemyBackRow", new Vector2(rowW, slotSize.y), new Vector2(-100f, -100));
        SetColor(enemyBackRow, Color.clear);
        AddHorizontalLayout(enemyBackRow, 10);

        // Slots 0-2 = front row (added left-to-right so slot 0 is leftmost = closest to player)
        // Slots 3-5 = back row
        var enemySlots = new PokemonSlotUI[ShopManager.MaxBattleSize];
        for (int i = 0; i < 3; i++)
            enemySlots[i] = UIGeneratorHelpers.CreateSlot(enemyFrontRow.transform, $"EnemySlot_{i}", slotSize);
        for (int i = 3; i < ShopManager.MaxBattleSize; i++)
            enemySlots[i] = UIGeneratorHelpers.CreateSlot(enemyBackRow.transform, $"EnemySlot_{i}", slotSize);

        // --- Battle Log ---
        var logPanel = CreatePanel(root, "LogPanel", new Vector2(900, 70), new Vector2(0, -300));
        SetColor(logPanel, new Color(0.1f, 0.1f, 0.1f, 0.9f));
        var battleLogText = CreateTMPLabel(logPanel.transform, "BattleLogText", "Battle starting...", 22, Vector2.zero, new Vector2(880, 60));

        // --- Playback Buttons ---
        var playbackPanel = CreatePanel(root, "PlaybackPanel", new Vector2(560, 60), new Vector2(-330, -440));
        SetColor(playbackPanel, Color.clear);
        AddHorizontalLayout(playbackPanel, 10);

        var stepBtn    = CreateButtonInLayout(playbackPanel.transform, "StepButton",    "Step",     new Vector2(170, 55));
        var autoBtn    = CreateButtonInLayout(playbackPanel.transform, "AutoButton",    "Auto",     new Vector2(170, 55));
        var speedUpBtn = CreateButtonInLayout(playbackPanel.transform, "SpeedUpButton", "Speed Up", new Vector2(170, 55));

        // --- BattleSceneManager ---
        var bsmGO = GameObject.Find("BattleSceneManager");
        if (bsmGO == null)
        {
            bsmGO = new GameObject("BattleSceneManager");
            Undo.RegisterCreatedObjectUndo(bsmGO, "Create BattleSceneManager");
        }

        var bsm = bsmGO.GetComponent<BattleSceneManager>() ?? bsmGO.AddComponent<BattleSceneManager>();

        bsm.playerSlots   = playerSlots;
        bsm.enemySlots    = enemySlots;
        bsm.playerHPLabel = hpLabel;
        bsm.battleLogText = battleLogText;
        bsm.stepButton    = stepBtn;
        bsm.autoButton    = autoBtn;
        bsm.speedUpButton = speedUpBtn;

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
