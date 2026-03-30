using UnityEngine;
using UnityEngine.UI;
using TMPro;

// PokemonSlotUI is attached to every slot button in the game (shop, bench, battle).
// It handles displaying the Pokemon inside it and notifying ShopManager when clicked.

public class PokemonSlotUI : MonoBehaviour
{
    [Header("UI References")]
    public Image pokemonSprite;             // The sprite image inside the slot
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
    private readonly Color selectedColor   = new Color(1f, 0.9f, 0f, 1f);  // Yellow
    private readonly Color unselectedColor = new Color(1f, 1f, 1f, 0f);    // Transparent

    // -------------------------------------------------------

    void Awake()
    {
        if (!flipSlot) return;

        // Flip the entire slot so the sprite and stat positions mirror automatically
        transform.localScale = new Vector3(-1f, 1f, 1f);

        // Counter-flip each text child so the text itself stays readable
        if (hpText != null)     hpText.transform.localScale     = new Vector3(-1f, 1f, 1f);
        if (attackText != null) attackText.transform.localScale = new Vector3(-1f, 1f, 1f);
        if (speedText != null)  speedText.transform.localScale  = new Vector3(-1f, 1f, 1f);
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

    // Called by ShopManager/UIManager to display a shop Pokemon (PokemonData)
    public void DisplayShopPokemon(PokemonData data)
    {
        gameObject.SetActive(true);

        pokemonSprite.sprite = data.sprite;
        pokemonSprite.gameObject.SetActive(data.sprite != null);

        hpText.text     = data.hp.ToString();
        attackText.text = data.attack.ToString();
        speedText.text  = data.speed.ToString();

        if (healthBarFill != null) healthBarFill.transform.parent.gameObject.SetActive(false);

        SetHighlight(false);
    }

    // Called by UIManager to display an owned Pokemon (PokemonInstance)
    public void DisplayPokemon(PokemonInstance instance)
    {
        gameObject.SetActive(true);

        pokemonSprite.sprite = instance.baseData.sprite;
        pokemonSprite.gameObject.SetActive(instance.baseData.sprite != null);

        hpText.text     = instance.currentHP.ToString();
        attackText.text = instance.attack.ToString();
        speedText.text  = instance.speed.ToString();

        if (healthBarFill != null) healthBarFill.transform.parent.gameObject.SetActive(true);
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
        if (healthBarFill != null) healthBarFill.transform.parent.gameObject.SetActive(false);
        SetHighlight(false);
    }

    // Turns the highlight border on or off
    public void SetHighlight(bool on)
    {
        if (highlight != null)
            highlight.color = on ? selectedColor : unselectedColor;
    }

    // Called when the player clicks this slot
    public void OnClicked()
    {
        switch (source)
        {
            case ShopManager.SelectionSource.Shop:
                ShopManager.Instance.SelectShopPokemon(slotIndex);
                break;
            case ShopManager.SelectionSource.Bench:
                ShopManager.Instance.SelectBenchPokemon(slotIndex);
                break;
            case ShopManager.SelectionSource.Battle:
                ShopManager.Instance.SelectBattlePokemon(slotIndex);
                break;
        }

        // Tell UIManager to update highlights and buttons
        UIManager.Instance.RefreshAll();
    }
}
