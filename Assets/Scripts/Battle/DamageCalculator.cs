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
        int rawDamage = Mathf.CeilToInt(attacker.attack * typeMultiplier * abilityMultiplier * passiveMultiplier * weatherMultiplier);
        int damage = rawDamage;

        // Type immunity — no damage and no on-hit effects
        if (typeMultiplier == 0f)
        {
            AbilitySystem.SetMoldBreaker(false);
            return;
        }

        // Before-hit check — may fully negate the hit (Shell Armor, Protect)
        if (AbilitySystem.FireBeforeHit(defender, attacker, false))
        {
            AbilitySystem.SetMoldBreaker(false);
            return;
        }

        // Capture reduction values for the debug log before applying them
        bool ignoresReduction = AbilitySystem.IgnoresDamageReduction(attacker);
        float reductionMult = ignoresReduction ? 1f :
                              AbilitySystem.GetDamageReduction(defender)
                            * AbilitySystem.GetTypeBasedDamageReduction(defender, attacker.baseData.type1)
                            * AbilitySystem.GetAllyDamageReduction(defender, defenderTeam)
                            * AbilitySystem.GetWeatherDamageReduction(defender);
        int flatReduction = ignoresReduction ? 0 : AbilitySystem.GetFlatDamageReduction(defender);

        // Apply damage reduction unless the attacker ignores it (Aerial Ace)
        if (!ignoresReduction)
        {
            damage = Mathf.CeilToInt(damage * reductionMult);
            damage -= flatReduction;
            damage = Mathf.Max(1, damage);
        }

        // damageTakenMultiplier — permanent (Cursed Aura etc.)
        damage = Mathf.CeilToInt(damage * defender.damageTakenMultiplier);

        // nextHitDamageMultiplier — one-shot (Screech, Leer); consumed on this hit — capture before reset
        float nextHitMult = defender.nextHitDamageMultiplier;
        damage = Mathf.CeilToInt(damage * nextHitMult);
        defender.nextHitDamageMultiplier = 1f;

        // on_hit — may modify damage (Sturdy, Rough Skin, Iron Barbs)
        damage = AbilitySystem.FireOnHit(defender, attacker, damage, defenderTeam, attackerTeam);

        // Apply damage, track excess (for Bone Club overflow)
        int preCombatHP    = defender.currentHP;
        defender.currentHP = Mathf.Max(0, defender.currentHP - damage);
        int actualDamage   = preCombatHP - defender.currentHP;
        int excessDamage   = damage - actualDamage;

        // Summary log
        string effectText = GetEffectivenessText(typeMultiplier);
        Debug.Log($"{attacker.baseData.pokemonName} attacks {defender.baseData.pokemonName} " +
                  $"for {actualDamage} damage{effectText} — {defender.baseData.pokemonName} HP: {defender.currentHP}/{defender.maxHP}");

        // Detailed breakdown
        Debug.Log($"[DmgCalc] {attacker.baseData.pokemonName} -> {defender.baseData.pokemonName}\n" +
                  $"  Base ATK:         {attacker.attack}\n" +
                  $"  Type multiplier:  x{typeMultiplier}\n" +
                  $"  Ability mult:     x{abilityMultiplier}\n" +
                  $"  Passive mult:     x{passiveMultiplier}\n" +
                  $"  Weather mult:     x{weatherMultiplier}\n" +
                  $"  Raw damage:       {rawDamage}\n" +
                  $"  Reduction mult:   x{reductionMult} (-{flatReduction} flat){(ignoresReduction ? " [IGNORED]" : "")}\n" +
                  $"  damageTakenMult:  x{defender.damageTakenMultiplier}\n" +
                  $"  nextHitMult:      x{nextHitMult}\n" +
                  $"  Actual damage:    {actualDamage}  (excess: {excessDamage})\n" +
                  $"  Defender HP:      {defender.currentHP}/{defender.maxHP}");

        // After-hit (Rest heal check, ally buffs on survive)
        AbilitySystem.FireAfterHit(defender, defenderTeam);

        // on_attack — splash damage, overflow, etc. (Surf, Earthquake, Ember, Bone Club...)
        AbilitySystem.FireOnAttack(attacker, defender, actualDamage, excessDamage, attackerTeam, defenderTeam);

        // after_attack — self heals (Absorb, Mega Drain, Leech Life, Roost...)
        AbilitySystem.FireAfterAttack(attacker, defender, actualDamage, attackerTeam, defenderTeam);

        // Log final HP after all post-attack callbacks
        Debug.Log($"[DmgCalc] Final HP after callbacks — {defender.baseData.pokemonName}: {defender.currentHP}/{defender.maxHP}  |  {attacker.baseData.pokemonName}: {attacker.currentHP}/{attacker.maxHP}");

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
