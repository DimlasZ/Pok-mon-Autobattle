using UnityEngine;
using System.Collections.Generic;

// Handles all damage calculation for a single attack action.
// Called by BattleManager (simulation) and BattleSceneManager (visual coroutine).

public static class DamageCalculator
{
    public static void Attack(PokemonInstance attacker, PokemonInstance defender,
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
                            * AbilitySystem.GetTypeBasedDamageReduction(defender, attacker.baseData.type1)
                            * AbilitySystem.GetAllyDamageReduction(defender, defenderTeam);
            damage = Mathf.CeilToInt(damage * reduction);
            damage -= AbilitySystem.GetFlatDamageReduction(defender);
            damage = Mathf.Max(1, damage);
        }

        // on_hit — may modify damage (Sturdy, Rough Skin recoil)
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
    }

    public static string GetEffectivenessText(float multiplier)
    {
        if (multiplier == 0f)     return " (no effect)";
        if (multiplier >= 1.5f)   return " (it's super effective!)";
        if (multiplier <= 0.75f)  return " (it's not very effective...)";
        return "";
    }
}
