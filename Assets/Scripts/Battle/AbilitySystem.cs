using UnityEngine;
using System.Collections.Generic;

// AbilitySystem fires ability effects at the right moment during battle.
// All methods are static — call them from BattleManager at each trigger point.
//
// VALUE RULES (per effect):
//   heal_self         : value < 2  → % of max HP  |  value >= 2 → flat HP
//   boost_attack      : value <= 4 → multiplier    |  value >  4 → flat bonus
//   damage_*          : always flat damage
//   damage_reduction  : value < 1  → multiplier (0.8 = take 80%)
//   boost_attack_enemy: always multiplier
//   recoil_damage     : always % of defender's max HP

public static class AbilitySystem
{
    // --- Battle State ---
    public static string ActiveWeather { get; private set; } = "";
    public static string ActiveScreen  { get; private set; } = "";

    private static readonly HashSet<PokemonInstance> _sturdyUsed    = new HashSet<PokemonInstance>();
    private static readonly HashSet<PokemonInstance> _shellArmorUsed = new HashSet<PokemonInstance>();

    public static void ResetBattleState()
    {
        ActiveWeather = "";
        ActiveScreen  = "";
        _sturdyUsed.Clear();
        _shellArmorUsed.Clear();
    }

    // -------------------------------------------------------
    // TRIGGER: on_battle_start
    // -------------------------------------------------------

    public static void FireBattleStart(List<PokemonInstance> team, List<PokemonInstance> enemyTeam)
    {
        foreach (var p in new List<PokemonInstance>(team))
        {
            var ab = p.baseData.ability;
            if (ab == null || ab.trigger != "on_battle_start") continue;
            if (!CheckCondition(p, ab.condition)) continue;
            FireEffect(p, team, enemyTeam, ab, 0, 0, null);
        }
    }

    // -------------------------------------------------------
    // TRIGGER: on_round_start
    // -------------------------------------------------------

    public static void FireRoundStart(List<PokemonInstance> team, List<PokemonInstance> enemyTeam)
    {
        foreach (var p in new List<PokemonInstance>(team))
        {
            if (p.currentHP <= 0) continue;
            var ab = p.baseData.ability;
            if (ab == null || ab.trigger != "on_round_start") continue;
            if (!CheckCondition(p, ab.condition)) continue;
            FireEffect(p, team, enemyTeam, ab, 0, 0, null);
        }
    }

    // -------------------------------------------------------
    // TRIGGER: before_attack
    // Returns a damage multiplier (1.0 = no change).
    // -------------------------------------------------------

    public static float FireBeforeAttack(PokemonInstance attacker, List<PokemonInstance> attackerTeam, List<PokemonInstance> defenderTeam)
    {
        var ab = attacker.baseData.ability;
        if (ab == null || ab.trigger != "before_attack") return 1f;
        if (!CheckCondition(attacker, ab.condition)) return 1f;

        if (ab.effect == "damage_multiplier")
            return ab.FloatValue;

        return 1f;
    }

    // -------------------------------------------------------
    // TRIGGER: on_attack
    // Called after damage is dealt. Handles splash effects.
    // -------------------------------------------------------

    public static void FireOnAttack(PokemonInstance attacker, PokemonInstance defender,
        int damageDealt, int excessDamage,
        List<PokemonInstance> attackerTeam, List<PokemonInstance> defenderTeam)
    {
        var ab = attacker.baseData.ability;
        if (ab == null || ab.trigger != "on_attack") return;
        if (!CheckCondition(attacker, ab.condition)) return;
        FireEffect(attacker, attackerTeam, defenderTeam, ab, damageDealt, excessDamage, defender);
    }

    // -------------------------------------------------------
    // TRIGGER: after_attack
    // Called after attack resolves. Handles self-heals.
    // -------------------------------------------------------

    public static void FireAfterAttack(PokemonInstance attacker, PokemonInstance defender,
        int damageDealt, List<PokemonInstance> attackerTeam, List<PokemonInstance> defenderTeam)
    {
        var ab = attacker.baseData.ability;
        if (ab == null || ab.trigger != "after_attack") return;
        FireEffect(attacker, attackerTeam, defenderTeam, ab, damageDealt, 0, defender);
    }

