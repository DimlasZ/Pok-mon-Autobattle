using UnityEngine;
using System.Collections.Generic;

// Handles all damage calculation for a single attack action.
// Called by BattleManager (simulation) and BattleSceneManager (visual coroutine).

public static class DamageCalculator
{
    public static void Attack(PokemonInstance attacker, PokemonInstance defender,
        List<PokemonInstance> attackerTeam, List<PokemonInstance> defenderTeam)
    {
        // Mold Breaker — attacker's ability bypasses all defender passives
        bool hasMoldBreaker = attacker.baseData.ability?.effect == "ignore_abilities";
        AbilitySystem.SetMoldBreaker(hasMoldBreaker);

        // Type effectiveness
        float typeMultiplier = TypeChart.GetMultiplier(
            attacker.baseData.type1,
            defender.baseData.type1
        );

        // Before-attack ability multiplier
        float abilityMultiplier = AbilitySystem.FireBeforeAttack(attacker, attackerTeam, defenderTeam);

        // Passive attack multiplier (Huge Power, Flower Gift, etc. — checked live each attack)
        float passiveMultiplier = AbilitySystem.GetPassiveAttackMultiplier(attacker);

        // Weather bonus/penalty for the attacker's type
        float weatherMultiplier = AbilitySystem.GetWeatherDamageMultiplier(attacker);

        // Calculate raw damage
        int damage = Mathf.CeilToInt(attacker.attack * typeMultiplier * abilityMultiplier * passiveMultiplier * weatherMultiplier);

        // Before-hit check — may fully negate the hit (Shell Armor, Protect)
        if (AbilitySystem.FireBeforeHit(defender, attacker, false))
        {
            AbilitySystem.SetMoldBreaker(false);
            return;
        }

        // Apply damage reduction unless the attacker ignores it (Aerial Ace)
        if (!AbilitySystem.IgnoresDamageReduction(attacker))
        {
            float reduction = AbilitySystem.GetDamageReduction(defender)
                            * AbilitySystem.GetTypeBasedDamageReduction(defender, attacker.baseData.type1)
                            * AbilitySystem.GetAllyDamageReduction(defender, defenderTeam)
                            * AbilitySystem.GetWeatherDamageReduction(defender);
            damage = Mathf.CeilToInt(damage * reduction);
            damage -= AbilitySystem.GetFlatDamageReduction(defender);
            damage = Mathf.Max(1, damage);
        }

        // damageTakenMultiplier — permanent (Cursed Aura etc.)
        damage = Mathf.CeilToInt(damage * defender.damageTakenMultiplier);

        // nextHitDamageMultiplier — one-shot (Screech, Leer); consumed on this hit
        damage = Mathf.CeilToInt(damage * defender.nextHitDamageMultiplier);
        defender.nextHitDamageMultiplier = 1f;

        // on_hit — may modify damage (Sturdy, Rough Skin, Iron Barbs)
        damage = AbilitySystem.FireOnHit(defender, attacker, damage, defenderTeam, attackerTeam);

        // Apply damage, track excess (for Bone Club overflow)
        int preCombatHP    = defender.currentHP;
        defender.currentHP = Mathf.Max(0, defender.currentHP - damage);
        int actualDamage   = preCombatHP - defender.currentHP;
        int excessDamage   = damage - actualDamage;

        // Log
        string effectText = GetEffectivenessText(typeMultiplier);
        Debug.Log($"{attacker.baseData.pokemonName} attacks {defender.baseData.pokemonName} " +
                  $"for {actualDamage} damage{effectText} — {defender.baseData.pokemonName} HP: {defender.currentHP}/{defender.maxHP}");

        // After-hit (Rest heal check, ally buffs on survive)
        AbilitySystem.FireAfterHit(defender, defenderTeam);

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

        AbilitySystem.SetMoldBreaker(false);
    }

    public static string GetEffectivenessText(float multiplier)
    {
        if (multiplier == 0f)     return " (no effect)";
        if (multiplier >= 1.5f)   return " (it's super effective!)";
        if (multiplier <= 0.75f)  return " (it's not very effective...)";
        return "";
    }
}
