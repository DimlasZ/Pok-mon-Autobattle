using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// Full-screen victory overlay shown when the player reaches 13 wins.
// Built entirely in code — no prefab or Inspector assignments required.
// Call Show(team) from GameManager.EnterVictory(); Continue button returns to Main Menu.

public class VictoryOverlayUI : MonoBehaviour
{
    const float CardSize  = 140f;
    const float CardGap   = 20f;

    private GameObject _teamRow;

    // -------------------------------------------------------

    private void Awake()
    {
        BuildLayout();
        gameObject.SetActive(false);
    }

    public void Show(PokemonInstance[] team)
    {
        gameObject.SetActive(true);
        AudioManager.Instance?.PlayMusic("HallofFame");
        PopulateTeam(team);
        transform.localScale = Vector3.one * 0.85f;
        transform.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        AudioManager.Instance?.StopMusic();
    }

    // -------------------------------------------------------
    // Layout
    // -------------------------------------------------------

    void BuildLayout()
    {
        // Fullscreen dark backdrop
        var bg = gameObject.AddComponent<Image>();
        bg.color         = new Color(0f, 0f, 0f, 0.985f);
        bg.raycastTarget = true;

        // Gold title
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(transform, false);
        var titleRT  = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0.5f, 1f);
        titleRT.anchorMax        = new Vector2(0.5f, 1f);
        titleRT.pivot            = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -80f);
        titleRT.sizeDelta        = new Vector2(1400f, 140f);
        var titleTMP  = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "You are the Champion now!";
        titleTMP.fontSize  = 80;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color     = new Color(1f, 0.84f, 0f);   // gold

        // Subtitle
        var subGO = new GameObject("Subtitle");
        subGO.transform.SetParent(transform, false);
        var subRT  = subGO.AddComponent<RectTransform>();
        subRT.anchorMin        = new Vector2(0.5f, 1f);
        subRT.anchorMax        = new Vector2(0.5f, 1f);
        subRT.pivot            = new Vector2(0.5f, 1f);
        subRT.anchoredPosition = new Vector2(0f, -230f);
        subRT.sizeDelta        = new Vector2(900f, 60f);
        var subTMP  = subGO.AddComponent<TextMeshProUGUI>();
        subTMP.text      = "Your Champion Team";
        subTMP.fontSize  = 36;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color     = new Color(0.85f, 0.85f, 0.85f);

        // Team row container (filled at runtime by PopulateTeam)
        _teamRow = new GameObject("TeamRow");
        _teamRow.transform.SetParent(transform, false);
        var rowRT = _teamRow.AddComponent<RectTransform>();
        rowRT.anchorMin        = new Vector2(0.5f, 0.5f);
        rowRT.anchorMax        = new Vector2(0.5f, 0.5f);
        rowRT.pivot            = new Vector2(0.5f, 0.5f);
        rowRT.anchoredPosition = new Vector2(0f, 20f);
        rowRT.sizeDelta        = new Vector2(1200f, CardSize + 60f);

        var hLayout = _teamRow.AddComponent<HorizontalLayoutGroup>();
        hLayout.childAlignment         = TextAnchor.MiddleCenter;
        hLayout.childControlWidth      = false;
        hLayout.childControlHeight     = false;
        hLayout.childForceExpandWidth  = false;
        hLayout.childForceExpandHeight = false;
        hLayout.spacing = CardGap;

        // Continue button
        var btnGO = new GameObject("ContinueButton");
        btnGO.transform.SetParent(transform, false);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin        = new Vector2(0.5f, 0f);
        btnRT.anchorMax        = new Vector2(0.5f, 0f);
        btnRT.pivot            = new Vector2(0.5f, 0f);
        btnRT.anchoredPosition = new Vector2(0f, 60f);
        btnRT.sizeDelta        = new Vector2(380f, 80f);

        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.75f, 0.55f, 0.05f);   // dark gold

        var btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(OnContinueClicked);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text      = "Continue";
        labelTMP.fontSize  = 36;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.color     = Color.white;
    }

    void PopulateTeam(PokemonInstance[] team)
    {
        // Clear previous cards
        foreach (Transform child in _teamRow.transform)
            Destroy(child.gameObject);

        if (team == null) return;

        foreach (var pokemon in team)
        {
            if (pokemon == null) continue;
            BuildPokemonCard(_teamRow.transform, pokemon);
        }
    }

    void BuildPokemonCard(Transform parent, PokemonInstance pokemon)
    {
        var card = new GameObject($"Card_{pokemon.baseData.pokemonName}");
        card.transform.SetParent(parent, false);

        var cardRT = card.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(CardSize, CardSize + 60f);

        var cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.12f, 0.12f, 0.20f, 0.95f);

        // Sprite
        var spriteGO = new GameObject("Sprite");
        spriteGO.transform.SetParent(card.transform, false);
        var spriteRT  = spriteGO.AddComponent<RectTransform>();
        spriteRT.anchorMin        = new Vector2(0.5f, 1f);
        spriteRT.anchorMax        = new Vector2(0.5f, 1f);
        spriteRT.pivot            = new Vector2(0.5f, 1f);
        spriteRT.anchoredPosition = new Vector2(0f, -8f);
        spriteRT.sizeDelta        = new Vector2(CardSize - 16f, CardSize - 16f);
        var spriteImg  = spriteGO.AddComponent<Image>();
        spriteImg.sprite         = pokemon.baseData.sprite;
        spriteImg.preserveAspect = true;
        spriteImg.raycastTarget  = false;
        if (pokemon.baseData.sprite == null)
            spriteImg.color = Color.clear;

        // Name
        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(card.transform, false);
        var nameRT  = nameGO.AddComponent<RectTransform>();
        nameRT.anchorMin        = new Vector2(0f, 0f);
        nameRT.anchorMax        = new Vector2(1f, 0f);
        nameRT.pivot            = new Vector2(0.5f, 0f);
        nameRT.anchoredPosition = new Vector2(0f, 24f);
        nameRT.sizeDelta        = new Vector2(0f, 28f);
        var nameTMP  = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text      = pokemon.baseData.pokemonName;
        nameTMP.fontSize  = 18;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.color     = Color.white;

        // Star level
        var starsGO = new GameObject("Stars");
        starsGO.transform.SetParent(card.transform, false);
        var starsRT  = starsGO.AddComponent<RectTransform>();
        starsRT.anchorMin        = new Vector2(0f, 0f);
        starsRT.anchorMax        = new Vector2(1f, 0f);
        starsRT.pivot            = new Vector2(0.5f, 0f);
        starsRT.anchoredPosition = new Vector2(0f, 4f);
        starsRT.sizeDelta        = new Vector2(0f, 22f);
        var starsTMP  = starsGO.AddComponent<TextMeshProUGUI>();
        starsTMP.text      = new string('★', pokemon.starLevel);
        starsTMP.fontSize  = 18;
        starsTMP.alignment = TextAlignmentOptions.Center;
        starsTMP.color     = new Color(1f, 0.84f, 0f);
    }

    // -------------------------------------------------------

    void OnContinueClicked()
    {
        AudioManager.Instance?.PlayButtonSound();
        Hide();
        GameManager.Instance?.ReturnToMainMenu();
    }
}
