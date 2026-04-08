using UnityEngine;

[CreateAssetMenu(fileName = "New Ability", menuName = "Pokemon/AbilityData")]
public class AbilityData : ScriptableObject
{
    public int    abilityID;
    public string abilityName;
    public string trigger;      // on_battle_start | on_round_start | before_attack | on_attack | after_attack | before_hit | on_hit | on_kill | on_faint | on_ally_faint | passive
    public string target;       // self | enemy_front | enemy_next | enemy_last | enemy_all | all_others | all | attacker | ally_all
    public string effect;       // heal | leech | deal_damage | boost_attack | boost_attack_once | reduce_attack | boost_speed | reduce_speed | summon_weather | summon_screen | survive_ko | negate_damage | recoil_damage | damage_reduction | damage_reduction_flat | damage_reduction_abilities | immune_to_ability_damage | immune_to_ground | ignore_damage_reduction | boost_attack_enemy | boost_defense_allies | boost_attack_all_allies | overflow_damage_next | damage_same_position | damage_last_enemy | damage_all | swap_enemies | move_ally_to_front | boost_healing | solar_power | moody
    public string value;        // numeric ("1.5", "20") or keyword ("rain", "sun", "sandstorm", "light_screen")
    public string condition;    // hp_below_20 | hp_below_33 | hp_below_50 | at_full_hp | weather_sun | weather_rain | weather_sandstorm | first_enemy | last_enemy | first_last | last_two | first_hit | survived_hit | on_contact | super_effective
    public float  chance;       // 0 = always trigger, 0.3 = 30% chance
    public string custom;       // flags special handling: multi_hit | eruption | priority | mold_breaker | technician | emergency_exit | serene_grace
    public string description;

    public float FloatValue
    {
        get
        {
            if (float.TryParse(value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out float f))
                return f;
            return 0f;
        }
    }

    public bool ShouldTrigger()
    {
        if (chance <= 0f) return true;
        return Random.value <= chance;
    }
}