    // -------------------------------------------------------
    // TRIGGER: before_hit
    // Returns true if the hit should be fully negated (Shell Armor, immune_to_ability_damage).
    // -------------------------------------------------------

    public static bool FireBeforeHit(PokemonInstance defender, PokemonInstance attacker, bool isAbilityDamage)
    {
        var ab = defender.baseData.ability;
        if (ab == null) return false;

        // Shell Armor: negate the very first hit
        if (ab.trigger == "before_hit" && ab.effect == "negate_damage" && ab.condition == "first_hit")
        {
            if (!_shellArmorUsed.Contains(defender))
            {
                _shellArmorUsed.Add(defender);
                Debug.Log($"{defender.baseData.pokemonName}'s {ab.abilityName}: First hit negated!");
                return true;
            }
        }

        // Fur Coat / Battle Armor / Vital Spirit: immune to ability-sourced damage
        if (ab.effect == "immune_to_ability_damage" && isAbilityDamage)
        {
            Debug.Log($"{defender.baseData.pokemonName}'s {ab.abilityName}: Immune to ability damage!");
            return true;
        }

        return false;
    }

    // -------------------------------------------------------
    // TRIGGER: on_hit (defender receives damage)
    // Returns the potentially modified damage value.
    // -------------------------------------------------------

    public static int FireOnHit(PokemonInstance defender, PokemonInstance attacker, int damage,
        List<PokemonInstance> defenderTeam, List<PokemonInstance> attackerTeam)
    {
        var ab = defender.baseData.ability;
        if (ab == null || ab.trigger != "on_hit") return damage;

        // Sturdy: survive a KO hit when at full HP
        if (ab.effect == "survive_ko" && ab.condition == "at_full_hp")
        {
            if (!_sturdyUsed.Contains(defender) &&
                defender.currentHP == defender.maxHP &&
                damage >= defender.currentHP)
            {
                _sturdyUsed.Add(defender);
                damage = defender.currentHP - 1;
                Debug.Log($"{defender.baseData.pokemonName}'s {ab.abilityName}: Survived with 1 HP!");
            }
        }

        // Rest: fully heal when HP drops below 20% after this hit
        if (ab.effect == "heal_self" && ab.condition == "hp_below_20")
        {
            float postRatio = (float)(defender.currentHP - damage) / defender.maxHP;
            if (postRatio > 0f && postRatio < 0.2f)
            {
                Debug.Log($"{defender.baseData.pokemonName}'s {ab.abilityName}: Fully healed!");
                // Healing fires after damage is applied — handled post-damage
                // We flag it here by setting damage to not exceed HP - 1 and then heal below
            }
        }

        // Rough Skin: deal recoil damage back to the attacker
        if (ab.effect == "recoil_damage")
        {
            int recoil = Mathf.CeilToInt(defender.maxHP * ab.FloatValue);
            if (!FireBeforeHit(attacker, defender, true))
            {
                attacker.currentHP = Mathf.Max(0, attacker.currentHP - recoil);
                Debug.Log($"{defender.baseData.pokemonName}'s {ab.abilityName}: {attacker.baseData.pokemonName} takes {recoil} recoil!");
            }
        }

        return damage;
    }

    // Called AFTER damage is applied — handles Rest heal
    public static void FireAfterHit(PokemonInstance defender)
    {
        var ab = defender.baseData.ability;
        if (ab == null || ab.trigger != "on_hit") return;

        if (ab.effect == "heal_self" && ab.condition == "hp_below_20")
        {
            float ratio = (float)defender.currentHP / defender.maxHP;
            if (ratio > 0f && ratio < 0.2f)
            {
                defender.currentHP = defender.maxHP;
                Debug.Log($"{defender.baseData.pokemonName}'s {ab.abilityName}: Fully healed to {defender.maxHP} HP!");
            }
        }
    }

    // -------------------------------------------------------
    // TRIGGER: on_kill
    // -------------------------------------------------------

    public static void FireOnKill(PokemonInstance attacker,
        List<PokemonInstance> attackerTeam, List<PokemonInstance> defenderTeam)
    {
        var ab = attacker.baseData.ability;
        if (ab == null || ab.trigger != "on_kill") return;
        FireEffect(attacker, attackerTeam, defenderTeam, ab, 0, 0, null);
    }

    // -------------------------------------------------------
    // TRIGGER: on_faint
    // -------------------------------------------------------

