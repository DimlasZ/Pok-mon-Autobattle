using UnityEngine;
using System.Collections.Generic;

public static partial class AbilitySystem
{
    private static void HandleReduceAttack(EffectContext ctx)
    {
        var targets = GetTargets(ctx);
        foreach (var tgt in targets)
        {
            var tTeam   = ctx.enemyTeam.Contains(tgt) ? ctx.enemyTeam : ctx.sourceTeam;
            var oppTeam = ctx.enemyTeam.Contains(tgt) ? ctx.sourceTeam : ctx.enemyTeam;
            ApplyDebuff(tgt, tTeam, oppTeam, "attack", ctx.ab);
        }
    }

    private static void HandleReduceSpeed(EffectContext ctx)
    {
        var targets = GetTargets(ctx);
        foreach (var tgt in targets)
        {
            var tTeam   = ctx.enemyTeam.Contains(tgt) ? ctx.enemyTeam : ctx.sourceTeam;
            var oppTeam = ctx.enemyTeam.Contains(tgt) ? ctx.sourceTeam : ctx.enemyTeam;
            ApplyDebuff(tgt, tTeam, oppTeam, "speed", ctx.ab);
        }
    }

    // -------------------------------------------------------
    // DEBUFF HELPER — checks immunity, applies stat change, fires on_debuff
    // -------------------------------------------------------
    private static void ApplyDebuff(PokemonInstance target, List<PokemonInstance> targetTeam,
        List<PokemonInstance> opponentTeam, string stat, AbilityData ab)
    {
        if (target.currentHP <= 0) return;

        if (IsImmuneToDebuff(target, targetTeam))
        {
            Debug.Log($"{target.DisplayName} is immune to stat reduction ({ab.abilityName})!");
            return;
        }

        float v = ab.FloatValue;
        if (stat == "attack")
        {
            int old = target.attack;
            target.attack = Mathf.Max(1, Mathf.RoundToInt(target.attack * v));
            Debug.Log($"{target.DisplayName}'s Attack: {old} → {target.attack} ({ab.abilityName})");
        }
        else if (stat == "speed")
        {
            int old = target.speed;
            target.speed = Mathf.Max(1, Mathf.RoundToInt(target.speed * v));
            Debug.Log($"{target.DisplayName}'s Speed: {old} → {target.speed} ({ab.abilityName})");
        }

        FireOnDebuff(target);
    }

    private static bool IsImmuneToDebuff(PokemonInstance target, List<PokemonInstance> targetTeam)
    {
        // Self immunity (Hyper Cutter)
        var ab = target.baseData.ability;
        if (ab != null && ab.trigger == "passive" && ab.effect == "immune_to_debuff") return true;

        // Ally-wide immunity (Aroma Veil, Flower Veil)
        if (targetTeam != null)
        {
            foreach (var ally in targetTeam)
            {
                if (ally == target || ally.currentHP <= 0) continue;
                var allyAb = ally.baseData.ability;
                if (allyAb != null && allyAb.trigger == "passive"
                    && allyAb.target == "ally_all" && allyAb.effect == "immune_to_debuff")
                    return true;
            }
        }
        return false;
    }
}
