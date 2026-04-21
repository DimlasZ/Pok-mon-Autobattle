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

    [Header("Progress Icons (TopBar)")]
    public Image[] badgeImages;   // 8 gym badges — grey until won
    public Image[] starImages;    // 4 Elite 4 stars — grey until won
    public Image   champImage;    // Champion — grey until won
    public Image[] heartImages;   // Player HP — white by default, grey when lost

    [Header("Action Buttons")]
    public Button releaseButton;
    public Button baitButton;
    public Button rerollButton;
    public Button startBattleButton;

    [Header("Confirm Panels")]
    public ConfirmBattlePanel confirmBattlePanel;

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
        baitButton.onClick.AddListener(OnBaitClicked);
        rerollButton.onClick.AddListener(OnRerollClicked);
        startBattleButton.onClick.AddListener(OnStartBattleClicked);

        // Apply pending save load (Continue button from main menu).
        // Must happen before RefreshAll so the UI shows the restored team.
        var pendingSave = GameManager.Instance?.PendingSaveLoad;
        if (pendingSave != null)
        {
            ShopManager.Instance?.LoadFromSave(pendingSave);
            GameManager.Instance.ClearPendingSaveLoad();
        }

        AudioManager.Instance?.PlayShopMusic();
        RefreshAll();

        // Show tier upgrade overlay now that the scene is fully loaded.
        var sm = ShopManager.Instance;
        if (sm != null && sm.PendingTierUpgrade > 0)
        {
            int tier       = sm.PendingTierUpgrade;
            var newPokemon = System.Array.FindAll(sm.AllPokemon, p => p != null && p.tier == tier);
            bool heartRestored = GameManager.Instance != null && GameManager.Instance.PendingHeartRestored;
            GlobalOverlayManager.Instance?.tierUpgradeOverlay?.Show(tier, newPokemon, heartRestored);
            sm.ClearPendingTierUpgrade();
        }
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
                var pokemon = ShopManager.Instance.ShopRow[i];
                shopSlots[i].DisplayPokemon(pokemon);
                bool locked = pokemon.baseData.preEvolutionId > 0
                              && !ShopManager.Instance.PlayerOwnsPreEvolution(pokemon.baseData.preEvolutionId);
                bool evoAvailable = pokemon.baseData.preEvolutionId > 0 && !locked;
                bool duplicate = pokemon.baseData.preEvolutionId == 0
                              && ShopManager.Instance.IsAlreadyOnTeam(pokemon.baseData.id);
                shopSlots[i].SetLocked(locked);
                shopSlots[i].SetEvolutionAvailable(evoAvailable);
                shopSlots[i].SetDuplicate(duplicate);
                shopSlots[i].SetHighlight(isSelected);
                shopSlots[i].SetBaited(ShopManager.Instance.BaitedSlots[i]);
            }
            else
            {
                shopSlots[i].DisplayEmpty();
                shopSlots[i].SetBaited(false);
            }
        }
    }

    private void RefreshBench()
    {
        int activeSize = ShopManager.Instance.BenchSize;
        for (int i = 0; i < benchSlots.Length; i++)
        {
            bool active = i < activeSize;
            benchSlots[i].gameObject.SetActive(active);
            if (!active) continue;

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

    private static readonly Color IconLocked   = new Color(0.2f, 0.2f, 0.2f, 0.7f);
    private static readonly Color IconUnlocked = Color.white;

    private void RefreshInfoDisplay()
    {
        pokedollarText.text = $"{ShopManager.Instance.CurrentPokedollars}";
        roundText.text      = $"Wins {GameManager.Instance.PlayerWins}/{GameManager.Instance.winsToVictory}";
        playerHPText.text   = $"HP: {GameManager.Instance.PlayerHP}/{GameManager.Instance.playerMaxHP}";
        RefreshProgressIcons();
    }

    private void RefreshProgressIcons()
    {
        int wins = GameManager.Instance.PlayerWins;
        int hp   = GameManager.Instance.PlayerHP;

        for (int i = 0; i < badgeImages.Length; i++)
            if (badgeImages[i] != null)
                badgeImages[i].color = wins > i ? IconUnlocked : IconLocked;

        for (int i = 0; i < starImages.Length; i++)
            if (starImages[i] != null)
                starImages[i].color = wins > 8 + i ? IconUnlocked : IconLocked;

        if (champImage != null)
            champImage.color = wins >= GameManager.Instance.winsToVictory ? IconUnlocked : IconLocked;

        for (int i = 0; i < heartImages.Length; i++)
            if (heartImages[i] != null)
                heartImages[i].color = i < hp ? IconUnlocked : IconLocked;
    }

    public void RefreshActionButtons()
    {
        var selection = ShopManager.Instance.CurrentSelection;
        bool hasSelection = selection != ShopManager.SelectionSource.None;

        // Release is available when bench or battle pokemon is selected
        bool canRelease = selection == ShopManager.SelectionSource.Bench
                       || selection == ShopManager.SelectionSource.Battle;
        releaseButton.gameObject.SetActive(canRelease);

        // Bait is available when a shop slot with a Pokemon is selected
        bool canBait = selection == ShopManager.SelectionSource.Shop
                    && ShopManager.Instance.SelectedIndex >= 0
                    && ShopManager.Instance.ShopRow[ShopManager.Instance.SelectedIndex] != null;
        baitButton.gameObject.SetActive(canBait);
        if (canBait)
        {
            bool isBaited = ShopManager.Instance.BaitedSlots[ShopManager.Instance.SelectedIndex];
            var label = baitButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (label != null) label.text = isBaited ? "Unbait" : "Bait";
        }

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
                var selectedPokemon = ShopManager.Instance.GetSelectedShopPokemon();
                if (selectedPokemon != null && selectedPokemon.baseData.preEvolutionId > 0)
                {
                    // Evolution buy — only highlight slots containing the pre-evolution
                    int preEvoId = selectedPokemon.baseData.preEvolutionId;
                    foreach (var slot in battleSlots)
                    {
                        if (!slot.gameObject.activeSelf) continue;
                        var p = ShopManager.Instance.BattleRow[slot.slotIndex];
                        if (p != null && p.baseData.id == preEvoId) slot.SetValidTarget(true);
                    }
                    foreach (var slot in benchSlots)
                    {
                        var p = ShopManager.Instance.BenchRow[slot.slotIndex];
                        if (p != null && p.baseData.id == preEvoId) slot.SetValidTarget(true);
                    }
                }
                else
                {
                    // Normal buy — highlight all bench and battle slots
                    foreach (var slot in battleSlots)
                        if (slot.gameObject.activeSelf) slot.SetValidTarget(true);
                    foreach (var slot in benchSlots)
                        slot.SetValidTarget(true);
                }
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
        AudioManager.Instance?.PlayButtonSound();
        ShopManager.Instance.ReleaseSelected();
        RefreshAll();
    }

    private void OnBaitClicked()
    {
        AudioManager.Instance?.PlayButtonSound();
        ShopManager.Instance.ToggleBait(ShopManager.Instance.SelectedIndex);
        RefreshAll();
    }

    private void OnRerollClicked()
    {
        AudioManager.Instance?.PlayButtonSound();
        ShopManager.Instance.Reroll();
        StartCoroutine(RefreshShopDelayed());
    }

    public void RefreshShopWithDelay()
    {
        StartCoroutine(RefreshShopDelayed());
    }

    private System.Collections.IEnumerator RefreshShopDelayed()
    {
        yield return new WaitForSeconds(0.2f);
        RefreshAll();
    }

    private void OnStartBattleClicked()
    {
        AudioManager.Instance?.PlayButtonSound();
        int money = ShopManager.Instance.CurrentPokedollars;
        if (money > 0 && confirmBattlePanel != null)
            confirmBattlePanel.Show(money);
        else
            GameManager.Instance.StartBattle();
    }
}
