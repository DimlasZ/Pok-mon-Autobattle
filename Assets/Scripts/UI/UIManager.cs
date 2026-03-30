using UnityEngine;
using UnityEngine.UI;
using TMPro;

// UIManager is responsible for keeping the screen in sync with the game state.
// It does NOT make game decisions — it only reads from ShopManager/GameManager
// and updates what the player sees.

public class UIManager : MonoBehaviour
{
    // --- Singleton ---
    public static UIManager Instance { get; private set; }

    [Header("Shop Row (3 slots)")]
    public PokemonSlotUI[] shopSlots;

    [Header("Battle Row (3 slots)")]
    public PokemonSlotUI[] battleSlots;

    [Header("Bench Row (6 slots)")]
    public PokemonSlotUI[] benchSlots;

    [Header("Info Display")]
    public TextMeshProUGUI pokedollarText;  // Shows current Pokédollars
    public TextMeshProUGUI roundText;       // Shows current round
    public TextMeshProUGUI playerHPText;    // Shows player HP

    [Header("Action Buttons")]
    public Button buyButton;          // Visible when a shop slot is selected
    public Button sellButton;         // Visible when a bench slot is selected
    public Button moveToBattleButton; // Visible when a bench slot is selected
    public Button moveToBenchButton;  // Visible when a battle slot is selected
    public Button rerollButton;       // Always visible during buy phase
    public Button startBattleButton;  // Always visible during buy phase

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Wire up slot indices and sources so each slot knows who it is
        SetupSlots(shopSlots,   ShopManager.SelectionSource.Shop);
        SetupSlots(battleSlots, ShopManager.SelectionSource.Battle);
        SetupSlots(benchSlots,  ShopManager.SelectionSource.Bench);

        // Wire up action buttons to ShopManager methods
        buyButton.onClick.AddListener(OnBuyClicked);
        sellButton.onClick.AddListener(OnSellClicked);
        moveToBattleButton.onClick.AddListener(OnMoveToBattleClicked);
        moveToBenchButton.onClick.AddListener(OnMoveToBenchClicked);
        rerollButton.onClick.AddListener(OnRerollClicked);
        startBattleButton.onClick.AddListener(OnStartBattleClicked);

        RefreshAll();
    }

    // Assigns source and index to each slot in an array
    private void SetupSlots(PokemonSlotUI[] slots, ShopManager.SelectionSource source)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].source    = source;
            slots[i].slotIndex = i;
        }
    }

    // -------------------------------------------------------
    // REFRESH
    // Call this any time the game state changes to update the whole UI
    // -------------------------------------------------------

    public void RefreshAll()
    {
        RefreshShop();
        RefreshBench();
        RefreshBattle();
        RefreshInfoDisplay();
        RefreshActionButtons();
    }

    // Updates the 3 shop slots
    private void RefreshShop()
    {
        for (int i = 0; i < shopSlots.Length; i++)
        {
            bool isSelected = ShopManager.Instance.CurrentSelection == ShopManager.SelectionSource.Shop
                              && ShopManager.Instance.SelectedIndex == i;

            if (ShopManager.Instance.ShopRow[i] != null)
            {
                shopSlots[i].DisplayShopPokemon(ShopManager.Instance.ShopRow[i]);
                shopSlots[i].SetHighlight(isSelected);
            }
            else
            {
                shopSlots[i].DisplayEmpty();
            }
        }
    }

    // Updates the 6 bench slots
    private void RefreshBench()
    {
        for (int i = 0; i < benchSlots.Length; i++)
        {
            bool isSelected = ShopManager.Instance.CurrentSelection == ShopManager.SelectionSource.Bench
                              && ShopManager.Instance.SelectedIndex == i;

            if (ShopManager.Instance.BenchRow[i] != null)
            {
                benchSlots[i].DisplayPokemon(ShopManager.Instance.BenchRow[i]);
                benchSlots[i].SetHighlight(isSelected);
            }
            else
            {
                benchSlots[i].DisplayEmpty();
            }
        }
    }

    // Updates the 3 battle slots
    private void RefreshBattle()
    {
        for (int i = 0; i < battleSlots.Length; i++)
        {
            bool isSelected = ShopManager.Instance.CurrentSelection == ShopManager.SelectionSource.Battle
                              && ShopManager.Instance.SelectedIndex == i;

            if (ShopManager.Instance.BattleRow[i] != null)
            {
                battleSlots[i].DisplayPokemon(ShopManager.Instance.BattleRow[i]);
                battleSlots[i].SetHighlight(isSelected);
            }
            else
            {
                battleSlots[i].DisplayEmpty();
            }
        }
    }

    // Updates the Pokédollar, round, and HP text
    private void RefreshInfoDisplay()
    {
        pokedollarText.text = $"P${ShopManager.Instance.CurrentPokedollars}";
        roundText.text      = $"Wins {GameManager.Instance.PlayerWins}/{GameManager.Instance.winsToVictory}";
        playerHPText.text   = $"HP: {GameManager.Instance.PlayerHP}/{GameManager.Instance.playerMaxHP}";
    }

    // Shows/hides action buttons based on what is currently selected
    private void RefreshActionButtons()
    {
        var selection = ShopManager.Instance.CurrentSelection;

        // Buy: only when a shop slot is selected
        buyButton.gameObject.SetActive(selection == ShopManager.SelectionSource.Shop);

        // Sell + Move to Battle: only when a bench slot is selected
        sellButton.gameObject.SetActive(selection == ShopManager.SelectionSource.Bench);
        moveToBattleButton.gameObject.SetActive(selection == ShopManager.SelectionSource.Bench);

        // Disable Move to Battle if battle row is full
        if (selection == ShopManager.SelectionSource.Bench)
        {
            bool battleFull = IsBattleRowFull();
            moveToBattleButton.interactable = !battleFull;
        }

        // Move to Bench: only when a battle slot is selected
        moveToBenchButton.gameObject.SetActive(selection == ShopManager.SelectionSource.Battle);

        // Start Battle: always visible, disabled if no Pokemon in battle row
        startBattleButton.interactable = ShopManager.Instance.CanStartBattle();
    }

    // Returns true if all battle row slots are filled
    private bool IsBattleRowFull()
    {
        foreach (var p in ShopManager.Instance.BattleRow)
            if (p == null) return false;
        return true;
    }

    // -------------------------------------------------------
    // BUTTON CALLBACKS
    // -------------------------------------------------------

    private void OnBuyClicked()
    {
        ShopManager.Instance.BuySelected();
        RefreshAll();
    }

    private void OnSellClicked()
    {
        ShopManager.Instance.SellSelected();
        RefreshAll();
    }

    private void OnMoveToBattleClicked()
    {
        ShopManager.Instance.MoveToBattle();
        RefreshAll();
    }

    private void OnMoveToBenchClicked()
    {
        ShopManager.Instance.MoveToBench();
        RefreshAll();
    }

    private void OnRerollClicked()
    {
        ShopManager.Instance.Reroll();
        RefreshAll();
    }

    private void OnStartBattleClicked()
    {
        // Capture the player's battle team and load the battle scene
        GameManager.Instance.StartBattle();
    }
}
