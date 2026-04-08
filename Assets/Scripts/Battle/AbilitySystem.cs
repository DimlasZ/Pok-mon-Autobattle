using UnityEngine;
using System.Collections.Generic;

// AbilitySystem fires ability effects at the right trigger points during battle.
// All methods are static — called from BattleManager / BattleSceneManager.
//
// Flow per trigger:
//   1. Check trigger matches
//   2. Check condition passes
//   3. Check chance passes
//   4. Resolve targets
//   5. Apply effect

public static class AbilitySystem
{
    // -------------------------------------------------------
    // BATTLE STATE
    // -------------------------------------------------------

    public static string ActiveWeather { get; private set; } = "";
    public static string ActiveScreen  { get; private set; } = "";

    private static readonly HashSet<PokemonInstance> _sturdyUsed      = new HashSet<PokemonInstance>();
    private static readonly HashSet<PokemonInstance> _shellArmorUsed  = new HashSet<PokemonInstance>();
    private static readonly HashSet<PokemonInstance> _boostOnceApplied = new HashSet<PokemonInstance>();
    private static readonly Dictionary<PokemonInstance, int> _allyFaintCount = new Dictionary<PokemonInstance, int>();

    public static void ResetBattleState()
    {
        ActiveWeather = "";
        ActiveScreen  = "";
        _sturdyUsed.Clear();
        _shellArmorUsed.Clear();
        _boostOnceApplied.Clear();
        _allyFaintCount.Clear();
    }

    // -------------------------------------------------------
    // TRIGGERS
    // -------------------------------------------------------

    public static void FireBattleStart(List<PokemonInstance> team, List<PokemonInstance> enemyTeam)
    {
        foreach (var p in new List<PokemonInstance>(team))
            TryFire("on_battle_start", p, team, enemyTeam, 0, 0, null);
    }

    public static void FireRoundStart(List<PokemonInstance> team, List<PokemonInstance> enemyTeam)
    {
        foreach (var p in new List<PokemonInstance>(team))
        {
            if (p.currentHP <= 0) continue;
            TryFire("on_round_start", p, team, enemyTeam, 0, 0, null);
        }
    }

    // Returns a damage multiplier (1.0 = no change)
    public static float FireBeforeAttack(PokemonInstance attacker, List<PokemonInstance> attackerTeam, List<PokemonInstance> defenderTeam)
    {
        var ab = attacker.baseData.ability;
        if (ab == null || ab.trigger != "before_attack") return 1f;
        if (!CheckCondition(attacker, ab.condition)) return 1f;
        if (!ab.ShouldTrigger()) return 1f;

        return 1f;
    }

    public static void FireOnAttack(PokemonInstance attacker, PokemonInstance defender,
        int damageDealt, int excessDamage,
        List<PokemonInstance> attackerTeam, List<PokemonInstance> defenderTeam)
    {
        TryFire("on_attack", attacker, attackerTeam, defenderTeam, damageDealt, excessDamage, defender);
    }

    public static void FireAfterAttack(PokemonInstance attacker, PokemonInstance defender,
        int damageDealt, List<PokemonInstance> attackerTeam, List<PokemonInstance> defenderTeam)
    {
        TryFire("after_attack", attacker, attackerTeam, defenderTeam, damageDealt, 0, defender);
    }

    // Returns true if the hit should be fully negated
    public static bool FireBeforeHit(PokemonInstance defender, PokemonInstance attacker, bool isAbilityDamage)
    {
        var ab = defender.baseData.ability;
        if (ab == null) return false;

        if (ab.trigger == "before_hit" && ab.effect == "negate_damage" && ab.condition == "first_hit")
        {
            if (!_shellArmorUsed.Contains(defender))
            {
                _shellArmorUsed.Add(defender);
                Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: First hit negated!");
                return true;
            }
        }

        if (ab.effect == "immune_to_ability_damage" && isAbilityDamage)
        {
            Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: Immune to ability damage!");
            return true;
        }

        return false;
    }

