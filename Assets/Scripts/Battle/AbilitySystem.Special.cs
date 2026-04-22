using UnityEngine;

public static partial class AbilitySystem
{
    private static void HandleSwapEnemies(EffectContext ctx)
    {
        if (ctx.ab.condition == "first_last" && ctx.enemyTeam.Count >= 2)
        {
            var tmp = ctx.enemyTeam[0];
            ctx.enemyTeam[0] = ctx.enemyTeam[ctx.enemyTeam.Count - 1];
            ctx.enemyTeam[ctx.enemyTeam.Count - 1] = tmp;
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: Swapped first and last enemy!");
        }
    }

    private static void HandleSwapEnemiesRandom(EffectContext ctx)
    {
        int n = ctx.enemyTeam.Count;
        if (n < 2) return;
        // Fisher-Yates shuffle
        for (int i = n - 1; i > 0; i--)
        {
            int j = _rng.Next(0, i + 1);
            var tmp = ctx.enemyTeam[i];
            ctx.enemyTeam[i] = ctx.enemyTeam[j];
            ctx.enemyTeam[j] = tmp;
        }
        Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: Shuffled all enemy positions!");
    }

    private static void HandleMoveAllyToFront(EffectContext ctx)
    {
        int idx = ctx.sourceTeam.IndexOf(ctx.source);
        PokemonInstance nextAlly = null;
        for (int i = idx + 1; i < ctx.sourceTeam.Count; i++)
            if (ctx.sourceTeam[i].currentHP > 0) { nextAlly = ctx.sourceTeam[i]; break; }
        if (nextAlly != null)
        {
            ctx.sourceTeam.Remove(nextAlly);
            ctx.sourceTeam.Insert(0, nextAlly);
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {nextAlly.DisplayName} moved to front!");
        }
    }

    private static void HandleCureStatus(EffectContext ctx)
    {
        Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: Status cleared.");
    }

    private static void HandleMoody(EffectContext ctx)
    {
        int oldAtk = ctx.source.attack, oldSpd = ctx.source.speed;
        ctx.source.attack = Mathf.RoundToInt(ctx.source.attack * 1.15f);
        ctx.source.speed  = Mathf.Max(1, Mathf.RoundToInt(ctx.source.speed * 0.90f));
        Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: Attack {oldAtk}→{ctx.source.attack}, Speed {oldSpd}→{ctx.source.speed}");
    }

    private static void HandleSolarPower(EffectContext ctx)
    {
        if (IsWeatherActive("sun"))
        {
            ctx.source.attack    = Mathf.RoundToInt(ctx.source.attack * ctx.v);
            int drain            = Mathf.CeilToInt(ctx.source.maxHP * 0.05f);
            ctx.source.currentHP = Mathf.Max(1, ctx.source.currentHP - drain);
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: +{(int)((ctx.v - 1) * 100)}% attack, -{drain} HP");
        }
    }

    // Baton Pass: on faint, transfer positive attack/speed multipliers to the next ally.
    private static void HandleTransferBuffs(EffectContext ctx)
    {
        var targets = GetTargets(ctx);
        if (targets.Count == 0) return;

        float atkMult = ctx.source.baseAttack > 0 ? (float)ctx.source.attack / ctx.source.baseAttack : 1f;
        float spdMult = ctx.source.baseSpeed  > 0 ? (float)ctx.source.speed  / ctx.source.baseSpeed  : 1f;

        if (atkMult <= 1f && spdMult <= 1f) return;

        foreach (var t in targets)
        {
            if (atkMult > 1f) t.attack = Mathf.RoundToInt(t.attack * atkMult);
            if (spdMult > 1f) t.speed  = Mathf.RoundToInt(t.speed  * spdMult);
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: Passed buffs to {t.DisplayName} (atk x{atkMult:F2}, spd x{spdMult:F2})");
        }
    }
}
