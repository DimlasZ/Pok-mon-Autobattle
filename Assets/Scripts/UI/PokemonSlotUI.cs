using UnityEngine;
using UnityEngine.UI;
using TMPro;

// PokemonSlotUI is attached to every slot button in the game (shop, bench, battle).
// It handles displaying the Pokemon inside it and notifying ShopManager when clicked.

public class PokemonSlotUI : MonoBehaviour
{
    [Header("UI References")]
    public Image pokemonSprite;           // The sprite image inside the slot
    public TextMeshProUGUI hpText;        // HP display (bottom left) — shown as a single number
    public TextMeshProUGUI attackText;    // Attack display (bottom right)
    public Image highlight;               // Colored border shown when this slot is selected

    [Header("Display")]
    public bool flipSprite; // True for player Pokemon (mirrors sprite to face right)

    // Which row this slot belongs to and its index within that row
    public ShopManager.SelectionSource source;
    public int slotIndex;

    // Colors for the highlight border
    private readonly Color selectedColor   = new Color(1f, 0.9f, 0f, 1f);  // Yellow
    private readonly Color unselectedColor = new Color(1f, 1f, 1f, 0f);    // Transparent

    // -------------------------------------------------------

    // Called by ShopManager/UIManager to display a shop Pokemon (PokemonData)
    public void DisplayShopPokemon(PokemonData data)
    {
        gameObject.SetActive(true);

        pokemonSprite.sprite = data.sprite;
        pokemonSprite.gameObject.SetActive(data.sprite != null);
        pokemonSprite.transform.localScale = new Vector3(flipSprite ? -1f : 1f, 1f, 1f);

        hpText.text     = data.hp.ToString();
        attackText.text = data.attack.ToString();

        SetHighlight(false);
    }

    // Called by UIManager to display an owned Pokemon (PokemonInstance)
    public void DisplayPokemon(PokemonInstance instance)
    {
        gameObject.SetActive(true);

        pokemonSprite.sprite = instance.baseData.sprite;
        pokemonSprite.gameObject.SetActive(instance.baseData.sprite != null);
        pokemonSprite.transform.localScale = new Vector3(flipSprite ? -1f : 1f, 1f, 1f);

        hpText.text     = instance.currentHP.ToString();
        attackText.text = instance.attack.ToString();

        SetHighlight(false);
    }

    // Called when this slot is empty — shows a blank slot
    public void DisplayEmpty()
    {
        pokemonSprite.sprite = null;
        pokemonSprite.gameObject.SetActive(false);
        pokemonSprite.transform.localScale = Vector3.one;
        hpText.text     = "";
        attackText.text = "";
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