    // Returns potentially modified incoming damage
    public static int FireOnHit(PokemonInstance defender, PokemonInstance attacker, int damage,
        List<PokemonInstance> defenderTeam, List<PokemonInstance> attackerTeam)
    {
        var ab = defender.baseData.ability;
        if (ab == null || ab.trigger != "on_hit") return damage;
        if (!CheckCondition(defender, ab.condition)) return damage;
        if (!ab.ShouldTrigger()) return damage;

        switch (ab.effect)
        {
            case "survive_ko":
                if (!_sturdyUsed.Contains(defender) &&
                    defender.currentHP == defender.maxHP &&
                    damage >= defender.currentHP)
                {
                    _sturdyUsed.Add(defender);
                    damage = defender.currentHP - 1;
                    Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: Survived with 1 HP!");
                }
                break;

            case "heal":
                // heal fires after damage is applied — handled in FireAfterHit
                break;

            case "recoil_damage":
                int recoil = Mathf.CeilToInt(defender.maxHP * ab.FloatValue);
                if (!FireBeforeHit(attacker, defender, true))
                {
                    attacker.currentHP = Mathf.Max(0, attacker.currentHP - recoil);
                    Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: {attacker.DisplayName} takes {recoil} recoil!");
                }
                break;

            case "reduce_attack":
                int oldAtk = attacker.attack;
                attacker.attack = Mathf.Max(1, Mathf.RoundToInt(attacker.attack * ab.FloatValue));
                Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: {attacker.DisplayName} Attack {oldAtk} → {attacker.attack}");
                break;

            case "reduce_speed":
                int oldSpd = attacker.speed;
                attacker.speed = Mathf.Max(1, Mathf.RoundToInt(attacker.speed * ab.FloatValue));
                Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: {attacker.DisplayName} Speed {oldSpd} → {attacker.speed}");
                break;

            case "boost_attack":
                // raise all allies' attack when hit and surviving (e.g. ability 35)
                break;

            case "boost_speed":
                // e.g. Weak Armor — speed boost on super-effective hit
                if (ab.condition == "super_effective")
                {
                    int old = defender.speed;
                    defender.speed = Mathf.RoundToInt(defender.speed * ab.FloatValue);
                    Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: Speed {old} → {defender.speed}");
                }
                break;
        }

        return damage;
    }

    public static void FireAfterHit(PokemonInstance defender, List<PokemonInstance> defenderTeam)
    {
        var ab = defender.baseData.ability;
        if (ab == null || ab.trigger != "on_hit") return;
        if (!ab.ShouldTrigger()) return;

        // heal effect on on_hit trigger (e.g. Rest — heals after damage lands)
        if (ab.effect == "heal")
        {
            if (!CheckCondition(defender, ab.condition)) return;
            ApplyHeal(defender, defender, ab);
        }

        // Raise all allies' attack when hit and surviving
        if (ab.effect == "boost_attack" && ab.target == "ally_all" && defender.currentHP > 0)
        {
            foreach (var ally in defenderTeam)
            {
                if (ally == defender || ally.currentHP <= 0) continue;
                int old = ally.attack;
                ally.attack = Mathf.RoundToInt(ally.attack * ab.FloatValue);
                Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: {ally.DisplayName} Attack {old} → {ally.attack}");
            }
        }

        // One-time attack boost when HP first drops below threshold (Blaze, Torrent, Overgrow)
        if (ab.effect == "boost_attack_once" && defender.currentHP > 0)
        {
            if (!_boostOnceApplied.Contains(defender) && CheckCondition(defender, ab.condition))
            {
                _boostOnceApplied.Add(defender);
                int old = defender.attack;
                defender.attack = Mathf.RoundToInt(defender.attack * ab.FloatValue);
                Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: Attack {old} → {defender.attack}!");
            }
        }
    }

    public static void FireOnKill(PokemonInstance attacker,
        List<PokemonInstance> attackerTeam, List<PokemonInstance> defenderTeam)
    {
        TryFire("on_kill", attacker, attackerTeam, defenderTeam, 0, 0, null);
    }

