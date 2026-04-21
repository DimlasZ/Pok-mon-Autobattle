using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// AbilitySystem fires ability effects at the right trigger points during battle.
// All methods are static — called from BattleManager / BattleSceneManager.
//
// Effect implementations live in the AbilitySystem.*.cs partial class files:
//   AbilitySystem.Heal.cs       — heal, heal_flat, leech
//   AbilitySystem.Damage.cs     — deal_damage, deal_damage_flat, deal_damage_percent
//   AbilitySystem.Buffs.cs      — boost_attack, boost_speed, boost_all_stats, …
//   AbilitySystem.Debuffs.cs    — reduce_attack, reduce_speed
//   AbilitySystem.Weather.cs    — summon_weather, summon_screen, weather queries
//   AbilitySystem.Special.cs    — moody, solar_power, swap_enemies, transfer_buffs, …
//   AbilitySystem.Passives.cs   — passive query methods (GetDamageReduction, etc.)
//   AbilitySystem.Targets.cs    — ResolveTargets, team helpers
//   AbilitySystem.Conditions.cs — CheckCondition
//   AbilitySystem.Context.cs    — EffectContext struct

public static partial class AbilitySystem
{
    // -------------------------------------------------------
    // BATTLE STATE
    // -------------------------------------------------------

    public static string ActiveWeather { get; private set; } = "";
    public static string ActiveScreen  { get; private set; } = "";

    private static List<PokemonInstance> _teamA = new List<PokemonInstance>();
    private static List<PokemonInstance> _teamB = new List<PokemonInstance>();

    private static bool _weatherNegated   = false;
    public  static bool MoldBreakerActive { get; private set; } = false;

    private static readonly HashSet<PokemonInstance> _sturdyUsed       = new HashSet<PokemonInstance>();
    private static readonly HashSet<PokemonInstance> _shellArmorUsed   = new HashSet<PokemonInstance>();
    private static readonly HashSet<PokemonInstance> _boostOnceApplied = new HashSet<PokemonInstance>();
    private static readonly Dictionary<PokemonInstance, int> _allyFaintCount = new Dictionary<PokemonInstance, int>();
    // Stores pre-boost (attack, speed) for each Pokemon boosted by a passive weather ability,
    // so the boost can be reverted when the weather changes.
    private static readonly Dictionary<PokemonInstance, (int attack, int speed)> _weatherBoostedStats
        = new Dictionary<PokemonInstance, (int, int)>();

    // -------------------------------------------------------
    // EVENTS
    // -------------------------------------------------------

    // Fires whenever an ability resolves its targets, so BattleSceneManager
    // can spawn the correct animation travelling from source → each target.
    public static event System.Action<PokemonInstance, AbilityData, List<PokemonInstance>> OnAbilityFired;

    // Fires when the active weather changes (new weather name, or "" when cleared).
    public static event System.Action<string> OnWeatherChanged;

    // -------------------------------------------------------
    // EFFECT HANDLER REGISTRY
    // To add a new effect: add one entry here + one method in the appropriate partial file.
    // -------------------------------------------------------

