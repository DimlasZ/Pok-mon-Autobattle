using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// AbilitySystem fires ability effects at the right trigger points during battle.
// All methods are static — called from BattleManager / BattleSceneManager.
//
// Flow per trigger:
//   1. Check trigger matches
//   2. Check condition passes
//   3. Check chance passes
//   4. Resolve targets (using ab.target + ab.count)
//   5. Apply effect

public static class AbilitySystem
{
    // -------------------------------------------------------
    // BATTLE STATE
    // -------------------------------------------------------

    public static string ActiveWeather { get; private set; } = "";
    public static string ActiveScreen  { get; private set; } = "";

    // Stored at InitBattle so helpers can resolve teams without passing them everywhere
    private static List<PokemonInstance> _teamA = new List<PokemonInstance>(); // player team
    private static List<PokemonInstance> _teamB = new List<PokemonInstance>(); // enemy team

    private static bool _weatherNegated   = false;
    public  static bool MoldBreakerActive { get; private set; } = false;

    private static readonly HashSet<PokemonInstance> _sturdyUsed       = new HashSet<PokemonInstance>();
    private static readonly HashSet<PokemonInstance> _shellArmorUsed   = new HashSet<PokemonInstance>();
    private static readonly HashSet<PokemonInstance> _boostOnceApplied = new HashSet<PokemonInstance>();
    private static readonly Dictionary<PokemonInstance, int> _allyFaintCount = new Dictionary<PokemonInstance, int>();

    // -------------------------------------------------------
    // INIT
    // -------------------------------------------------------

    public static void InitBattle(List<PokemonInstance> teamA, List<PokemonInstance> teamB)
    {
        _teamA = teamA;
        _teamB = teamB;
    }

    public static void ResetBattleState()
    {
        ActiveWeather     = "";
        ActiveScreen      = "";
        _weatherNegated   = false;
        MoldBreakerActive = false;
        _sturdyUsed.Clear();
        _shellArmorUsed.Clear();
        _boostOnceApplied.Clear();
        _allyFaintCount.Clear();
    }

    private static void RecalculateWeatherNegation()
    {
        _weatherNegated = false;
        foreach (var p in _teamA)
            if (p.currentHP > 0 && p.baseData.ability?.effect == "negate_weather") { _weatherNegated = true; return; }
        foreach (var p in _teamB)
            if (p.currentHP > 0 && p.baseData.ability?.effect == "negate_weather") { _weatherNegated = true; return; }
    }

    // -------------------------------------------------------
    // TRIGGERS
    // -------------------------------------------------------

    public static void FireBattleStart(List<PokemonInstance> team, List<PokemonInstance> enemyTeam)
    {
        foreach (var p in new List<PokemonInstance>(team))
            TryFire("on_battle_start", p, team, enemyTeam, 0, 0, null);
        RecalculateWeatherNegation();
    }

    public static void FireRoundStart(List<PokemonInstance> team, List<PokemonInstance> enemyTeam)
    {
        foreach (var p in new List<PokemonInstance>(team))
        {
            if (p.currentHP <= 0) continue;
            TryFire("on_round_start", p, team, enemyTeam, 0, 0, null);
        }
    }

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

        // Mold Breaker bypasses all defender abilities
        if (MoldBreakerActive) return false;

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
        if (!ab.ShouldTrigger()) return damage;