    public static void FireOnFaint(PokemonInstance fainted,
        List<PokemonInstance> faintedTeam, List<PokemonInstance> opposingTeam)
    {
        TryFire("on_faint", fainted, faintedTeam, opposingTeam, 0, 0, null);

        // Notify all surviving allies on the same team
        foreach (var ally in new List<PokemonInstance>(faintedTeam))
        {
            if (ally == fainted || ally.currentHP <= 0) continue;
            var ab = ally.baseData.ability;
            if (ab == null || ab.trigger != "on_ally_faint") continue;
            if (!ab.ShouldTrigger()) continue;

            if (!_allyFaintCount.ContainsKey(ally)) _allyFaintCount[ally] = 0;
            _allyFaintCount[ally]++;

            ApplyEffect(ally, faintedTeam, opposingTeam, ab, 0, 0, null);
        }
    }

    // -------------------------------------------------------
    // PASSIVE QUERIES — called inline during damage calculation
    // -------------------------------------------------------

    public static float GetPassiveAttackMultiplier(PokemonInstance attacker)
    {
        var ab = attacker.baseData.ability;
        if (ab == null || ab.trigger != "passive") return 1f;
        if (!CheckCondition(attacker, ab.condition)) return 1f;

        switch (ab.effect)
        {
            case "boost_attack" when ab.FloatValue <= 4f: return ab.FloatValue;
        }
        return 1f;
    }

    public static int GetFlatAttackBonus(PokemonInstance attacker)
    {
        var ab = attacker.baseData.ability;
        if (ab == null || ab.trigger != "passive") return 0;
        if (!CheckCondition(attacker, ab.condition)) return 0;

        if (ab.effect == "boost_attack" && ab.FloatValue > 4f)
            return (int)ab.FloatValue;

        return 0;
    }

    public static float GetDamageReduction(PokemonInstance defender, bool isAbilityDamage = false)
    {
        var ab = defender.baseData.ability;
        if (ab == null) return 1f;

        // Water Sport — only reduces Fire-type incoming damage, handled via GetTypeBasedDamageReduction
        if (ab.custom == "water_sport") return 1f;

        if (ab.effect == "damage_reduction" && CheckCondition(defender, ab.condition))
            return ab.FloatValue;

        if (ab.effect == "damage_reduction_abilities" && isAbilityDamage)
            return ab.FloatValue;

        return 1f;
    }

    // Returns a damage reduction multiplier based on the attacker's type (e.g. Water Sport vs Fire)
    public static float GetTypeBasedDamageReduction(PokemonInstance defender, string attackerType)
    {
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
        var ab = defender.baseData.ability;
        if (ab == null) return 0;

        if (ab.effect == "damage_reduction_flat" && CheckCondition(defender, ab.condition))
            return (int)ab.FloatValue;

        return 0;
    }

