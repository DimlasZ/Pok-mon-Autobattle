using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// ShopManager handles everything during the Buy Phase:
// - The shop row (Pokemon available to buy)
// - The bench row (stored Pokemon)
// - The battle row (Pokemon that will fight)
// - Pokédollars
// - Selection logic (what happens when you click a Pokemon)

public class ShopManager : MonoBehaviour
{
    // --- Singleton ---
    public static ShopManager Instance { get; private set; }

    // --- Settings ---
    [Header("Settings")]
    [Tooltip("All PokemonData assets in the game. Assign these in the Inspector.")]
    public PokemonData[] allPokemon;

    [Tooltip("How much Pokédollars the player starts with each round")]
    public int startingPokedollars = 5;

    [Tooltip("How much a reroll costs")]
    public int rerollCost = 1;

    [Tooltip("How many Pokemon are shown in the shop")]
    public int shopSize = 3;

    [Tooltip("How many slots in the battle row (will increase later)")]
    public int battleRowSize = 3;

    [Tooltip("How many slots in the bench")]
    public int benchSize = 6;

    // --- Pokédollars ---
    public int CurrentPokedollars { get; private set; }

    // --- Rows ---
    // Shop row: PokemonData (not owned yet, just available to buy)
    public PokemonData[] ShopRow { get; private set; }

    // Bench row: owned Pokemon in reserve (null = empty slot)
    public PokemonInstance[] BenchRow { get; private set; }

    // Battle row: owned Pokemon that will fight (null = empty slot)
    public PokemonInstance[] BattleRow { get; private set; }

    // --- Selection ---
    // Where the currently selected Pokemon is
    public enum SelectionSource { None, Shop, Bench, Battle }
    public SelectionSource CurrentSelection { get; private set; } = SelectionSource.None;
    public int SelectedIndex { get; private set; } = -1; // Which slot is selected

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Persists across scenes so team is kept between rounds

