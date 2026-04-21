using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Brief overlay shown when the shop tier increases.
// Dismissed only by the "Got it!" button.
// Call Show(newTier, newPokemon) from ShopManager.StartRound().

public class TierUpgradeOverlayUI : MonoBehaviour
{
    const float CardSize = 95f;
    const float CardGap  = 8f;

    private TextMeshProUGUI _tierLabel;
    private TextMeshProUGUI _heartLabel;
    private RectTransform   _cardGrid;

    // -------------------------------------------------------

    private void Awake()
    {
        BuildLayout();
        gameObject.SetActive(false);
    }

    public void Show(int newTier, PokemonData[] newPokemon, bool heartRestored = false)
    {
        if (_tierLabel != null)
            _tierLabel.text = $"Tier {newTier} unlocked!";

        PopulateCards(newPokemon);

        if (_heartLabel != null)
        {
            _heartLabel.text    = heartRestored ? "You got a Heart back." : "";
            _heartLabel.enabled = heartRestored;
        }

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // -------------------------------------------------------

    void PopulateCards(PokemonData[] pokemon)
    {
        if (_cardGrid == null) return;
        foreach (Transform child in _cardGrid)
            Destroy(child.gameObject);

        if (pokemon == null) return;
        foreach (var p in pokemon)
            BuildCard(_cardGrid, p);
    }

    void BuildCard(Transform parent, PokemonData pokemon)
    {
        var card = new GameObject($"Card_{pokemon.pokemonName}");
        card.transform.SetParent(parent, false);
        card.AddComponent<RectTransform>().sizeDelta = new Vector2(CardSize, CardSize + 38f);

        card.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.22f, 0.95f);

        var spriteGO = new GameObject("Sprite");
        spriteGO.transform.SetParent(card.transform, false);
        var sRT = spriteGO.AddComponent<RectTransform>();
        sRT.anchorMin        = new Vector2(0.5f, 1f);
        sRT.anchorMax        = new Vector2(0.5f, 1f);
        sRT.pivot            = new Vector2(0.5f, 1f);
        sRT.anchoredPosition = new Vector2(0f, -6f);
        sRT.sizeDelta        = new Vector2(CardSize - 14f, CardSize - 14f);
        var sImg = spriteGO.AddComponent<Image>();
        sImg.sprite         = pokemon.sprite;
        sImg.preserveAspect = true;
        sImg.raycastTarget  = false;
        if (pokemon.sprite == null) sImg.color = new Color(0.3f, 0.3f, 0.3f);

        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(card.transform, false);
        var nRT = nameGO.AddComponent<RectTransform>();
        nRT.anchorMin        = new Vector2(0f, 0f);
        nRT.anchorMax        = new Vector2(1f, 0f);
        nRT.pivot            = new Vector2(0.5f, 0f);
        nRT.anchoredPosition = new Vector2(0f, 4f);
        nRT.sizeDelta        = new Vector2(0f, 28f);
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text      = pokemon.pokemonName;
        nameTMP.fontSize  = 12;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.color     = Color.white;
    }

    // -------------------------------------------------------

    void BuildLayout()
    {
        var bg = gameObject.AddComponent<Image>();
        bg.color         = new Color(0f, 0f, 0f, 0.82f);
        bg.raycastTarget = true;

        // Centre panel
        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(transform, false);
        var panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRT.pivot            = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta        = new Vector2(1500f, 750f);
        panelGO.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.14f, 0.97f);
        panelGO.AddComponent<Outline>().effectColor = new Color(0.6f, 0.5f, 0.1f, 0.9f);

