using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Populates the Pokédex overlay at runtime.
// Loads every PokemonData from Resources/PokemonDatabase and builds a scrollable card grid.
// Clicking a card fills the detail panel on the right.

public class PokedexPanel : MonoBehaviour
{
    [Header("Grid")]
    public RectTransform cardContainer;   // the Content RectTransform inside the ScrollView

    [Header("Filters")]
    public TMP_InputField searchInput;    // searches name, ability name, and ability description
    public TMP_Dropdown   typeDropdown;   // filters by type1
    public TMP_Dropdown   tierDropdown;   // filters by tier

    [Header("Detail Panel")]
    public GameObject      detailPanel;
    public Image           detailSprite;
    public TextMeshProUGUI detailName;
    public TextMeshProUGUI detailTypes;
    public TextMeshProUGUI detailStats;
    public TextMeshProUGUI detailAbility;
    public RectTransform   evolutionContainer; // vertical container to the right of the sprite
    public Image           detailTypeIcon;     // type icon, top-left of the detail panel

    // Auto-generated at runtime — no manual wiring needed
    private Image _detailElite4Icon;
    private Image _detailChampIcon;

    // -------------------------------------------------------

    private PokemonData[] _allPokemon;

    private struct CardEntry { public GameObject go; public PokemonData data; }
    private readonly List<CardEntry> _cards = new List<CardEntry>();