    public static float GetAllyDamageReduction(PokemonInstance defender, List<PokemonInstance> defenderTeam)
    {
        foreach (var ally in defenderTeam)
        {
            if (ally == defender || ally.currentHP <= 0) continue;
            var ab = ally.baseData.ability;
            if (ab != null && ab.effect == "boost_defense_allies")
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
        return ab != null && ab.effect == "immune_to_ground";
    }

    // -------------------------------------------------------
    // CORE DISPATCHER
    // -------------------------------------------------------

    private static void TryFire(string trigger, PokemonInstance source,
        List<PokemonInstance> sourceTeam, List<PokemonInstance> enemyTeam,
        int contextDamage, int excessDamage, PokemonInstance contextTarget)
    {
        var ab = source.baseData.ability;
        if (ab == null || ab.trigger != trigger) return;
        if (!CheckCondition(source, ab.condition)) return;
        if (!ab.ShouldTrigger()) return;

        ApplyEffect(source, sourceTeam, enemyTeam, ab, contextDamage, excessDamage, contextTarget);
    }

    private static void ApplyEffect(PokemonInstance source,
        List<PokemonInstance> sourceTeam, List<PokemonInstance> enemyTeam,
        AbilityData ab, int contextDamage, int excessDamage, PokemonInstance contextTarget)
    {
        float v = ab.FloatValue;

        switch (ab.effect)
        {
            // --- HEAL (target = who gets healed) ---
            // self      → heal user from own max HP
            // ally_all  → heal all allies from their own max HP
            case "heal":
            {
                switch (ab.target)
                {
                    case "self":
                        ApplyHeal(source, source, ab);
                        break;
                    case "ally_all":
                        foreach (var ally in sourceTeam)
                        {
                            if (ally.currentHP <= 0) continue;
                            ApplyHeal(source, ally, ab);
                        }
                        break;
                }
                break;
            }

            // --- LEECH (drain from target's max HP, heal goes to user) ---
            // enemy_front → drain from front enemy's max HP, heal self
            // enemy_all   → drain from all enemies' max HP, heal self
            case "leech":
            {
                switch (ab.target)
                {
                    case "enemy_front":
                    {
                        var t = contextTarget ?? GetFirstAlive(enemyTeam);
                        if (t != null) ApplyHeal(source, source, ab, t.maxHP);
                        break;
                    }
                    case "enemy_all":
                    {
                        foreach (var t in enemyTeam)
                        {
                            if (t.currentHP <= 0) continue;
                            ApplyHeal(source, source, ab, t.maxHP);
                        }
                        break;
                    }
                }
                break;
            }

            // --- DEAL DAMAGE (generic — target resolved from ab.target, type chart applied) ---
            case "deal_damage":
            {
                int dmg = v < 1f ? Mathf.CeilToInt(source.attack * v) : (int)v;
                switch (ab.target)
                {
                    case "enemy_front":
                    {
                        var t = GetFirstAlive(enemyTeam);
                        if (t != null) DealAbilityDamage(source, t, enemyTeam, ab, dmg, true);
                        break;
                    }
                    case "enemy_next":
                    {
                        var t = GetNextAlive(enemyTeam, contextTarget);
                        if (t != null) DealAbilityDamage(source, t, enemyTeam, ab, dmg, true);
                        break;
                    }
                    case "enemy_last":
                    {
                        var t = GetLastAlive(enemyTeam);
                        if (t != null && t != contextTarget) DealAbilityDamage(source, t, enemyTeam, ab, dmg, true);
                        break;
                    }
                    case "enemy_all":
                    {
                        foreach (var t in new List<PokemonInstance>(enemyTeam))
                            if (t.currentHP > 0) DealAbilityDamage(source, t, enemyTeam, ab, dmg, true);
                        break;
                    }
                    case "all_others":
                    {
                        var all = new List<PokemonInstance>(enemyTeam);
                        all.AddRange(sourceTeam);
                        foreach (var t in all)
                        {
                            if (t == source || t.currentHP <= 0) continue;
                            bool isEnemy = enemyTeam.Contains(t);
                            DealAbilityDamage(source, t, isEnemy ? enemyTeam : sourceTeam, ab, dmg, true);
                        }
                        break;
                    }
                    case "all":
                    {
                        var all = new List<PokemonInstance>(enemyTeam);
                        all.AddRange(sourceTeam);
                        foreach (var t in all)
                        {
                            if (t.currentHP <= 0) continue;
                            bool isEnemy = enemyTeam.Contains(t);
                            DealAbilityDamage(source, t, isEnemy ? enemyTeam : sourceTeam, ab, dmg, true);
                        }
                        break;
                    }
                }
                break;
            }

            // --- OVERFLOW DAMAGE (Bone Club) ---
            case "overflow_damage_next":
            {
                if (excessDamage <= 0) break;
                var next = GetNextAlive(enemyTeam, contextTarget);
                if (next == null) break;
                if (FireBeforeHit(next, source, true)) break;
                next.currentHP = Mathf.Max(0, next.currentHP - excessDamage);
                Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {excessDamage} overflow to {next.DisplayName} ({next.currentHP}/{next.maxHP})");
                break;
            }

            // --- BOOST ATTACK (target-driven) ---
            case "boost_attack":
            {
                var targets = ResolveTargets(ab.target, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var tgt in targets)
                {
                    int old = tgt.attack;
                    tgt.attack = v <= 4f
                        ? Mathf.RoundToInt(tgt.attack * v)
                        : tgt.attack + (int)v;
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {tgt.DisplayName} Attack {old} → {tgt.attack}");
                }
                break;
            }

            // --- BOOST SPEED (target-driven) ---
            case "boost_speed":
            {
                var targets = ResolveTargets(ab.target, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var tgt in targets)
                {
                    int old = tgt.speed;
                    tgt.speed = v <= 4f
                        ? Mathf.RoundToInt(tgt.speed * v)
                        : tgt.speed + (int)v;
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {tgt.DisplayName} Speed {old} → {tgt.speed}");
                }
                break;
            }

            // --- REDUCE ATTACK (target-driven) ---
            case "reduce_attack":
            {
                var targets = ResolveTargets(ab.target, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var tgt in targets)
                {
                    int old = tgt.attack;
                    tgt.attack = Mathf.Max(1, Mathf.RoundToInt(tgt.attack * v));
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {tgt.DisplayName} Attack {old} → {tgt.attack}");
                }
                break;
            }

            // --- REDUCE SPEED (target-driven) ---
            case "reduce_speed":
            {
                var targets = ResolveTargets(ab.target, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var tgt in targets)
                {
                    int old = tgt.speed;
                    tgt.speed = Mathf.Max(1, Mathf.RoundToInt(tgt.speed * v));
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {tgt.DisplayName} Speed {old} → {tgt.speed}");
                }
                break;
            }

            // --- SUMMON WEATHER ---
            case "summon_weather":
            {
                ActiveWeather = ab.value;
                Debug.Log($"{source.DisplayName}'s {ab.abilityName}: Weather → {ActiveWeather}!");
                break;
            }

            // --- SUMMON SCREEN ---
            case "summon_screen":
            {
                ActiveScreen = ab.value;
                Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {ActiveScreen} activated!");
                break;
            }

            // --- SWAP ENEMIES ---
            case "swap_enemies":
            {
                if (ab.condition == "first_last" && enemyTeam.Count >= 2)
                {
                    var tmp = enemyTeam[0];
                    enemyTeam[0] = enemyTeam[enemyTeam.Count - 1];
                    enemyTeam[enemyTeam.Count - 1] = tmp;
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: Swapped first and last enemy!");
                }
                else if (ab.condition == "last_two" && enemyTeam.Count >= 2)
                {
                    int c = enemyTeam.Count;
                    var tmp = enemyTeam[c - 1];
                    enemyTeam[c - 1] = enemyTeam[c - 2];
                    enemyTeam[c - 2] = tmp;
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: Swapped last two enemies!");
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
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {nextAlly.DisplayName} moved to front!");
                }
                break;
            }


            // --- MOODY ---
            case "moody":
            {
                int oldAtk = source.attack, oldSpd = source.speed;
                source.attack = Mathf.RoundToInt(source.attack * 1.15f);
                source.speed  = Mathf.Max(1, Mathf.RoundToInt(source.speed * 0.90f));
                Debug.Log($"{source.DisplayName}'s {ab.abilityName}: Attack {oldAtk}→{source.attack}, Speed {oldSpd}→{source.speed}");
                break;
            }

            // --- SOLAR POWER ---
            case "solar_power":
            {
                if (ActiveWeather == "sun")
                {
                    source.attack = Mathf.RoundToInt(source.attack * v);
                    int drain = Mathf.CeilToInt(source.maxHP * 0.05f);
                    source.currentHP = Mathf.Max(1, source.currentHP - drain);
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: +{(int)((v-1)*100)}% attack in sun, -{drain} HP ({source.currentHP}/{source.maxHP})");
                }
                break;
            }
        }
    }

    // -------------------------------------------------------
    // ABILITY DAMAGE HELPER
    // Applies before-hit check, damage reduction, and logs result.
    // -------------------------------------------------------

    private static void DealAbilityDamage(PokemonInstance source, PokemonInstance target,
        List<PokemonInstance> targetTeam, AbilityData ab, int baseDamage, bool applyTypeChart = false)
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

        dmg = Mathf.Max(0, dmg - GetFlatDamageReduction(target));
        float reduction = GetDamageReduction(target, true) * GetAllyDamageReduction(target, targetTeam);
        dmg = Mathf.CeilToInt(dmg * reduction);

        if (dmg > 0)
        {
            target.currentHP = Mathf.Max(0, target.currentHP - dmg);
            Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {dmg} to {target.DisplayName} ({target.currentHP}/{target.maxHP})");
        }
    }

