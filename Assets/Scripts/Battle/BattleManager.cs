using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// BattleManager runs the battle between the player's battle row and an enemy team.
// Battle flow per turn:
//   1. Get the front alive Pokemon from each side
//   2. The faster one attacks first (tie = random)
//   3. If the defender survives, they attack back
//   4. Remove fainted Pokemon
//   5. Repeat until one side is wiped or 20 turns pass (draw)

public class BattleManager : MonoBehaviour
{
    // --- Singleton ---
    public static BattleManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Maximum turns before the battle ends in a draw")]
    public int maxTurns = 20;

    // The result of the last battle
    public enum BattleResult { PlayerWin, PlayerLoss, Draw }

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // -------------------------------------------------------
    // BATTLE ENTRY POINT
    // Call this to run a full battle. Returns the result.
    // -------------------------------------------------------

    public BattleResult RunBattle()
    {
        // Copy the player's battle row into a working list (only alive slots)
        List<PokemonInstance> playerTeam = ShopManager.Instance.BattleRow
            .Where(p => p != null)
            .ToList();

        // Generate the enemy team based on current round
        List<PokemonInstance> enemyTeam = GenerateEnemyTeam();

        if (playerTeam.Count == 0)
        {
            Debug.Log("Battle: Player has no Pokemon in battle row — auto loss.");
            return BattleResult.PlayerLoss;
        }

        Debug.Log($"Battle start! Player: {playerTeam.Count} Pokemon | Enemy: {enemyTeam.Count} Pokemon");

        // Run turns
        for (int turn = 1; turn <= maxTurns; turn++)
        {
            Debug.Log($"--- Turn {turn} ---");

            // Get the front (first alive) Pokemon from each side
            PokemonInstance playerFront = GetFront(playerTeam);
            PokemonInstance enemyFront  = GetFront(enemyTeam);

            if (playerFront == null || enemyFront == null) break;

            // Run the turn — faster Pokemon attacks first
            RunTurn(playerFront, enemyFront);

            // Remove any fainted Pokemon from both teams
            RemoveFainted(playerTeam);
            RemoveFainted(enemyTeam);

            // Check win/loss after each turn
            if (playerTeam.Count == 0 && enemyTeam.Count == 0)
            {
                Debug.Log("Battle result: Draw (both teams wiped on the same turn)");
                return BattleResult.Draw;
            }
            if (playerTeam.Count == 0)
            {
                Debug.Log("Battle result: Player loses!");
                return BattleResult.PlayerLoss;
            }
            if (enemyTeam.Count == 0)
            {
                Debug.Log("Battle result: Player wins!");
                return BattleResult.PlayerWin;
            }
        }

        // If we reached the turn limit, it's a draw
        Debug.Log($"Battle result: Draw (reached {maxTurns} turn limit)");
        return BattleResult.Draw;
    }

    // -------------------------------------------------------
    // TURN LOGIC
    // -------------------------------------------------------

    private void RunTurn(PokemonInstance a, PokemonInstance b)
    {
        // Determine attack order based on Speed stat
        // If speeds are equal, randomly pick who goes first
        bool aGoesFirst;

        if (a.baseData.speed != b.baseData.speed)
            aGoesFirst = a.baseData.speed > b.baseData.speed;
        else
            aGoesFirst = Random.value > 0.5f;

        PokemonInstance first  = aGoesFirst ? a : b;
        PokemonInstance second = aGoesFirst ? b : a;

        Debug.Log($"{first.baseData.pokemonName} (Speed {first.baseData.speed}) goes before {second.baseData.pokemonName} (Speed {second.baseData.speed})");

        // First Pokemon attacks
        Attack(first, second);

        // Second Pokemon only attacks back if they survived
        if (second.currentHP > 0)
            Attack(second, first);
    }

    // -------------------------------------------------------
    // ATTACK CALCULATION
    // -------------------------------------------------------

    public void Attack(PokemonInstance attacker, PokemonInstance defender)
    {
        // Get the type effectiveness multiplier
        // Attacker's type1 is used as the attack type
        float multiplier = TypeChart.GetMultiplier(
            attacker.baseData.type1,
            defender.baseData.type1,
            defender.baseData.type2
        );

        // Base damage = attacker's attack stat
        // Apply multiplier and always round UP (so minimum 1 damage if multiplier > 0)
        int damage = Mathf.CeilToInt(attacker.attack * multiplier);

        // Apply damage
        defender.currentHP -= damage;
        defender.currentHP  = Mathf.Max(defender.currentHP, 0); // Never go below 0

        // Log the result with effectiveness message
        string effectText = GetEffectivenessText(multiplier);
        Debug.Log($"{attacker.baseData.pokemonName} attacks {defender.baseData.pokemonName} " +
                  $"for {damage} damage{effectText} — {defender.baseData.pokemonName} HP: {defender.currentHP}/{defender.maxHP}");

        if (defender.currentHP == 0)
            Debug.Log($"{defender.baseData.pokemonName} fainted!");
    }

    // Returns a readable string for the type effectiveness
    private string GetEffectivenessText(float multiplier)
    {
        if (multiplier == 0f)   return " (no effect)";
        if (multiplier >= 4f)   return " (it's super effective! x4)";
        if (multiplier >= 2f)   return " (it's super effective!)";
        if (multiplier <= 0.25f) return " (it's not very effective... x0.25)";
        if (multiplier <= 0.5f)  return " (it's not very effective...)";
        return "";
    }

    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    // Returns the first Pokemon in the list that still has HP (the "front")
    private PokemonInstance GetFront(List<PokemonInstance> team)
    {
        return team.FirstOrDefault(p => p.currentHP > 0);
    }

    // Removes all fainted (0 HP) Pokemon from the team
    private void RemoveFainted(List<PokemonInstance> team)
    {
        team.RemoveAll(p => p.currentHP <= 0);
    }

    // -------------------------------------------------------
    // ENEMY TEAM GENERATION
    // Creates an enemy team based on the current round
    // -------------------------------------------------------

    public List<PokemonInstance> GenerateEnemyTeam()
    {
        int round   = GameManager.Instance.CurrentRound;
        int maxTier = GetMaxTierForRound(round);

        // How many enemy Pokemon to spawn — scales with round (min 1, max 3)
        int enemyCount = Mathf.Min(round, ShopManager.Instance.battleRowSize);

        // Pick from the same pool as the shop
        List<PokemonData> available = ShopManager.Instance.allPokemon
            .Where(p => p.tier > 0 && p.tier <= maxTier)
            .ToList();

        List<PokemonInstance> enemyTeam = new List<PokemonInstance>();

        for (int i = 0; i < enemyCount; i++)
        {
            if (available.Count == 0) break;
            PokemonData picked = available[Random.Range(0, available.Count)];
            enemyTeam.Add(new PokemonInstance(picked));
            Debug.Log($"Enemy Pokemon {i + 1}: {picked.pokemonName}");
        }

        return enemyTeam;
    }

    // Mirrors ShopManager's tier unlock schedule
    private int GetMaxTierForRound(int round)
    {
        if (round <= 2) return 1;
        if (round <= 4) return 2;
        if (round <= 6) return 3;
        return 4;
    }
}