    public static void FireOnFaint(PokemonInstance fainted,
        List<PokemonInstance> faintedTeam, List<PokemonInstance> opposingTeam)
    {
        var ab = fainted.baseData.ability;
        if (ab == null || ab.trigger != "on_faint") return;
        FireEffect(fainted, faintedTeam, opposingTeam, ab, 0, 0, null);
    }

    // -------------------------------------------------------
    // PASSIVE MODIFIERS — called inline during Attack() calculation
    // -------------------------------------------------------

    // Returns a damage multiplier from the attacker's conditional passive (e.g. Guts at low HP)
    public static float GetPassiveAttackMultiplier(PokemonInstance attacker)
    {
        var ab = attacker.baseData.ability;
        if (ab == null || ab.trigger != "passive") return 1f;
        if (!CheckCondition(attacker, ab.condition)) return 1f;

        float v = ab.FloatValue;
        if (ab.effect == "boost_attack" && v <= 4f)
            return v; // multiplicative: 1.25, 1.5, 2.0

        if (ab.effect == "damage_multiplier")
            return v;

        if (ab.effect == "ignore_damage_reduction")
            return 1f; // handled separately in Attack()

        return 1f;
    }

    // Returns a flat attack bonus from the attacker's passive (e.g. +20 from Chlorophyll in sun)
    public static int GetFlatAttackBonus(PokemonInstance attacker)
    {
        var ab = attacker.baseData.ability;
        if (ab == null || ab.trigger != "passive") return 0;
        if (!CheckCondition(attacker, ab.condition)) return 0;

        float v = ab.FloatValue;
        if (ab.effect == "boost_attack" && v > 4f)
            return (int)v; // flat: 20

        return 0;
    }

    // Returns damage multiplier from the defender's passive reduction (e.g. Thick Fat)
    public static float GetDamageReduction(PokemonInstance defender, bool isAbilityDamage = false)
    {
        var ab = defender.baseData.ability;
        if (ab == null) return 1f;

        if (ab.effect == "damage_reduction" && CheckCondition(defender, ab.condition))
            return ab.FloatValue; // 0.8 = take 80%

        if (ab.effect == "damage_reduction_abilities" && isAbilityDamage)
            return ab.FloatValue;

        return 1f;
    }

    // Returns flat damage reduction from the defender's passive (e.g. Bubble Shield -5)
    public static int GetFlatDamageReduction(PokemonInstance defender)
    {
        var ab = defender.baseData.ability;
        if (ab == null) return 0;

        if (ab.effect == "damage_reduction_flat" && CheckCondition(defender, ab.condition))
            return (int)ab.FloatValue;

        return 0;
    }

    // Returns damage multiplier from an ally's Friend Guard
    public static float GetAllyDamageReduction(PokemonInstance defender, List<PokemonInstance> defenderTeam)
    {
        foreach (var ally in defenderTeam)
        {
            if (ally == defender || ally.currentHP <= 0) continue;
            var ab = ally.baseData.ability;
            if (ab != null && ab.effect == "boost_defense_allies")
                return ab.FloatValue; // 0.9 = allies take 90%
        }
        return 1f;
    }

    // Returns true if the attacker's ability ignores all damage reduction (Aerial Ace)
    public static bool IgnoresDamageReduction(PokemonInstance attacker)
    {
        var ab = attacker.baseData.ability;
        return ab != null && ab.effect == "ignore_damage_reduction";
    }

    // -------------------------------------------------------
    // CORE EFFECT DISPATCHER
    // -------------------------------------------------------

