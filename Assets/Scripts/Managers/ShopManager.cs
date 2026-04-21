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
    // Loaded automatically from Resources/Data/Pokemon/ — no manual assignment needed
    public PokemonData[] AllPokemon { get; private set; }

    [Tooltip("How much Pokédollars the player starts with each round")]
    public int startingPokedollars = 5;

    [Tooltip("How much a reroll costs")]
    public int rerollCost = 1;

    // --- Array capacities (never change at runtime) ---
    public const int MaxShopSize   = 6;
    public const int MaxBattleSize = 6;
    public const int MaxBenchSize  = 6;

    // --- Active sizes (computed from current round) ---
    public int ShopSize   => GetBattleSizeForRound(GameManager.Instance.CurrentRound);
    public int BattleSize => GetBattleSizeForRound(GameManager.Instance.CurrentRound);
    public int BenchSize  => GetBattleSizeForRound(GameManager.Instance.CurrentRound);

    // --- Pokédollars ---
    public int CurrentPokedollars { get; private set; }

    // --- Rows ---
    public PokemonInstance[] ShopRow   { get; private set; }
    public PokemonInstance[] BenchRow  { get; private set; }
    public PokemonInstance[] BattleRow { get; private set; }

    // --- Bait: tracks which shop slots are frozen and survive reroll ---
    public bool[] BaitedSlots { get; private set; }

    private bool _restoredFromSave;
    private int  _lastKnownTier = 0;

    // Set when a tier upgrade happens; consumed by UIManager after the shop scene loads.
    public int  PendingTierUpgrade { get; private set; } = 0;

    // --- Selection ---
    public enum SelectionSource { None, Shop, Bench, Battle }
    public SelectionSource CurrentSelection { get; private set; } = SelectionSource.None;
    public int SelectedIndex { get; private set; } = -1;

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ShopRow     = new PokemonInstance[MaxShopSize];
        BenchRow    = new PokemonInstance[MaxBenchSize];
        BattleRow   = new PokemonInstance[MaxBattleSize];
        BaitedSlots = new bool[MaxShopSize];

        AllPokemon = Resources.LoadAll<PokemonData>("Data/Pokemon");
        Debug.Log($"[ShopManager] Loaded {AllPokemon.Length} Pokémon from Resources.");
    }

    private void Start()
    {
        // Music is handled by UIManager.Start() which runs on every shop scene load.
        // First load from main menu: GameManager.EnterBuyPhase() ran before ShopManager existed,
        // so StartRound() was skipped. Call it now — unless we just restored from an autosave,
        // in which case the rows are already populated and UIManager just needs a refresh.
        // On subsequent rounds ShopManager persists and Start() never runs again.
        if (_restoredFromSave)
        {
            _restoredFromSave = false;
            if (UIManager.Instance != null)
                UIManager.Instance.RefreshShopWithDelay();
        }
        else
        {
            StartRound();
        }
    }

    // -------------------------------------------------------
    // ROUND START
    // -------------------------------------------------------

    public void StartRound()
    {
        CurrentPokedollars = startingPokedollars;
        ClearSelection();

        int newTier = GetTierForRound(GameManager.Instance.CurrentRound);
        bool tierIncreased = newTier > _lastKnownTier && _lastKnownTier > 0;
        _lastKnownTier = newTier;

        PopulateShop();

        Debug.Log($"Round {GameManager.Instance.CurrentRound} — Shop: {ShopSize} slots, Battle: {BattleSize} slots");

        if (UIManager.Instance != null)
            UIManager.Instance.RefreshShopWithDelay();

        PendingTierUpgrade = tierIncreased ? newTier : 0;
    }

    // -------------------------------------------------------
    // SHOP POPULATION
    // -------------------------------------------------------

    private void PopulateShop()
    {
        int tier   = GetTierForRound(GameManager.Instance.CurrentRound);
        int active = ShopSize;

        List<PokemonData> available = AllPokemon
            .Where(p => p.tier > 0 && p.tier <= tier)
            .ToList();

        if (available.Count == 0)
        {
            Debug.LogWarning($"No Pokemon available up to tier {tier}!");
            return;
        }

        // Build weighted pool: normal Pokemon appear 10x, legendaries appear 1x
        var weightedPool = new List<PokemonData>();
        foreach (var p in available)
        {
            int weight = p.isLegendary ? 1 : 10;
            for (int w = 0; w < weight; w++) weightedPool.Add(p);
        }

        for (int i = 0; i < MaxShopSize; i++)
        {
            if (BaitedSlots[i]) continue; // frozen — keep existing Pokemon
            ShopRow[i] = i < active
                ? new PokemonInstance(weightedPool[Random.Range(0, weightedPool.Count)])
                : null;
        }

        Debug.Log($"Shop populated — up to Tier {tier}, {active} slots");
    }

    // -------------------------------------------------------
    // ROUND SCALING
    // -------------------------------------------------------

    public int GetTierForRound(int round)
    {
        if (round <= 2)  return 1;
        if (round <= 5)  return 2;
        if (round <= 8)  return 3;
        if (round <= 11) return 4;
        if (round <= 14) return 5;
        return 6;
    }

    private int GetBattleSizeForRound(int round)
    {
        if (round < 3) return 3;
        if (round < 5) return 4;
        if (round < 7) return 5;
        return 6;
    }

    // -------------------------------------------------------
    // SELECTION
    // -------------------------------------------------------

    public void SelectShopPokemon(int index)
    {
        if (index < 0 || index >= ShopSize || ShopRow[index] == null) return;
        var p = ShopRow[index];
        if (p.baseData.preEvolutionId > 0 && !PlayerOwnsPreEvolution(p.baseData.preEvolutionId))
        {
            Debug.Log($"Can't select {p.baseData.pokemonName} — need its pre-evolution first!");
            return;
        }
        if (p.baseData.preEvolutionId == 0 && IsAlreadyOnTeam(p.baseData.id))
        {
            Debug.Log($"Can't select {p.baseData.pokemonName} — already on the team!");
            return;
        }
        SelectedIndex    = index;
        CurrentSelection = SelectionSource.Shop;
        Debug.Log($"Selected shop slot {index}: {ShopRow[index].baseData.pokemonName}");
    }

    public void SelectBenchPokemon(int index)
    {
        if (index < 0 || index >= BenchSize || BenchRow[index] == null) return;
        SelectedIndex    = index;
        CurrentSelection = SelectionSource.Bench;
        Debug.Log($"Selected bench slot {index}: {BenchRow[index].baseData.pokemonName}");
    }

    public void SelectBattlePokemon(int index)
    {
        if (index < 0 || index >= BattleSize || BattleRow[index] == null) return;
        SelectedIndex    = index;
        CurrentSelection = SelectionSource.Battle;
        Debug.Log($"Selected battle slot {index}: {BattleRow[index].baseData.pokemonName}");
    }

    public void ClearSelection()
    {
        SelectedIndex    = -1;
        CurrentSelection = SelectionSource.None;
    }

    // -------------------------------------------------------
    // UNIFIED PLACEMENT
    // Called by both click-to-place and drag-and-drop.
    // Handles buy, move, and insert depending on source/target.
    // -------------------------------------------------------

    // Insert pokemon at targetIndex in the battle row.
    // Shifts entries from targetIndex rightward until the first null absorbs the shift.
    // Returns false if there is no empty slot at or after targetIndex.
    private bool InsertIntoBattleRow(PokemonInstance pokemon, int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= BattleSize) return false;

        // Find the first null at or after targetIndex to absorb the shift
        int emptySlot = -1;
        for (int i = targetIndex; i < BattleSize; i++)
        {
            if (BattleRow[i] == null) { emptySlot = i; break; }
        }
        if (emptySlot == -1) return false; // Battle row is full from targetIndex onwards

        // Shift everything between targetIndex and emptySlot one step right
        for (int i = emptySlot; i > targetIndex; i--)
            BattleRow[i] = BattleRow[i - 1];

        BattleRow[targetIndex] = pokemon;
        return true;
    }

    public bool IsAlreadyOnTeam(int pokemonId)
    {
        foreach (var p in BattleRow) if (p != null && p.baseData.id == pokemonId) return true;
        foreach (var p in BenchRow)  if (p != null && p.baseData.id == pokemonId) return true;
        return false;
    }

    // Unified action: place whatever is selected at (targetSource, targetIndex).
    // Returns true if the placement succeeded.
    public bool PlaceSelected(SelectionSource targetSource, int targetIndex)
    {
        if (CurrentSelection == SelectionSource.None) return false;

        bool success = false;

        switch (CurrentSelection)
        {
            case SelectionSource.Shop:
            {
                var p = ShopRow[SelectedIndex];
                if (p == null || CurrentPokedollars < 1)
                {
                    Debug.Log(CurrentPokedollars < 1 ? "Not enough Pokédollars!" : "Empty shop slot.");
                    return false;
                }

                // Evolution buy — target slot must contain the pre-evolution
                if (p.baseData.preEvolutionId > 0)
                {
                    var targetPokemon = GetSlotPokemon(targetSource, targetIndex);
                    if (targetPokemon == null || targetPokemon.baseData.id != p.baseData.preEvolutionId)
                    {
                        Debug.Log($"{p.baseData.pokemonName} must be placed on its pre-evolution!");
                        return false;
                    }
                    SetSlotPokemon(targetSource, targetIndex, p);
                    BaitedSlots[SelectedIndex] = false;
                    ShopRow[SelectedIndex] = null;
                    CurrentPokedollars--;
                    AudioManager.Instance?.PlayCry(p.baseData.id);
                    Debug.Log($"Evolved to {p.baseData.pokemonName} — P$ remaining: {CurrentPokedollars}");
                    success = true;
                    break;
                }

                // Normal buy
                if (IsAlreadyOnTeam(p.baseData.id))
                {
                    Debug.Log($"You already have a {p.baseData.pokemonName} on your team!");
                    return false;
                }

                if (targetSource == SelectionSource.Battle)
                {
                    if (InsertIntoBattleRow(p, targetIndex))
                    {
                        BaitedSlots[SelectedIndex] = false;
                        ShopRow[SelectedIndex] = null;
                        CurrentPokedollars--;
                        AudioManager.Instance?.PlayCry(p.baseData.id);
                        Debug.Log($"Bought {p.baseData.pokemonName} to battle slot {targetIndex} — P$ remaining: {CurrentPokedollars}");
                        success = true;
                    }
                    else Debug.Log("Battle row is full at that position!");
                }
                else if (targetSource == SelectionSource.Bench)
                {
                    if (BenchRow[targetIndex] != null) { Debug.Log("Bench slot is occupied!"); return false; }
                    BenchRow[targetIndex]      = p;
                    BaitedSlots[SelectedIndex] = false;
                    ShopRow[SelectedIndex]     = null;
                    CurrentPokedollars--;
                    AudioManager.Instance?.PlayCry(p.baseData.id);
                    Debug.Log($"Bought {p.baseData.pokemonName} to bench — P$ remaining: {CurrentPokedollars}");
                    success = true;
                }
                break;
            }

            case SelectionSource.Bench:
            {
                var p = BenchRow[SelectedIndex];
                if (p == null) return false;

                if (targetSource == SelectionSource.Battle)
                {
                    if (InsertIntoBattleRow(p, targetIndex))
                    {
                        BenchRow[SelectedIndex] = null;
                        Debug.Log($"Moved {p.baseData.pokemonName} to battle slot {targetIndex}");
                        success = true;
                    }
                    else if (targetIndex >= 0 && targetIndex < BattleSize)
                    {
                        // Battle row is full — swap bench ↔ battle slot directly
                        var displaced = BattleRow[targetIndex];
                        BattleRow[targetIndex]  = p;
                        BenchRow[SelectedIndex] = displaced;
                        Debug.Log($"Swapped {p.baseData.pokemonName} ↔ {displaced?.baseData.pokemonName}");
                        success = true;
                    }
                    else Debug.Log("Invalid battle slot!");
                }
                else if (targetSource == SelectionSource.Bench && targetIndex != SelectedIndex)
                {
                    if (BenchRow[targetIndex] != null) { Debug.Log("Bench slot is occupied!"); return false; }
                    BenchRow[targetIndex]   = p;
                    BenchRow[SelectedIndex] = null;
                    Debug.Log($"Moved {p.baseData.pokemonName} to bench slot {targetIndex}");
                    success = true;
                }
                break;
            }

            case SelectionSource.Battle:
            {
                var p = BattleRow[SelectedIndex];
                if (p == null) return false;

                if (targetSource == SelectionSource.Battle && targetIndex != SelectedIndex)
                {
                    int from = SelectedIndex;
                    int to   = targetIndex;

                    if (to > from)
                    {
                        // Moving right: shift entries from from+1..to one step left
                        for (int i = from; i < to; i++)
                            BattleRow[i] = BattleRow[i + 1];
                    }
                    else
                    {
                        // Moving left: shift entries from to..from-1 one step right
                        for (int i = from; i > to; i--)
                            BattleRow[i] = BattleRow[i - 1];
                    }

                    BattleRow[to] = p;
                    Debug.Log($"Moved {p.baseData.pokemonName} to battle slot {to}");
                    success = true;
                }
                else if (targetSource == SelectionSource.Bench)
                {
                    var displaced = BenchRow[targetIndex];
                    BenchRow[targetIndex]    = p;
                    BattleRow[SelectedIndex] = displaced;
                    if (displaced != null)
                        Debug.Log($"Swapped {p.baseData.pokemonName} ↔ {displaced.baseData.pokemonName}");
                    else
                        Debug.Log($"Moved {p.baseData.pokemonName} to bench");
                    success = true;
                }
                break;
            }
        }

        if (success) ClearSelection();
        return success;
    }

    // -------------------------------------------------------
    // RELEASE (bench or battle → gone)
    // -------------------------------------------------------

    public void ReleaseSelected()
    {
        string name = null;

        if (CurrentSelection == SelectionSource.Bench && BenchRow[SelectedIndex] != null)
        {
            name = BenchRow[SelectedIndex].baseData.pokemonName;
            BenchRow[SelectedIndex] = null;
        }
        else if (CurrentSelection == SelectionSource.Battle && BattleRow[SelectedIndex] != null)
        {
            name = BattleRow[SelectedIndex].baseData.pokemonName;
            BattleRow[SelectedIndex] = null;
        }

        if (name != null) Debug.Log($"Released {name}");
        ClearSelection();
    }

    // -------------------------------------------------------
    // REROLL
    // -------------------------------------------------------

    public void Reroll()
    {
        if (CurrentPokedollars < rerollCost)
        {
            Debug.Log("Not enough Pokédollars to reroll!");
            return;
        }

        CurrentPokedollars -= rerollCost;
        PopulateShop();

        Debug.Log($"Rerolled shop — P$ remaining: {CurrentPokedollars}");
    }

    // -------------------------------------------------------
    // BAIT
    // -------------------------------------------------------

    public void ToggleBait(int shopIndex)
    {
        if (shopIndex < 0 || shopIndex >= ShopSize || ShopRow[shopIndex] == null) return;
        BaitedSlots[shopIndex] = !BaitedSlots[shopIndex];
        Debug.Log($"Slot {shopIndex} bait: {BaitedSlots[shopIndex]}");
    }

    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    // Returns true if the player owns any Pokémon with the given ID (bench or battle)
    public bool PlayerOwnsPreEvolution(int preEvoId)
    {
        foreach (var p in BattleRow) if (p != null && p.baseData.id == preEvoId) return true;
        foreach (var p in BenchRow)  if (p != null && p.baseData.id == preEvoId) return true;
        return false;
    }

    // Returns the currently selected shop Pokémon (or null)
    public PokemonInstance GetSelectedShopPokemon()
    {
        if (CurrentSelection != SelectionSource.Shop || SelectedIndex < 0) return null;
        return ShopRow[SelectedIndex];
    }

    private PokemonInstance GetSlotPokemon(SelectionSource source, int index)
    {
        return source switch
        {
            SelectionSource.Battle => index >= 0 && index < BattleSize ? BattleRow[index] : null,
            SelectionSource.Bench  => index >= 0 && index < BenchSize  ? BenchRow[index]  : null,
            _ => null
        };
    }

    private void SetSlotPokemon(SelectionSource source, int index, PokemonInstance pokemon)
    {
        if (source == SelectionSource.Battle) BattleRow[index] = pokemon;
        else if (source == SelectionSource.Bench) BenchRow[index] = pokemon;
    }

    public bool CanStartBattle()
    {
        for (int i = 0; i < BattleSize; i++)
            if (BattleRow[i] != null) return true;
        return false;
    }

    // -------------------------------------------------------
    // SAVE / LOAD / RESET
    // -------------------------------------------------------

    // Clears all team rows — called before starting a new game so the previous run doesn't bleed in.
    public void ClearAllRows()
    {
        for (int i = 0; i < MaxShopSize;   i++) { ShopRow[i]   = null; BaitedSlots[i] = false; }
        for (int i = 0; i < MaxBattleSize; i++)   BattleRow[i] = null;
        for (int i = 0; i < MaxBenchSize;  i++)   BenchRow[i]  = null;
        _lastKnownTier     = 0;
        PendingTierUpgrade = 0;
        ClearSelection();
    }

    // Restores all rows from a save — called by GameManager.ContinueGame().
    public void ClearPendingTierUpgrade() => PendingTierUpgrade = 0;

    public void LoadFromSave(GameSaveData save)
    {
        ClearAllRows();
        CurrentPokedollars = save.currentPokedollars;

        for (int i = 0; i < MaxShopSize;   i++) ShopRow[i]    = AutoSaveManager.Deserialize(save.shopRow[i],   AllPokemon);
        for (int i = 0; i < MaxBattleSize; i++) BattleRow[i]  = AutoSaveManager.Deserialize(save.battleRow[i], AllPokemon);
        for (int i = 0; i < MaxBenchSize;  i++) BenchRow[i]   = AutoSaveManager.Deserialize(save.benchRow[i],  AllPokemon);
        for (int i = 0; i < MaxShopSize;   i++) BaitedSlots[i] = save.baitedSlots[i];

        _restoredFromSave = true;
        Debug.Log($"[AutoSave] Loaded round {save.currentRound} — P$ {save.currentPokedollars}");
    }
}
