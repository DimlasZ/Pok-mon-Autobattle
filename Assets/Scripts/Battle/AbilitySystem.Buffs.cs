using UnityEngine;

public static partial class AbilitySystem
{
    private static void HandleBoostAttack(EffectContext ctx)
    {
        // hp_below_X condition means one-shot (e.g. Power Construct)
        if (!string.IsNullOrEmpty(ctx.ab.condition) && ctx.ab.condition.StartsWith("hp_below_"))
        {
            if (_boostOnceApplied.Contains(ctx.source)) return;
            _boostOnceApplied.Add(ctx.source);
        }
        var targets = GetTargets(ctx);
        foreach (var tgt in targets)
        {
            int old = tgt.attack;
            tgt.attack = Mathf.RoundToInt(tgt.attack * ctx.v);
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {tgt.DisplayName} Attack {old} → {tgt.attack}");
        }
    }

    private static void HandleBoostAttackOnce(EffectContext ctx)
    {
        if (_boostOnceApplied.Contains(ctx.source)) return;
        _boostOnceApplied.Add(ctx.source);
        var targets = GetTargets(ctx);
        foreach (var tgt in targets)
        {
            int old = tgt.attack;
            tgt.attack = Mathf.RoundToInt(tgt.attack * ctx.v);
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {tgt.DisplayName} Attack {old} → {tgt.attack}!");
        }
    }

    private static void HandleBoostSpeed(EffectContext ctx)
    {
        var targets = GetTargets(ctx);
        foreach (var tgt in targets)
        {
            int old = tgt.speed;
            tgt.speed = Mathf.RoundToInt(tgt.speed * ctx.v);
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {tgt.DisplayName} Speed {old} → {tgt.speed}");
        }
    }

    private static void HandleBoostAllStats(EffectContext ctx)
    {
        var targets = GetTargets(ctx);
        foreach (var tgt in targets)
        {
            int hpGain    = Mathf.RoundToInt(tgt.maxHP * ctx.v) - tgt.maxHP;
            tgt.maxHP     = Mathf.RoundToInt(tgt.maxHP  * ctx.v);
            tgt.currentHP = Mathf.Min(tgt.currentHP + hpGain, tgt.maxHP);
            tgt.attack    = Mathf.RoundToInt(tgt.attack * ctx.v);
            tgt.speed     = Mathf.RoundToInt(tgt.speed  * ctx.v);
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {tgt.DisplayName} all stats x{ctx.v:F2}");
        }
    }

    private static void HandleBoostSpeedAttack(EffectContext ctx)
    {
        var targets = GetTargets(ctx);
        foreach (var tgt in targets)
        {
            int oldAtk = tgt.attack, oldSpd = tgt.speed;
            tgt.attack = Mathf.RoundToInt(tgt.attack * ctx.v);
            tgt.speed  = Mathf.RoundToInt(tgt.speed  * ctx.v);
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {tgt.DisplayName} Atk {oldAtk}→{tgt.attack}, Spd {oldSpd}→{tgt.speed}");
        }
    }

    private static void HandleBoostHp(EffectContext ctx)
    {
        var targets = GetTargets(ctx);
        foreach (var tgt in targets)
        {
            int old    = tgt.maxHP;
            int hpGain = Mathf.RoundToInt(tgt.maxHP * ctx.v) - tgt.maxHP;
            tgt.maxHP     = Mathf.RoundToInt(tgt.maxHP * ctx.v);
            tgt.currentHP = Mathf.Min(tgt.currentHP + hpGain, tgt.maxHP);
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {tgt.DisplayName} MaxHP {old} → {tgt.maxHP}");
        }
    }

    private static void HandleBoostDamageTaken(EffectContext ctx)
    {
        var targets = GetTargets(ctx);
        foreach (var tgt in targets)
        {
            float old = tgt.damageTakenMultiplier;
            tgt.damageTakenMultiplier *= ctx.v;
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {tgt.DisplayName} takes x{tgt.damageTakenMultiplier:F2} damage permanently (was x{old:F2})");
        }
    }