    // -------------------------------------------------------
    // CONDITION CHECKER
    // -------------------------------------------------------

    private static bool CheckCondition(PokemonInstance p, string condition)
    {
        if (string.IsNullOrEmpty(condition)) return true;

        // hp_below_X — parses any threshold (hp_below_33, hp_below_40, hp_below_75, etc.)
        if (condition.StartsWith("hp_below_"))
        {
            if (int.TryParse(condition.Substring("hp_below_".Length), out int threshold))
                return (float)p.currentHP / p.maxHP < threshold / 100f;
        }

        switch (condition)
        {
            case "at_full_hp":        return p.currentHP == p.maxHP;
            case "weather_sun":       return ActiveWeather == "sun";
            case "weather_rain":      return ActiveWeather == "rain";
            case "weather_sandstorm": return ActiveWeather == "sandstorm";
            case "weather_hail":      return ActiveWeather == "hail";
            default:                  return true;
        }
    }

    // -------------------------------------------------------
    // SERENE GRACE — doubles healing for the recipient
    // -------------------------------------------------------

    // Unified heal helper.
    // hpPool overrides whose maxHP is used as the basis (for leech effects).
    private static void ApplyHeal(PokemonInstance healer, PokemonInstance recipient,
        AbilityData ab, int hpPool = -1)
    {
        float v = ab.FloatValue;
        int pool = hpPool >= 0 ? hpPool : recipient.maxHP;
        int amount = v < 2f ? Mathf.CeilToInt(pool * v) : (int)v;

        // Serene Grace doubles healing for the recipient
        var recipientAbility = recipient.baseData.ability;
        if (recipientAbility != null && recipientAbility.effect == "boost_healing")
            amount = Mathf.RoundToInt(amount * recipientAbility.FloatValue);

        int actual = Mathf.Min(amount, recipient.maxHP - recipient.currentHP);
        if (actual > 0)
        {
            recipient.currentHP += actual;
            Debug.Log($"{healer.DisplayName}'s {ab.abilityName}: Healed {recipient.DisplayName} {actual} HP ({recipient.currentHP}/{recipient.maxHP})");
        }
    }

