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
    // -------------------------------------------------------

    public BattleResult RunBattle()
    {
        List<PokemonInstance> playerTeam = ShopManager.Instance.BattleRow
            .Where(p => p != null)
            .ToList();

        List<PokemonInstance> enemyTeam = GenerateEnemyTeam();

        if (playerTeam.Count == 0)
        {
            Debug.Log("Battle: Player has no Pokemon in battle row — auto loss.");
            return BattleResult.PlayerLoss;
        }

        Debug.Log($"Battle start! Player: {playerTeam.Count} Pokemon | Enemy: {enemyTeam.Count} Pokemon");

        // Reset ability state (weather, sturdy flags, etc.)
        AbilitySystem.ResetBattleState();

        // Fire on_battle_start for all Pokemon (player first, then enemy)
        AbilitySystem.FireBattleStart(playerTeam, enemyTeam);
        AbilitySystem.FireBattleStart(enemyTeam, playerTeam);

        for (int turn = 1; turn <= maxTurns; turn++)
        {
            Debug.Log($"--- Turn {turn} ---");

            // Fire on_round_start for all alive Pokemon
            AbilitySystem.FireRoundStart(playerTeam, enemyTeam);
            AbilitySystem.FireRoundStart(enemyTeam, playerTeam);

            // Get the front (first alive) Pokemon from each side
            PokemonInstance playerFront = GetFront(playerTeam);
            PokemonInstance enemyFront  = GetFront(enemyTeam);

            if (playerFront == null || enemyFront == null) break;

            RunTurn(playerFront, enemyFront, playerTeam, enemyTeam);

            RemoveFainted(playerTeam);
            RemoveFainted(enemyTeam);

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

        Debug.Log($"Battle result: Draw (reached {maxTurns} turn limit)");
        return BattleResult.Draw;
    }

    // -------------------------------------------------------
    // TURN LOGIC
    // -------------------------------------------------------

    private void RunTurn(PokemonInstance playerFront, PokemonInstance enemyFront,
        List<PokemonInstance> playerTeam, List<PokemonInstance> enemyTeam)
    {
        bool playerGoesFirst;

        if (playerFront.speed != enemyFront.speed)
            playerGoesFirst = playerFront.speed > enemyFront.speed;
        else
            playerGoesFirst = Random.value > 0.5f;

        PokemonInstance first      = playerGoesFirst ? playerFront : enemyFront;
        PokemonInstance second     = playerGoesFirst ? enemyFront  : playerFront;
        List<PokemonInstance> firstTeam  = playerGoesFirst ? playerTeam : enemyTeam;
        List<PokemonInstance> secondTeam = playerGoesFirst ? enemyTeam  : playerTeam;

        Debug.Log($"{first.baseData.pokemonName} (Speed {first.speed}) goes before {second.baseData.pokemonName} (Speed {second.speed})");

        Attack(first, second, firstTeam, secondTeam);

        if (second.currentHP > 0)
            Attack(second, first, secondTeam, firstTeam);
    }

    // -------------------------------------------------------
    // ATTACK CALCULATION
    // -------------------------------------------------------

    public void Attack(PokemonInstance attacker, PokemonInstance defender,
        List<PokemonInstance> attackerTeam, List<PokemonInstance> defenderTeam)
    {
        // Type effectiveness
        float typeMultiplier = TypeChart.GetMultiplier(
            attacker.baseData.type1,
            defender.baseData.type1
        );

        // Before-attack ability multiplier (e.g. Blaze at low HP)
        float abilityMultiplier = AbilitySystem.FireBeforeAttack(attacker, attackerTeam, defenderTeam);

        // Passive attack multiplier (e.g. Guts, Huge Power — checked live each attack)
        float passiveMultiplier = AbilitySystem.GetPassiveAttackMultiplier(attacker);

        // Flat attack bonus (e.g. Chlorophyll +20 in sun)
        int flatBonus = AbilitySystem.GetFlatAttackBonus(attacker);

        // Calculate raw damage
        int effectiveAttack = attacker.attack + flatBonus;
        int damage = Mathf.CeilToInt(effectiveAttack * typeMultiplier * abilityMultiplier * passiveMultiplier);

        // Before-hit check — may fully negate the hit (Shell Armor, immune_to_ability_damage)
        if (AbilitySystem.FireBeforeHit(defender, attacker, false))
            return;

        // Apply damage reduction unless the attacker ignores it (Aerial Ace)
        if (!AbilitySystem.IgnoresDamageReduction(attacker))
        {
            float reduction = AbilitySystem.GetDamageReduction(defender)
                            * AbilitySystem.GetAllyDamageReduction(defender, defenderTeam);
            damage = Mathf.CeilToInt(damage * reduction);
            damage -= AbilitySystem.GetFlatDamageReduction(defender);
            damage = Mathf.Max(1, damage);
        }

        // on_hit — may modify damage (Sturdy, Rough Skin recoil)
        damage = AbilitySystem.FireOnHit(defender, attacker, damage, defenderTeam, attackerTeam);

        // Apply damage, track excess (for Bone Club overflow)
        int preCombatHP   = defender.currentHP;
        defender.currentHP = Mathf.Max(0, defender.currentHP - damage);
        int actualDamage  = preCombatHP - defender.currentHP;
        int excessDamage  = damage - actualDamage;

        // Log
        string effectText = GetEffectivenessText(typeMultiplier);
        Debug.Log($"{attacker.baseData.pokemonName} attacks {defender.baseData.pokemonName} " +
                  $"for {actualDamage} damage{effectText} — {defender.baseData.pokemonName} HP: {defender.currentHP}/{defender.maxHP}");

        // After-hit (Rest heal check)
        AbilitySystem.FireAfterHit(defender);

        // on_attack — splash damage, overflow, etc. (Surf, Earthquake, Ember, Bone Club...)
        AbilitySystem.FireOnAttack(attacker, defender, actualDamage, excessDamage, attackerTeam, defenderTeam);

        // after_attack — self heals (Absorb, Mega Drain, Leech Life, Roost...)
        AbilitySystem.FireAfterAttack(attacker, defender, actualDamage, attackerTeam, defenderTeam);

        // on_faint / on_kill
        if (defender.currentHP == 0)
        {
            Debug.Log($"{defender.baseData.pokemonName} fainted!");
            AbilitySystem.FireOnFaint(defender, defenderTeam, attackerTeam);
            AbilitySystem.FireOnKill(attacker, attackerTeam, defenderTeam);
        }
    }

    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    private string GetEffectivenessText(float multiplier)
    {
        if (multiplier == 0f)    return " (no effect)";
        if (multiplier >= 4f)    return " (it's super effective! x4)";
        if (multiplier >= 2f)    return " (it's super effective!)";
        if (multiplier <= 0.25f) return " (it's not very effective... x0.25)";
        if (multiplier <= 0.5f)  return " (it's not very effective...)";
        return "";
    }

    private PokemonInstance GetFront(List<PokemonInstance> team)
    {
        return team.FirstOrDefault(p => p.currentHP > 0);
    }

    private void RemoveFainted(List<PokemonInstance> team)
    {
        team.RemoveAll(p => p.currentHP <= 0);
    }

    // -------------------------------------------------------
    // ENEMY TEAM GENERATION
    // -------------------------------------------------------

    public List<PokemonInstance> GenerateEnemyTeam()
    {
        int round   = GameManager.Instance.CurrentRound;
        int maxTier = GetMaxTierForRound(round);
        int enemyCount = Mathf.Min(round, ShopManager.Instance.battleRowSize);

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

    private int GetMaxTierForRound(int round)
    {
        if (round <= 2) return 1;
        if (round <= 4) return 2;
        if (round <= 6) return 3;
        return 4;
    }
}
