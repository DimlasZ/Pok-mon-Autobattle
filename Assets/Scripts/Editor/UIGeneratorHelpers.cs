using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public static class UIGeneratorHelpers
{
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
        outline.effectColor    = new Color(0.6f, 0.6f, 0.6f, 0.8f);
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

        // Leave active so Awake() runs and sets TooltipUI.Instance; Awake hides it immediately.
        tooltipGO.transform.SetAsLastSibling();
    }

    static TextMeshProUGUI CreateTMPText(Transform parent, string name, string text, int fontSize, Vector2 pos, Vector2 size)
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