    private static int ApplySereneGrace(PokemonInstance p, int healAmount)
    {
        var ab = p.baseData.ability;
        if (ab != null && ab.effect == "boost_healing")
            return Mathf.RoundToInt(healAmount * ab.FloatValue);
        return healAmount;
    }

    // -------------------------------------------------------
    // TARGET RESOLVER
    // Returns the list of PokemonInstances the effect should apply to.
    // -------------------------------------------------------

    private static List<PokemonInstance> ResolveTargets(string target, PokemonInstance source,
        List<PokemonInstance> sourceTeam, List<PokemonInstance> enemyTeam,
        PokemonInstance contextTarget)
    {
        var result = new List<PokemonInstance>();
        switch (target)
        {
            case "self":
                result.Add(source);
                break;
            case "enemy_front":
            {
                var t = GetFirstAlive(enemyTeam);
                if (t != null) result.Add(t);
                break;
            }
            case "enemy_next":
            {
                var t = GetNextAlive(enemyTeam, contextTarget);
                if (t != null) result.Add(t);
                break;
            }
            case "enemy_last":
            {
                var t = GetLastAlive(enemyTeam);
                if (t != null) result.Add(t);
                break;
            }
            case "enemy_all":
                foreach (var t in enemyTeam)
                    if (t.currentHP > 0) result.Add(t);
                break;
            case "ally_all":
                foreach (var t in sourceTeam)
                    if (t != source && t.currentHP > 0) result.Add(t);
                break;
            case "attacker":
                if (contextTarget != null) result.Add(contextTarget);
                break;
            case "all_others":
                foreach (var t in enemyTeam)  if (t.currentHP > 0) result.Add(t);
                foreach (var t in sourceTeam) if (t != source && t.currentHP > 0) result.Add(t);
                break;
            case "all":
                foreach (var t in enemyTeam)  if (t.currentHP > 0) result.Add(t);
                foreach (var t in sourceTeam) if (t.currentHP > 0) result.Add(t);
                break;
        }
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
