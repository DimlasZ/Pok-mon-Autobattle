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
    private readonly Color selectedColor   = new Color(0.15f, 0.45f, 1f,  0.4f); // Blue overlay — this slot is selected
    private readonly Color targetColor     = new Color(0.2f,  0.2f,  0.2f, 1f); // Same grey as background — shows outline only
    private readonly Color unselectedColor = new Color(0f,    0f,    0f,   0f); // Fully transparent

    private bool  _isValidTarget    = false;
    private bool  _isLocked         = false;
    private bool  _isEvoAvailable   = false;
    private bool  _isDuplicate      = false;
    private bool  _isBaited         = false;
    private Color _defaultAttackColor;
    private Color _defaultSpeedColor;

    // -------------------------------------------------------

    void Awake()
    {
        _defaultAttackColor = attackText != null ? attackText.color : Color.white;
        _defaultSpeedColor  = speedText  != null ? speedText.color  : Color.white;
    }

    private Color StatColor(int current, int baseVal, Color defaultColor)
    {
        if (current > baseVal) return new Color(0.2f, 0.9f, 0.2f); // green — buffed
        if (current < baseVal) return new Color(1f,   0.2f, 0.2f); // red   — debuffed
        return defaultColor;
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

        attackText.color = StatColor(instance.attack, instance.baseAttack, _defaultAttackColor);
        speedText.color  = StatColor(instance.speed,  instance.baseSpeed,  _defaultSpeedColor);

        _currentPokemonData = instance.baseData;
        _currentAbility     = instance.baseData.ability;
        _currentPokemonName = instance.baseData.pokemonName;
        _currentType        = instance.baseData.type1;

        if (hpBarBox != null) hpBarBox.gameObject.SetActive(true);
        RefreshHealthBar(instance.currentHP, instance.maxHP);

        SetHighlight(false);
    }

    // Greys out the slot when the player already owns this Pokémon
    public void SetDuplicate(bool duplicate)
    {
        _isDuplicate = duplicate;
        if (duplicate)
            pokemonSprite.color = new Color(0.35f, 0.35f, 0.35f, 0.6f);
        else if (!_isLocked)
            pokemonSprite.color = Color.white;
        RefreshBackground();
    }

    // Greys out the slot when the pre-evolution requirement is not met
    public void SetLocked(bool locked)
    {
        _isLocked = locked;
        pokemonSprite.color = locked ? new Color(0.35f, 0.35f, 0.35f, 0.6f) : Color.white;
        if (locked)
        {
            hpText.text     = "?";
            attackText.text = "?";
            speedText.text  = "?";
        }
        RefreshBackground();
    }

    // Tints the slot background green when the player owns the pre-evolution and can buy this evolution
    public void SetEvolutionAvailable(bool available)
    {
        _isEvoAvailable = available;
        RefreshBackground();
    }

    // Tints the slot background gold when baited (frozen — survives reroll)
    public void SetBaited(bool baited)
    {
        _isBaited = baited;
        RefreshBackground();
    }

    private static readonly Color DefaultSlotColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    private void RefreshBackground()
    {
        var bg = GetComponent<Image>();
        if (bg == null) return;
        if (_isBaited)
            bg.color = new Color(1f, 0.85f, 0.2f, 1f); // gold
        else if (_isEvoAvailable && !_isLocked)
            bg.color = new Color(0.6f, 1f, 0.6f, 1f);  // light green
        else
            bg.color = DefaultSlotColor;
    }

    // Called when this slot is empty — shows a blank slot
    public void DisplayEmpty()
    {
        pokemonSprite.sprite = null;
        pokemonSprite.gameObject.SetActive(false);
        hpText.text         = "";
        attackText.text     = "";
        speedText.text      = "";
        attackText.color    = _defaultAttackColor;
        speedText.color     = _defaultSpeedColor;
        _isEvoAvailable     = false;
        _isDuplicate        = false;
        _isBaited           = false;
        RefreshBackground();
        _currentPokemonData = null;
        _currentAbility     = null;
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

    // Data of the Pokemon currently displayed in this slot
    private PokemonData _currentPokemonData;
    private AbilityData _currentAbility;
    private string      _currentPokemonName = "";
    private string      _currentType        = "";

    // Show tooltip on hover — anchored to the top of the slot, not the cursor
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipUI.Instance == null || _currentPokemonData == null) return;

        var rect = GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 topCenter = (corners[1] + corners[2]) / 2f;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, topCenter);

        if (_isDuplicate)
            TooltipUI.Instance.ShowAlreadyOwned(_currentPokemonData, screenPos);
        else
            TooltipUI.Instance.Show(_currentPokemonData, screenPos);
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
        if (_isLocked) return; // Can't drag a locked (evolution-gated) shop slot
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
        AudioManager.Instance?.PlayButtonSound();
        var sm = ShopManager.Instance;

        // If something is already selected and this is a valid target → place it here
        if (_isValidTarget && sm.CurrentSelection != ShopManager.SelectionSource.None)
        {
            sm.PlaceSelected(source, slotIndex);
            UIManager.Instance.RefreshAll();
            return;
        }

        // Otherwise select this slot (only if it has a Pokemon).
        // Clicking the already-selected slot deselects it.
        switch (source)
        {
            case ShopManager.SelectionSource.Shop:
                if (sm.CurrentSelection == ShopManager.SelectionSource.Shop && sm.SelectedIndex == slotIndex)
                    sm.ClearSelection();
                else
                    sm.SelectShopPokemon(slotIndex);
                break;
            case ShopManager.SelectionSource.Bench:
                if (sm.CurrentSelection == ShopManager.SelectionSource.Bench && sm.SelectedIndex == slotIndex)
                    sm.ClearSelection();
                else
                    sm.SelectBenchPokemon(slotIndex);
                break;
            case ShopManager.SelectionSource.Battle:
                if (sm.CurrentSelection == ShopManager.SelectionSource.Battle && sm.SelectedIndex == slotIndex)
                    sm.ClearSelection();
                else
                    sm.SelectBattlePokemon(slotIndex);
                break;
        }

        UIManager.Instance.RefreshAll();
    }
}
