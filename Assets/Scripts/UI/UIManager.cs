using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// UIManager keeps the screen in sync with game state.
// It does NOT make game decisions — it only reads from ShopManager/GameManager
// and updates what the player sees.

public class UIManager : MonoBehaviour
{
    // --- Singleton ---
    public static UIManager Instance { get; private set; }

    [Header("Shop Row (up to 5 slots)")]
    public PokemonSlotUI[] shopSlots;

    [Header("Battle Row (up to 6 slots)")]
    public PokemonSlotUI[] battleSlots;

    [Header("Bench Row (1 slot)")]
    public PokemonSlotUI[] benchSlots;

    [Header("Info Display")]
    public TextMeshProUGUI pokedollarText;
    public TextMeshProUGUI roundText;
    public TextMeshProUGUI playerHPText;

    [Header("Action Buttons")]
    public Button releaseButton;
    public Button rerollButton;
    public Button startBattleButton;

    [Header("Release Drop Zone")]
    [Tooltip("The UI object the player can drag Pokemon onto to release them.")]
    public ReleaseDropZone releaseDropZone;

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        SetupSlots(shopSlots,   ShopManager.SelectionSource.Shop);
        SetupSlots(battleSlots, ShopManager.SelectionSource.Battle);
        SetupSlots(benchSlots,  ShopManager.SelectionSource.Bench);

        releaseButton.onClick.AddListener(OnReleaseClicked);
        rerollButton.onClick.AddListener(OnRerollClicked);
        startBattleButton.onClick.AddListener(OnStartBattleClicked);

        RefreshAll();
    }

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
    // -------------------------------------------------------

    public void RefreshAll()
    {
        RefreshShop();
        RefreshBench();
        RefreshBattle();
        RefreshInfoDisplay();
        RefreshActionButtons();

        // Re-apply target highlights based on current selection
        var sel = ShopManager.Instance.CurrentSelection;
        if (sel != ShopManager.SelectionSource.None)
            HighlightValidTargets(sel);
        else
            ClearTargetHighlights();
    }

    private void RefreshShop()
    {
        int active = ShopManager.Instance.ShopSize;
        for (int i = 0; i < shopSlots.Length; i++)
        {
            bool on = i < active;
            shopSlots[i].gameObject.SetActive(on);
            if (!on) continue;

            bool isSelected = ShopManager.Instance.CurrentSelection == ShopManager.SelectionSource.Shop
                              && ShopManager.Instance.SelectedIndex == i;

            if (ShopManager.Instance.ShopRow[i] != null)
            {
                shopSlots[i].DisplayPokemon(ShopManager.Instance.ShopRow[i]);
                shopSlots[i].SetHighlight(isSelected);
            }
            else
            {
                shopSlots[i].DisplayEmpty();
            }
        }
    }

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

    private void RefreshBattle()
    {
        int active = ShopManager.Instance.BattleSize;
        for (int i = 0; i < battleSlots.Length; i++)
        {
            bool on = i < active;
            battleSlots[i].gameObject.SetActive(on);
            if (!on) continue;

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

    private void RefreshInfoDisplay()
    {
        pokedollarText.text = $"P${ShopManager.Instance.CurrentPokedollars}";
        roundText.text      = $"Wins {GameManager.Instance.PlayerWins}/{GameManager.Instance.winsToVictory}";
        playerHPText.text   = $"HP: {GameManager.Instance.PlayerHP}/{GameManager.Instance.playerMaxHP}";
    }

    private void RefreshActionButtons()
    {
        var selection = ShopManager.Instance.CurrentSelection;
        bool hasSelection = selection != ShopManager.SelectionSource.None;

        // Release is available when bench or battle pokemon is selected
        bool canRelease = selection == ShopManager.SelectionSource.Bench
                       || selection == ShopManager.SelectionSource.Battle;
        releaseButton.gameObject.SetActive(canRelease);

        startBattleButton.interactable = ShopManager.Instance.CanStartBattle();
    }

    // -------------------------------------------------------
    // HIGHLIGHT VALID TARGETS
    // Called when a selection is made (click or drag start).
    // Lights up slots where the selected pokemon can be placed.
    // -------------------------------------------------------

    public void HighlightValidTargets(ShopManager.SelectionSource fromSource)
    {
        ClearTargetHighlights();

        switch (fromSource)
        {
            case ShopManager.SelectionSource.Shop:
                // Can go to any battle slot or bench slot
                foreach (var slot in battleSlots)
                    if (slot.gameObject.activeSelf) slot.SetValidTarget(true);
                foreach (var slot in benchSlots)
                    slot.SetValidTarget(true);
                break;

            case ShopManager.SelectionSource.Bench:
                // Can go to any battle slot
                foreach (var slot in battleSlots)
                    if (slot.gameObject.activeSelf) slot.SetValidTarget(true);
                break;

            case ShopManager.SelectionSource.Battle:
                // Can go to bench or any other battle slot
                foreach (var slot in benchSlots)
                    slot.SetValidTarget(true);
                int selectedIdx = ShopManager.Instance.SelectedIndex;
                for (int i = 0; i < battleSlots.Length; i++)
                {
                    if (!battleSlots[i].gameObject.activeSelf) continue;
                    if (i == selectedIdx) continue; // don't highlight source
                    battleSlots[i].SetValidTarget(true);
                }
                break;
        }
    }

    public void ClearTargetHighlights()
    {
        foreach (var slot in shopSlots)   slot.SetValidTarget(false);
        foreach (var slot in battleSlots) slot.SetValidTarget(false);
        foreach (var slot in benchSlots)  slot.SetValidTarget(false);
    }

    // -------------------------------------------------------
    // BUTTON CALLBACKS
    // -------------------------------------------------------

    private void OnReleaseClicked()
    {
        ShopManager.Instance.ReleaseSelected();
        RefreshAll();
    }

    private void OnRerollClicked()   { ShopManager.Instance.Reroll();       RefreshAll(); }
    private void OnStartBattleClicked() { GameManager.Instance.StartBattle(); }
}
