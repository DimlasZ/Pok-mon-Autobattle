using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public static class UIGeneratorHelpers
{
    // -------------------------------------------------------
    // SLOT — shared between Shop and Battle scene generators
    // -------------------------------------------------------

    public static PokemonSlotUI CreateSlot(Transform parent, string name, Vector2 size)
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
        var highlight        = CreateChildImage(go.transform, "Highlight", size, Color.clear);
        var highlightOutline = highlight.AddComponent<Outline>();
        highlightOutline.effectColor    = new Color(1f, 0.9f, 0f, 1f);
        highlightOutline.effectDistance = new Vector2(4, 4);
        slotUI.highlight = highlight.GetComponent<Image>();

        // Pokemon sprite
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

        // HP Bar Box
        var hpBox  = new GameObject("HPBarBox");
        var hpBoxR = hpBox.AddComponent<RectTransform>();
        var hpBoxI = hpBox.AddComponent<Image>();
        hpBox.transform.SetParent(go.transform, false);
        hpBoxR.anchoredPosition = new Vector2(0f, -20.8f);
        hpBoxR.sizeDelta        = new Vector2(186f, 124f);
        if (hpBarBoxSprite != null) { hpBoxI.sprite = hpBarBoxSprite; hpBoxI.type = Image.Type.Sliced; }
        else hpBoxI.color = new Color(0.1f, 0.1f, 0.3f, 0.9f);
        slotUI.hpBarBox = hpBoxI;

        // Speed icon
        CreateIcon(hpBox.transform, "SpeedImage",  "Assets/Sprites/UI/flash.png", new Vector2(-65f, -12f), new Vector2(15f, 15f));

        // Attack icon
        CreateIcon(hpBox.transform, "AttackImage", "Assets/Sprites/UI/sword.png", new Vector2(-65f,  26f), new Vector2(15f, 17f));

        // HP Bar Track
        var barBg  = new GameObject("HealthBarBG");
        var barBgR = barBg.AddComponent<RectTransform>();
        var barBgI = barBg.AddComponent<Image>();
        barBg.transform.SetParent(hpBox.transform, false);
        barBgR.anchoredPosition = new Vector2(39f, -37.6f);
        barBgR.sizeDelta        = new Vector2(87f, 5f);
        if (hpBarTrackSprite != null) { barBgI.sprite = hpBarTrackSprite; barBgI.type = Image.Type.Sliced; }
        else barBgI.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // HP Fill
        var barFill  = new GameObject("HealthBarFill");
        var barFillR = barFill.AddComponent<RectTransform>();
        var barFillI = barFill.AddComponent<Image>();
        barFill.transform.SetParent(barBg.transform, false);
        barFillR.anchorMin   = Vector2.zero;
        barFillR.anchorMax   = Vector2.one;
        barFillR.offsetMin   = Vector2.zero;
        barFillR.offsetMax   = Vector2.zero;
        barFillI.color       = new Color(0.18f, 0.78f, 0.18f);
        slotUI.healthBarFill = barFillI;

        float third = size.x / 3f;

        // Attack text
        slotUI.attackText = CreateStatText(go.transform, "AttackText", new Vector2(-91f, 21.3f), third);

        // Speed text
        slotUI.speedText = CreateStatText(go.transform, "SpeedText", new Vector2(-91f, -18f), third);

        // HP text
        slotUI.hpText = CreateStatText(go.transform, "HPText", new Vector2(-67.5f, -60.5f), third);

        return slotUI;
    }

    // -------------------------------------------------------
    // TOOLTIP — shared between Shop and Battle scene generators
    // -------------------------------------------------------

    public static void CreateTooltip(Transform root)
    {
        var tooltipGO   = new GameObject("Tooltip");
        var tooltipRect = tooltipGO.AddComponent<RectTransform>();
        var tooltipImg  = tooltipGO.AddComponent<Image>();
        tooltipGO.transform.SetParent(root, false);
        tooltipRect.sizeDelta = new Vector2(300, 100);
        tooltipRect.pivot     = new Vector2(0f, 0f);
        tooltipImg.color      = new Color(0.05f, 0.05f, 0.05f, 0.95f);

        var outline = tooltipGO.AddComponent<Outline>();
        outline.effectColor    = new Color(0.6f, 0.6f, 0.8f, 0.8f);
        outline.effectDistance = new Vector2(2, 2);

        var tooltipUI = tooltipGO.AddComponent<TooltipUI>();

        tooltipUI.abilityNameText = CreateTMPText(tooltipGO.transform, "AbilityNameText", "Ability", 25,
            new Vector2(0, 25), new Vector2(280, 35));
        tooltipUI.abilityNameText.fontStyle = FontStyles.Bold;
        tooltipUI.abilityNameText.alignment = TextAlignmentOptions.Center;

        tooltipUI.abilityDescText = CreateTMPText(tooltipGO.transform, "AbilityDescText", "Description", 20,
            new Vector2(0, -15), new Vector2(280, 55));
        tooltipUI.abilityDescText.alignment          = TextAlignmentOptions.Center;
        tooltipUI.abilityDescText.enableWordWrapping = true;

        tooltipGO.transform.SetAsLastSibling();
    }

    // -------------------------------------------------------
    // PRIVATE HELPERS
    // -------------------------------------------------------

    static TextMeshProUGUI CreateStatText(Transform parent, string name, Vector2 pos, float width)
    {
        var tmp = CreateTMPText(parent, name, "", 23, pos, new Vector2(width, 36));
        tmp.alignment        = TextAlignmentOptions.Right;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.characterSpacing = -10;
        tmp.color            = new Color(36/255f, 36/255f, 36/255f);
        return tmp;
    }

    static void CreateIcon(Transform parent, string name, string spritePath, Vector2 pos, Vector2 size)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        var img  = go.AddComponent<Image>();
        go.transform.SetParent(parent, false);
        rect.anchoredPosition = pos;
        rect.sizeDelta        = size;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite != null) img.sprite = sprite;
        img.color = Color.white;
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

    public static TextMeshProUGUI CreateTMPText(Transform parent, string name, string text, int fontSize, Vector2 pos, Vector2 size)
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
        return tmp;
    }
}