    private void OnEnable()
    {
        BuildGrid();

        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveAllListeners();
            searchInput.onValueChanged.AddListener(_ => ApplyFilters());
            searchInput.text = "";
        }
        if (typeDropdown != null)
        {
            typeDropdown.onValueChanged.RemoveAllListeners();
            typeDropdown.onValueChanged.AddListener(_ => ApplyFilters());
        }
        if (tierDropdown != null)
        {
            tierDropdown.onValueChanged.RemoveAllListeners();
            tierDropdown.onValueChanged.AddListener(_ => ApplyFilters());
        }
    }

    // -------------------------------------------------------
    // GRID BUILDER
    // -------------------------------------------------------

    void BuildGrid()
    {
        if (cardContainer == null) return;

        // Clear previous cards (handles re-open after runtime changes)
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);
        _cards.Clear();

        var db = Resources.Load<PokemonDatabase>("PokemonDatabase");
        if (db == null)
        {
            Debug.LogWarning("PokedexPanel: PokemonDatabase not found at Resources/PokemonDatabase");
            return;
        }

        // Deduplicate by id, then sort ascending by id — cache for evolution traversal
        var seen = new HashSet<int>();
        _allPokemon = db.allPokemon
            .Where(p => p != null && seen.Add(p.id))
            .OrderBy(p => p.id)
            .ToArray();

        foreach (var pokemon in _allPokemon)
        {
            var card = CreateCard(pokemon);
            _cards.Add(new CardEntry { go = card, data = pokemon });
        }

        PopulateDropdowns();

        _detailElite4Icon = null;
        _detailChampIcon  = null;
        if (detailPanel != null) detailPanel.SetActive(false);
    }

    // -------------------------------------------------------
    // FILTER / SEARCH
    // -------------------------------------------------------

    void PopulateDropdowns()
    {
        if (typeDropdown != null)
        {
            typeDropdown.ClearOptions();
            var types = new List<string> { "All Types" };
            types.AddRange(_allPokemon
                .Where(p => !string.IsNullOrEmpty(p.type1))
                .Select(p => p.type1)
                .Distinct()
                .OrderBy(t => t));
            typeDropdown.AddOptions(types);
            typeDropdown.value = 0;
        }

        if (tierDropdown != null)
        {
            tierDropdown.ClearOptions();
            var tiers = new List<string> { "All Tiers" };
            tiers.AddRange(_allPokemon
                .Select(p => p.tier)
                .Distinct()
                .OrderBy(t => t)
                .Select(t => "Tier " + t));
            tierDropdown.AddOptions(tiers);
            tierDropdown.value = 0;
        }
    }

    void ApplyFilters()
    {
        string search = searchInput != null ? searchInput.text.ToLower().Trim() : "";

        string typeFilter = "";
        if (typeDropdown != null && typeDropdown.value > 0)
            typeFilter = typeDropdown.options[typeDropdown.value].text;

        int tierFilter = -1;
        if (tierDropdown != null && tierDropdown.value > 0)
        {
            string opt = tierDropdown.options[tierDropdown.value].text; // "Tier X"
            if (int.TryParse(opt.Replace("Tier ", ""), out int t))
                tierFilter = t;
        }

        foreach (var entry in _cards)
        {
            bool show = true;

            // Search: name, ability name, or ability description
            if (!string.IsNullOrEmpty(search))
            {
                bool nameMatch    = entry.data.pokemonName.ToLower().Contains(search);
                bool abilityMatch = entry.data.ability != null && (
                    entry.data.ability.abilityName.ToLower().Contains(search) ||
                    entry.data.ability.description.ToLower().Contains(search));
                show = nameMatch || abilityMatch;
            }

            // Type filter
            if (show && !string.IsNullOrEmpty(typeFilter))
                show = string.Equals(entry.data.type1, typeFilter, System.StringComparison.OrdinalIgnoreCase);

            // Tier filter
            if (show && tierFilter >= 0)
                show = entry.data.tier == tierFilter;

            entry.go.SetActive(show);
        }
    }

    // -------------------------------------------------------
    // CARD CREATION
    // -------------------------------------------------------

    GameObject CreateCard(PokemonData pokemon)
    {
        // Root: button + dark background
        var card    = new GameObject(pokemon.pokemonName);
        var cardImg = card.AddComponent<Image>();
        var cardBtn = card.AddComponent<Button>();
        card.transform.SetParent(cardContainer, false);
        cardImg.color = new Color(0.15f, 0.15f, 0.25f, 1f);

        // Hover tint
        var colors              = cardBtn.colors;
        colors.highlightedColor = new Color(0.25f, 0.25f, 0.45f, 1f);
        colors.pressedColor     = new Color(0.1f, 0.1f, 0.2f, 1f);
        cardBtn.colors          = colors;

        // Pokemon sprite (top 70% of card)
        if (pokemon.sprite != null)
        {
            var spriteGO   = new GameObject("Sprite");
            var spriteRect = spriteGO.AddComponent<RectTransform>();
            var spriteImg  = spriteGO.AddComponent<Image>();
            spriteGO.transform.SetParent(card.transform, false);
            spriteRect.anchorMin     = new Vector2(0.05f, 0.3f);
            spriteRect.anchorMax     = new Vector2(0.95f, 1f);
            spriteRect.offsetMin     = Vector2.zero;
            spriteRect.offsetMax     = Vector2.zero;
            spriteImg.sprite         = pokemon.sprite;
            spriteImg.preserveAspect = true;
            spriteImg.raycastTarget  = false;
        }

        // Name label (bottom 25% of card)
        var nameGO   = new GameObject("Name");
        var nameRect = nameGO.AddComponent<RectTransform>();
        var nameTMP  = nameGO.AddComponent<TextMeshProUGUI>();
        nameGO.transform.SetParent(card.transform, false);
        nameRect.anchorMin       = new Vector2(0, 0);
        nameRect.anchorMax       = new Vector2(1, 0.3f);
        nameRect.offsetMin       = new Vector2(2, 0);
        nameRect.offsetMax       = new Vector2(-2, 0);
        nameTMP.text             = pokemon.pokemonName;
        nameTMP.fontSize         = 15;
        nameTMP.alignment        = TextAlignmentOptions.Center;
        nameTMP.color            = Color.white;
        nameTMP.raycastTarget    = false;
        nameTMP.enableAutoSizing = true;
        nameTMP.fontSizeMin      = 10;
        nameTMP.fontSizeMax      = 15;

        // Wire click
        var captured = pokemon;
        cardBtn.onClick.AddListener(() => ShowDetail(captured));

        return card;
    }

    // -------------------------------------------------------
    // DETAIL VIEW
    // -------------------------------------------------------

    void ShowDetail(PokemonData p)
    {
        if (detailPanel == null) return;
        detailPanel.SetActive(true);

        AudioManager.Instance?.PlayCry(p.id);

        if (detailSprite != null)
        {
            detailSprite.sprite         = p.sprite;
            detailSprite.preserveAspect = true;
            detailSprite.color          = p.sprite != null ? Color.white : Color.clear;
        }

        if (detailName != null)
        {
            detailName.characterSpacing = -2f;
            detailName.text = p.pokemonName;
        }

        if (detailTypes != null)
            detailTypes.text = string.IsNullOrEmpty(p.type1) ? "—" : p.type1.ToUpper();

        if (detailTypeIcon != null)
        {
            if (!string.IsNullOrEmpty(p.type1))
            {
                var icon = Resources.Load<Sprite>("Icons/" + p.type1.ToLower());
                detailTypeIcon.sprite = icon;
                detailTypeIcon.gameObject.SetActive(icon != null);
            }
            else
                detailTypeIcon.gameObject.SetActive(false);
        }

        EnsureUnlockIcons();

        if (_detailElite4Icon != null)
            _detailElite4Icon.color = PokedexUnlockManager.HasElite4(p.id)
                ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.5f);

        if (_detailChampIcon != null)
            _detailChampIcon.color = PokedexUnlockManager.HasChamp(p.id)
                ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.5f);

        if (detailStats != null)
            detailStats.text = $"ATK {p.attack}\n" +
                               $"SPD {p.speed}\n" +
                               $"HP {p.hp}\n" +
                               $"Tier {p.tier}";

        if (detailAbility != null)
        {
            if (p.ability != null)
                detailAbility.text = $"<b>{p.ability.abilityName}</b>\n{p.ability.description}";
            else
                detailAbility.text = "—";
        }

        BuildEvolutionChain(p);
    }

    // Creates the star and champ achievement icons below detailTypeIcon on first call.
    void EnsureUnlockIcons()
    {
        if (detailTypeIcon == null) return;
        if (_detailElite4Icon != null && _detailChampIcon != null) return;

        var typeRT = detailTypeIcon.GetComponent<RectTransform>();
        float size = typeRT.sizeDelta.y;
        float gap  = size + 6f;

        // Parent directly to detailTypeIcon so positions are in its local space
        _detailElite4Icon = CreateUnlockIcon(detailTypeIcon.transform, "Elite4Icon", "Icons/star",
            new Vector2(0f, -gap), typeRT.sizeDelta);

        _detailChampIcon  = CreateUnlockIcon(detailTypeIcon.transform, "ChampIcon",  "Icons/champ",
            new Vector2(0f, -gap * 2f), typeRT.sizeDelta);
    }

    Image CreateUnlockIcon(Transform parent, string goName, string spritePath,
                           Vector2 anchoredPos, Vector2 size)
    {
        var existing = parent.Find(goName);
        if (existing != null) return existing.GetComponent<Image>();

        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);

        var rt              = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = size;
        rt.anchoredPosition = anchoredPos;

        var img             = go.AddComponent<Image>();
        img.sprite          = Resources.Load<Sprite>(spritePath);
        img.preserveAspect  = true;
        img.raycastTarget   = false;

        return img;
    }

    // -------------------------------------------------------
    // EVOLUTION CHAIN
    // -------------------------------------------------------

    // Returns the ordered chain: root → stage 1 → stage 2 (→ branches last).
    // Uses BFS from the root so branching evolutions (e.g. Eevee) all appear.
    List<PokemonData> GetEvolutionChain(PokemonData current)
    {
        if (_allPokemon == null) return new List<PokemonData>();

        // Walk backwards to find the base form
        var root = current;
        int guard = 0;
        while (root.preEvolutionId != 0 && guard++ < 10)
        {
            var pre = System.Array.Find(_allPokemon, p => p.id == root.preEvolutionId);
            if (pre == null) break;
            root = pre;
        }

        // BFS forward to collect the full family in order
        var chain   = new List<PokemonData>();
        var queue   = new Queue<PokemonData>();
        var visited = new HashSet<int>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (!visited.Add(node.id)) continue;
            chain.Add(node);
            foreach (var p in _allPokemon)
                if (p.preEvolutionId == node.id)
                    queue.Enqueue(p);
        }

        return chain;
    }

    void BuildEvolutionChain(PokemonData current)
    {
        if (evolutionContainer == null) return;

        // Clear old entries
        foreach (Transform child in evolutionContainer)
            Destroy(child.gameObject);

        var chain = GetEvolutionChain(current);

        // Only show the chain if there is more than one stage
        evolutionContainer.gameObject.SetActive(chain.Count > 1);
        if (chain.Count <= 1) return;

        // Ensure a vertical layout group exists on the container
        var vlg = evolutionContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = evolutionContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing            = 3f;
        vlg.childAlignment     = TextAnchor.UpperCenter;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = evolutionContainer.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = evolutionContainer.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (var stage in chain)
        {
            bool isCurrent = stage.id == current.id;
            CreateEvoEntry(stage, isCurrent);
        }
    }

    void CreateEvoEntry(PokemonData pokemon, bool isCurrent)
    {
        // Row: invisible button, just sprite + name
        var row    = new GameObject(pokemon.pokemonName);
        var rowImg = row.AddComponent<Image>();
        var rowBtn = row.AddComponent<Button>();
        var rowLE  = row.AddComponent<LayoutElement>();
        row.transform.SetParent(evolutionContainer, false);
        rowLE.preferredHeight = 64f;
        // Fully transparent background — no box
        rowImg.color = Color.clear;

        // Subtle hover tint on the invisible bg
        var colors              = rowBtn.colors;
        colors.normalColor      = Color.clear;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
        colors.pressedColor     = new Color(1f, 1f, 1f, 0.04f);
        colors.selectedColor    = Color.clear;
        rowBtn.colors           = colors;

        // Sprite
        var spriteGO   = new GameObject("Sprite");
        var spriteRect = spriteGO.AddComponent<RectTransform>();
        var spriteImg  = spriteGO.AddComponent<Image>();
        spriteGO.transform.SetParent(row.transform, false);
        spriteRect.anchorMin     = new Vector2(0f, 0f);
        spriteRect.anchorMax     = new Vector2(0f, 1f);
        spriteRect.pivot         = new Vector2(0f, 0.5f);
        spriteRect.offsetMin     = new Vector2(2f,  2f);
        spriteRect.offsetMax     = new Vector2(58f, -2f);
        spriteImg.sprite         = pokemon.sprite;
        spriteImg.preserveAspect = true;
        spriteImg.color          = pokemon.sprite != null ? Color.white : Color.clear;
        spriteImg.raycastTarget  = false;

        // Name
        var nameGO   = new GameObject("Name");
        var nameRect = nameGO.AddComponent<RectTransform>();
        var nameTMP  = nameGO.AddComponent<TextMeshProUGUI>();
        nameGO.transform.SetParent(row.transform, false);
        nameRect.anchorMin    = new Vector2(0f, 0f);
        nameRect.anchorMax    = new Vector2(1f, 1f);
        nameRect.offsetMin    = new Vector2(62f, 0f);
        nameRect.offsetMax    = new Vector2(-2f, 0f);
        nameTMP.text          = pokemon.pokemonName;
        nameTMP.fontSize      = 11f;
        nameTMP.alignment     = TextAlignmentOptions.MidlineLeft;
        // Current pokemon is white, others are dimmer
        nameTMP.color         = isCurrent ? Color.white : new Color(0.65f, 0.65f, 0.65f, 1f);
        nameTMP.fontStyle     = isCurrent ? FontStyles.Bold : FontStyles.Normal;
        nameTMP.raycastTarget = false;
        nameTMP.enableAutoSizing = true;
        nameTMP.fontSizeMin   = 9f;
        nameTMP.fontSizeMax   = 12f;

        var captured = pokemon;
        rowBtn.onClick.AddListener(() => ShowDetail(captured));
    }
}