        switch (ab.effect)
        {
            case "survive_ko":
                if (CheckCondition(defender, ab.condition) &&
                    !_sturdyUsed.Contains(defender) &&
                    defender.currentHP == defender.maxHP &&
                    damage >= defender.currentHP)
                {
                    _sturdyUsed.Add(defender);
                    damage = defender.currentHP - 1;
                    Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: Survived with 1 HP!");
                }
                break;

            case "recoil_damage":
                if (CheckCondition(defender, ab.condition))
                {
                    int recoil = Mathf.CeilToInt(defender.maxHP * ab.FloatValue);
                    if (!FireBeforeHit(attacker, defender, true))
                    {
                        attacker.currentHP = Mathf.Max(0, attacker.currentHP - recoil);
                        Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: {attacker.DisplayName} takes {recoil} recoil!");
                    }
                }
                break;

            case "deal_damage_flat":
                // Iron Barbs — deal flat damage back to attacker
                if (CheckCondition(defender, ab.condition))
                {
                    int ironDmg = (int)ab.FloatValue;
                    if (!FireBeforeHit(attacker, defender, true))
                    {
                        attacker.currentHP = Mathf.Max(0, attacker.currentHP - ironDmg);
                        Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: {ironDmg} damage to {attacker.DisplayName} ({attacker.currentHP}/{attacker.maxHP})");
                    }
                }
                break;

            case "reduce_attack":
                if (CheckCondition(defender, ab.condition))
                    ApplyDebuff(attacker, attackerTeam, defenderTeam, "attack", ab);
                break;

            case "reduce_speed":
                if (CheckCondition(defender, ab.condition))
                    ApplyDebuff(attacker, attackerTeam, defenderTeam, "speed", ab);
                break;

        }

