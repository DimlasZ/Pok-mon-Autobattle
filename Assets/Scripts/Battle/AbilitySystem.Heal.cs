using UnityEngine;
using System.Collections.Generic;

public static partial class AbilitySystem
{
    private static void HandleHeal(EffectContext ctx)
    {
        var targets = GetTargets(ctx);
        foreach (var t in targets)
            if (t.currentHP > 0) ApplyHeal(ctx.source, t, ctx.ab, ctx.sourceTeam, ctx.enemyTeam);
    }

    private static void HandleHealFlat(EffectContext ctx)
    {
        var targets = GetTargets(ctx);
        foreach (var t in targets)
            if (t.currentHP > 0) ApplyHealFlat(ctx.source, t, ctx.ab, ctx.sourceTeam, ctx.enemyTeam);
    }

    private static void HandleHealDamageDealt(EffectContext ctx)
    {
        if (ctx.contextDamage <= 0) return;
        int amount = Mathf.CeilToInt(ctx.contextDamage * ctx.v);
        amount = ApplyHealModifiers(ctx.source, amount, ctx.enemyTeam);

        int actual = Mathf.Min(amount, ctx.source.maxHP - ctx.source.currentHP);
        if (actual > 0)
        {
            ctx.source.currentHP += actual;
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: Healed {actual} HP from damage dealt ({ctx.source.currentHP}/{ctx.source.maxHP})");
            FireOnHealTrigger(ctx.source, ctx.sourceTeam, ctx.enemyTeam);
            FireOnAllyHeal(ctx.source, ctx.sourceTeam, ctx.enemyTeam);
        }
    }

    private static void HandleLeech(EffectContext ctx)
    {
        var targets = GetTargets(ctx);
        foreach (var t in targets)
            if (t.currentHP > 0) ApplyHeal(ctx.source, ctx.source, ctx.ab, ctx.sourceTeam, ctx.enemyTeam, t.maxHP);
    }

    // Percentage heal: v is a fraction of hpPool (0.3 = 30% of max HP)
    private static void ApplyHeal(PokemonInstance healer, PokemonInstance recipient,
        AbilityData ab, List<PokemonInstance> recipientTeam, List<PokemonInstance> opponentTeam,
        int hpPool = -1)
    {
        float v      = ab.FloatValue;
        int   pool   = hpPool >= 0 ? hpPool : recipient.maxHP;
        int   amount = Mathf.CeilToInt(pool * v);
        amount = ApplyHealModifiers(recipient, amount, opponentTeam);

        int actual = Mathf.Min(amount, recipient.maxHP - recipient.currentHP);
        if (actual > 0)
        {
            recipient.currentHP += actual;
            Debug.Log($"{healer.DisplayName}'s {ab.abilityName}: Healed {recipient.DisplayName} {actual} HP ({recipient.currentHP}/{recipient.maxHP})");
            FireOnHealTrigger(recipient, recipientTeam, opponentTeam);
            FireOnAllyHeal(recipient, recipientTeam, opponentTeam);
        }
    }

    // Flat heal: amount is a fixed HP value from ab.FloatValue
    private static void ApplyHealFlat(PokemonInstance healer, PokemonInstance recipient,
        AbilityData ab, List<PokemonInstance> recipientTeam, List<PokemonInstance> opponentTeam)
    {
        int amount = (int)ab.FloatValue;
        amount = ApplyHealModifiers(recipient, amount, opponentTeam);

        int actual = Mathf.Min(amount, recipient.maxHP - recipient.currentHP);
        if (actual > 0)
        {
            recipient.currentHP += actual;
            Debug.Log($"{healer.DisplayName}'s {ab.abilityName}: Healed {recipient.DisplayName} {actual} HP ({recipient.currentHP}/{recipient.maxHP})");
            FireOnHealTrigger(recipient, recipientTeam, opponentTeam);
            FireOnAllyHeal(recipient, recipientTeam, opponentTeam);
        }
    }

    // Applies boost_healing (Wish, Serene Grace) and reduce_healing (Unnerve) modifiers
    private static int ApplyHealModifiers(PokemonInstance recipient, int amount, List<PokemonInstance> opponentTeam)
    {
        // Self boost_healing passive
        var rAb = recipient.baseData.ability;
        if (rAb != null && rAb.trigger == "passive" && rAb.target == "self" && rAb.effect == "boost_healing")
            amount = Mathf.RoundToInt(amount * rAb.FloatValue);

        // Ally-wide boost_healing (Serene Grace, Cheek Pouch)
        List<PokemonInstance> recipientTeam = GetTeamOf(recipient);
        if (recipientTeam != null)
        {
            foreach (var ally in recipientTeam)
            {
                if (ally == recipient || ally.currentHP <= 0) continue;
                var allyAb = ally.baseData.ability;
                if (allyAb != null && allyAb.target == "ally_all" && allyAb.effect == "boost_healing")
                    amount = Mathf.RoundToInt(amount * allyAb.FloatValue);
            }
        }

        // Unnerve (reduce_healing passive on opponents)
        if (opponentTeam != null)
        {
            foreach (var opp in opponentTeam)
            {
                if (opp.currentHP <= 0) continue;
                var oppAb = opp.baseData.ability;
                if (oppAb != null && oppAb.trigger == "passive" && oppAb.effect == "reduce_healing")
                    amount = Mathf.RoundToInt(amount * oppAb.FloatValue);
            }
        }

        return amount;
    }
}