    private static readonly Dictionary<string, System.Action<EffectContext>> _handlers =
        new Dictionary<string, System.Action<EffectContext>>
    {
        // Heal — AbilitySystem.Heal.cs
        { "heal",                    HandleHeal              },
        { "heal_flat",               HandleHealFlat          },
        { "heal_damage_dealt",       HandleHealDamageDealt   },
        { "leech",                   HandleLeech             },
        // Damage — AbilitySystem.Damage.cs
        { "deal_damage",             HandleDealDamage        },
        { "deal_damage_flat",        HandleDealDamageFlat    },
        { "deal_damage_percent",     HandleDealDamagePercent },
        { "deal_damage_source_hp",   HandleDealDamageSourceHp},
        // Buffs — AbilitySystem.Buffs.cs
        { "boost_attack",                      HandleBoostAttack              },
        { "boost_attack_once",                 HandleBoostAttackOnce          },
        { "boost_attack_flat",                 HandleBoostAttackFlat          },
        { "boost_speed",                       HandleBoostSpeed               },
        { "boost_speed_flat",                  HandleBoostSpeedFlat           },
        { "boost_all_stats",                   HandleBoostAllStats            },
        { "boost_all_stats_per_enemy_type",    HandleBoostAllStatsPerEnemyType},
        { "boost_all_stats_per_ally_type",     HandleBoostAllStatsPerAllyType },
        { "boost_speed_and_attack",            HandleBoostSpeedAttack         },
        { "boost_hp",                          HandleBoostHp                  },
        { "boost_hp_flat",                     HandleBoostHpFlat              },
        { "boost_damage_taken",                HandleBoostDamageTaken         },
        { "boost_damage_taken_once",           HandleBoostDamageTakenOnce     },
        // Debuffs — AbilitySystem.Debuffs.cs
        { "reduce_attack",           HandleReduceAttack      },
        { "reduce_speed",            HandleReduceSpeed       },
        // Weather / Screen — AbilitySystem.Weather.cs
        { "summon_weather",          HandleSummonWeather     },
        { "summon_screen",           HandleSummonScreen      },
        // Special — AbilitySystem.Special.cs
        { "swap_enemies",            HandleSwapEnemies       },
        { "swap_enemies_random",     HandleSwapEnemiesRandom },
        { "move_ally_to_front",      HandleMoveAllyToFront   },
        { "cure_status",             HandleCureStatus        },
        { "moody",                   HandleMoody             },
        { "solar_power",             HandleSolarPower        },
        { "transfer_buffs",          HandleTransferBuffs     },
        // Passives — resolved via query methods, no active effect needed
        { "damage_reduction",          PassiveNoOp },
        { "damage_reduction_flat",     PassiveNoOp },
        { "immune_to_ability_damage",  PassiveNoOp },
        { "immune_to_debuff",          PassiveNoOp },
        { "immune",                    PassiveNoOp },
        { "negate_weather",            PassiveNoOp },
        { "ignore_damage_reduction",   PassiveNoOp },
        { "ignore_abilities",          PassiveNoOp },
        { "boost_healing",             PassiveNoOp },
        { "boost_ability_damage",      PassiveNoOp },
        { "reduce_healing",            PassiveNoOp },
        // on_hit passives handled directly in FireOnHit switch — NoOp here to suppress unknown-effect warning
        { "recoil_damage",             PassiveNoOp },
        // priority_move — resolved via HasPriorityMove() in BattleManager, no active effect
        { "priority_move",             PassiveNoOp },
    };

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
        OnWeatherChanged?.Invoke("");
        _weatherNegated   = false;
        MoldBreakerActive = false;
        _sturdyUsed.Clear();
        _shellArmorUsed.Clear();
        _boostOnceApplied.Clear();
        _allyFaintCount.Clear();
        _weatherBoostedStats.Clear();
    }

    // -------------------------------------------------------
    // TRIGGERS
    // -------------------------------------------------------

    // Returns all alive Pokemon that have the given trigger from both teams,
    // sorted by Speed descending (fastest fires first, ties random).
    public static List<(PokemonInstance pokemon, List<PokemonInstance> ownTeam, List<PokemonInstance> oppTeam)>
        GetSpeedOrder(string trigger, List<PokemonInstance> teamA, List<PokemonInstance> teamB)
    {
        var list = new List<(PokemonInstance, List<PokemonInstance>, List<PokemonInstance>)>();
        foreach (var p in teamA)
            if (p.currentHP > 0 && p.baseData.ability?.trigger == trigger)
                list.Add((p, teamA, teamB));
        foreach (var p in teamB)
            if (p.currentHP > 0 && p.baseData.ability?.trigger == trigger)
                list.Add((p, teamB, teamA));
        list.Sort((a, b) =>
        {
            int cmp = b.Item1.speed.CompareTo(a.Item1.speed);
            return cmp != 0 ? cmp : (Random.value > 0.5f ? 1 : -1); // random tie-break
        });
        return list;
    }

    // Fire one Pokemon's ability for the given trigger.
    // Returns true if the ability actually did something visible (VFX/sound would have fired).
    public static bool FireSingle(string trigger, PokemonInstance p,
        List<PokemonInstance> ownTeam, List<PokemonInstance> oppTeam)
    {
        bool fired = TryFire(trigger, p, ownTeam, oppTeam, 0, 0, null);
        if (trigger == "on_battle_start")
            RecalculateWeatherNegation();
        return fired;
    }

    // Legacy — kept so nothing else breaks if called directly
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

    // Returns potentially modified incoming damage.
    // Handles on_hit effects that react to being hit (Sturdy, Flame Body, Iron Barbs, Gooey).
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
                    FireAbilityVFX(defender, ab, defenderTeam, attackerTeam, attacker);
                    int recoil = Mathf.CeilToInt(defender.maxHP * ab.FloatValue);
                    if (!FireBeforeHit(attacker, defender, true))
                    {
                        attacker.currentHP = Mathf.Max(0, attacker.currentHP - recoil);
                        Debug.Log($"{defender.DisplayName}'s {ab.abilityName}: {attacker.DisplayName} takes {recoil} recoil!");
                    }
                }
                break;

            case "deal_damage_flat":
                if (CheckCondition(defender, ab.condition))
                {
                    FireAbilityVFX(defender, ab, defenderTeam, attackerTeam, attacker);
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
                {
                    FireAbilityVFX(defender, ab, defenderTeam, attackerTeam, attacker);
                    ApplyDebuff(attacker, attackerTeam, defenderTeam, "attack", ab);
                }
                break;

            case "reduce_speed":
                if (CheckCondition(defender, ab.condition))
                {
                    FireAbilityVFX(defender, ab, defenderTeam, attackerTeam, attacker);
                    ApplyDebuff(attacker, attackerTeam, defenderTeam, "speed", ab);
                }
                break;
        }

        return damage;
    }

    public static void FireAfterHit(PokemonInstance defender, List<PokemonInstance> defenderTeam)
    {
        List<PokemonInstance> opponentTeam = GetOpponentOf(defender);
        TryFire("on_hit", defender, defenderTeam, opponentTeam, 0, 0, null);
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

    // Called after any Pokemon heals — lets own-team on_ally_heal abilities react
    public static void FireOnAllyHeal(PokemonInstance healed,
        List<PokemonInstance> healedTeam, List<PokemonInstance> opponentTeam)
    {
        foreach (var p in new List<PokemonInstance>(healedTeam))
        {
            if (p == healed || p.currentHP <= 0) continue;
            var ab = p.baseData.ability;
            if (ab == null || ab.trigger != "on_ally_heal") continue;
            if (!ab.ShouldTrigger()) continue;
            ApplyEffect(p, healedTeam, opponentTeam, ab, 0, 0, null);
        }
    }

    // Fires OnAbilityFired using ResolveTargets — fully driven by ab.target in the CSV.
    private static void FireAbilityVFX(PokemonInstance source, AbilityData ab,
        List<PokemonInstance> sourceTeam, List<PokemonInstance> oppTeam,
        PokemonInstance contextTarget)
    {
        if (string.IsNullOrEmpty(ab.vfxSheet)) return;
        var targets = ResolveTargets(ab.target, ab.count, source, sourceTeam, oppTeam, contextTarget);
        if (targets == null || targets.Count == 0) return;
        OnAbilityFired?.Invoke(source, ab, targets);
    }

    // -------------------------------------------------------
    // MOLD BREAKER
    // -------------------------------------------------------

    public static void SetMoldBreaker(bool active) => MoldBreakerActive = active;

    // -------------------------------------------------------
    // CORE DISPATCHER
    // -------------------------------------------------------

    private static bool TryFire(string trigger, PokemonInstance source,
        List<PokemonInstance> sourceTeam, List<PokemonInstance> enemyTeam,
        int contextDamage, int excessDamage, PokemonInstance contextTarget)
    {
        var ab = source.baseData.ability;
        if (ab == null || ab.trigger != trigger) return false;
        if (!CheckCondition(source, ab.condition)) return false;
        if (!ab.ShouldTrigger()) return false;

        return ApplyEffect(source, sourceTeam, enemyTeam, ab, contextDamage, excessDamage, contextTarget);
    }

    // Returns true when the ability produced a visible effect (VFX/sound would fire).
    private static bool ApplyEffect(PokemonInstance source,
        List<PokemonInstance> sourceTeam, List<PokemonInstance> enemyTeam,
        AbilityData ab, int contextDamage, int excessDamage, PokemonInstance contextTarget)
    {
        // Resolve targets once so VFX, sound, and the effect handler all use the same list.
        // This prevents random targets (enemy_random, ally_random) from rolling a different
        // Pokémon for the animation vs. the actual effect.
        var preResolved = ResolveTargets(ab.target, ab.count, source, sourceTeam, enemyTeam, contextTarget);

        // Only fire VFX/sound/log when there is a valid target AND
        // the effect would actually change something:
        //   leech    — any resolved target (including just-killed); skip when source is already at full HP
        //   heal / heal_flat — skip when all live targets are already at full HP
        bool anyLiveTarget = preResolved.Any(t => t.currentHP > 0);
        bool wouldHeal     = (ab.effect == "heal" || ab.effect == "heal_flat")
                             && !preResolved.Any(t => t.currentHP > 0 && t.currentHP < t.maxHP);
        bool shouldFire    = (ab.effect == "leech" ? preResolved.Count > 0 : anyLiveTarget) &&
                             (ab.effect != "leech" || source.currentHP < source.maxHP) &&
                             (ab.effect != "boost_attack_once" || !_boostOnceApplied.Contains(source)) &&
                             !wouldHeal;

        if (shouldFire && !string.IsNullOrEmpty(ab.vfxSheet))
            OnAbilityFired?.Invoke(source, ab, preResolved);

        if (shouldFire && !string.IsNullOrEmpty(ab.sound))
            AudioManager.Instance?.PlaySound("Audio/Sounds/" + ab.sound);

        var ctx = new EffectContext(source, sourceTeam, enemyTeam, ab, contextDamage, excessDamage, contextTarget, preResolved);

        if (_handlers.TryGetValue(ab.effect, out var handler))
            handler(ctx);
        else
            Debug.LogWarning($"[AbilitySystem] Unknown effect: '{ab.effect}' on {ab.abilityName}");

        return shouldFire;
    }
}
