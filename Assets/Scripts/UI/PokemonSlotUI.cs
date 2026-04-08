using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// PokemonSlotUI is attached to every slot button in the game (shop, bench, battle).
// It handles displaying the Pokemon inside it and notifying ShopManager when clicked.

public class PokemonSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                                              IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI References")]
    public Image pokemonSprite;             // The sprite image inside the slot
    public Image hpBarBox;                  // The HP bar frame sprite (shown in battle, hidden in shop)
    public Image healthBarFill;             // The filled portion of the HP bar (child of a bar background)
    public TextMeshProUGUI hpText;          // HP display — bottom right in code (left side visually when flipped)
    public TextMeshProUGUI attackText;      // Attack display — bottom left in code (right side visually when flipped)
    public TextMeshProUGUI speedText;       // Speed display — bottom center
    public Image highlight;                 // Colored border shown when this slot is selected

    [Header("Display")]
    public bool flipSlot; // True for player Pokemon — flips the entire slot so sprite and all stats mirror correctly

    // Which row this slot belongs to and its index within that row
    public ShopManager.SelectionSource source;
    public int slotIndex;

    // Colors for the highlight border
    private readonly Color selectedColor   = new Color(1f, 0.9f, 0f, 1f);  // Yellow — this slot is selected
    private readonly Color targetColor     = new Color(0f, 0.8f, 1f, 1f);  // Cyan  — valid drop target
    private readonly Color unselectedColor = new Color(1f, 1f, 1f, 0f);    // Transparent

    private bool _isValidTarget = false;

    // -------------------------------------------------------

    void Awake()
    {
    }

    // Updates the health bar width and color based on current/max HP
    private void RefreshHealthBar(int currentHP, int maxHP)
    {
        if (healthBarFill == null) return;

        float ratio = maxHP > 0 ? Mathf.Clamp01((float)currentHP / maxHP) : 0f;

        // Scale the fill by moving its right anchor — works without a Source Image sprite
        var rect = healthBarFill.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(ratio, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Green > 50%, Yellow 25-50%, Red < 25%
        if      (ratio > 0.5f)  healthBarFill.color = new Color(0.18f, 0.78f, 0.18f);
        else if (ratio > 0.25f) healthBarFill.color = new Color(1f,    0.76f, 0.05f);
        else                    healthBarFill.color = new Color(0.85f, 0.15f, 0.15f);
    }

    // Called by UIManager to display an owned Pokemon (PokemonInstance)
    public void DisplayPokemon(PokemonInstance instance)
    {
        gameObject.SetActive(true);

        pokemonSprite.sprite = instance.baseData.sprite;
        pokemonSprite.gameObject.SetActive(instance.baseData.sprite != null);

        hpText.text     = $"{instance.currentHP}/{instance.maxHP}";
        attackText.text = instance.attack.ToString();
        speedText.text  = instance.speed.ToString();

        _currentAbility = instance.baseData.ability;

        if (hpBarBox != null) hpBarBox.gameObject.SetActive(true);
        RefreshHealthBar(instance.currentHP, instance.maxHP);

        SetHighlight(false);
    }

    // Called when this slot is empty — shows a blank slot
    public void DisplayEmpty()
    {
        pokemonSprite.sprite = null;
        pokemonSprite.gameObject.SetActive(false);
        hpText.text     = "";
        attackText.text = "";
        speedText.text  = "";
        _currentAbility = null;
        if (hpBarBox != null) hpBarBox.gameObject.SetActive(false);
        SetHighlight(false);
    }

    // Turns the selected highlight on or off
    public void SetHighlight(bool on)
    {
        if (highlight == null) return;
        if (!on && _isValidTarget) return; // keep target color
        highlight.color = on ? selectedColor : unselectedColor;
    }

    // Marks this slot as a valid drop target (cyan border)
    public void SetValidTarget(bool on)
    {
        _isValidTarget = on;
        if (highlight != null)
            highlight.color = on ? targetColor : unselectedColor;
    }

    // The ability of the Pokemon currently displayed in this slot (null if empty)
    private AbilityData _currentAbility;

    // Show tooltip on hover — anchored to the top of the slot, not the cursor
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_currentAbility == null || TooltipUI.Instance == null) return;

        // Use the top-center of this slot as the anchor point
        var rect = GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        // corners: 0=bottom-left, 1=top-left, 2=top-right, 3=bottom-right
        Vector2 topCenter = (corners[1] + corners[2]) / 2f;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, topCenter);

        TooltipUI.Instance.Show(_currentAbility, screenPos);
    }

    // Hide tooltip when cursor leaves
    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipUI.Instance != null)
            TooltipUI.Instance.Hide();
    }

    // -------------------------------------------------------
    // DRAG AND DROP
    // Unity separates clicks from drags via the drag threshold (~10px movement).
    // Clicking still fires OnClicked() normally.
    // -------------------------------------------------------

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (DragDropManager.Instance == null) return;
        if (pokemonSprite.sprite == null) return; // Empty slot — nothing to drag
        DragDropManager.Instance.BeginDrag(this, pokemonSprite.sprite);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (DragDropManager.Instance == null) return;
        DragDropManager.Instance.UpdatePosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Fires on the SOURCE after every drag (even after a successful drop).
        // DragDropManager.Drop() already cleared state on a successful drop, so this is a safe no-op then.
        if (DragDropManager.Instance == null) return;
        DragDropManager.Instance.CancelDrag();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (DragDropManager.Instance == null) return;
        DragDropManager.Instance.Drop(this);
    }

    // Called when the player clicks this slot
    public void OnClicked()
    {
        var sm = ShopManager.Instance;

        // If something is already selected and this is a valid target → place it here
        if (_isValidTarget && sm.CurrentSelection != ShopManager.SelectionSource.None)
        {
            sm.PlaceSelected(source, slotIndex);
            UIManager.Instance.RefreshAll();
            return;
        }

        // Otherwise select this slot (only if it has a Pokemon)
        switch (source)
        {
            case ShopManager.SelectionSource.Shop:
                sm.SelectShopPokemon(slotIndex);
                break;
            case ShopManager.SelectionSource.Bench:
                sm.SelectBenchPokemon(slotIndex);
                break;
            case ShopManager.SelectionSource.Battle:
                sm.SelectBattlePokemon(slotIndex);
                break;
        }

        UIManager.Instance.RefreshAll();
    }
}
