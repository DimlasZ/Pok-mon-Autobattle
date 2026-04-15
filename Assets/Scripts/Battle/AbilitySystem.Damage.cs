using UnityEngine;
using System.Collections.Generic;

public static partial class AbilitySystem
{
    private static void HandleDealDamage(EffectContext ctx)
    {
        int dmg = Mathf.CeilToInt(ctx.source.attack * ctx.v);
        var targets = GetTargets(ctx);
        foreach (var t in targets)
        {
            if (t.currentHP <= 0) continue;
            var tTeam = ctx.enemyTeam.Contains(t) ? ctx.enemyTeam : ctx.sourceTeam;
            DealAbilityDamage(ctx.source, t, tTeam, ctx.sourceTeam, ctx.ab, dmg, true);
        }
    }

    private static void HandleDealDamageFlat(EffectContext ctx)
    {
        int dmg = Mathf.CeilToInt(ctx.v * GetWeatherDamageMultiplier(ctx.source));
        var targets = GetTargets(ctx);
        foreach (var t in targets)
        {
            if (t.currentHP <= 0) continue;
            var tTeam = ctx.enemyTeam.Contains(t) ? ctx.enemyTeam : ctx.sourceTeam;
            DealAbilityDamage(ctx.source, t, tTeam, ctx.sourceTeam, ctx.ab, dmg, true);
        }
    }

    // deal_damage_percent: damage = target.maxHP * value  (Stealth Pebbles, Spikes, Toxic Spikes…)
    private static void HandleDealDamagePercent(EffectContext ctx)
    {
        var targets = GetTargets(ctx);
        foreach (var t in targets)
        {
            if (t.currentHP <= 0) continue;
            int dmg = Mathf.CeilToInt(t.maxHP * ctx.v);
            var tTeam = ctx.enemyTeam.Contains(t) ? ctx.enemyTeam : ctx.sourceTeam;
            DealAbilityDamage(ctx.source, t, tTeam, ctx.sourceTeam, ctx.ab, dmg, false);
        }
    }

    // deal_damage_source_hp: damage = source.maxHP * value  (Seismic Toss, Self-Destruct, Vengeance…)
    private static void HandleDealDamageSourceHp(EffectContext ctx)
    {
        int dmg = Mathf.CeilToInt(ctx.source.maxHP * ctx.v);
        var targets = GetTargets(ctx);
        foreach (var t in targets)
        {
            if (t.currentHP <= 0) continue;
            var tTeam = ctx.enemyTeam.Contains(t) ? ctx.enemyTeam : ctx.sourceTeam;
            DealAbilityDamage(ctx.source, t, tTeam, ctx.sourceTeam, ctx.ab, dmg, false);
        }
    }

    // -------------------------------------------------------
    // ABILITY DAMAGE HELPER
    // Applies before-hit check, Battery boost, damageTakenMultiplier,
    // damage reduction, and logs result.
    // -------------------------------------------------------
    private static void DealAbilityDamage(PokemonInstance source, PokemonInstance target,
        List<PokemonInstance> targetTeam, List<PokemonInstance> sourceTeam,
        AbilityData ab, int baseDamage, bool applyTypeChart)
    {
        if (FireBeforeHit(target, source, true)) return;

        int dmg = baseDamage;

        if (applyTypeChart)
        {
            float typeMultiplier = TypeChart.GetMultiplier(source.baseData.type1, target.baseData.type1);
            dmg = Mathf.CeilToInt(dmg * typeMultiplier);
            string effectText = DamageCalculator.GetEffectivenessText(typeMultiplier);
            if (!string.IsNullOrEmpty(effectText)) Debug.Log(effectText.Trim());
            if (typeMultiplier == 0f) return;
        }

        // Battery: ally ability damage boost
        float abilityBoost = GetAllyAbilityDamageBoost(source, sourceTeam);
        dmg = Mathf.CeilToInt(dmg * abilityBoost);

        // damageTakenMultiplier — permanent (Cursed Aura etc.)
        dmg = Mathf.CeilToInt(dmg * target.damageTakenMultiplier);

        // nextHitDamageMultiplier — one-shot (Screech, Leer); consumed on this hit
        dmg = Mathf.CeilToInt(dmg * target.nextHitDamageMultiplier);
        target.nextHitDamageMultiplier = 1f;

        // Flat and percentage damage reduction
        dmg = Mathf.Max(0, dmg - GetFlatDamageReduction(target));
        float reduction = GetDamageReduction(target, true) * GetAllyDamageReduction(target, targetTeam);
        dmg = Mathf.CeilToInt(dmg * reduction);

        if (dmg > 0)
        {
            target.currentHP = Mathf.Max(0, target.currentHP - dmg);
            Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {dmg} to {target.DisplayName} ({target.currentHP}/{target.maxHP})");
            FireAfterHit(target, targetTeam);

            if (target.currentHP == 0)
            {
                Debug.Log($"{target.DisplayName} fainted from {ab.abilityName}!");
                FireOnFaint(target, targetTeam, sourceTeam);
                FireOnKill(source, sourceTeam, targetTeam);
            }
        }
    }
}
