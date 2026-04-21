using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Hall of Fame panel — lists all previous champion runs loaded from JSON.
// Built entirely in code. Attach to a panel GO; call Refresh() when opening.

public class HallOfFamePanel : MonoBehaviour
{
    const float EntryHeight = 200f;
    const float CardSize    = 90f;
    const float CardGap     = 10f;

    private RectTransform _content;

    // -------------------------------------------------------

    private void Awake()
    {
        BuildLayout();
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        Refresh();
    }

    // -------------------------------------------------------

    public void Refresh()
    {
        if (_content == null) return;

        foreach (Transform child in _content)
            Destroy(child.gameObject);

        var data = HallOfFameManager.Load();

        if (data.entries == null || data.entries.Count == 0)
        {
            var emptyGO = new GameObject("EmptyLabel");
            emptyGO.transform.SetParent(_content, false);
            var rt  = emptyGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900f, 80f);
            var tmp = emptyGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = "No champions yet. Go win!";
            tmp.fontSize  = 32;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = new Color(0.6f, 0.6f, 0.6f);
            return;
        }

        // Most recent first
        for (int i = data.entries.Count - 1; i >= 0; i--)
            BuildEntry(data.entries[i]);
    }

    void BuildEntry(HallOfFameEntry entry)
    {
        var row = new GameObject($"Entry_{entry.runNumber}");
        row.transform.SetParent(_content, false);

        var rowRT = row.AddComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(0f, EntryHeight);

        var rowBg = row.AddComponent<Image>();
        rowBg.color = new Color(0.10f, 0.10f, 0.18f, 0.95f);

        // Run number + date header
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(row.transform, false);
        var headerRT  = headerGO.AddComponent<RectTransform>();
        headerRT.anchorMin        = new Vector2(0f, 1f);
        headerRT.anchorMax        = new Vector2(1f, 1f);
        headerRT.pivot            = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = new Vector2(0f, -8f);
        headerRT.sizeDelta        = new Vector2(-20f, 36f);
        var headerTMP  = headerGO.AddComponent<TextMeshProUGUI>();
        headerTMP.text      = $"<b>Champion Run #{entry.runNumber}</b>   <color=#AAAAAA>{entry.date}</color>";
        headerTMP.fontSize  = 22;
        headerTMP.alignment = TextAlignmentOptions.Left;
        headerTMP.color     = new Color(1f, 0.84f, 0f);

        // Pokemon cards row
        var cardsGO = new GameObject("Cards");
        cardsGO.transform.SetParent(row.transform, false);
        var cardsRT = cardsGO.AddComponent<RectTransform>();
        cardsRT.anchorMin        = new Vector2(0f, 0f);
        cardsRT.anchorMax        = new Vector2(0f, 0f);
        cardsRT.pivot            = new Vector2(0f, 0f);
        cardsRT.anchoredPosition = new Vector2(10f, 10f);
        cardsRT.sizeDelta        = new Vector2(800f, CardSize + 36f);

        var hLayout = cardsGO.AddComponent<HorizontalLayoutGroup>();
        hLayout.childAlignment         = TextAnchor.MiddleLeft;
        hLayout.childControlWidth      = false;
        hLayout.childControlHeight     = false;
        hLayout.childForceExpandWidth  = false;
        hLayout.childForceExpandHeight = false;
        hLayout.spacing = CardGap;

        if (entry.pokemonNames != null)
        {
            for (int i = 0; i < entry.pokemonNames.Length; i++)
            {
                int   id        = entry.pokemonIds   != null && i < entry.pokemonIds.Length   ? entry.pokemonIds[i]   : 0;
                int   starLevel = entry.starLevels   != null && i < entry.starLevels.Length   ? entry.starLevels[i]   : 1;
                string name     = entry.pokemonNames[i];
                BuildMiniCard(cardsGO.transform, id, name, starLevel);
            }
        }
    }

    void BuildMiniCard(Transform parent, int pokemonId, string pokemonName, int starLevel)
    {
        var card = new GameObject($"MiniCard_{pokemonName}");
        card.transform.SetParent(parent, false);

        var cardRT = card.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(CardSize, CardSize + 36f);

        var cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.16f, 0.16f, 0.26f, 0.95f);

        // Sprite
        Sprite sprite = null;
        if (pokemonId > 0)
            sprite = Resources.Load<Sprite>($"Data/Pokemon/{pokemonId:D4} {pokemonName}") ??
                     TryLoadSprite(pokemonId);

        var spriteGO = new GameObject("Sprite");
        spriteGO.transform.SetParent(card.transform, false);
        var srt  = spriteGO.AddComponent<RectTransform>();
        srt.anchorMin        = new Vector2(0.5f, 1f);
        srt.anchorMax        = new Vector2(0.5f, 1f);
        srt.pivot            = new Vector2(0.5f, 1f);
        srt.anchoredPosition = new Vector2(0f, -4f);
        srt.sizeDelta        = new Vector2(CardSize - 8f, CardSize - 8f);
        var sImg  = spriteGO.AddComponent<Image>();
        sImg.sprite         = sprite;
        sImg.preserveAspect = true;
        sImg.raycastTarget  = false;
        if (sprite == null) sImg.color = new Color(0.3f, 0.3f, 0.3f);

        // Name
        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(card.transform, false);
        var nrt  = nameGO.AddComponent<RectTransform>();
        nrt.anchorMin        = new Vector2(0f, 0f);
        nrt.anchorMax        = new Vector2(1f, 0f);
        nrt.pivot            = new Vector2(0.5f, 0f);
        nrt.anchoredPosition = new Vector2(0f, 18f);
        nrt.sizeDelta        = new Vector2(0f, 20f);
        var nameTMP  = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text      = pokemonName;
        nameTMP.fontSize  = 13;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.color     = Color.white;

        // Stars
        var starsGO = new GameObject("Stars");
        starsGO.transform.SetParent(card.transform, false);
        var strt  = starsGO.AddComponent<RectTransform>();
        strt.anchorMin        = new Vector2(0f, 0f);
        strt.anchorMax        = new Vector2(1f, 0f);
        strt.pivot            = new Vector2(0.5f, 0f);
        strt.anchoredPosition = new Vector2(0f, 2f);
        strt.sizeDelta        = new Vector2(0f, 18f);
        var starsTMP  = starsGO.AddComponent<TextMeshProUGUI>();
        starsTMP.text      = new string('★', Mathf.Clamp(starLevel, 1, 3));
        starsTMP.fontSize  = 14;
        starsTMP.alignment = TextAlignmentOptions.Center;
        starsTMP.color     = new Color(1f, 0.84f, 0f);
    }

    Sprite TryLoadSprite(int pokemonId)
    {
        var db = Resources.Load<PokemonDatabase>("PokemonDatabase");
        if (db == null) return null;
        foreach (var p in db.allPokemon)
            if (p != null && p.id == pokemonId) return p.sprite;
        return null;
    }

    // -------------------------------------------------------
    // Layout build
    // -------------------------------------------------------

    void BuildLayout()
    {
        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.10f, 0.97f);
        gameObject.AddComponent<Outline>().effectColor = new Color(0.5f, 0.4f, 0.1f, 0.8f);

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(transform, false);
        var titleRT  = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0.5f, 1f);
        titleRT.anchorMax        = new Vector2(0.5f, 1f);
        titleRT.pivot            = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -20f);
        titleRT.sizeDelta        = new Vector2(900f, 70f);
        var titleTMP  = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "Hall of Fame";
        titleTMP.fontSize  = 52;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color     = new Color(1f, 0.84f, 0f);

        // Close button
        var closeBtnGO = new GameObject("CloseButton");
        closeBtnGO.transform.SetParent(transform, false);
        var cbRT = closeBtnGO.AddComponent<RectTransform>();
        cbRT.anchorMin        = new Vector2(1f, 1f);
        cbRT.anchorMax        = new Vector2(1f, 1f);
        cbRT.pivot            = new Vector2(1f, 1f);
        cbRT.anchoredPosition = new Vector2(-15f, -15f);
        cbRT.sizeDelta        = new Vector2(55f, 55f);
        var cbImg = closeBtnGO.AddComponent<Image>();
        cbImg.color = new Color(0.6f, 0.1f, 0.1f);
        var cbBtn = closeBtnGO.AddComponent<Button>();
        cbBtn.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayButtonSound();
            GlobalOverlayManager.Instance?.CloseAll();
        });
        var xSprite = Resources.Load<Sprite>("Icons/X");
        if (xSprite != null)
        {
            var iconGO  = new GameObject("Icon");
            var iconRT  = iconGO.AddComponent<RectTransform>();
            var iconImg = iconGO.AddComponent<Image>();
            iconGO.transform.SetParent(closeBtnGO.transform, false);
            iconRT.anchorMin = new Vector2(0.1f, 0.1f);
            iconRT.anchorMax = new Vector2(0.9f, 0.9f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            iconImg.sprite         = xSprite;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
        }

        // Scroll view
        var scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(transform, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin        = new Vector2(0f, 0f);
        scrollRT.anchorMax        = new Vector2(1f, 1f);
        scrollRT.offsetMin        = new Vector2(20f, 20f);
        scrollRT.offsetMax        = new Vector2(-20f, -110f);
        var scrollImg = scrollGO.AddComponent<Image>();
        scrollImg.color = new Color(0.08f, 0.08f, 0.14f, 0.9f);
        var scrollRect = scrollGO.AddComponent<ScrollRect>();

        var vpGO   = new GameObject("Viewport");
        var vpRT   = vpGO.AddComponent<RectTransform>();
        vpGO.AddComponent<Image>();
        vpGO.AddComponent<Mask>().showMaskGraphic = false;
        vpGO.transform.SetParent(scrollGO.transform, false);
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(vpGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0f, 0f);
        _content = contentRT;

        var vLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        vLayout.childAlignment         = TextAnchor.UpperCenter;
        vLayout.childControlWidth      = true;
        vLayout.childControlHeight     = false;
        vLayout.childForceExpandWidth  = true;
        vLayout.childForceExpandHeight = false;
        vLayout.spacing = 12f;
        vLayout.padding = new RectOffset(10, 10, 10, 10);

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content          = contentRT;
        scrollRect.viewport         = vpRT;
        scrollRect.horizontal       = false;
        scrollRect.vertical         = true;
        scrollRect.scrollSensitivity = 35;
        scrollRect.movementType     = ScrollRect.MovementType.Clamped;
    }
}