        return damage;
    }

    public static void FireAfterHit(PokemonInstance defender, List<PokemonInstance> defenderTeam)
    {
        var ab = defender.baseData.ability;
        if (ab == null || ab.trigger != "on_hit") return;

        List<PokemonInstance> opponentTeam = GetOpponentOf(defender);

        // Percentage heal on hit
        if (ab.effect == "heal" && defender.currentHP > 0 && CheckCondition(defender, ab.condition))
            ApplyHeal(defender, defender, ab, defenderTeam, opponentTeam);

        // Boost own attack on hit (Guts — stacks; Power Construct — once below 50%)
        if (ab.effect == "boost_attack" && ab.target == "self" && defender.currentHP > 0 && CheckCondition(defender, ab.condition))
        {
            bool isOnceAbility = !string.IsNullOrEmpty(ab.condition) && ab.condition.StartsWith("hp_below_");
            if (isOnceAbility && _boostOnceApplied.Contains(defender)) return;
            if (isOnceAbility) _boostOnceApplied.Add(defender);

            int old = defender.attack;
            defender.attack = Mathf.RoundToInt(defender.attack * ab.FloatValue);
            Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: Attack {old} → {defender.attack}");
        }

        // Boost all allies' attack on hit (Target Dummy, Wimp Out)
        if (ab.effect == "boost_attack" && ab.target == "ally_all" && defender.currentHP > 0 && CheckCondition(defender, ab.condition))
        {
            bool isOnceAbility = !string.IsNullOrEmpty(ab.condition) && ab.condition.StartsWith("hp_below_");
            if (isOnceAbility && _boostOnceApplied.Contains(defender)) return;
            if (isOnceAbility) _boostOnceApplied.Add(defender);

            foreach (var ally in defenderTeam)
            {
                if (ally == defender || ally.currentHP <= 0) continue;
                int old = ally.attack;
                ally.attack = Mathf.RoundToInt(ally.attack * ab.FloatValue);
                Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: {ally.DisplayName} Attack {old} → {ally.attack}");
            }
        }

        // One-time attack boost below HP threshold (Blaze, Torrent, Overgrow)
        if (ab.effect == "boost_attack_once" && defender.currentHP > 0 && CheckCondition(defender, ab.condition))
        {
            if (!_boostOnceApplied.Contains(defender))
            {
                _boostOnceApplied.Add(defender);
                int old = defender.attack;
                defender.attack = Mathf.RoundToInt(defender.attack * ab.FloatValue);
                Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: Attack {old} → {defender.attack}!");
            }
        }

        // Boost speed on hit (Unburden — every hit, no condition)
        if (ab.effect == "boost_speed" && ab.target == "self" && defender.currentHP > 0 && CheckCondition(defender, ab.condition))
        {
            int old = defender.speed;
            defender.speed = Mathf.RoundToInt(defender.speed * ab.FloatValue);
            Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: Speed {old} → {defender.speed}");
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

        // Notify all surviving allies
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

        RecalculateWeatherNegation();
    }

    // Called when any stat is reduced on a Pokemon — fires that Pokemon's on_debuff ability
    public static void FireOnDebuff(PokemonInstance debuffed)
    {
        var ab = debuffed.baseData.ability;
        if (ab == null || ab.trigger != "on_debuff") return;
        if (!ab.ShouldTrigger()) return;

        List<PokemonInstance> debuffedTeam = GetTeamOf(debuffed);
        List<PokemonInstance> opponentTeam = GetOpponentOf(debuffed);
        ApplyEffect(debuffed, debuffedTeam, opponentTeam, ab, 0, 0, null);
    }

    // Called after any Pokemon heals — lets enemy on_heal abilities react
    public static void FireOnHealTrigger(PokemonInstance healed,
        List<PokemonInstance> healedTeam, List<PokemonInstance> opponentTeam)
    {
        foreach (var p in new List<PokemonInstance>(opponentTeam))
        {
            if (p.currentHP <= 0) continue;
            var ab = p.baseData.ability;
            if (ab == null || ab.trigger != "on_heal") continue;
            if (!ab.ShouldTrigger()) continue;
            ApplyEffect(p, opponentTeam, healedTeam, ab, 0, 0, null);
        }
    }

    // -------------------------------------------------------
    // MOLD BREAKER
    // -------------------------------------------------------

    public static void SetMoldBreaker(bool active) => MoldBreakerActive = active;

    // -------------------------------------------------------
    // PASSIVE QUERIES — called inline during damage calculation
    // -------------------------------------------------------

    public static float GetPassiveAttackMultiplier(PokemonInstance attacker)
    {
        var ab = attacker.baseData.ability;
        if (ab == null || ab.trigger != "passive") return 1f;
        if (!CheckCondition(attacker, ab.condition)) return 1f;
        if (ab.effect == "boost_attack") return ab.FloatValue;
        return 1f;
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
        float v     = ab.FloatValue;
        int   count = ab.count; // 0 = unset → treated as 1 for single-target selectors

        switch (ab.effect)
        {
            // ==========================================================
            // HEAL — percentage of max HP
            // ==========================================================
            case "heal":
            {
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var t in targets)
                    if (t.currentHP > 0) ApplyHeal(source, t, ab, sourceTeam, enemyTeam);
                break;
            }

            // ==========================================================
            // HEAL FLAT — fixed HP amount
            // ==========================================================
            case "heal_flat":
            {
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var t in targets)
                    if (t.currentHP > 0) ApplyHealFlat(source, t, ab, sourceTeam, enemyTeam);
                break;
            }

            // ==========================================================
            // LEECH — drain from target's max HP, heal goes to source
            // ==========================================================
            case "leech":
            {
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var t in targets)
                    if (t.currentHP > 0) ApplyHeal(source, source, ab, sourceTeam, enemyTeam, t.maxHP);
                break;
            }

            // ==========================================================
            // DEAL DAMAGE — multiplier of attacker's attack stat
            // ==========================================================
            case "deal_damage":
            {
                int dmg = Mathf.CeilToInt(source.attack * v);
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var t in targets)
                {
                    if (t.currentHP <= 0) continue;
                    List<PokemonInstance> tTeam = enemyTeam.Contains(t) ? enemyTeam : sourceTeam;
                    DealAbilityDamage(source, t, tTeam, sourceTeam, ab, dmg, true);
                }
                break;
            }

            // ==========================================================
            // DEAL DAMAGE FLAT — fixed HP damage
            // ==========================================================
            case "deal_damage_flat":
            {
                int dmg = (int)v;
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var t in targets)
                {
                    if (t.currentHP <= 0) continue;
                    List<PokemonInstance> tTeam = enemyTeam.Contains(t) ? enemyTeam : sourceTeam;
                    DealAbilityDamage(source, t, tTeam, sourceTeam, ab, dmg, true);
                }
                break;
            }

            // ==========================================================
            // DEAL DAMAGE PERCENT — percentage of target's max HP
            // ==========================================================
            case "deal_damage_percent":
            {
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var t in targets)
                {
                    if (t.currentHP <= 0) continue;
                    int dmg = Mathf.CeilToInt(t.maxHP * v);
                    List<PokemonInstance> tTeam = enemyTeam.Contains(t) ? enemyTeam : sourceTeam;
                    DealAbilityDamage(source, t, tTeam, sourceTeam, ab, dmg, false);
                }
                break;
            }

            // ==========================================================
            // BOOST ATTACK
            // ==========================================================
            case "boost_attack":
            {
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var tgt in targets)
                {
                    int old = tgt.attack;
                    tgt.attack = Mathf.RoundToInt(tgt.attack * v);
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {tgt.DisplayName} Attack {old} → {tgt.attack}");
                }
                break;
            }

            // ==========================================================
            // BOOST SPEED
            // ==========================================================
            case "boost_speed":
            {
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var tgt in targets)
                {
                    int old = tgt.speed;
                    tgt.speed = Mathf.RoundToInt(tgt.speed * v);
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {tgt.DisplayName} Speed {old} → {tgt.speed}");
                }
                break;
            }

            // ==========================================================
            // BOOST ALL STATS — scales HP, Attack, and Speed
            // ==========================================================
            case "boost_all_stats":
            {
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var tgt in targets)
                {
                    int hpGain = Mathf.RoundToInt(tgt.maxHP * v) - tgt.maxHP;
                    tgt.maxHP     = Mathf.RoundToInt(tgt.maxHP * v);
                    tgt.currentHP = Mathf.Min(tgt.currentHP + hpGain, tgt.maxHP);
                    tgt.attack    = Mathf.RoundToInt(tgt.attack * v);
                    tgt.speed     = Mathf.RoundToInt(tgt.speed  * v);
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {tgt.DisplayName} all stats x{v:F2}");
                }
                break;
            }

            // ==========================================================
            // BOOST SPEED AND ATTACK
            // ==========================================================
            case "boost_speed_and_attack":
            {
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var tgt in targets)
                {
                    int oldAtk = tgt.attack, oldSpd = tgt.speed;
                    tgt.attack = Mathf.RoundToInt(tgt.attack * v);
                    tgt.speed  = Mathf.RoundToInt(tgt.speed  * v);
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {tgt.DisplayName} Atk {oldAtk}→{tgt.attack}, Spd {oldSpd}→{tgt.speed}");
                }
                break;
            }

            // ==========================================================
            // BOOST HP — scales max HP (current HP scales proportionally)
            // ==========================================================
            case "boost_hp":
            {
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var tgt in targets)
                {
                    int old = tgt.maxHP;
                    int hpGain = Mathf.RoundToInt(tgt.maxHP * v) - tgt.maxHP;
                    tgt.maxHP     = Mathf.RoundToInt(tgt.maxHP * v);
                    tgt.currentHP = Mathf.Min(tgt.currentHP + hpGain, tgt.maxHP);
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {tgt.DisplayName} MaxHP {old} → {tgt.maxHP}");
                }
                break;
            }

            // ==========================================================
            // BOOST DAMAGE TAKEN — permanent multiplier (Cursed Aura etc.)
            // ==========================================================
            case "boost_damage_taken":
            {
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var tgt in targets)
                {
                    float old = tgt.damageTakenMultiplier;
                    tgt.damageTakenMultiplier *= v;
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {tgt.DisplayName} takes x{tgt.damageTakenMultiplier:F2} damage permanently (was x{old:F2})");
                }
                break;
            }

            // ==========================================================
            // BOOST DAMAGE TAKEN ONCE — one-shot multiplier, consumed on next hit (Screech, Leer)
            // ==========================================================
            case "boost_damage_taken_once":
            {
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var tgt in targets)
                {
                    float old = tgt.nextHitDamageMultiplier;
                    tgt.nextHitDamageMultiplier *= v;
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {tgt.DisplayName} takes x{tgt.nextHitDamageMultiplier:F2} damage on next hit (was x{old:F2})");
                }
                break;
            }

            // ==========================================================
            // REDUCE ATTACK
            // ==========================================================
            case "reduce_attack":
            {
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var tgt in targets)
                {
                    List<PokemonInstance> tTeam   = enemyTeam.Contains(tgt) ? enemyTeam : sourceTeam;
                    List<PokemonInstance> oppTeam  = enemyTeam.Contains(tgt) ? sourceTeam : enemyTeam;
                    ApplyDebuff(tgt, tTeam, oppTeam, "attack", ab);
                }
                break;
            }

            // ==========================================================
            // REDUCE SPEED
            // ==========================================================
            case "reduce_speed":
            {
                var targets = ResolveTargets(ab.target, count, source, sourceTeam, enemyTeam, contextTarget);
                foreach (var tgt in targets)
                {
                    List<PokemonInstance> tTeam  = enemyTeam.Contains(tgt) ? enemyTeam : sourceTeam;
                    List<PokemonInstance> oppTeam = enemyTeam.Contains(tgt) ? sourceTeam : enemyTeam;
                    ApplyDebuff(tgt, tTeam, oppTeam, "speed", ab);
                }
                break;
            }

            // ==========================================================
            // WEATHER / SCREEN
            // ==========================================================
            case "summon_weather":
                ActiveWeather = ab.value;
                RecalculateWeatherNegation(); // re-check in case Cloud Nine is present
                Debug.Log($"{source.DisplayName}'s {ab.abilityName}: Weather → {ActiveWeather}!");
                break;

            case "summon_screen":
                ActiveScreen = ab.value;
                Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {ActiveScreen} activated!");
                break;

            // ==========================================================
            // SWAP ENEMIES (Whirlwind)
            // ==========================================================
            case "swap_enemies":
                if (ab.condition == "first_last" && enemyTeam.Count >= 2)
                {
                    var tmp = enemyTeam[0];
                    enemyTeam[0] = enemyTeam[enemyTeam.Count - 1];
                    enemyTeam[enemyTeam.Count - 1] = tmp;
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: Swapped first and last enemy!");
                }
                break;

            // ==========================================================
            // MOVE ALLY TO FRONT (U-Turn)
            // ==========================================================
            case "move_ally_to_front":
            {
                int idx = sourceTeam.IndexOf(source);
                PokemonInstance nextAlly = null;
                for (int i = idx + 1; i < sourceTeam.Count; i++)
                    if (sourceTeam[i].currentHP > 0) { nextAlly = sourceTeam[i]; break; }
                if (nextAlly != null)
                {
                    sourceTeam.Remove(nextAlly);
                    sourceTeam.Insert(0, nextAlly);
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: {nextAlly.DisplayName} moved to front!");
                }
                break;
            }

            // ==========================================================
            // CURE STATUS (stub — no status system yet)
            // ==========================================================
            case "cure_status":
                Debug.Log($"{source.DisplayName}'s {ab.abilityName}: Status cleared.");
                break;

            // ==========================================================
            // PASSIVES — handled via query methods, no event action needed
            // ==========================================================
            case "damage_reduction":
            case "damage_reduction_flat":
            case "immune_to_ability_damage":
            case "immune_to_debuff":
            case "immune":
            case "negate_weather":
            case "ignore_damage_reduction":
            case "ignore_abilities":
            case "boost_healing":
            case "boost_ability_damage":
            case "reduce_healing":
                // Queried via GetDamageReduction, IsImmuneToDebuff, ApplyHealModifiers, etc.
                break;

            // ==========================================================
            // SPECIAL CASES
            // ==========================================================
            case "moody":
            {
                int oldAtk = source.attack, oldSpd = source.speed;
                source.attack = Mathf.RoundToInt(source.attack * 1.15f);
                source.speed  = Mathf.Max(1, Mathf.RoundToInt(source.speed * 0.90f));
                Debug.Log($"{source.DisplayName}'s {ab.abilityName}: Attack {oldAtk}→{source.attack}, Speed {oldSpd}→{source.speed}");
                break;
            }

            case "solar_power":
            {
                if (IsWeatherActive("sun"))
                {
                    source.attack = Mathf.RoundToInt(source.attack * v);
                    int drain = Mathf.CeilToInt(source.maxHP * 0.05f);
                    source.currentHP = Mathf.Max(1, source.currentHP - drain);
                    Debug.Log($"{source.DisplayName}'s {ab.abilityName}: +{(int)((v - 1) * 100)}% attack, -{drain} HP");
                }
                break;
            }
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

    // -------------------------------------------------------
    // HEAL HELPERS
    // -------------------------------------------------------

    // Percentage heal: v is a fraction of hpPool (0.3 = 30% of max HP)
    private static void ApplyHeal(PokemonInstance healer, PokemonInstance recipient,
        AbilityData ab, List<PokemonInstance> recipientTeam, List<PokemonInstance> opponentTeam,
        int hpPool = -1)
    {
        float v    = ab.FloatValue;
        int   pool = hpPool >= 0 ? hpPool : recipient.maxHP;
        int   amount = Mathf.CeilToInt(pool * v);
        amount = ApplyHealModifiers(recipient, amount, opponentTeam);

        int actual = Mathf.Min(amount, recipient.maxHP - recipient.currentHP);
        if (actual > 0)
        {
            recipient.currentHP += actual;
            Debug.Log($"{healer.DisplayName}'s {ab.abilityName}: Healed {recipient.DisplayName} {actual} HP ({recipient.currentHP}/{recipient.maxHP})");
            FireOnHealTrigger(recipient, recipientTeam, opponentTeam);
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
        }
    }

    // Applies Wish/Serene Grace/Cheek Pouch (boost_healing) and Unnerve (reduce_healing)
    private static int ApplyHealModifiers(PokemonInstance recipient, int amount, List<PokemonInstance> opponentTeam)
    {
        // Self boost_healing passive (Wish)
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

    // -------------------------------------------------------
    // DEBUFF HELPER — checks immunity, applies stat change, fires on_debuff
    // -------------------------------------------------------

    private static void ApplyDebuff(PokemonInstance target, List<PokemonInstance> targetTeam,
        List<PokemonInstance> opponentTeam, string stat, AbilityData ab)
    {
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

    // -------------------------------------------------------
    // CONDITION CHECKER
    // -------------------------------------------------------

    private static bool CheckCondition(PokemonInstance p, string condition)
    {
        if (string.IsNullOrEmpty(condition)) return true;

        if (condition.StartsWith("hp_below_"))
        {
            if (int.TryParse(condition.Substring("hp_below_".Length), out int threshold))
                return (float)p.currentHP / p.maxHP < threshold / 100f;
        }

        switch (condition)
        {
            case "full_hp":
            case "at_full_hp":        return p.currentHP == p.maxHP;
            case "weather_sun":       return IsWeatherActive("sun");
            case "weather_rain":      return IsWeatherActive("rain");
            case "weather_sandstorm": return IsWeatherActive("sandstorm");
            case "weather_hail":      return IsWeatherActive("hail");
            // These are context-checked or always-pass in this simplified system
            case "super_effective":
            case "first_hit":
            case "first_last":
            case "last_two":
            default:                  return true;
        }
    }

    private static bool IsWeatherActive(string weather)
    {
        if (_weatherNegated) return false;
        return ActiveWeather == weather;
    }

    // -------------------------------------------------------
    // TARGET RESOLVER
    // -------------------------------------------------------

    private static List<PokemonInstance> ResolveTargets(string target, int count, PokemonInstance source,
        List<PokemonInstance> sourceTeam, List<PokemonInstance> enemyTeam, PokemonInstance contextTarget)
    {
        var result = new List<PokemonInstance>();
        int n = count > 0 ? count : 1;

        switch (target)
        {
            case "self":
                result.Add(source);
                break;

            case "enemy_front":
                // If called from an on_attack context, prefer the actual defender (contextTarget)
                // even if it just died — avoids spilling debuffs onto the next alive enemy.
                if (contextTarget != null && enemyTeam.Contains(contextTarget))
                    result.Add(contextTarget);
                else
                    result.AddRange(GetFirstNAlive(enemyTeam, n));
                break;

            case "enemy_second":
            {
                var t = GetSecondAlive(enemyTeam);
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
                result.AddRange(GetLastNAlive(enemyTeam, n));
                break;

            case "enemy_all":
                foreach (var t in enemyTeam) if (t.currentHP > 0) result.Add(t);
                break;

            case "enemy_random":
            {
                var t = GetRandomAlive(enemyTeam, null);
                if (t != null) result.Add(t);
                break;
            }

            case "ally_all":
                foreach (var t in sourceTeam) if (t != source && t.currentHP > 0) result.Add(t);
                break;

            case "ally_random":
            {
                var t = GetRandomAlive(sourceTeam, source);
                if (t != null) result.Add(t);
                break;
            }

            case "ally_last":
                result.AddRange(GetLastNAlive(sourceTeam.Where(p => p != source).ToList(), n));
                break;

            case "ally_behind":
            {
                int idx = sourceTeam.IndexOf(source);
                for (int i = idx + 1; i < sourceTeam.Count; i++)
                    if (sourceTeam[i].currentHP > 0) { result.Add(sourceTeam[i]); break; }
                break;
            }

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

    private static List<PokemonInstance> GetTeamOf(PokemonInstance p)
    {
        if (_teamB != null && _teamB.Contains(p)) return _teamB;
        return _teamA;
    }

    private static List<PokemonInstance> GetOpponentOf(PokemonInstance p)
    {
        if (_teamB != null && _teamB.Contains(p)) return _teamA;
        return _teamB;
    }

    private static PokemonInstance GetFirstAlive(List<PokemonInstance> team)
    {
        foreach (var p in team) if (p.currentHP > 0) return p;
        return null;
    }

    private static List<PokemonInstance> GetFirstNAlive(List<PokemonInstance> team, int n)
    {
        var result = new List<PokemonInstance>();
        foreach (var p in team)
        {
            if (p.currentHP <= 0) continue;
            result.Add(p);
            if (result.Count >= n) break;
        }
        return result;
    }

    private static PokemonInstance GetSecondAlive(List<PokemonInstance> team)
    {
        int seen = 0;
        foreach (var p in team)
        {
            if (p.currentHP <= 0) continue;
            if (++seen == 2) return p;
        }
        return null;
    }

    private static List<PokemonInstance> GetLastNAlive(List<PokemonInstance> team, int n)
    {
        var alive = team.Where(p => p.currentHP > 0).ToList();
        return alive.Skip(Mathf.Max(0, alive.Count - n)).ToList();
    }

    private static PokemonInstance GetNextAlive(List<PokemonInstance> team, PokemonInstance current)
    {
        if (current == null) return GetFirstAlive(team);
        int idx = team.IndexOf(current);
        for (int i = idx + 1; i < team.Count; i++)
            if (team[i].currentHP > 0) return team[i];
        return null;
    }

    private static PokemonInstance GetRandomAlive(List<PokemonInstance> team, PokemonInstance exclude)
    {
        var alive = team.Where(p => p != exclude && p.currentHP > 0).ToList();
        if (alive.Count == 0) return null;
        return alive[Random.Range(0, alive.Count)];
    }
}