        // Tier label
        var tierGO = new GameObject("TierLabel");
        tierGO.transform.SetParent(panelGO.transform, false);
        var tierRT = tierGO.AddComponent<RectTransform>();
        tierRT.anchorMin        = new Vector2(0.5f, 1f);
        tierRT.anchorMax        = new Vector2(0.5f, 1f);
        tierRT.pivot            = new Vector2(0.5f, 1f);
        tierRT.anchoredPosition = new Vector2(0f, -28f);
        tierRT.sizeDelta        = new Vector2(1100f, 85f);
        _tierLabel           = tierGO.AddComponent<TextMeshProUGUI>();
        _tierLabel.fontSize  = 62;
        _tierLabel.fontStyle = FontStyles.Bold;
        _tierLabel.alignment = TextAlignmentOptions.Center;
        _tierLabel.color     = new Color(1f, 0.84f, 0f);

        // Sub-label
        var subGO = new GameObject("SubLabel");
        subGO.transform.SetParent(panelGO.transform, false);
        var subRT = subGO.AddComponent<RectTransform>();
        subRT.anchorMin        = new Vector2(0.5f, 1f);
        subRT.anchorMax        = new Vector2(0.5f, 1f);
        subRT.pivot            = new Vector2(0.5f, 1f);
        subRT.anchoredPosition = new Vector2(0f, -120f);
        subRT.sizeDelta        = new Vector2(1100f, 44f);
        var subTMP = subGO.AddComponent<TextMeshProUGUI>();
        subTMP.text      = "New Pokémon available in the shop:";
        subTMP.fontSize  = 28;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color     = new Color(0.8f, 0.8f, 0.8f);

        // Card grid
        var gridGO = new GameObject("CardGrid");
        gridGO.transform.SetParent(panelGO.transform, false);
        var gridRT = gridGO.AddComponent<RectTransform>();
        gridRT.anchorMin        = new Vector2(0f, 0f);
        gridRT.anchorMax        = new Vector2(1f, 0f);
        gridRT.pivot            = new Vector2(0.5f, 0f);
        gridRT.anchoredPosition = new Vector2(0f, 80f);
        gridRT.sizeDelta        = new Vector2(-60f, 460f);
        _cardGrid = gridRT;

        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(CardSize, CardSize + 38f);
        grid.spacing         = new Vector2(CardGap, CardGap);
        grid.padding         = new RectOffset(12, 12, 12, 12);
        grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment  = TextAnchor.UpperCenter;
        grid.constraint      = GridLayoutGroup.Constraint.Flexible;

        // Heart restored label
        var heartGO = new GameObject("HeartLabel");
        heartGO.transform.SetParent(panelGO.transform, false);
        var heartRT = heartGO.AddComponent<RectTransform>();
        heartRT.anchorMin        = new Vector2(0.5f, 0f);
        heartRT.anchorMax        = new Vector2(0.5f, 0f);
        heartRT.pivot            = new Vector2(0.5f, 0f);
        heartRT.anchoredPosition = new Vector2(0f, 88f);
        heartRT.sizeDelta        = new Vector2(900f, 40f);
        _heartLabel           = heartGO.AddComponent<TextMeshProUGUI>();
        _heartLabel.fontSize  = 26;
        _heartLabel.alignment = TextAlignmentOptions.Center;
        _heartLabel.color     = new Color(1f, 0.4f, 0.4f);
        _heartLabel.enabled   = false;

        // "Got it!" button
        var btnGO = new GameObject("GotItButton");
        btnGO.transform.SetParent(panelGO.transform, false);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin        = new Vector2(0.5f, 0f);
        btnRT.anchorMax        = new Vector2(0.5f, 0f);
        btnRT.pivot            = new Vector2(0.5f, 0f);
        btnRT.anchoredPosition = new Vector2(0f, 18f);
        btnRT.sizeDelta        = new Vector2(300f, 55f);
        btnGO.AddComponent<Image>().color = new Color(0.18f, 0.52f, 0.18f);
        var btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonSound(); Hide(); });

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var lRT = labelGO.AddComponent<RectTransform>();
        lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
        lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;
        var lTMP = labelGO.AddComponent<TextMeshProUGUI>();
        lTMP.text      = "Got it!";
        lTMP.fontSize  = 28;
        lTMP.fontStyle = FontStyles.Bold;
        lTMP.alignment = TextAlignmentOptions.Center;
        lTMP.color     = Color.white;
    }
}