        // Initialize arrays in Awake so GameManager.Start() can call StartRound() safely
        ShopRow   = new PokemonData[shopSize];
        BenchRow  = new PokemonInstance[benchSize];
        BattleRow = new PokemonInstance[battleRowSize];
    }

    private void Start()
    {
        // StartRound() is called by GameManager.EnterBuyPhase() — not here
    }

    // -------------------------------------------------------
    // ROUND START
    // Called by GameManager at the beginning of each Buy Phase
    // -------------------------------------------------------

    public void StartRound()
    {
        CurrentPokedollars = startingPokedollars;
        ClearSelection();
        PopulateShop();

        Debug.Log($"Round started — Pokédollars: {CurrentPokedollars}");

        if (UIManager.Instance != null)
            UIManager.Instance.RefreshAll();
    }

    // -------------------------------------------------------
    // SHOP POPULATION
    // Fills the shop with random Pokemon based on current tier
    // -------------------------------------------------------

    private void PopulateShop()
    {
        int maxTier = GetMaxTierForRound(GameManager.Instance.CurrentRound);

        // Filter to only Pokemon available at this tier level
        List<PokemonData> available = allPokemon
            .Where(p => p.tier > 0 && p.tier <= maxTier)
            .ToList();

        if (available.Count == 0)
        {
            Debug.LogWarning("No Pokemon available for this tier!");
            return;
        }

        // Fill each shop slot with a random pick
        for (int i = 0; i < shopSize; i++)
        {
            ShopRow[i] = available[Random.Range(0, available.Count)];
        }

        Debug.Log($"Shop populated (max tier: {maxTier})");

        // TODO: Tell UIManager to refresh shop display
    }

    // Returns the highest tier available based on the current round
    // Round 1-2 = Tier 1, Round 3-4 = Tier 2, Round 5-6 = Tier 3, Round 7+ = Tier 4
    private int GetMaxTierForRound(int round)
    {
        if (round <= 2) return 1;
        if (round <= 4) return 2;
        if (round <= 6) return 3;
        return 4;
    }

    // -------------------------------------------------------
    // SELECTION
    // Clicking a Pokemon sets it as selected so buttons know what to act on
    // -------------------------------------------------------

    // Call this when the player clicks a Pokemon in the shop
    public void SelectShopPokemon(int index)
    {
        if (index < 0 || index >= shopSize || ShopRow[index] == null) return;

        SelectedIndex     = index;
        CurrentSelection  = SelectionSource.Shop;

        Debug.Log($"Selected shop slot {index}: {ShopRow[index].pokemonName}");

        // TODO: Tell UIManager to show the Buy button
    }

    // Call this when the player clicks a Pokemon on the bench
    public void SelectBenchPokemon(int index)
    {
        if (index < 0 || index >= benchSize || BenchRow[index] == null) return;

        SelectedIndex    = index;
        CurrentSelection = SelectionSource.Bench;

        Debug.Log($"Selected bench slot {index}: {BenchRow[index].baseData.pokemonName}");

        // TODO: Tell UIManager to show Sell and Move to Battle buttons
        // TODO: Disable Move to Battle if battle row is full
    }

    // Call this when the player clicks a Pokemon in the battle row
    public void SelectBattlePokemon(int index)
    {
        if (index < 0 || index >= battleRowSize || BattleRow[index] == null) return;

        SelectedIndex    = index;
        CurrentSelection = SelectionSource.Battle;

        Debug.Log($"Selected battle slot {index}: {BattleRow[index].baseData.pokemonName}");

        // TODO: Tell UIManager to show the Move to Bench button
    }

    // Deselects everything
    public void ClearSelection()
    {
        SelectedIndex    = -1;
        CurrentSelection = SelectionSource.None;

        // TODO: Tell UIManager to hide all action buttons
    }

    // -------------------------------------------------------
    // ACTIONS
    // These are called by UI buttons
    // -------------------------------------------------------

    // Buy the currently selected shop Pokemon — moves it to the first empty bench slot
    public void BuySelected()
    {
        if (CurrentSelection != SelectionSource.Shop) return;

        PokemonData data = ShopRow[SelectedIndex];
        if (data == null) return;

        if (CurrentPokedollars < 1)
        {
            Debug.Log("Not enough Pokédollars!");
            return;
        }

        int emptyBench = GetFirstEmptySlot(BenchRow);
        if (emptyBench == -1)
        {
            Debug.Log("Bench is full!");
            return;
        }

        // Deduct Pokédollars, create instance, place on bench, remove from shop
        CurrentPokedollars--;
        BenchRow[emptyBench] = new PokemonInstance(data);
        ShopRow[SelectedIndex] = null;

        Debug.Log($"Bought {data.pokemonName} — Pokédollars remaining: {CurrentPokedollars}");

        ClearSelection();

        // TODO: Tell UIManager to refresh bench and Pokédollars display
    }

    // Sell the currently selected bench Pokemon — no Pokédollars returned
    public void SellSelected()
    {
        if (CurrentSelection != SelectionSource.Bench) return;
        if (BenchRow[SelectedIndex] == null) return;

        string name = BenchRow[SelectedIndex].baseData.pokemonName;
        BenchRow[SelectedIndex] = null;

        Debug.Log($"Released {name}");

        ClearSelection();

        // TODO: Tell UIManager to refresh bench display
    }

    // Move the selected bench Pokemon to the first empty battle slot
    public void MoveToBattle()
    {
        if (CurrentSelection != SelectionSource.Bench) return;
        if (BenchRow[SelectedIndex] == null) return;

        int emptyBattle = GetFirstEmptySlot(BattleRow);
        if (emptyBattle == -1)
        {
            Debug.Log("Battle row is full!");
            return;
        }

        BattleRow[emptyBattle] = BenchRow[SelectedIndex];
        BenchRow[SelectedIndex] = null;

        Debug.Log($"Moved {BattleRow[emptyBattle].baseData.pokemonName} to battle row");

        ClearSelection();

        // TODO: Tell UIManager to refresh bench and battle row display
    }

    // Move the selected battle Pokemon back to the first empty bench slot
    public void MoveToBench()
    {
        if (CurrentSelection != SelectionSource.Battle) return;
        if (BattleRow[SelectedIndex] == null) return;

        int emptyBench = GetFirstEmptySlot(BenchRow);
        if (emptyBench == -1)
        {
            Debug.Log("Bench is full!");
            return;
        }

        BenchRow[emptyBench] = BattleRow[SelectedIndex];
        BattleRow[SelectedIndex] = null;

        Debug.Log($"Moved {BenchRow[emptyBench].baseData.pokemonName} to bench");

        ClearSelection();

        // TODO: Tell UIManager to refresh bench and battle row display
    }

    // Reroll the shop — costs 1 Pokédollars
    public void Reroll()
    {
        if (CurrentPokedollars < rerollCost)
        {
            Debug.Log("Not enough Pokédollars to reroll!");
            return;
        }

        CurrentPokedollars -= rerollCost;
        PopulateShop();

        Debug.Log($"Rerolled shop — Pokédollars remaining: {CurrentPokedollars}");

        // TODO: Tell UIManager to refresh Pokédollars display
    }

    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    // Returns the index of the first null (empty) slot in an array, or -1 if full
    private int GetFirstEmptySlot<T>(T[] row) where T : class
    {
        for (int i = 0; i < row.Length; i++)
            if (row[i] == null) return i;
        return -1;
    }

    // Returns true if the battle row has at least one Pokemon (needed before starting battle)
    public bool CanStartBattle()
    {
        foreach (var p in BattleRow)
            if (p != null) return true;
        return false;
    }
}
