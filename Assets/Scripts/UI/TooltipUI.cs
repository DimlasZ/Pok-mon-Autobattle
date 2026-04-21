using UnityEngine;
using UnityEngine.UI;
using TMPro;

// TooltipUI shows a floating panel with ability info when hovering over a Pokemon slot.
// Attach this to a UI panel GameObject in your Canvas. Assign the text fields in the Inspector.

public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance { get; private set; }

    [Header("References")]
    public TextMeshProUGUI pokemonNameText;
    public TextMeshProUGUI abilityText;      // Shows "<b>AbilityName</b> - {ability.description}" combined
    public Image           typeIcon;         // Small type symbol next to the Pokemon name
    public Image           preEvoImage;      // Sprite of the pre-evolution (hidden if none)
    public Image           evoImage;         // Sprite of the evolution (hidden if none)

    private RectTransform _rect;
    private Canvas        _canvas;

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _rect   = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();

        gameObject.SetActive(false);
    }

    // -------------------------------------------------------

    public void Show(PokemonData pokemon, Vector2 screenPosition)
    {
        string pokemonName = pokemon != null ? pokemon.pokemonName : "";
        string type        = pokemon != null ? pokemon.type1 : "";
        AbilityData ability = pokemon != null ? pokemon.ability : null;

        if (pokemonNameText != null) pokemonNameText.text = pokemonName;
        if (abilityText != null)
            abilityText.text = ability != null
                ? $"<b>{ability.abilityName}</b> - {ability.description}"
                : "";

        if (typeIcon != null)
        {
            if (!string.IsNullOrEmpty(type))
            {
                var sprite = Resources.Load<Sprite>("Icons/" + type.ToLower());
                typeIcon.sprite = sprite;
                typeIcon.gameObject.SetActive(sprite != null);
            }
            else
            {
                typeIcon.gameObject.SetActive(false);
            }
        }

        if (preEvoImage != null) preEvoImage.gameObject.SetActive(false);
        if (evoImage   != null) evoImage.gameObject.SetActive(false);

        gameObject.SetActive(true);
        RepositionTooltip(screenPosition);
    }

    public void ShowAlreadyOwned(PokemonData pokemon, Vector2 screenPosition)
    {
        if (pokemonNameText != null) pokemonNameText.text = pokemon != null ? pokemon.pokemonName : "";
        if (abilityText     != null) abilityText.text     = "Already on your team";

        if (typeIcon != null)
        {
            string type = pokemon != null ? pokemon.type1 : "";
            if (!string.IsNullOrEmpty(type))
            {
                var sprite = Resources.Load<Sprite>("Icons/" + type.ToLower());
                typeIcon.sprite = sprite;
                typeIcon.gameObject.SetActive(sprite != null);
            }
            else typeIcon.gameObject.SetActive(false);
        }

        if (preEvoImage != null) preEvoImage.gameObject.SetActive(false);
        if (evoImage    != null) evoImage.gameObject.SetActive(false);

        gameObject.SetActive(true);
        RepositionTooltip(screenPosition);
    }

    public void ShowMessage(string message, Vector2 screenPosition)
    {
        if (pokemonNameText != null) pokemonNameText.text = "";
        if (abilityText     != null) abilityText.text     = message;
        if (typeIcon        != null) typeIcon.gameObject.SetActive(false);
        if (preEvoImage     != null) preEvoImage.gameObject.SetActive(false);
        if (evoImage        != null) evoImage.gameObject.SetActive(false);

        gameObject.SetActive(true);
        RepositionTooltip(screenPosition);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // Keeps the tooltip on screen — flips it left or up if it would go off edge
    private void RepositionTooltip(Vector2 screenPos)
    {
        // Convert screen position to canvas local position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            screenPos,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
            out Vector2 localPos
        );

        // Offset upward so the tooltip appears above the slot
        Vector2 offset = new Vector2(-_rect.sizeDelta.x / 2f, 10f);
        _rect.localPosition = localPos + offset;

        // Clamp so it stays inside the canvas
        Vector2 canvasSize = (_canvas.transform as RectTransform).sizeDelta;
        Vector2 tooltipSize = _rect.sizeDelta;

        float clampedX = Mathf.Clamp(_rect.localPosition.x, -canvasSize.x / 2f, canvasSize.x / 2f - tooltipSize.x);
        float clampedY = Mathf.Clamp(_rect.localPosition.y, -canvasSize.y / 2f + tooltipSize.y, canvasSize.y / 2f);

        _rect.localPosition = new Vector3(clampedX, clampedY, 0f);
    }
}