    private static void FireEffect(PokemonInstance source,
        List<PokemonInstance> sourceTeam, List<PokemonInstance> enemyTeam,
        AbilityData ab, int contextDamage, int excessDamage, PokemonInstance target)
    {
        float v = ab.FloatValue;

        switch (ab.effect)
        {
            // --- HEALING ---

            case "heal_self":
            {
                int amount = v < 2f
                    ? Mathf.CeilToInt(source.maxHP * v)
                    : (int)v;
                int actual = Mathf.Min(amount, source.maxHP - source.currentHP);
                if (actual > 0)
                {
                    source.currentHP += actual;
                    Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: Healed {actual} HP ({source.currentHP}/{source.maxHP})");
                }
                break;
            }

            // --- DAMAGE TO ALL ENEMIES ---

            case "damage_all_enemies":
            {
                foreach (var enemy in new List<PokemonInstance>(enemyTeam))
                {
                    if (enemy.currentHP <= 0) continue;
                    if (FireBeforeHit(enemy, source, true)) continue;
                    int dmg = ApplyTypeMultiplier(source, enemy, (int)v);
                    dmg = Mathf.Max(0, dmg - GetFlatDamageReduction(enemy));
                    float red = GetDamageReduction(enemy, true) * GetAllyDamageReduction(enemy, enemyTeam);
                    dmg = Mathf.CeilToInt(dmg * red);
                    if (dmg > 0)
                    {
                        enemy.currentHP = Mathf.Max(0, enemy.currentHP - dmg);
                        Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: {dmg} to {enemy.baseData.pokemonName} ({enemy.currentHP}/{enemy.maxHP})");
                    }
                }
                break;
            }

            // --- DAMAGE TO ALL (including allies) ---

            case "damage_all":
            {
                var all = new List<PokemonInstance>(enemyTeam);
                all.AddRange(sourceTeam);
                foreach (var p in all)
                {
                    if (p == source || p.currentHP <= 0) continue;
                    // Levitate is immune to Earthquake
                    if (ab.abilityName == "Earthquake" && p.baseData.ability != null
                        && p.baseData.ability.effect == "immune_to_ground")
                    {
                        Debug.Log($"{p.baseData.pokemonName} is immune to Earthquake (Levitate)!");
                        continue;
                    }
                    bool isEnemy = enemyTeam.Contains(p);
                    List<PokemonInstance> pTeam = isEnemy ? enemyTeam : sourceTeam;
                    if (FireBeforeHit(p, source, true)) continue;
                    int dmg = ApplyTypeMultiplier(source, p, (int)v);
                    dmg = Mathf.Max(0, dmg - GetFlatDamageReduction(p));
                    float red = GetDamageReduction(p, true) * GetAllyDamageReduction(p, pTeam);
                    dmg = Mathf.CeilToInt(dmg * red);
                    if (dmg > 0)
                    {
                        p.currentHP = Mathf.Max(0, p.currentHP - dmg);
                        Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: {dmg} to {p.baseData.pokemonName} ({p.currentHP}/{p.maxHP})");
                    }
                }
                break;
            }

            // --- DAMAGE TO NEXT ENEMY ---

            case "damage_next_enemy":
            {
                var next = GetNextAlive(enemyTeam, target);
                if (next == null) break;
                if (FireBeforeHit(next, source, true)) break;
                int baseDmg = v < 1f ? Mathf.CeilToInt(source.attack * v) : (int)v;
                int dmg = ApplyTypeMultiplier(source, next, baseDmg);
                float red = GetDamageReduction(next, true) * GetAllyDamageReduction(next, enemyTeam);
                dmg = Mathf.Max(0, Mathf.CeilToInt((dmg - GetFlatDamageReduction(next)) * red));
                if (dmg > 0)
                {
                    next.currentHP = Mathf.Max(0, next.currentHP - dmg);
                    Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: {dmg} to {next.baseData.pokemonName} ({next.currentHP}/{next.maxHP})");
                }
                break;
            }

            // --- DAMAGE TO LAST ENEMY ---

            case "damage_last_enemy":
            {
                var last = GetLastAlive(enemyTeam);
                if (last == null || last == target) break;
                if (FireBeforeHit(last, source, true)) break;
                int baseDmg = v < 1f ? Mathf.CeilToInt(source.attack * v) : (int)v;
                int dmg = ApplyTypeMultiplier(source, last, baseDmg);
                float red = GetDamageReduction(last, true) * GetAllyDamageReduction(last, enemyTeam);
                dmg = Mathf.Max(0, Mathf.CeilToInt((dmg - GetFlatDamageReduction(last)) * red));
                if (dmg > 0)
                {
                    last.currentHP = Mathf.Max(0, last.currentHP - dmg);
                    Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: {dmg} to {last.baseData.pokemonName} ({last.currentHP}/{last.maxHP})");
                }
                break;
            }

            // --- DAMAGE TO ENEMY IN SAME POSITION (Quick Claw) ---

            case "damage_same_position":
            {
                int pos = sourceTeam.IndexOf(source);
                if (pos < 0 || pos >= enemyTeam.Count) break;
                var samePos = enemyTeam[pos];
                if (samePos == null || samePos.currentHP <= 0) break;
                if (FireBeforeHit(samePos, source, true)) break;
                int dmg = ApplyTypeMultiplier(source, samePos, Mathf.CeilToInt(source.attack * v));
                float red = GetDamageReduction(samePos, true) * GetAllyDamageReduction(samePos, enemyTeam);
                dmg = Mathf.Max(0, Mathf.CeilToInt((dmg - GetFlatDamageReduction(samePos)) * red));
                if (dmg > 0)
                {
                    samePos.currentHP = Mathf.Max(0, samePos.currentHP - dmg);
                    Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: {dmg} to {samePos.baseData.pokemonName} at same position ({samePos.currentHP}/{samePos.maxHP})");
                }
                break;
            }

            // --- OVERFLOW DAMAGE (Bone Club) ---

            case "overflow_damage_next":
            {
                if (excessDamage <= 0) break;
                var next = GetNextAlive(enemyTeam, target);
                if (next == null) break;
                if (FireBeforeHit(next, source, true)) break;
                next.currentHP = Mathf.Max(0, next.currentHP - excessDamage);
                Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: {excessDamage} overflow to {next.baseData.pokemonName} ({next.currentHP}/{next.maxHP})");
                break;
            }

            // --- ENEMY ATTACK DEBUFF (Intimidate, Thunder Wave) ---

            case "boost_attack_enemy":
            {
                PokemonInstance tgt = ab.condition == "last_enemy"
                    ? GetLastAlive(enemyTeam)
                    : GetFirstAlive(enemyTeam);
                if (tgt == null) break;
                int old = tgt.attack;
                tgt.attack = Mathf.Max(1, Mathf.RoundToInt(tgt.attack * v));
                Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: {tgt.baseData.pokemonName} attack {old} → {tgt.attack}");
                break;
            }

            // --- SELF ATTACK BUFF (Moxie on kill) ---

            case "boost_attack":
            {
                int old = source.attack;
                source.attack = v <= 4f
                    ? Mathf.RoundToInt(source.attack * v)
                    : source.attack + (int)v;
                Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: Attack {old} → {source.attack}");
                break;
            }

            // --- SELF SPEED BUFF ---

            case "boost_speed":
            {
                int old = source.speed;
                source.speed += (int)v;
                Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: Speed {old} → {source.speed}");
                break;
            }

            // --- ALLY ATTACK BUFF (Flower Gift in sun) ---

            case "boost_attack_all_allies":
            {
                foreach (var ally in sourceTeam)
                {
                    if (ally == source || ally.currentHP <= 0) continue;
                    ally.attack += (int)v;
                    Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: {ally.baseData.pokemonName} attack +{(int)v} → {ally.attack}");
                }
                break;
            }

            // --- SUMMON WEATHER ---

            case "summon_weather":
            {
                // Weather name is stored in value field ("rain", "sun", "sandstorm")
                ActiveWeather = ab.value;
                Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: Weather → {ActiveWeather}!");
                break;
            }

            // --- SUMMON SCREEN ---

            case "summon_screen":
            {
                ActiveScreen = ab.value; // "light_screen"
                Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: {ActiveScreen} activated!");
                break;
            }

            // --- SWAP ENEMIES (Stench, Whirlwind) ---

            case "swap_enemies":
            {
                if (ab.condition == "first_last" && enemyTeam.Count >= 2)
                {
                    var tmp = enemyTeam[0];
                    enemyTeam[0] = enemyTeam[enemyTeam.Count - 1];
                    enemyTeam[enemyTeam.Count - 1] = tmp;
                    Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: Swapped first and last enemy!");
                }
                else if (ab.condition == "last_two" && enemyTeam.Count >= 2)
                {
                    int c = enemyTeam.Count;
                    var tmp = enemyTeam[c - 1];
                    enemyTeam[c - 1] = enemyTeam[c - 2];
                    enemyTeam[c - 2] = tmp;
                    Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: Swapped last two enemies!");
                }
                break;
            }

            // --- MOVE ALLY TO FRONT (U-Turn) ---

            case "move_ally_to_front":
            {
                int idx = sourceTeam.IndexOf(source);
                PokemonInstance nextAlly = null;
                for (int i = idx + 1; i < sourceTeam.Count; i++)
                {
                    if (sourceTeam[i].currentHP > 0) { nextAlly = sourceTeam[i]; break; }
                }
                if (nextAlly != null)
                {
                    sourceTeam.Remove(nextAlly);
                    sourceTeam.Insert(0, nextAlly);
                    Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: {nextAlly.baseData.pokemonName} moved to front!");
                }
                break;
            }

            // --- MOODY: +15% attack, -10% speed ---

            case "moody":
            {
                int oldAtk = source.attack, oldSpd = source.speed;
                source.attack = Mathf.RoundToInt(source.attack * 1.15f);
                source.speed  = Mathf.Max(1, Mathf.RoundToInt(source.speed * 0.90f));
                Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: Attack {oldAtk}→{source.attack}, Speed {oldSpd}→{source.speed}");
                break;
            }

            // --- SOLAR POWER: +30% attack in sun, lose 5% HP per round ---

            case "solar_power":
            {
                if (ActiveWeather == "sun")
                {
                    source.attack = Mathf.RoundToInt(source.attack * 1.3f);
                    int drain = Mathf.CeilToInt(source.maxHP * 0.05f);
                    source.currentHP = Mathf.Max(1, source.currentHP - drain);
                    Debug.Log($"{source.baseData.pokemonName}'s {ab.abilityName}: +30% attack in sun, -{drain} HP ({source.currentHP}/{source.maxHP})");
                }
                break;
            }
        }
    }

