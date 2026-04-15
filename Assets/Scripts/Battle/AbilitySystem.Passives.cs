using UnityEngine;
using System.Collections.Generic;

public static partial class AbilitySystem
{
    // No-op handler for passives that are resolved via query methods below, not via events.
    private static void PassiveNoOp(EffectContext ctx) { }

    public static float GetPassiveAttackMultiplier(PokemonInstance attacker)
    {
        var ab = attacker.baseData.ability;
        if (ab == null || ab.effect != "boost_attack" || ab.trigger != "passive") return 1f;
        if (!string.IsNullOrEmpty(ab.condition)) return 1f;
        return ab.FloatValue; // Huge Power
    }

    public static float GetDamageReduction(PokemonInstance defender, bool isAbilityDamage = false)
    {
        if (MoldBreakerActive) return 1f;

        var ab = defender.baseData.ability;
        if (ab == null) return 1f;

        if (ab.custom == "water_sport") return 1f;

        if (ab.effect == "damage_reduction" && CheckCondition(defender, ab.condition))
            return ab.FloatValue;

        if (ab.effect == "damage_reduction_abilities" && isAbilityDamage)
            return ab.FloatValue;

        return 1f;
    }

    public static float GetTypeBasedDamageReduction(PokemonInstance defender, string attackerType)
    {
        if (MoldBreakerActive) return 1f;

        var ab = defender.baseData.ability;
        if (ab == null) return 1f;

        if (ab.custom == "water_sport" && attackerType == "Fire")
        {
            Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: Fire damage reduced!");
            return ab.FloatValue;
        }

        return 1f;
    }

    public static int GetFlatDamageReduction(PokemonInstance defender)
    {
        if (MoldBreakerActive) return 0;

        var ab = defender.baseData.ability;
        if (ab == null) return 0;

        if (ab.effect == "damage_reduction_flat" && CheckCondition(defender, ab.condition))
            return (int)ab.FloatValue;

        return 0;
    }

    // Friend Guard and similar passive ally-wide damage reduction
    public static float GetAllyDamageReduction(PokemonInstance defender, List<PokemonInstance> defenderTeam)
    {
        if (MoldBreakerActive) return 1f;

        float mult = 1f;
        foreach (var ally in defenderTeam)
        {
            if (ally == defender || ally.currentHP <= 0) continue;
            var ab = ally.baseData.ability;
            if (ab == null) continue;
            if (ab.trigger == "passive" && ab.target == "ally_all" && ab.effect == "damage_reduction")
                mult *= ab.FloatValue;
        }
        return mult;
    }

    // Battery passive — boosts ally ability damage by a multiplier
    public static float GetAllyAbilityDamageBoost(PokemonInstance source, List<PokemonInstance> sourceTeam)
    {
        foreach (var ally in sourceTeam)
        {
            if (ally == source || ally.currentHP <= 0) continue;
            var ab = ally.baseData.ability;
            if (ab != null && ab.trigger == "passive" && ab.effect == "boost_ability_damage")
                return ab.FloatValue;
        }
        return 1f;
    }

    public static bool IgnoresDamageReduction(PokemonInstance attacker)
    {
        var ab = attacker.baseData.ability;
        return ab != null && ab.effect == "ignore_damage_reduction";
    }

    public static bool IsImmuneToGround(PokemonInstance defender)
    {
        var ab = defender.baseData.ability;
        return ab != null && ab.effect == "immune" && ab.condition == "Ground";
    }

    // Returns true if this Pokemon always attacks first (Aqua Jet etc.)
    public static bool HasPriorityMove(PokemonInstance p)
    {
        var ab = p.baseData.ability;
        return ab != null && ab.trigger == "passive" && ab.effect == "priority_move";
    }
}