    private static void HandleBoostDamageTakenOnce(EffectContext ctx)
    {
        var targets = GetTargets(ctx);
        foreach (var tgt in targets)
        {
            float old = tgt.nextHitDamageMultiplier;
            tgt.nextHitDamageMultiplier *= ctx.v;
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {tgt.DisplayName} takes x{tgt.nextHitDamageMultiplier:F2} damage on next hit (was x{old:F2})");
        }
    }

    // -------------------------------------------------------
    // FLAT STAT BOOSTS — add a fixed number to the stat
    // -------------------------------------------------------

    private static void HandleBoostAttackFlat(EffectContext ctx)
    {
        int amount = Mathf.RoundToInt(ctx.v);
        var targets = GetTargets(ctx);
        foreach (var tgt in targets)
        {
            int old = tgt.attack;
            tgt.attack += amount;
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {tgt.DisplayName} Attack {old} → {tgt.attack} (+{amount})");
        }
    }

    private static void HandleBoostSpeedFlat(EffectContext ctx)
    {
        int amount = Mathf.RoundToInt(ctx.v);
        var targets = GetTargets(ctx);
        foreach (var tgt in targets)
        {
            int old = tgt.speed;
            tgt.speed += amount;
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {tgt.DisplayName} Speed {old} → {tgt.speed} (+{amount})");
        }
    }

    private static void HandleBoostHpFlat(EffectContext ctx)
    {
        int amount = Mathf.RoundToInt(ctx.v);
        var targets = GetTargets(ctx);
        foreach (var tgt in targets)
        {
            int old = tgt.maxHP;
            tgt.maxHP     += amount;
            tgt.currentHP  = Mathf.Min(tgt.currentHP + amount, tgt.maxHP);
            Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {tgt.DisplayName} MaxHP {old} → {tgt.maxHP} (+{amount})");
        }
    }

    // -------------------------------------------------------
    // PER-TYPE ALL-STAT BOOSTS — Scout / Rainbow Aura
    // value = multiplier applied once per unique type
    // e.g. value=1.1 with 3 unique types → total multiplier = 1.3 (additive +10% per type)
    // -------------------------------------------------------

    private static void HandleBoostAllStatsPerEnemyType(EffectContext ctx)
    {
        int uniqueTypes = CountUniqueTypes(ctx.enemyTeam);
        if (uniqueTypes == 0) return;
        float bonus = 1f + (ctx.v - 1f) * uniqueTypes;
        ApplyAllStatsMultiplier(ctx.source, ctx.ab, bonus);
        Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {uniqueTypes} unique enemy types → x{bonus:F2} to all stats");
    }

    private static void HandleBoostAllStatsPerAllyType(EffectContext ctx)
    {
        int uniqueTypes = CountUniqueTypes(ctx.sourceTeam);
        if (uniqueTypes == 0) return;
        float bonus = 1f + (ctx.v - 1f) * uniqueTypes;
        ApplyAllStatsMultiplier(ctx.source, ctx.ab, bonus);
        Debug.Log($"{ctx.source.DisplayName}'s {ctx.ab.abilityName}: {uniqueTypes} unique ally types → x{bonus:F2} to all stats");
    }

    private static int CountUniqueTypes(System.Collections.Generic.List<PokemonInstance> team)
    {
        var types = new System.Collections.Generic.HashSet<string>();
        foreach (var p in team)
            if (p.currentHP > 0 && !string.IsNullOrEmpty(p.baseData.type1))
                types.Add(p.baseData.type1);
        return types.Count;
    }

    private static void ApplyAllStatsMultiplier(PokemonInstance tgt, AbilityData ab, float mult)
    {
        int hpGain    = Mathf.RoundToInt(tgt.maxHP * mult) - tgt.maxHP;
        tgt.maxHP     = Mathf.RoundToInt(tgt.maxHP  * mult);
        tgt.currentHP = Mathf.Min(tgt.currentHP + hpGain, tgt.maxHP);
        tgt.attack    = Mathf.RoundToInt(tgt.attack * mult);
        tgt.speed     = Mathf.RoundToInt(tgt.speed  * mult);
    }
}