    // -------------------------------------------------------
    // CONDITION CHECKER
    // -------------------------------------------------------

    private static bool CheckCondition(PokemonInstance p, string condition)
    {
        if (string.IsNullOrEmpty(condition)) return true;

        switch (condition)
        {
            case "hp_below_20":       return (float)p.currentHP / p.maxHP < 0.20f;
            case "hp_below_33":       return (float)p.currentHP / p.maxHP < 0.33f;
            case "hp_below_50":       return (float)p.currentHP / p.maxHP < 0.50f;
            case "at_full_hp":        return p.currentHP == p.maxHP;
            case "weather_sun":       return ActiveWeather == "sun";
            case "weather_rain":      return ActiveWeather == "rain";
            case "weather_sandstorm": return ActiveWeather == "sandstorm";
            case "weather_hail":      return ActiveWeather == "hail";
            // Targeting conditions are handled inside FireEffect, not here
            case "first_enemy":
            case "last_enemy":
            case "first_last":
            case "last_two":
            case "first_hit":
            case "light_screen":      return true;
            default:                  return true;
        }
    }

    // -------------------------------------------------------
    // TYPE EFFECTIVENESS HELPER
    // -------------------------------------------------------

    // Applies type multiplier from source's type against a single target
    private static int ApplyTypeMultiplier(PokemonInstance source, PokemonInstance target, int baseDamage)
    {
        float multiplier = TypeChart.GetMultiplier(source.baseData.type1, target.baseData.type1);
        int result = Mathf.CeilToInt(baseDamage * multiplier);
        if (multiplier >= 2f)  Debug.Log($"It's super effective! (x{multiplier})");
        if (multiplier <= 0.5f && multiplier > 0f) Debug.Log($"It's not very effective... (x{multiplier})");
        if (multiplier == 0f) { Debug.Log("It has no effect!"); return 0; }
        return result;
    }

    // -------------------------------------------------------
    // TEAM HELPERS
    // -------------------------------------------------------

    private static PokemonInstance GetFirstAlive(List<PokemonInstance> team)
    {
        foreach (var p in team) if (p.currentHP > 0) return p;
        return null;
    }

    private static PokemonInstance GetLastAlive(List<PokemonInstance> team)
    {
        for (int i = team.Count - 1; i >= 0; i--)
            if (team[i].currentHP > 0) return team[i];
        return null;
    }

    private static PokemonInstance GetNextAlive(List<PokemonInstance> team, PokemonInstance current)
    {
        if (current == null) return GetFirstAlive(team);
        int idx = team.IndexOf(current);
        for (int i = idx + 1; i < team.Count; i++)
            if (team[i].currentHP > 0) return team[i];
        return null;
    }
}
